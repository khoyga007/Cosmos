using System;
using System.Collections.Generic;
using Godot;
using Cosmos.Core;

namespace Cosmos.Game;

/// <summary>
/// UI panel for god tools (SPEC 6 P2 & SPEC 7 Round 2):
/// 1. Create panel (mass + 6 element sliders, preview kind + radius).
/// 2. Edit & SeedLife panel (modify matter + seed life slider 0..1).
/// 3. Push panel (drag helper + quick velocity nudges).
/// 4. Time & FastForward panel (warp 1x..64x + jump 1 / 100 / 1e4 / 1e6 years).
/// 5. Rules & Consts panel (SetRule switches + editable Consts.All()).
/// 6. Event log panel (World.Events translated into Vietnamese sentences).
/// </summary>
public partial class GodUi : CanvasLayer
{
    readonly Main _main;
    readonly World _w;

    // Create tab
    LineEdit _txtMass = null!;
    readonly HSlider[] _sliders = new HSlider[World.NElem];
    readonly Label[] _sliderLabels = new Label[World.NElem];
    Label _lblSum = null!;
    Label _lblPreview = null!;
    Label _lblParent = null!;

    // Edit tab
    Label _lblEditTarget = null!;
    readonly Label[] _editElemLabels = new Label[World.NElem];
    HSlider _sliderSeedLife = null!;
    Label _lblSeedVal = null!;
    Button _btnSeedLife = null!;

    // Push tab
    Label _lblPushTarget = null!;

    // Time & Jump tab
    readonly Button[] _warpButtons = new Button[7];
    static readonly int[] WarpSpeeds = { 1, 2, 4, 8, 16, 32, 64 };

    // Consts & Rules tab
    VBoxContainer _rulesList = null!;
    VBoxContainer _constList = null!;
    readonly Dictionary<string, LineEdit> _constEdits = new();

    // Event Log tab
    RichTextLabel _txtEvents = null!;

    public static readonly string[] ElemVi = { "Khí nhẹ", "Băng", "Đá", "Kim loại", "Carbon", "Phóng xạ" };
    public static readonly string[] KindVi = { "Sao", "Hành tinh", "Vệ tinh", "Tiểu hành tinh" };
    public static readonly string[] WaterVi = { "Không có", "Băng tuyết", "Nước lỏng", "Hơi nước" };
    public static readonly string[] LifeStageVi = { "Chưa có", "Vi sinh vật", "Đa bào phức tạp", "Sinh quyển trù phú" };
    public static readonly string[] TechStageVi = { "Chưa phát triển", "Thời kỳ Nông nghiệp", "Thời kỳ Công nghiệp", "Kỷ nguyên Không gian" };
    public static readonly string[] BandVi = { "Đóng băng", "Ôn đới", "Thiêu đốt" };

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
        BuildTimeJumpTab(tabs);
        BuildRulesConstsTab(tabs);
        BuildEventsTab(tabs);

