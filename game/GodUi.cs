using System;
using System.Collections.Generic;
using Godot;
using Cosmos.Core;

namespace Cosmos.Game;

/// <summary>
/// UI panel for god tools (SPEC 6, P2):
/// 1. Create panel (mass + 6 element sliders, preview kind + radius).
/// 2. Edit panel (add/remove matter on selected object).
/// 3. Push panel (drag helper + quick velocity nudges).
/// 4. Constants panel (editable Consts.All()).
/// 5. Time warp buttons (1x..64x).
/// </summary>
public partial class GodUi : CanvasLayer
{
    readonly Main _main;
    readonly World _w;

    LineEdit _txtMass = null!;
    readonly HSlider[] _sliders = new HSlider[World.NElem];
    readonly Label[] _sliderLabels = new Label[World.NElem];
    Label _lblSum = null!;
    Label _lblPreview = null!;
    Label _lblParent = null!;

    Label _lblEditTarget = null!;
    readonly Label[] _editElemLabels = new Label[World.NElem];

    Label _lblPushTarget = null!;
    VBoxContainer _constList = null!;
    readonly Dictionary<string, LineEdit> _constEdits = new();

    readonly Button[] _warpButtons = new Button[7];
    static readonly int[] WarpSpeeds = { 1, 2, 4, 8, 16, 32, 64 };

    static readonly string[] ElemVi = { "Khí nhẹ", "Băng", "Đá", "Kim loại", "Carbon", "Phóng xạ" };
    static readonly string[] KindVi = { "Sao", "Hành tinh", "Vệ tinh", "Tiểu hành tinh" };

    public GodUi(Main main, World w)
    {
        _main = main;
        _w = w;
    }

