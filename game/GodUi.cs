using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using Cosmos.Core;

namespace Cosmos.Game;

/// <summary>
/// Panels of the window (SPEC 7 round 3). Laid out by anchors, so they follow the window size.
///   bottom bar  : time — pause, speed, jumps. Always on screen.
///   right panel : tabs "Tạo" (what the create tool drops), "Vật thể" (the selected object), "Vũ trụ" (rules and
///                 constants), "Nhật ký" (events as sentences). Tab hides it.
/// No button, slider or tab takes the keyboard: Space must always pause, never press the last button again.
/// </summary>
public partial class GodUi : CanvasLayer
{
    readonly Main _main;
    readonly World _w;

    Control _side = null!;
    TabContainer _tabs = null!;

    // create
    Button _btnCreate = null!;
    LineEdit _txtMass = null!;
    readonly HSlider[] _sliders = new HSlider[World.NElem];
    Label _lblPreview = null!;

    // selected object
    Label _lblTarget = null!;
    VBoxContainer _objBody = null!;
    readonly Label[] _elemLabels = new Label[World.NElem];
    HSlider _sliderSeed = null!;
    Button _btnSeed = null!, _btnWipe = null!;
    Label _lblLife = null!;

    // tools
    readonly Button[] _toolButtons = new Button[6];
    HBoxContainer _handOptions = null!;
    HSlider _handRadius = null!, _handStrength = null!;
    CheckBox _moveCircular = null!, _moveCarry = null!;
    public float HandRadiusPx => (float)_handRadius.Value;
    public double HandStrength => _handStrength.Value;
    public bool MoveCircular => _moveCircular.ButtonPressed;
    public bool MoveAlone => !_moveCarry.ButtonPressed;
    public void SetMoveCircular(bool on) => _moveCircular.SetPressedNoSignal(on);

    // time
    Button _btnPause = null!;
    readonly Button[] _warpButtons = new Button[7];
    static readonly int[] WarpSpeeds = { 1, 2, 4, 8, 16, 32, 64 };

    // universe
    VBoxContainer _constList = null!;

    // log
    RichTextLabel _txtEvents = null!;
    int _eventsCount = -1; double _eventsLastYear = double.NaN;

    public static readonly string[] ElemVi = { "Khí nhẹ", "Băng", "Đá", "Kim loại", "Carbon", "Phóng xạ" };
    public static readonly string[] KindVi = { "Sao", "Hành tinh", "Vệ tinh", "Tiểu hành tinh" };
    public static readonly string[] StarPhaseVi = { "", "Sao lùn nâu", "Sao dãy chính", "Sao khổng lồ đỏ", "Sao lùn trắng", "Sao neutron", "Lỗ đen" };

    /// What the object is called in the panel: the star's stage when it is or was a star, else its kind.
    public static string KindName(World w, int i)
    {
        StarPhase ph = w.StarPhaseOf(i);
        if (ph == StarPhase.None) return KindVi[(int)w.KindOf(i)];
        StarSpectrum sp = w.StarSpectralClass(i);
        return ph == StarPhase.MainSequence && sp != StarSpectrum.None ? $"{StarPhaseVi[(int)ph]} loại {sp}" : StarPhaseVi[(int)ph];
    }

    /// Panel lines for a star or what is left of one; empty for anything else.
    public static string StarLines(World w, int i)
    {
        StarPhase ph = w.StarPhaseOf(i);
        if (ph == StarPhase.None) return "";
        var sb = new System.Text.StringBuilder();
        double t = w.StarSurfaceTemperature(i), light = w.StarLuminosity(i);
        if (ph == StarPhase.BlackHole) sb.Append("không phát sáng\n");
        else sb.Append($"bề mặt {t:N0} K   độ sáng {light:G3} Mặt Trời\n");
        sb.Append($"tuổi {Years(w.StarAge[i])}");
        if (ph == StarPhase.MainSequence)
            sb.Append($"   đã đốt {Math.Clamp(w.StarFuel[i], 0, 1) * 100:F1}% nhiên liệu (cả đời khoảng {Years(w.StarLifetime(w.M[i]))})");
        else if (ph == StarPhase.RedGiant) sb.Append("   lõi đã cạn hydro, đang phình to và thổi vật chất ra ngoài");
        else if (ph is StarPhase.WhiteDwarf or StarPhase.NeutronStar) sb.Append($"   tàn dư, nguội dần đã {Years(w.StarCoolingAge[i])}");
        else if (ph == StarPhase.BrownDwarf) sb.Append("   quá nhẹ để đốt hydro");
        return sb.Append('\n').ToString();
    }