        UpdatePreview();
    }

    void BuildCreateTab(TabContainer tabs)
    {
        var vbox = new VBoxContainer { Name = "Tạo vật thể" };
        tabs.AddChild(vbox);

        vbox.AddChild(new Label { Text = "Tạo vật thể mới (thả bằng chuột phải):" });

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
            Text = "Thao tác: Bấm chuột phải vào không gian để thả.\n- Nếu đang chọn vật thể: bay quỹ đạo tròn.\n- Nếu không chọn vật thể: đứng yên.",
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
        var vbox = new VBoxContainer { Name = "Sửa & Gieo sống" };
        tabs.AddChild(vbox);

        _lblEditTarget = new Label { Text = "Chưa chọn vật thể nào." };
        vbox.AddChild(_lblEditTarget);
        vbox.AddChild(new HSeparator());

        vbox.AddChild(new Label { Text = "Thêm / Bớt nguyên tố:" });
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

        vbox.AddChild(new HSeparator());
        vbox.AddChild(new Label { Text = "Quyền năng thần: Gieo mầm sống (SeedLife):" });

        var rowSeed = new HBoxContainer();
        _sliderSeedLife = new HSlider { MinValue = 0, MaxValue = 1, Step = 0.05, Value = 0.5, CustomMinimumSize = new Vector2(180, 0), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _lblSeedVal = new Label { Text = "50%", CustomMinimumSize = new Vector2(50, 0) };
        _sliderSeedLife.ValueChanged += v => _lblSeedVal.Text = $"{v * 100:F0}%";
        rowSeed.AddChild(_sliderSeedLife);
        rowSeed.AddChild(_lblSeedVal);
        vbox.AddChild(rowSeed);

        var rowSeedBtns = new HBoxContainer();
        _btnSeedLife = new Button { Text = "Gieo sự sống", CustomMinimumSize = new Vector2(120, 0) };
        _btnSeedLife.Pressed += () =>
        {
            int sel = _main.Selected;
            if (sel >= 0 && sel < _w.N && _w.Alive[sel] && _w.IsWorld(sel))
            {
                GodTools.SeedLife(_w, sel, _sliderSeedLife.Value);
                UpdateEditTab();
            }
        };
        rowSeedBtns.AddChild(_btnSeedLife);

        var btnWipe = new Button { Text = "Diệt sạch (level 0)" };
        btnWipe.Pressed += () =>
        {
            int sel = _main.Selected;
            if (sel >= 0 && sel < _w.N && _w.Alive[sel] && _w.IsWorld(sel))
            {
                GodTools.SeedLife(_w, sel, 0);
                UpdateEditTab();
            }
        };
        rowSeedBtns.AddChild(btnWipe);
        vbox.AddChild(rowSeedBtns);
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
            Text = "Cách đẩy chuột:\n1. Bấm chuột trái chọn vật thể.\n2. Giữ chuột trái trên vật thể và kéo ra ngoài.\n3. Mũi tên lực vàng sẽ hiện; thả chuột để kích hoạt.\n\nHoặc bấm nút đẩy nhanh bên dưới:",
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

    void BuildTimeJumpTab(TabContainer tabs)
    {
        var vbox = new VBoxContainer { Name = "Thời gian & Nhảy" };
        tabs.AddChild(vbox);

        vbox.AddChild(new Label { Text = "Tốc độ bước tính (Advance / khung hình):" });
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

        vbox.AddChild(new HSeparator());
        vbox.AddChild(new Label { Text = "Cú nhảy thời gian FastForward (quỹ đạo giải tích):" });

        var rowJump1 = new HBoxContainer();
        AddJumpButton(rowJump1, "Nhảy 1 năm", 1.0);
        AddJumpButton(rowJump1, "Nhảy 100 năm", 100.0);
        vbox.AddChild(rowJump1);

        var rowJump2 = new HBoxContainer();
        AddJumpButton(rowJump2, "Nhảy 10.000 năm", 1e4);
        AddJumpButton(rowJump2, "Nhảy 1.000.000 năm", 1e6);
        vbox.AddChild(rowJump2);

        var note = new Label
        {
            Text = "\nPhím tắt:\n- 1 đến 7: chọn tốc độ 1× đến 64×\n- Space: tạm dừng / tiếp tục\n- Cú nhảy FastForward chạy mô hình 2 vật thể đóng; các luật sinh quyển tự chạy theo nhịp.",
            AutowrapMode = TextServer.AutowrapMode.Word
        };
        vbox.AddChild(note);
    }

    void AddJumpButton(HBoxContainer parent, string text, double years)
    {
        var btn = new Button { Text = text, CustomMinimumSize = new Vector2(170, 0) };
        btn.Pressed += () =>
        {
            GodTools.FastForward(_w, years);
            RefreshSelection();
            RefreshEvents();
        };
        parent.AddChild(btn);
    }

    void BuildRulesConstsTab(TabContainer tabs)
    {
        var vbox = new VBoxContainer { Name = "Luật & Hằng số" };
        tabs.AddChild(vbox);

        vbox.AddChild(new Label { Text = "Công tắc bật/tắt các luật (SetRule):" });
        _rulesList = new VBoxContainer();
        vbox.AddChild(_rulesList);
        RefreshRules();

        vbox.AddChild(new HSeparator());
        vbox.AddChild(new Label { Text = "Hằng số vũ trụ (Consts.All):" });
        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(390, 360),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        _constList = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        scroll.AddChild(_constList);
        vbox.AddChild(scroll);

        RefreshConsts();
    }

    public void RefreshRules()
    {
        if (_rulesList == null) return;
        foreach (Node c in _rulesList.GetChildren()) c.QueueFree();

        foreach (var rule in _w.Rules)
        {
            var row = new HBoxContainer();
            var chk = new CheckBox { Text = $"{rule.Id} (nhịp {rule.RhythmYears:G2} năm)", ButtonPressed = rule.Enabled };
            string rId = rule.Id;
            chk.Toggled += toggled =>
            {
                GodTools.SetRule(_w, rId, toggled);
            };
            row.AddChild(chk);
            _rulesList.AddChild(row);
        }
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

    void BuildEventsTab(TabContainer tabs)
    {
        var vbox = new VBoxContainer { Name = "Nhật ký" };
        tabs.AddChild(vbox);

        var rowTop = new HBoxContainer();
        rowTop.AddChild(new Label { Text = "Nhật ký sự kiện mô phỏng:", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        var btnRef = new Button { Text = "Làm mới" };
        btnRef.Pressed += RefreshEvents;
        rowTop.AddChild(btnRef);
        vbox.AddChild(rowTop);

        _txtEvents = new RichTextLabel
        {
            BbcodeEnabled = true,
            ScrollActive = true,
            CustomMinimumSize = new Vector2(390, 640),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        vbox.AddChild(_txtEvents);
        RefreshEvents();
    }

    public static string TranslateEvent(World w, RuleEvent e)
    {
        string name = e.ObjectSlot >= 0 && e.ObjectSlot < w.N ? (w.Name[e.ObjectSlot] ?? $"Vật thể #{e.ObjectSlot}") : "Thiên thể";
        string yr = $"[b][Năm {e.Year:N0}][/b] {name}: ";

        if (e.RuleId == "temperature")
        {
            if (e.Change.StartsWith("band."))
            {
                var parts = e.Change.Split('.');
                if (parts.Length == 3 && int.TryParse(parts[1], out int o) && int.TryParse(parts[2], out int n))
                {
                    string oldName = o >= 0 && o < BandVi.Length ? BandVi[o] : $"Dải {o}";
                    string newName = n >= 0 && n < BandVi.Length ? BandVi[n] : $"Dải {n}";
                    return $"{yr}Nhiệt độ đổi từ [b]{oldName}[/b] sang [b]{newName}[/b] ({e.B:F1} K).";
                }
            }
            return $"{yr}Thay đổi nhiệt độ ({e.Change}): {e.A:F1} → {e.B:F1} K.";
        }

        if (e.RuleId == "water")
        {
            if (e.Change.StartsWith("water."))
            {
                var parts = e.Change.Split('.');
                if (parts.Length == 3 && int.TryParse(parts[1], out int o) && int.TryParse(parts[2], out int n))
                {
                    string oldState = o >= 0 && o < WaterVi.Length ? WaterVi[o] : $"Mức {o}";
                    string newState = n >= 0 && n < WaterVi.Length ? WaterVi[n] : $"Mức {n}";
                    return $"{yr}Trạng thái nước chuyển từ [b]{oldState}[/b] sang [b]{newState}[/b] ({e.A:F1} K).";
                }
            }
            return $"{yr}Nước biến đổi ({e.Change}).";
        }

        if (e.RuleId == "life")
        {
            if (e.Change == "life.start")
                return $"{yr}[color=#55ff88]Mầm sống đầu tiên tự sinh khởi[/color] sau {e.A:N0} năm có nước lỏng!";
            if (e.Change.StartsWith("life.stage."))
            {
                var parts = e.Change.Split('.');
                if (parts.Length == 4 && int.TryParse(parts[2], out int o) && int.TryParse(parts[3], out int n))
                {
                    string oldStage = o >= 0 && o < LifeStageVi.Length ? LifeStageVi[o] : $"Cấp {o}";
                    string newStage = n >= 0 && n < LifeStageVi.Length ? LifeStageVi[n] : $"Cấp {n}";
                    return $"{yr}Sinh quyển tiến hoá: [b]{oldStage}[/b] → [color=#55ff88][b]{newStage}[/b][/color] (mức {e.A * 100:F1}%).";
                }
            }
            if (e.Change == "life.end")
                return $"{yr}[color=#ff5555]Đại tuyệt chủng![/color] Mọi sinh vật đã biến mất (nhiệt {e.A:F1} K).";
            return $"{yr}Sự sống thay đổi ({e.Change}).";
        }

        if (e.RuleId == "civ")
        {
            if (e.Change == "civ.start")
                return $"{yr}[color=#ffd700]Nền văn minh đầu tiên trỗi dậy[/color] sau {e.A:N0} năm sinh quyển cực thịnh!";
            if (e.Change.StartsWith("civ.stage."))
            {
                var parts = e.Change.Split('.');
                if (parts.Length == 4 && int.TryParse(parts[2], out int o) && int.TryParse(parts[3], out int n))
                {
                    string newStage = n >= 0 && n < TechStageVi.Length ? TechStageVi[n] : $"Cấp {n}";
                    return $"{yr}Văn minh bước vào [color=#ffd700][b]{newStage}[/b][/color] (dân số {e.B * 100:F1}%).";
                }
            }
            if (e.Change == "civ.end")
                return $"{yr}[color=#ff4444]Nền văn minh sụp đổ và diệt vong![/color]";
            return $"{yr}Văn minh biến chuyển ({e.Change}).";
        }

        if (e.RuleId == "impact")
        {
            return $"{yr}[color=#ff8844]Thiên thạch va chạm mạnh![/color] Tỉ lệ va chạm {e.A * 100:F2}% khối lượng, cắt giảm sinh quyển từ {e.B * 100:F1}% còn {e.C * 100:F1}%.";
        }

        return $"{yr}[{e.RuleId}] {e.Change}";
    }

    public void RefreshEvents()
    {
        if (_txtEvents == null) return;
        var sb = new System.Text.StringBuilder();
        int count = _w.Events.Count;
        if (count == 0)
        {
            _txtEvents.Text = "Chưa có sự kiện nào được ghi nhận.";
            return;
        }

        // Display newest first
        for (int i = count - 1; i >= Math.Max(0, count - 100); i--)
        {
            sb.AppendLine(TranslateEvent(_w, _w.Events[i]));
            sb.AppendLine();
        }
        _txtEvents.Text = sb.ToString();
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
            if (_btnSeedLife != null) _btnSeedLife.Disabled = true;
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

        if (_btnSeedLife != null)
            _btnSeedLife.Disabled = !_w.IsWorld(sel);
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