    public override void _Ready()
    {
        var root = new PanelContainer
        {
            Position = new Vector2(850, 36),
            Size = new Vector2(418, 740),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        AddChild(root);

        var tabs = new TabContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        root.AddChild(tabs);

        BuildCreateTab(tabs);
        BuildEditTab(tabs);
        BuildPushTab(tabs);
        BuildConstsTab(tabs);
        BuildTimeTab(tabs);

        UpdatePreview();
    }

    void BuildCreateTab(TabContainer tabs)
    {
        var vbox = new VBoxContainer { Name = "Tạo vật thể" };
        tabs.AddChild(vbox);

        var lblTitle = new Label { Text = "Tạo vật thể mới (thả bằng chuột phải):" };
        vbox.AddChild(lblTitle);

        var hboxM = new HBoxContainer();
        hboxM.AddChild(new Label { Text = "Khối lượng (M_TráiĐất):", CustomMinimumSize = new Vector2(170, 0) });
        _txtMass = new LineEdit { Text = "1.0", CustomMinimumSize = new Vector2(80, 0) };
        _txtMass.TextChanged += _ => UpdatePreview();
        hboxM.AddChild(_txtMass);
        vbox.AddChild(hboxM);

        var hboxPresets = new HBoxContainer();
        AddPresetButton(hboxPresets, "M.Trăng", 0.0123);
        AddPresetButton(hboxPresets, "T.Đất", 1.0);
        AddPresetButton(hboxPresets, "S.Mộc", 317.8);
        AddPresetButton(hboxPresets, "M.Trời", 50.0 / World.EarthMass);
        vbox.AddChild(hboxPresets);

        vbox.AddChild(new HSeparator());
        vbox.AddChild(new Label { Text = "Tỉ lệ 6 nhóm nguyên tố:" });

        // Initial default: Earth-like (mostly rock + metal)
        double[] defaultRatios = { 0, 0.01, 0.66, 0.32, 0.005, 0.005 };
        for (int e = 0; e < World.NElem; e++)
        {
            int idx = e;
            var row = new HBoxContainer();
            row.AddChild(new Label { Text = ElemVi[e], CustomMinimumSize = new Vector2(75, 0) });
            var slider = new HSlider
            {
                MinValue = 0,
                MaxValue = 1,
                Step = 0.01,
                Value = defaultRatios[e],
                CustomMinimumSize = new Vector2(180, 0),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            var valLbl = new Label { Text = $"{defaultRatios[e] * 100:F0}%", CustomMinimumSize = new Vector2(45, 0) };
            slider.ValueChanged += v =>
            {
                valLbl.Text = $"{v * 100:F0}%";
                UpdatePreview();
            };
            _sliders[e] = slider;
            _sliderLabels[e] = valLbl;
            row.AddChild(slider);
            row.AddChild(valLbl);
            vbox.AddChild(row);
        }

        _lblSum = new Label();
        vbox.AddChild(_lblSum);

        vbox.AddChild(new HSeparator());
        _lblPreview = new Label();
        vbox.AddChild(_lblPreview);
        _lblParent = new Label();
        vbox.AddChild(_lblParent);

        var tip = new Label
        {
            Text = "Thao tác: Bấm chuột phải vào màn hình để đặt.\n- Nếu đang chọn vật thể: bay quỹ đạo tròn.\n- Nếu không chọn vật thể: đứng yên.",
            AutowrapMode = TextServer.AutowrapMode.Word
        };
        vbox.AddChild(tip);
    }

    void AddPresetButton(HBoxContainer parent, string title, double val)
    {
        var btn = new Button { Text = title, CustomMinimumSize = new Vector2(80, 0) };
        btn.Pressed += () =>
        {
            _txtMass.Text = val.ToString("G4");
            UpdatePreview();
        };
        parent.AddChild(btn);
    }

    void BuildEditTab(TabContainer tabs)
    {
        var vbox = new VBoxContainer { Name = "Sửa chất" };
        tabs.AddChild(vbox);

        _lblEditTarget = new Label { Text = "Chưa chọn vật thể nào." };
        vbox.AddChild(_lblEditTarget);
        vbox.AddChild(new HSeparator());

        for (int e = 0; e < World.NElem; e++)
        {
            int idx = e;
            var row = new HBoxContainer();
            row.AddChild(new Label { Text = ElemVi[e], CustomMinimumSize = new Vector2(75, 0) });
            _editElemLabels[e] = new Label { Text = "0%", CustomMinimumSize = new Vector2(80, 0) };
            row.AddChild(_editElemLabels[e]);

            var btnSub = new Button { Text = "-0.1 M⊕" };
            btnSub.Pressed += () => EditDelta(idx, -0.1 * World.EarthMass);
            row.AddChild(btnSub);

            var btnAdd = new Button { Text = "+0.1 M⊕" };
            btnAdd.Pressed += () => EditDelta(idx, 0.1 * World.EarthMass);
            row.AddChild(btnAdd);

            vbox.AddChild(row);
        }
    }

    void EditDelta(int elem, double delta)
    {
        int sel = _main.Selected;
        if (sel < 0 || sel >= _w.N || !_w.Alive[sel]) return;
        GodTools.AddMatter(_w, sel, elem, delta);
        UpdateEditTab();
    }

    void BuildPushTab(TabContainer tabs)
    {
        var vbox = new VBoxContainer { Name = "Đẩy" };
        tabs.AddChild(vbox);

        _lblPushTarget = new Label { Text = "Chưa chọn vật thể nào." };
        vbox.AddChild(_lblPushTarget);
        vbox.AddChild(new HSeparator());

        var desc = new Label
        {
            Text = "Cách đẩy chuột:\n1. Bấm chuột trái chọn vật thể.\n2. Giữ chuột trái trên vật thể và kéo ra ngoài.\n3. Mũi tên lực sẽ hiện; thả chuột để kích hoạt.\n\nHoặc bấm nút đẩy nhanh bên dưới:",
            AutowrapMode = TextServer.AutowrapMode.Word
        };
        vbox.AddChild(desc);

        var row1 = new HBoxContainer();
        AddNudgeButton(row1, "← Vx -0.1", -0.1, 0);
        AddNudgeButton(row1, "→ Vx +0.1", 0.1, 0);
        vbox.AddChild(row1);

        var row2 = new HBoxContainer();
        AddNudgeButton(row2, "↑ Vy -0.1", 0, -0.1);
        AddNudgeButton(row2, "↓ Vy +0.1", 0, 0.1);
        vbox.AddChild(row2);

        var btnStop = new Button { Text = "Hãm phanh (Vx=0, Vy=0)" };
        btnStop.Pressed += () =>
        {
            int sel = _main.Selected;
            if (sel >= 0 && sel < _w.N && _w.Alive[sel])
            {
                GodTools.Push(_w, sel, -_w.Vx[sel], -_w.Vy[sel]);
            }
        };
        vbox.AddChild(btnStop);
    }

    void AddNudgeButton(HBoxContainer parent, string text, double dvx, double dvy)
    {
        var btn = new Button { Text = text, CustomMinimumSize = new Vector2(120, 0) };
        btn.Pressed += () =>
        {
            int sel = _main.Selected;
            if (sel >= 0 && sel < _w.N && _w.Alive[sel])
            {
                GodTools.Push(_w, sel, dvx, dvy);
            }
        };
        parent.AddChild(btn);
    }

    void BuildConstsTab(TabContainer tabs)
    {
        var vbox = new VBoxContainer { Name = "Hằng số" };
        tabs.AddChild(vbox);

        vbox.AddChild(new Label { Text = "Bộ hằng số vũ trụ (Consts.All):" });
        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(390, 480),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        _constList = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        scroll.AddChild(_constList);
        vbox.AddChild(scroll);

        RefreshConsts();
    }

    public void RefreshConsts()
    {
        if (_constList == null) return;
        foreach (Node c in _constList.GetChildren()) c.QueueFree();
        _constEdits.Clear();

        foreach (var (name, val) in _w.C.All())
        {
            var row = new HBoxContainer();
            row.AddChild(new Label { Text = name, CustomMinimumSize = new Vector2(130, 0) });
            var txt = new LineEdit { Text = val.ToString("G4"), CustomMinimumSize = new Vector2(120, 0) };
            _constEdits[name] = txt;
            row.AddChild(txt);

            var btnSet = new Button { Text = "Áp dụng" };
            string cname = name;
            btnSet.Pressed += () =>
            {
                if (double.TryParse(txt.Text, out double nv))
                {
                    GodTools.SetConst(_w, cname, nv);
                }
            };
            row.AddChild(btnSet);
            _constList.AddChild(row);
        }
    }

    void BuildTimeTab(TabContainer tabs)
    {
        var vbox = new VBoxContainer { Name = "Thời gian" };
        tabs.AddChild(vbox);

        vbox.AddChild(new Label { Text = "Tốc độ bước tính (Advance / frame):" });
        var row = new HBoxContainer();
        for (int i = 0; i < WarpSpeeds.Length; i++)
        {
            int speed = WarpSpeeds[i];
            var btn = new Button { Text = $"{speed}×", CustomMinimumSize = new Vector2(48, 0) };
            btn.Pressed += () => _main.SetTimeWarp(speed);
            _warpButtons[i] = btn;
            row.AddChild(btn);
        }
        vbox.AddChild(row);

        var note = new Label
        {
            Text = "\nPhím tắt:\n- Phím 1 đến 7: chọn tốc độ 1× đến 64×\n- Space: tạm dừng / tiếp tục\n- Tự động hạ tốc nếu bước tính > 50 ms.",
            AutowrapMode = TextServer.AutowrapMode.Word
        };
        vbox.AddChild(note);
    }

    public void UpdateTimeWarp(int currentWarp)
    {
        for (int i = 0; i < WarpSpeeds.Length; i++)
        {
            if (_warpButtons[i] != null)
                _warpButtons[i].Modulate = WarpSpeeds[i] == currentWarp ? new Color(1, 1, 0) : Colors.White;
        }
    }

    public double GetCreateMass()
    {
        if (double.TryParse(_txtMass?.Text, out double m) && m > 0)
            return m * World.EarthMass;
        return 1.0 * World.EarthMass;
    }

    public double[] GetCreateMix()
    {
        var mix = new double[World.NElem];
        for (int i = 0; i < World.NElem; i++)
        {
            mix[i] = _sliders[i]?.Value ?? 0;
        }
        return mix;
    }

    public void UpdatePreview()
    {
        if (_lblSum == null || _lblPreview == null || _lblParent == null) return;

        double[] mix = GetCreateMix();
        double sum = 0;
        for (int i = 0; i < World.NElem; i++) sum += mix[i];
        _lblSum.Text = $"Tổng tỉ lệ: {sum:F2} (tự chuẩn hoá khi tạo)";

        double mass = GetCreateMass();
        int parent = _main.Selected;
        var (previewKind, previewR) = GodTools.Preview(_w, mass, mix, parent);

        _lblPreview.Text = $"Dự kiến: {KindVi[(int)previewKind]}   Bán kính: {previewR:G3}";
        if (parent >= 0 && parent < _w.N && _w.Alive[parent])
        {
            string pName = _w.Name[parent] ?? $"Vật thể #{parent}";
            _lblParent.Text = $"Quỹ đạo quanh: {pName}";
        }
        else
        {
            _lblParent.Text = "Tâm: Không (đứng yên tại chỗ thả)";
        }
    }

    public void RefreshSelection()
    {
        UpdatePreview();
        UpdateEditTab();
        UpdatePushTab();
    }

    void UpdateEditTab()
    {
        if (_lblEditTarget == null) return;
        int sel = _main.Selected;
        if (sel < 0 || sel >= _w.N || !_w.Alive[sel])
        {
            _lblEditTarget.Text = "Chưa chọn vật thể nào.";
            for (int e = 0; e < World.NElem; e++)
                if (_editElemLabels[e] != null) _editElemLabels[e].Text = "0%";
            return;
        }

        string sName = _w.Name[sel] ?? $"Vật thể #{sel}";
        _lblEditTarget.Text = $"Đang chọn: {sName} (khối lượng {_w.M[sel] / World.EarthMass:G4} M⊕)";

        double total = _w.M[sel];
        for (int e = 0; e < World.NElem; e++)
        {
            double val = _w.Comp[sel * World.NElem + e];
            double pct = total > 0 ? (val / total) * 100 : 0;
            if (_editElemLabels[e] != null)
                _editElemLabels[e].Text = $"{val / World.EarthMass:G3} ({pct:F1}%)";
        }
    }

    void UpdatePushTab()
    {
        if (_lblPushTarget == null) return;
        int sel = _main.Selected;
        if (sel < 0 || sel >= _w.N || !_w.Alive[sel])
        {
            _lblPushTarget.Text = "Chưa chọn vật thể nào.";
            return;
        }
        string sName = _w.Name[sel] ?? $"Vật thể #{sel}";
        double spd = Math.Sqrt(_w.Vx[sel] * _w.Vx[sel] + _w.Vy[sel] * _w.Vy[sel]);
        _lblPushTarget.Text = $"Đang chọn: {sName} (Vx={_w.Vx[sel]:F3}, Vy={_w.Vy[sel]:F3}, v={spd:F3})";
    }
}