    public static string Years(double y) => !double.IsFinite(y) ? "vô hạn" : y >= 1e9 ? $"{y / 1e9:G3} tỉ năm" : y >= 1e6 ? $"{y / 1e6:G3} triệu năm" : $"{y:N0} năm";
    public static readonly string[] WaterVi = { "Không có", "Băng tuyết", "Nước lỏng", "Hơi nước" };
    public static readonly string[] LifeStageVi = { "Chưa có", "Vi sinh vật", "Đa bào phức tạp", "Sinh quyển trù phú" };
    public static readonly string[] TechStageVi = { "Chưa phát triển", "Thời kỳ Nông nghiệp", "Thời kỳ Công nghiệp", "Kỷ nguyên Không gian" };
    public static readonly string[] BandVi = { "Đóng băng", "Ôn đới", "Thiêu đốt" };
    static readonly Dictionary<string, string> RuleVi = new() { ["temperature"] = "Nhiệt độ", ["water"] = "Nước", ["life"] = "Sự sống", ["civ"] = "Văn minh" };

    // what the create buttons drop: name, mass in Earths, shares of gas / ice / rock / metal / carbon / radio
    static readonly (string Name, double Earths, double[] Mix)[] Presets =
    {
        ("Thiên thạch", 1e-4, new[] { 0, 0.05, 0.75, 0.18, 0.02, 0 }),
        ("Vệ tinh", 0.0123, new[] { 0, 0.01, 0.80, 0.18, 0.005, 0.005 }),
        ("Hành tinh đá", 1, new[] { 0, 0.01, 0.66, 0.32, 0.005, 0.005 }),
        ("Hành tinh khí", 317.8, new[] { 0.90, 0.05, 0.03, 0.02, 0, 0 }),
        ("Sao", 50 / World.EarthMass, new[] { 1.0, 0, 0, 0, 0, 0 }),
    };

    public GodUi(Main main, World w)
    {
        _main = main;
        _w = w;
    }

    static Button Btn(string text, Action pressed, float minWidth = 0)
    {
        var b = new Button { Text = text, FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(minWidth, 0) };
        b.Pressed += pressed;
        return b;
    }

    static Label Note(string text) => new()
    {
        Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, Modulate = new Color(0.72f, 0.76f, 0.86f),
        SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
    };

    static bool Number(string text, out double value) =>
        double.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);

    public override void _Ready()
    {
        BuildTimeBar();

        // right panel: full height, fixed width, clear of the time bar
        var side = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop };
        side.AnchorLeft = 1; side.AnchorRight = 1; side.AnchorTop = 0; side.AnchorBottom = 1;
        side.OffsetLeft = -372; side.OffsetRight = -10; side.OffsetTop = 10; side.OffsetBottom = -10;
        AddChild(side);
        _side = side;

        var margin = new MarginContainer();
        foreach (string m in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" }) margin.AddThemeConstantOverride(m, 6);
        side.AddChild(margin);
        _tabs = new TabContainer();
        _tabs.GetTabBar().FocusMode = Control.FocusModeEnum.None;
        margin.AddChild(_tabs);

        BuildCreateTab();
        BuildObjectTab();
        BuildUniverseTab();
        BuildEventsTab();

        ApplyPreset(2);
        UpdateCreating(false);
        UpdatePaused(false);
        RefreshSelection();
    }

    public void TogglePanel() => _side.Visible = !_side.Visible;

    VBoxContainer Tab(string title)
    {
        var pad = new MarginContainer { Name = title };
        foreach (string m in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" }) pad.AddThemeConstantOverride(m, 8);
        _tabs.AddChild(pad);
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 8);
        pad.AddChild(v);
        return v;
    }

    // ---- time bar ----

    void BuildTimeBar()
    {
        // tools sit right above the time bar
        var tools = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop };
        tools.AnchorLeft = 0; tools.AnchorRight = 0; tools.AnchorTop = 1; tools.AnchorBottom = 1;
        tools.OffsetLeft = 10; tools.OffsetBottom = -62; tools.OffsetTop = -62;
        tools.GrowVertical = Control.GrowDirection.Begin; tools.GrowHorizontal = Control.GrowDirection.End;
        AddChild(tools);
        var toolPad = new MarginContainer();
        foreach (string m in new[] { "margin_left", "margin_right" }) toolPad.AddThemeConstantOverride(m, 8);
        foreach (string m in new[] { "margin_top", "margin_bottom" }) toolPad.AddThemeConstantOverride(m, 5);
        tools.AddChild(toolPad);
        var toolRow = new HBoxContainer();
        toolRow.AddThemeConstantOverride("separation", 4);
        toolPad.AddChild(toolRow);
        var names = new[] { "Chọn (Esc)", "Tạo (C)", "Vector (V)", "Di dời (M)", "Hút (A)", "Đẩy ra (R)" };
        for (int i = 0; i < names.Length; i++)
        {
            var tool = (Main.Tool)i;
            var b = Btn(names[i], () => _main.SetTool(tool));
            b.ToggleMode = true;
            _toolButtons[i] = b;
            toolRow.AddChild(b);
        }
        _moveCircular = new CheckBox { Text = "thả ra là vào quỹ đạo tròn", ButtonPressed = true, FocusMode = Control.FocusModeEnum.None, Visible = false };
        toolRow.AddChild(_moveCircular);
        _moveCarry = new CheckBox { Text = "kéo theo những gì nó đang giữ", ButtonPressed = true, FocusMode = Control.FocusModeEnum.None, Visible = false };
        toolRow.AddChild(_moveCarry);
        _handOptions = new HBoxContainer { Visible = false };
        _handOptions.AddChild(new Label { Text = "  Tầm" });
        _handRadius = new HSlider { MinValue = 20, MaxValue = 400, Step = 5, Value = 90, FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(110, 0), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        _handOptions.AddChild(_handRadius);
        _handOptions.AddChild(new Label { Text = "  Lực" });
        _handStrength = new HSlider { MinValue = 0.05, MaxValue = 5, Step = 0.05, Value = 0.5, FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(110, 0), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        _handOptions.AddChild(_handStrength);
        toolRow.AddChild(_handOptions);
        UpdateTool(Main.Tool.Select);

        var bar = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop };
        bar.AnchorLeft = 0; bar.AnchorRight = 0; bar.AnchorTop = 1; bar.AnchorBottom = 1;
        bar.OffsetLeft = 10; bar.OffsetBottom = -10; bar.OffsetTop = -10;
        bar.GrowVertical = Control.GrowDirection.Begin; bar.GrowHorizontal = Control.GrowDirection.End;
        AddChild(bar);
        var pad = new MarginContainer();
        foreach (string m in new[] { "margin_left", "margin_right" }) pad.AddThemeConstantOverride(m, 8);
        foreach (string m in new[] { "margin_top", "margin_bottom" }) pad.AddThemeConstantOverride(m, 5);
        bar.AddChild(pad);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 4);
        pad.AddChild(row);

        _btnPause = Btn("Tạm dừng", _main.TogglePause, 96);
        row.AddChild(_btnPause);
        for (int i = 0; i < WarpSpeeds.Length; i++)
        {
            int speed = WarpSpeeds[i];
            var b = Btn($"{speed}×", () => _main.SetTimeWarp(speed), 40);
            b.ToggleMode = true;
            _warpButtons[i] = b;
            row.AddChild(b);
        }
        row.AddChild(new VSeparator());
        row.AddChild(new Label { Text = "Nhảy tới" });
        foreach (var (text, years) in new[] { ("+1 năm", 1.0), ("+100 năm", 100.0), ("+1 vạn năm", 1e4), ("+1 triệu năm", 1e6), ("+100 triệu năm", 1e8), ("+1 tỉ năm", 1e9) })
        {
            double y = years; string t = text;
            row.AddChild(Btn(text, () =>
            {
                int before = _w.Events.Count;
                GodTools.FastForward(_w, y);
                int news = _w.Events.Count - before;
                _main.Toast(news > 0 ? $"Đã nhảy {t.TrimStart('+')}: {news} sự kiện mới, xem tab Nhật ký" : $"Đã nhảy {t.TrimStart('+')}: không có gì đáng kể xảy ra");
                RefreshSelection();
                RefreshEvents();
            }));
        }
    }

    public void UpdateTool(Main.Tool tool)
    {
        for (int i = 0; i < _toolButtons.Length; i++) _toolButtons[i]?.SetPressedNoSignal(i == (int)tool);
        if (_handOptions != null) _handOptions.Visible = tool == Main.Tool.Pull || tool == Main.Tool.Shove;
        if (_moveCircular != null) _moveCircular.Visible = _moveCarry.Visible = tool == Main.Tool.Move;
    }

    public void UpdateTimeWarp(int currentWarp)
    {
        for (int i = 0; i < WarpSpeeds.Length; i++) _warpButtons[i]?.SetPressedNoSignal(WarpSpeeds[i] == currentWarp);
    }

    public void UpdatePaused(bool paused)
    {
        if (_btnPause != null) _btnPause.Text = paused ? "Chạy tiếp" : "Tạm dừng";
    }

    // ---- create ----

    void BuildCreateTab()
    {
        var v = Tab("Tạo");

        _btnCreate = Btn("", () => _main.SetCreating(!_main.Creating));
        _btnCreate.CustomMinimumSize = new Vector2(0, 40);
        v.AddChild(_btnCreate);
        v.AddChild(Note("Con trỏ mang theo vật thể mới. Bấm vào không gian: nó tự quay tròn quanh vật đang thống trị chỗ đó. Giữ và kéo: phóng nó đi, đường vàng cho thấy trước quỹ đạo."));

        v.AddChild(new HSeparator());
        var grid = new GridContainer { Columns = 2 };
        for (int i = 0; i < Presets.Length; i++)
        {
            int idx = i;
            var b = Btn(Presets[i].Name, () => { ApplyPreset(idx); if (!_main.Creating) _main.SetCreating(true); }, 168);
            grid.AddChild(b);
        }
        v.AddChild(grid);

        var rowM = new HBoxContainer();
        rowM.AddChild(new Label { Text = "Khối lượng (số Trái Đất)", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        _txtMass = new LineEdit { CustomMinimumSize = new Vector2(96, 0) };
        _txtMass.TextChanged += _ => UpdatePreview();
        _txtMass.TextSubmitted += _ => _txtMass.ReleaseFocus();
        rowM.AddChild(_txtMass);
        v.AddChild(rowM);

        v.AddChild(new Label { Text = "Thành phần" });
        for (int e = 0; e < World.NElem; e++)
        {
            var row = new HBoxContainer();
            row.AddChild(new Label { Text = ElemVi[e], CustomMinimumSize = new Vector2(78, 0) });
            var slider = new HSlider { MinValue = 0, MaxValue = 1, Step = 0.005, FocusMode = Control.FocusModeEnum.None, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
            slider.ValueChanged += _ => UpdatePreview();
            _sliders[e] = slider;
            row.AddChild(slider);
            v.AddChild(row);
        }
        _lblPreview = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        v.AddChild(_lblPreview);
    }

    public void ApplyPreset(int i)
    {
        _txtMass.Text = Presets[i].Earths.ToString("G4", CultureInfo.InvariantCulture);
        for (int e = 0; e < World.NElem; e++) _sliders[e].SetValueNoSignal(Presets[i].Mix[e]);
        UpdatePreview();
    }

    public void UpdateCreating(bool on)
    {
        if (_btnCreate == null) return;
        _btnCreate.Text = on ? "Đang đặt vật thể — bấm để thôi (Esc)" : "Đặt vật thể vào không gian (C)";
        _btnCreate.Modulate = on ? new Color(1f, 0.9f, 0.35f) : Colors.White;
    }

    public double GetCreateMass() => (Number(_txtMass.Text, out double m) && m > 0 ? m : 1) * World.EarthMass;

    public double[] GetCreateMix()
    {
        var mix = new double[World.NElem];
        for (int i = 0; i < World.NElem; i++) mix[i] = _sliders[i].Value;
        return mix;
    }

    void UpdatePreview()
    {
        if (_lblPreview == null) return;
        double[] mix = GetCreateMix();
        double sum = 0;
        foreach (double s in mix) sum += s;
        var (kind, radius) = GodTools.Preview(_w, GetCreateMass(), mix);
        var parts = new List<string>();
        for (int e = 0; e < World.NElem; e++) if (sum > 0 && mix[e] / sum >= 0.005) parts.Add($"{ElemVi[e]} {100 * mix[e] / sum:0.#}%");
        _lblPreview.Text = $"Sẽ là: {KindVi[(int)kind]}, bán kính {radius:G3}\n{(parts.Count > 0 ? string.Join(" · ", parts) : "Đá 100%")}";
    }

    // ---- selected object ----

    void BuildObjectTab()
    {
        var v = Tab("Vật thể");
        _lblTarget = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        v.AddChild(_lblTarget);

        _objBody = new VBoxContainer();
        _objBody.AddThemeConstantOverride("separation", 8);
        v.AddChild(_objBody);

        var row = new HBoxContainer();
        row.AddChild(Btn("Bám theo (F)", _main.FollowSelected));
        row.AddChild(Btn("Về quỹ đạo tròn", _main.CircularizeSelected));
        row.AddChild(Btn("Tạo vành đai (vật chất: tab Tạo)", _main.RingSelected));
        row.AddChild(Btn("Xoá (Del)", _main.RemoveSelected));
        _objBody.AddChild(row);
        _objBody.AddChild(Note("Đẩy: giữ chuột trái trên vật đang chọn rồi kéo ra."));

        _objBody.AddChild(new HSeparator());
        _objBody.AddChild(new Label { Text = "Thêm / bớt vật chất (mỗi lần 10% khối lượng)" });
        for (int e = 0; e < World.NElem; e++)
        {
            int idx = e;
            var r = new HBoxContainer();
            r.AddChild(new Label { Text = ElemVi[e], CustomMinimumSize = new Vector2(78, 0) });
            _elemLabels[e] = new Label { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            r.AddChild(_elemLabels[e]);
            r.AddChild(Btn("−", () => EditMatter(idx, -0.1), 36));
            r.AddChild(Btn("+", () => EditMatter(idx, 0.1), 36));
            _objBody.AddChild(r);
        }

        _objBody.AddChild(new HSeparator());
        _lblLife = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _objBody.AddChild(_lblLife);
        _sliderSeed = new HSlider { MinValue = 0.05, MaxValue = 1, Step = 0.05, Value = 0.5, FocusMode = Control.FocusModeEnum.None };
        _sliderSeed.ValueChanged += _ => UpdateObjectTab();
        _objBody.AddChild(_sliderSeed);
        var rowSeed = new HBoxContainer();
        _btnSeed = Btn("Gieo sự sống", () => Seed(_sliderSeed.Value));
        _btnWipe = Btn("Diệt sạch sự sống", () => Seed(0));
        rowSeed.AddChild(_btnSeed); rowSeed.AddChild(_btnWipe);
        _objBody.AddChild(rowSeed);
        _objBody.AddChild(Note("Gieo xong có sống nổi hay không là do nước, nhiệt độ và thành phần của nó."));
    }

    bool Sel(out int i)
    {
        i = _main.Selected;
        return i >= 0 && i < _w.N && _w.Alive[i];
    }

    void EditMatter(int elem, double shareOfMass)
    {
        if (!Sel(out int i)) return;
        GodTools.AddMatter(_w, i, elem, shareOfMass * _w.M[i]);
        UpdateObjectTab();
    }

    void Seed(double level)
    {
        if (!Sel(out int i) || !_w.IsWorld(i)) return;
        string name = _w.Name[i] ?? $"Vật thể #{i}";
        if (GodTools.SeedLife(_w, i, level) >= 0) _main.Toast(level > 0 ? $"Đã gieo sự sống {level * 100:F0}% lên {name}" : $"Đã diệt sạch sự sống trên {name}");
        UpdateObjectTab();
    }

    void UpdateObjectTab()
    {
        if (_lblTarget == null) return;
        if (!Sel(out int i))
        {
            _lblTarget.Text = "Chưa chọn vật thể nào. Bấm vào một vật thể trong không gian.";
            _objBody.Visible = false;
            return;
        }
        _objBody.Visible = true;
        double m = _w.M[i];
        _lblTarget.Text = $"{_w.Name[i] ?? $"Vật thể #{i}"} — {KindVi[(int)_w.KindOf(i)]}, {m / World.EarthMass:G4} Trái Đất";
        for (int e = 0; e < World.NElem; e++) _elemLabels[e].Text = $"{100 * _w.Comp[i * World.NElem + e] / m:F1}%";
        bool world = _w.IsWorld(i);
        _sliderSeed.Editable = world; _btnSeed.Disabled = !world; _btnWipe.Disabled = !world || _w.Life[i] <= 0;
        _btnSeed.Text = $"Gieo sự sống {_sliderSeed.Value * 100:F0}%";
        _lblLife.Text = !world ? "Sự sống: chỉ hành tinh và vệ tinh mới mang được."
            : _w.Life[i] > 0 ? $"Sự sống hiện tại: {_w.Life[i] * 100:F1}% ({LifeStageVi[_w.LifeStage(i)]})"
            : "Sự sống: chưa có.";
    }

    public void RefreshSelection() { UpdateObjectTab(); }

    /// Numbers that move by themselves (called a few times a second).
    public void RefreshLive() { if (_side.Visible && _tabs.CurrentTab == 1) UpdateObjectTab(); }

    // ---- universe: rules and constants ----

    void BuildUniverseTab()
    {
        var v = Tab("Vũ trụ");
        v.AddChild(new Label { Text = "Luật đang chạy" });
        foreach (var rule in _w.Rules)
        {
            string id = rule.Id;
            var chk = new CheckBox { Text = $"{(RuleVi.TryGetValue(id, out string? vi) ? vi : id)}  (mỗi {rule.RhythmYears:G3} năm)", ButtonPressed = rule.Enabled, FocusMode = Control.FocusModeEnum.None };
            chk.Toggled += on => { GodTools.SetRule(_w, id, on); _main.Toast($"Luật {(RuleVi.TryGetValue(id, out string? n) ? n : id)}: {(on ? "bật" : "tắt")}"); };
            v.AddChild(chk);
        }
        v.AddChild(new HSeparator());
        v.AddChild(new Label { Text = "Hằng số (gõ số mới rồi Enter)" });
        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _constList = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        scroll.AddChild(_constList);
        v.AddChild(scroll);
        RefreshConsts();
    }

    public void RefreshConsts()
    {
        if (_constList == null) return;
        foreach (Node c in _constList.GetChildren()) c.QueueFree();
        foreach (var (name, val) in _w.C.All())
        {
            var row = new HBoxContainer();
            row.AddChild(new Label { Text = name, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, ClipText = true });
            var txt = new LineEdit { Text = val.ToString("G6", CultureInfo.InvariantCulture), CustomMinimumSize = new Vector2(110, 0) };
            string cname = name;
            txt.TextSubmitted += text =>
            {
                bool ok = Number(text, out double nv) && GodTools.SetConst(_w, cname, nv);
                _main.Toast(ok ? $"{cname} = {nv.ToString("G6", CultureInfo.InvariantCulture)}" : $"{cname}: số không hợp lệ");
                txt.ReleaseFocus();
            };
            row.AddChild(txt);
            _constList.AddChild(row);
        }
    }

    // ---- event log ----

    void BuildEventsTab()
    {
        var v = Tab("Nhật ký");
        _txtEvents = new RichTextLabel { BbcodeEnabled = true, ScrollActive = true, SizeFlagsVertical = Control.SizeFlags.ExpandFill, FocusMode = Control.FocusModeEnum.None };
        v.AddChild(_txtEvents);
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

        if (e.RuleId == "civ") return yr + CivLine(w, e, false);

        if (e.RuleId == "impact")
        {
            return $"{yr}[color=#ff8844]Thiên thạch va chạm mạnh![/color] Tỉ lệ va chạm {e.A * 100:F2}% khối lượng, cắt giảm sinh quyển từ {e.B * 100:F1}% còn {e.C * 100:F1}%.";
        }

        if (e.RuleId == "contact")
        {
            int by = (int)e.A;
            string eater = by >= 0 && by < w.N && w.Alive[by] ? (w.Name[by] ?? $"Vật thể #{by}") : "một vật khác";
            return $"{yr}[color=#ff8844]bị {eater} nuốt chửng[/color] ({e.B / World.EarthMass:G3} Trái Đất).";
        }

        if (e.RuleId == "stars")
        {
            if (e.Change == "star.giant") return $"{yr}[color=#ff7755]cạn hydro ở lõi, phình thành sao khổng lồ đỏ[/color], sáng gấp {e.C:G3} lần Mặt Trời.";
            if (e.Change == "star.nova") return $"{yr}[color=#ffffff][b]nổ tung khi chết[/b][/color] (sao nặng {e.A:G3} Mặt Trời).";
            if (e.Change == "star.remnant.white") return $"{yr}lõi còn lại thành [color=#eef3ff]sao lùn trắng[/color].";
            if (e.Change == "star.remnant.neutron") return $"{yr}lõi sụp thành [color=#bacfff]sao neutron[/color].";
            if (e.Change == "star.remnant.black") return $"{yr}lõi sụp thành [color=#ffb36d]lỗ đen[/color].";
        }

        return $"{yr}[{e.RuleId}] {e.Change}";
    }

    /// One line of a civilisation's story. `dated`: start with the year and the place (the object panel's chronicle);
    /// the journal already has both in front.
    public static string CivLine(World w, RuleEvent e, bool dated = true)
    {
        int slot = e.ObjectSlot;
        string place = slot >= 0 && slot < w.N ? (w.Name[slot] ?? $"Vật thể #{slot}") : "?";
        int civ = slot >= 0 && slot < w.N ? w.Civ[slot] : -1;
        if (e.Change is "civ.colony" or "civ.ship.first" or "civ.ship.turned" or "civ.ship.lost") civ = (int)e.A;
        string who = civ >= 0 && civ < w.Civs.Count ? w.Civs[civ].Name : "?";
        bool home = civ >= 0 && civ < w.Civs.Count && w.Civs[civ].Home == slot;
        string head = dated ? $"năm {e.Year:N0}, {place}: " : "";
        if (e.Change == "civ.start")
            return $"{head}[color=#ffd700]Văn minh {who} trỗi dậy[/color] sau {e.A:N0} năm sinh quyển cực thịnh.";
        if (e.Change == "civ.ship.first")
        {
            int goal = (int)e.B;
            string to = goal >= 0 && goal < w.N && w.Name[goal] != null ? w.Name[goal] : "một thế giới khác";
            return $"{head}[color=#ffd700]{who} phóng con tàu đầu tiên[/color], hướng tới {to}.";
        }
        if (e.Change == "civ.ship.turned")
        {
            int owner = (int)e.B;
            return $"{head}Tàu của {who} tới nơi thì {place} đã có văn minh {(owner >= 0 && owner < w.Civs.Count ? w.Civs[owner].Name : "khác")}. Không ai bước xuống.";
        }
        if (e.Change == "civ.ship.lost")
        {
            int goal = (int)e.B;
            string to = goal >= 0 && goal < w.N && w.Alive[goal] && w.Name[goal] != null ? $" trên đường tới {w.Name[goal]}" : "; nơi nó hướng tới không còn";
            return $"{head}[color=#ff8866]{who} mất một con tàu[/color]{to}.";
        }
        if (e.Change == "civ.colony")
            return $"{head}[color=#ffd700]{who} lập thuộc địa[/color]{(e.C < w.C.CivLifeMin / 5 ? " trong vòm kín" : " giữa sinh quyển sẵn có")}.";
        if (e.Change == "civ.end")
            return home ? $"{head}[color=#ff4444]{who} diệt vong trên quê hương.[/color]" : $"{head}[color=#ff4444]Thuộc địa của {who} lụi tàn.[/color]";
        if (e.Change.StartsWith("civ.stage."))
        {
            var parts = e.Change.Split('.');
            if (parts.Length == 4 && int.TryParse(parts[3], out int n))
            {
                string stage = n >= 0 && n < TechStageVi.Length ? TechStageVi[n] : $"Cấp {n}";
                return $"{head}{who} bước vào [color=#ffd700][b]{stage}[/b][/color] (dân số {e.B * 100:F1}%).";
            }
        }
        return $"{head}{who}: {e.Change}";
    }

    public void RefreshEvents()
    {
        if (_txtEvents == null) return;
        int count = _w.Events.Count;
        double last = count > 0 ? _w.Events[count - 1].Year : double.NaN;
        if (count == _eventsCount && last.Equals(_eventsLastYear)) return; // nothing new: keep the scroll where it is
        _eventsCount = count; _eventsLastYear = last;
        if (count == 0) { _txtEvents.Text = "Chưa có sự kiện nào. Thử nhảy tới 1 triệu năm."; return; }
        var sb = new System.Text.StringBuilder();
        for (int i = count - 1; i >= Math.Max(0, count - 100); i--) sb.AppendLine(TranslateEvent(_w, _w.Events[i])).AppendLine();
        _txtEvents.Text = sb.ToString();
    }
}
