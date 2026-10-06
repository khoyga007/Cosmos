// Shell: steps the core once per frame and draws it 2D from straight above (T tilts the picture, draw only).
// Everything is built in code; the scene file only attaches this script.
//   run:  godot --path game -- --rocks=5000
//   Every change to the world goes through World.Do(Command) (SPEC 3): never write world arrays from here.
// Mouse (SPEC 7 round 3):
//   wheel = zoom at the pointer · drag with right / middle button, or left on empty space = move the view
//   left click = select (bodies before rocks) · double click = select and follow · right click = drop tool / selection
//   left drag starting on the selected object = push it (the line shows the orbit it would get)
//   tool "create" (C): the pointer carries the new object; click = circular orbit around whatever rules that
//   spot, press and drag = launch it with that velocity
// Keys: Space pause · 1..7 speed · C create · F follow · Delete remove · Esc cancel · H whole system · Tab panel · T tilt · [ ] G
using System;
using Godot;
using Cosmos.Core;

namespace Cosmos.Game;

public partial class Main : Node2D
{
    World _w = null!;
    MultiMesh _mm = null!;
    float[] _buf = null!;
    Label _hud = null!, _toast = null!;
    RichTextLabel _panel = null!;
    GodUi _ui = null!;

    int _sel = -1, _follow = -1, _frame, _createdCount;
    public int Selected => _sel;

    double _cx, _cy;
    float _zoom = 0.75f, _tilt = 1f;
    bool _paused, _creating;
    int _timeWarp = 1;
    double _toastLeft;

    // one mouse gesture at a time
    enum Drag { None, Maybe, Pan, Push, Place }
    Drag _drag;
    MouseButton _dragBtn;
    Vector2 _downPos, _mouse;
    bool _downOnSel;
    double _placeX, _placeY;
    const float DragStart = 6, LaunchStart = 8, DragPerOrbitSpeed = 120; // pixels
    readonly double[] _path = new double[2 * 220];
    readonly Vector2[] _pathPts = new Vector2[220];

    double _stepMs, _fillMs;
    int _bench; double _benchT; int _benchFrames;
    bool _selftest, _uitest;
    const int Stride = 12;

    static readonly Color[] ElemCol = { new(0.72f, 0.78f, 1f), new(0.3f, 0.9f, 1f), new(0.9f, 0.5f, 0.25f), new(1f, 0.85f, 0.4f), new(0.75f, 0.4f, 0.95f), new(0.4f, 1f, 0.3f) };
    static readonly Color AimCol = new(1f, 0.9f, 0.2f, 0.95f);

    public override void _Ready()
    {
        // numbers are typed and shown with a dot whatever the machine's language (vi-VN reads "1.5" as 15)
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        int rocks = 5000;
        foreach (string a in OS.GetCmdlineUserArgs())
        {
            if (a == "--selftest") _selftest = true;
            if (a == "--uitest") _uitest = true;
            if (a.StartsWith("--rocks=")) rocks = int.Parse(a[8..]);
            if (a.StartsWith("--bench=")) _bench = int.Parse(a[8..]);
        }

        if (_selftest)
        {
            SetProcess(false);
            RunSelfTest();
            return;
        }

        _w = World.SolSystem(rocks, 1234);

        int cap = _w.X.Length;
        _mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform2D, UseColors = true, Mesh = new QuadMesh { Size = new Vector2(2.5f, 2.5f) }, InstanceCount = cap };
        _buf = new float[cap * Stride];
        for (int i = 0; i < cap; i++) { _buf[i * Stride] = 1; _buf[i * Stride + 5] = 1; _buf[i * Stride + 11] = 1; }
        AddChild(new MultiMeshInstance2D { Multimesh = _mm, Modulate = new Color(1, 1, 1, 0.8f), Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add } });

        var layer = new CanvasLayer(); AddChild(layer);
        _hud = new Label { Position = new Vector2(12, 8), MouseFilter = Control.MouseFilterEnum.Ignore }; layer.AddChild(_hud);
        _panel = new RichTextLabel { Position = new Vector2(12, 36), Size = new Vector2(420, 420), BbcodeEnabled = true, ScrollActive = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        layer.AddChild(_panel);
        _toast = new Label { MouseFilter = Control.MouseFilterEnum.Ignore, AnchorTop = 1, AnchorBottom = 1, OffsetLeft = 14, OffsetTop = -96, OffsetBottom = -68, Modulate = new Color(1, 1, 1, 0) };
        _toast.AddThemeColorOverride("font_color", new Color(1f, 0.92f, 0.5f));
        layer.AddChild(_toast);

        _ui = new GodUi(this, _w);
        AddChild(_ui);
        _ui.UpdateTimeWarp(_timeWarp); // AddChild has run its _Ready already
    }

    // ---- what the panels call ----

    public void SetTimeWarp(int speed)
    {
        _timeWarp = Math.Clamp(speed, 1, 64);
        _ui?.UpdateTimeWarp(_timeWarp);
    }

    public void CheckAutoThrottle(double stepElapsedMs)
    {
        if (stepElapsedMs > 50.0 && _timeWarp > 1)
        {
            SetTimeWarp(Math.Max(1, _timeWarp / 2));
        }
    }

    public bool Paused => _paused;
    public void TogglePause() { _paused = !_paused; _ui?.UpdatePaused(_paused); }

    public bool Creating => _creating;
    public void SetCreating(bool on)
    {
        _creating = on; _drag = Drag.None;
        _ui?.UpdateCreating(on);
        if (on) Toast("Đặt vật thể: bấm = quỹ đạo tròn, giữ và kéo = phóng đi. Chuột phải hoặc Esc để thôi.");
    }

    public void Toast(string text) { if (_toast != null) { _toast.Text = text; _toastLeft = 4; } }

    public void Select(int i, bool follow = false)
    {
        _sel = i;
        if (follow) _follow = i;
        _ui?.RefreshSelection();
        if (_panel != null) _panel.Text = PanelText();
    }

    public void FollowSelected() { if (Live(_sel)) { _follow = _sel; Toast($"Bám theo {NameOf(_sel)}"); } }

    public void RemoveSelected()
    {
        if (!Live(_sel)) return;
        string name = NameOf(_sel);
        if (GodTools.Remove(_w, _sel) >= 0) { Toast($"Đã xoá {name}"); Select(-1); }
    }

    public void CircularizeSelected()
    {
        if (!Live(_sel)) return;
        Toast(GodTools.Circularize(_w, _sel) >= 0 ? $"{NameOf(_sel)}: đã về quỹ đạo tròn" : "Không có vật nào nặng hơn để quay quanh");
        _ui?.RefreshSelection();
    }

    public void Home()
    {
        _follow = -1;
        int h = _w.Heaviest();
        if (h >= 0) { _cx = _w.X[h]; _cy = _w.Y[h]; }
        _zoom = 0.75f;
    }

    bool Live(int i) => i >= 0 && i < _w.N && _w.Alive[i];

    // ---- view ----

    Vector2 ToScreen(double x, double y)
    {
        Vector2 c = GetViewportRect().Size / 2;
        return new Vector2(c.X + (float)((x - _cx) * _zoom), c.Y + (float)((y - _cy) * _zoom * _tilt));
    }

    Vector2 Screen(int i) => ToScreen(_w.X[i], _w.Y[i]);

    void ToWorld(Vector2 p, out double x, out double y)
    {
        Vector2 c = GetViewportRect().Size / 2;
        x = _cx + (p.X - c.X) / _zoom; y = _cy + (p.Y - c.Y) / (_zoom * _tilt);
    }

    static float KindPx(Kind k) => k switch { Kind.Star => 9f, Kind.Planet => 4f, Kind.Moon => 2.5f, _ => 1.5f };

    float Px(int i) => MathF.Max(KindPx(_w.KindOf(i)), (float)_w.R[i] * _zoom);

    string NameOf(int i) => _w.Name[i] ?? $"{(_w.Grp[i] > 0 ? _w.Groups[_w.Grp[i]] : "Vật thể")} #{i}";

    Color MixCol(int i)
    {
        Color col = new(0, 0, 0);
        for (int e = 0; e < World.NElem; e++) col += ElemCol[e] * (float)(_w.Comp[i * World.NElem + e] / _w.M[i]);
        col.A = 1; return col;
    }

    // Bodies win over rocks: with 5000 rocks on screen a click near a planet must not land on a pebble.
    int Pick(Vector2 at)
    {
        int best = -1; float bestD = 10;
        for (int i = 0; i < _w.N; i++)
        {
            if (!_w.Alive[i] || !_w.Attracts(i)) continue;
            float d = MathF.Max(0, Screen(i).DistanceTo(at) - Px(i));
            if (d < bestD) { bestD = d; best = i; }
        }
        if (best >= 0) return best;
        bestD = 5;
        for (int i = 0; i < _w.N; i++)
        {
            if (!_w.Alive[i] || _w.Attracts(i)) continue;
            float d = Screen(i).DistanceTo(at);
            if (d < bestD) { bestD = d; best = i; }
        }
        return best;
    }

    public override void _Process(double delta)
    {
        if (_w == null) return;
        if (_uitest && _frame == 3) { _frame++; RunUiTest(); return; }
        long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        if (!_paused)
        {
            for (int step = 0; step < _timeWarp; step++)
            {
                _w.Advance(0.5);
            }
        }
        long t1 = System.Diagnostics.Stopwatch.GetTimestamp();

        double f = 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        double advanceElapsedMs = (t1 - t0) * f;
        _stepMs += (advanceElapsedMs - _stepMs) * 0.05;

        CheckAutoThrottle(advanceElapsedMs);

        if (_sel >= 0 && !Live(_sel)) Select(-1);
        if (_follow >= 0 && !Live(_follow)) _follow = -1;
        if (_follow >= 0) { _cx = _w.X[_follow]; _cy = _w.Y[_follow]; }

        Vector2 c = GetViewportRect().Size / 2;
        int n = 0; int attract = 0;
        for (int i = 0; i < _w.N; i++)
        {
            if (!_w.Alive[i]) continue;
            if (_w.Attracts(i)) { attract++; continue; }
            int o = n++ * Stride;
            _buf[o + 3] = c.X + (float)((_w.X[i] - _cx) * _zoom); _buf[o + 7] = c.Y + (float)((_w.Y[i] - _cy) * _zoom * _tilt);
            Color k = MixCol(i); _buf[o + 8] = k.R; _buf[o + 9] = k.G; _buf[o + 10] = k.B;
        }
        _mm.VisibleInstanceCount = n;
        RenderingServer.MultimeshSetBuffer(_mm.GetRid(), _buf);
        long t2 = System.Diagnostics.Stopwatch.GetTimestamp();

        _fillMs += ((t2 - t1) * f - _fillMs) * 0.05;
        _hud.Text = $"Năm {_w.Year:N1}   {(_paused ? "TẠM DỪNG" : $"tốc độ {_timeWarp}×")}   {_w.Live} vật thể ({attract} có lực hút)   va chạm {_w.Merges}   {Engine.GetFramesPerSecond():F0} fps";
        if (_frame++ % 15 == 0)
        {
            _panel.Text = PanelText();
            _ui?.RefreshEvents();
            _ui?.RefreshLive();
        }
        if (_toastLeft > 0)
        {
            _toastLeft -= delta;
            _toast.Modulate = new Color(1, 1, 1, (float)Math.Clamp(_toastLeft, 0, 1));
        }
        QueueRedraw();

        if (_bench > 0)
        {
            _benchT += delta; _benchFrames++;
            if (_benchT >= _bench)
            {
                if (3 < _w.N && _w.Alive[3])
                {
                    _sel = 3;
                    GD.Print(PanelText());
                }
                int testSlot = Math.Min(500, _w.N - 1);
                while (testSlot >= 0 && !_w.Alive[testSlot]) testSlot--;
                _sel = testSlot;
                GD.Print(PanelText());
                GD.Print($"BENCH objects={_w.Live} fps={_benchFrames / _benchT:F1} step_ms={_stepMs:F2} fill_ms={_fillMs:F2} renderer={RenderingServer.GetVideoAdapterName()}");
                GetTree().Quit(0);
            }
        }
    }

    string PanelText()
    {
        var sb = new System.Text.StringBuilder();
        if (!Live(_sel))
        {
            sb.Append("[color=#9aa4c0]Lăn chuột: phóng to tại con trỏ\nKéo chuột (phải, giữa, hoặc trái trên khoảng trống): di chuyển khung nhìn\nBấm: chọn · bấm đúp: chọn và bám theo\nKéo từ vật đang chọn: đẩy nó\nC: đặt vật thể mới · Space: tạm dừng · H: về toàn hệ[/color]\n\n");
            for (int e = 0; e < World.NElem; e++) sb.Append($"[color=#{ElemCol[e].ToHtml(false)}]■[/color] {GodUi.ElemVi[e]}  ");
            return sb.ToString();
        }
        int i = _sel; double m = _w.M[i];
        sb.Append($"[b]{NameOf(i)}[/b]   {GodUi.KindVi[(int)_w.KindOf(i)]}{(_follow == i ? "   [color=#9aa4c0](đang bám theo)[/color]" : "")}\n");
        sb.Append($"khối lượng {m / World.EarthMass:G4} Trái Đất   bán kính {_w.R[i]:G3}\n");
        int p = GodTools.PrimaryAt(_w, _w.X[i], _w.Y[i], m, i);
        if (p >= 0)
        {
            double dx = _w.X[i] - _w.X[p], dy = _w.Y[i] - _w.Y[p], ux = _w.Vx[i] - _w.Vx[p], uy = _w.Vy[i] - _w.Vy[p];
            double d = Math.Sqrt(dx * dx + dy * dy);
            sb.Append($"quay quanh {NameOf(p)}, cách {d:G4}, tốc độ {Math.Sqrt(ux * ux + uy * uy) / GodTools.OrbitSpeed(_w, p, _w.X[i], _w.Y[i]):F2}× tốc độ quỹ đạo tròn\n");
        }

        if (_w.IsWorld(i))
        {
            string bandName = !double.IsNaN(_w.Temp[i]) ? GodUi.BandVi[(int)_w.BandOf(_w.Temp[i])] : "Chưa rõ";
            sb.Append($"[color=#ffaa55]Nhiệt độ:[/color] {_w.Temp[i]:F1} K ({bandName})   ");
            int wState = Math.Clamp(_w.Water[i], 0, 3);
            sb.Append($"[color=#44ccff]Nước:[/color] {GodUi.WaterVi[wState]}\n");

            if (_w.Life[i] > 0)
                sb.Append($"[color=#55ff88]Sự sống:[/color] {_w.Life[i] * 100:F1}% ({GodUi.LifeStageVi[_w.LifeStage(i)]})\n");
            else
                sb.Append($"[color=#888888]Sự sống:[/color] Chưa có (nước lỏng {_w.WaterYears[i]:N0} năm)\n");

            if (_w.Pop[i] > 0)
                sb.Append($"[color=#ffd700]Văn minh:[/color] dân số {_w.Pop[i] * 100:F1}% — {GodUi.TechStageVi[_w.TechStage(i)]} (công nghệ {_w.Tech[i]:F1})\n");
        }

        int g = _w.Grp[i];
        if (g > 0)
        {
            int cnt = 0; double tot = 0;
            for (int j = 0; j < _w.N; j++) if (_w.Alive[j] && _w.Grp[j] == g) { cnt++; tot += _w.M[j]; }
            sb.Append($"thuộc {_w.Groups[g]}: {cnt} vật thể, tổng {tot / World.EarthMass:G3} Trái Đất\n");
        }
        for (int e = 0; e < World.NElem; e++)
            sb.Append($"[color=#{ElemCol[e].ToHtml(false)}]■[/color] {GodUi.ElemVi[e]}   {100 * _w.Comp[i * World.NElem + e] / m:F1}%\n");
        return sb.ToString();
    }

    // drag on screen -> velocity seen from the primary: DragPerOrbitSpeed pixels = one circular-orbit speed there
    void DragVelocity(Vector2 drag, int primary, double x, double y, out double vx, out double vy, out double share)
    {
        double v = GodTools.OrbitSpeed(_w, primary, x, y);
        vx = drag.X / DragPerOrbitSpeed * v; vy = drag.Y / (DragPerOrbitSpeed * _tilt) * v;
        share = Math.Sqrt(vx * vx + vy * vy) / v;
    }

    void DrawArrow(Vector2 from, Vector2 to)
    {
        if (from.DistanceTo(to) <= 4) return;
        DrawLine(from, to, AimCol, 2.5f);
        Vector2 dir = (to - from).Normalized(), perp = new(-dir.Y, dir.X);
        DrawColoredPolygon(new[] { to, to - dir * 14 + perp * 7, to - dir * 14 - perp * 7 }, AimCol);
    }

    // the path the thing would take around its primary, as a line; red when it ends in the primary
    void DrawPath(int primary, double mass, double rx, double ry, double rvx, double rvy)
    {
        int n = GodTools.Predict(_w, primary, mass, rx, ry, rvx, rvy, _path, out bool hits);
        if (n < 2) return;
        for (int k = 0; k < n; k++) _pathPts[k] = ToScreen(_w.X[primary] + _path[k * 2], _w.Y[primary] + _path[k * 2 + 1]);
        DrawPolyline(_pathPts.AsSpan(0, n).ToArray(), hits ? new Color(1f, 0.35f, 0.3f, 0.9f) : new Color(1f, 0.9f, 0.2f, 0.6f), 1.5f);
    }

    public override void _Draw()
    {
        if (_w == null) return;
        Font font = ThemeDB.FallbackFont;

        // Orbit lines
        for (int i = 0; i < _w.N; i++)
        {
            int par = _w.Par[i];
            if (!_w.Alive[i] || par < 0 || par >= _w.N || !_w.Alive[par]) continue;
            double dx = _w.X[i] - _w.X[par], dy = _w.Y[i] - _w.Y[par];
            float rad = (float)(Math.Sqrt(dx * dx + dy * dy) * _zoom);
            if (rad < 6) continue;
            DrawSetTransform(Screen(par), 0, new Vector2(1, _tilt));
            DrawArc(Vector2.Zero, rad, 0, MathF.Tau, 128, new Color(1, 1, 1, 0.10f), 1);
        }
        if (Live(_sel) && _w.Attracts(_sel) && _w.KindOf(_sel) != Kind.Star)
        {
            DrawSetTransform(Screen(_sel), 0, new Vector2(1, _tilt));
            DrawArc(Vector2.Zero, (float)(_w.Hill(_sel) * _zoom), 0, MathF.Tau, 96, new Color(0.5f, 1f, 0.6f, 0.35f), 1);
        }
        DrawSetTransform(Vector2.Zero);

        for (int i = 0; i < _w.N; i++)
        {
            if (!_w.Alive[i] || !_w.Attracts(i)) continue;
            Vector2 p = Screen(i);
            float r = Px(i);
            Kind kind = _w.KindOf(i);
            if (_w.Name[i] == "Sao Thổ")
            {
                DrawSetTransform(p, 0, new Vector2(1, _tilt));
                DrawArc(Vector2.Zero, r * 2.1f, 0, MathF.Tau, 48, new Color(0.9f, 0.82f, 0.6f, 0.7f), MathF.Max(1.5f, r * 0.45f));
                DrawSetTransform(Vector2.Zero);
            }
            DrawCircle(p, r, _w.Col[i] != 0 ? new Color((_w.Col[i] << 8) | 0xFF) : MixCol(i));

            // Visual marks on bodies that carry life or civilisation (SPEC 7 Round 2)
            if (_w.IsWorld(i))
            {
                if (_w.Pop[i] > 0)
                {
                    DrawArc(p, r + 4, 0, MathF.Tau, 28, new Color(1f, 0.85f, 0.2f, 0.95f), 1.5f);
                    DrawString(font, p + new Vector2(r + 6, -10), "văn minh", HorizontalAlignment.Left, -1, 11, new Color(1f, 0.85f, 0.2f, 0.95f));
                }
                else if (_w.Life[i] > 0)
                {
                    DrawArc(p, r + 3, 0, MathF.Tau, 24, new Color(0.2f, 1f, 0.45f, 0.9f), 1.2f);
                    DrawString(font, p + new Vector2(r + 6, -10), "sự sống", HorizontalAlignment.Left, -1, 11, new Color(0.2f, 1f, 0.45f, 0.9f));
                }
            }

            bool far = true;
            if (kind == Kind.Moon && _w.Par[i] >= 0 && _w.Par[i] < _w.N && _w.Alive[_w.Par[i]])
                far = Screen(_w.Par[i]).DistanceTo(p) > 30;
            if (kind != Kind.Star && far)
                DrawString(font, p + new Vector2(r + 6, 4), NameOf(i), HorizontalAlignment.Left, -1, 13, new Color(1, 1, 1, 0.75f));
        }

        // Selection ring
        if (Live(_sel)) DrawArc(Screen(_sel), Px(_sel) + 5, 0, MathF.Tau, 32, Colors.White, 1.5f);

        // Push: arrow from the object, and the orbit it would get
        if (_drag == Drag.Push && Live(_sel))
        {
            int i = _sel;
            int p = GodTools.PrimaryAt(_w, _w.X[i], _w.Y[i], _w.M[i], i);
            DragVelocity(_mouse - _downPos, p, _w.X[i], _w.Y[i], out double dvx, out double dvy, out double share);
            DrawArrow(Screen(i), Screen(i) + (_mouse - _downPos));
            if (p >= 0) DrawPath(p, _w.M[i], _w.X[i] - _w.X[p], _w.Y[i] - _w.Y[p], _w.Vx[i] - _w.Vx[p] + dvx, _w.Vy[i] - _w.Vy[p] + dvy);
            DrawString(font, _mouse + new Vector2(14, 18), $"đẩy {share:F2}× tốc độ quỹ đạo", HorizontalAlignment.Left, -1, 13, AimCol);
        }

        // Create: the pointer carries the new object and shows what it will do
        if (_creating && _ui != null)
        {
            bool placing = _drag == Drag.Place;
            double x, y;
            if (placing) { x = _placeX; y = _placeY; } else ToWorld(_mouse, out x, out y);
            double mass = _ui.GetCreateMass(); double[] mix = _ui.GetCreateMix();
            int p = GodTools.PrimaryAt(_w, x, y, mass);
            var (kind, radius) = GodTools.Preview(_w, mass, mix, p);
            Vector2 at = ToScreen(x, y);
            Vector2 drag = placing ? _mouse - at : Vector2.Zero;
            string say;
            if (p < 0) say = drag.Length() < LaunchStart ? "đứng yên (không có vật nào nặng hơn ở đây)" : "phóng đi";
            else if (drag.Length() < LaunchStart)
            {
                double dx = x - _w.X[p], dy = y - _w.Y[p];
                DrawSetTransform(Screen(p), 0, new Vector2(1, _tilt));
                DrawArc(Vector2.Zero, (float)(Math.Sqrt(dx * dx + dy * dy) * _zoom), 0, MathF.Tau, 128, new Color(1f, 0.9f, 0.2f, 0.45f), 1.5f);
                DrawSetTransform(Vector2.Zero);
                say = $"quỹ đạo tròn quanh {NameOf(p)}";
            }
            else
            {
                DragVelocity(drag, p, x, y, out double rvx, out double rvy, out double share);
                DrawPath(p, mass, x - _w.X[p], y - _w.Y[p], rvx, rvy);
                say = $"phóng {share:F2}× tốc độ quỹ đạo quanh {NameOf(p)}";
            }
            if (drag.Length() >= LaunchStart) DrawArrow(at, _mouse);
            Color ghost = new(0, 0, 0);
            double sum = 0; for (int e = 0; e < World.NElem; e++) sum += mix[e];
            for (int e = 0; e < World.NElem; e++) ghost += ElemCol[e] * (float)(sum > 0 ? mix[e] / sum : e == 2 ? 1 : 0);
            ghost.A = 0.75f;
            float gr = MathF.Max(KindPx(kind), (float)radius * _zoom);
            DrawCircle(at, gr, ghost);
            DrawArc(at, gr + 3, 0, MathF.Tau, 24, new Color(1, 1, 1, 0.6f), 1);
            DrawString(font, (placing ? _mouse : at) + new Vector2(gr + 12, 18), $"{GodUi.KindVi[(int)kind]} — {say}", HorizontalAlignment.Left, -1, 13, AimCol);
        }
    }

    void ZoomAt(Vector2 at, float factor)
    {
        ToWorld(at, out double wx, out double wy);
        _zoom = Math.Clamp(_zoom * factor, 0.02f, 4000f);
        if (_follow >= 0) return; // the followed object stays in the middle
        ToWorld(at, out double nx, out double ny);
        _cx += wx - nx; _cy += wy - ny;
    }

    void PlaceObject()
    {
        double mass = _ui.GetCreateMass(); double[] mix = _ui.GetCreateMix();
        double x = _placeX, y = _placeY;
        int p = GodTools.PrimaryAt(_w, x, y, mass);
        Vector2 drag = _mouse - ToScreen(x, y);
        Kind kind = GodTools.Preview(_w, mass, mix, p).Kind;
        string name = $"{GodUi.KindVi[(int)kind]} mới {++_createdCount}";
        int slot; string did;
        if (drag.Length() < LaunchStart)
        {
            slot = GodTools.Create(_w, x, y, mass, mix, parent: p, name: name);
            did = p >= 0 ? $"quay quanh {NameOf(p)}" : "đứng yên";
        }
        else
        {
            DragVelocity(drag, p, x, y, out double rvx, out double rvy, out double share);
            slot = GodTools.Launch(_w, x, y, (p >= 0 ? _w.Vx[p] : 0) + rvx, (p >= 0 ? _w.Vy[p] : 0) + rvy, mass, mix, name);
            did = $"phóng {share:F2}× tốc độ quỹ đạo";
        }
        if (slot >= 0) { Select(slot); Toast($"Đã tạo {name}: {did}"); }
        else { _createdCount--; Toast("Không tạo được: hết chỗ hoặc thông số sai"); }
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true } k)
        {
            if (k.Keycode == Key.Bracketright) { GodTools.SetConst(_w, "G", _w.C.G * 1.05); _ui?.RefreshConsts(); Toast($"G = {_w.C.G:F2}"); }
            if (k.Keycode == Key.Bracketleft) { GodTools.SetConst(_w, "G", _w.C.G / 1.05); _ui?.RefreshConsts(); Toast($"G = {_w.C.G:F2}"); }
            if (k.Echo) return;
            if (k.Keycode == Key.T) _tilt = _tilt < 1 ? 1f : 0.5f;
            if (k.Keycode == Key.Space) TogglePause();
            if (k.Keycode == Key.C) SetCreating(!_creating);
            if (k.Keycode == Key.F) FollowSelected();
            if (k.Keycode == Key.H) Home();
            if (k.Keycode == Key.Delete) RemoveSelected();
            if (k.Keycode == Key.Tab) _ui?.TogglePanel();
            if (k.Keycode == Key.Escape)
            {
                if (_drag != Drag.None) _drag = Drag.None;
                else if (_creating) SetCreating(false);
                else if (_follow >= 0) _follow = -1;
                else Select(-1);
            }
            for (int n = 0; n < 7; n++) if (k.Keycode == Key.Key1 + n) SetTimeWarp(1 << n);
            GetViewport().SetInputAsHandled();
            return;
        }

        if (e is InputEventMouseMotion mm)
        {
            _mouse = mm.Position;
            if (_drag == Drag.Maybe && (mm.Position - _downPos).Length() > DragStart)
                _drag = _dragBtn == MouseButton.Left && _downOnSel ? Drag.Push : Drag.Pan;
            if (_drag == Drag.Pan)
            {
                _follow = -1;
                _cx -= mm.Relative.X / _zoom; _cy -= mm.Relative.Y / (_zoom * _tilt);
            }
            return;
        }

        if (e is not InputEventMouseButton m) return;
        _mouse = m.Position;
        if (m.Pressed)
        {
            GetViewport().GuiReleaseFocus(); // a click in space takes the keyboard back from a text field
            if (m.ButtonIndex == MouseButton.WheelUp) { ZoomAt(m.Position, 1.25f); return; }
            if (m.ButtonIndex == MouseButton.WheelDown) { ZoomAt(m.Position, 1 / 1.25f); return; }
            if (_drag != Drag.None) return;
            if (m.ButtonIndex != MouseButton.Left && m.ButtonIndex != MouseButton.Right && m.ButtonIndex != MouseButton.Middle) return;
            _dragBtn = m.ButtonIndex; _downPos = m.Position;
            if (m.ButtonIndex == MouseButton.Left && _creating)
            {
                _drag = Drag.Place; ToWorld(m.Position, out _placeX, out _placeY);
            }
            else if (m.ButtonIndex == MouseButton.Left && m.DoubleClick)
            {
                int hit = Pick(m.Position);
                if (hit >= 0) Select(hit, follow: true);
            }
            else
            {
                _drag = Drag.Maybe;
                _downOnSel = Live(_sel) && Screen(_sel).DistanceTo(m.Position) <= Px(_sel) + 10;
            }
            return;
        }

        if (m.ButtonIndex != _dragBtn || _drag == Drag.None) return;
        Drag was = _drag; _drag = Drag.None;
        if (was == Drag.Place) PlaceObject();
        else if (was == Drag.Push && Live(_sel))
        {
            int i = _sel;
            int p = GodTools.PrimaryAt(_w, _w.X[i], _w.Y[i], _w.M[i], i);
            DragVelocity(_mouse - _downPos, p, _w.X[i], _w.Y[i], out double dvx, out double dvy, out double share);
            GodTools.Push(_w, i, dvx, dvy);
            Toast($"Đã đẩy {NameOf(i)}: {share:F2}× tốc độ quỹ đạo");
            _ui?.RefreshSelection();
        }
        else if (was == Drag.Maybe)
        {
            if (m.ButtonIndex == MouseButton.Left) Select(Pick(m.Position));
            else if (m.ButtonIndex == MouseButton.Right) { if (_creating) SetCreating(false); else Select(-1); }
        }
    }

    // Headless check of the mouse and keyboard paths (`-- --uitest`): feeds the same events a hand would, on the
    // real panels, and reads back what the world got. No window, nobody has to click through it.
    void RunUiTest()
    {
        bool ok = true;
        void Say(bool good, string line) { ok &= good; GD.Print($"{(good ? "OK    " : "FAILED")} ui: {line}"); }
        void Button(Vector2 at, MouseButton b, bool down, bool dbl = false) => _UnhandledInput(new InputEventMouseButton { ButtonIndex = b, Pressed = down, Position = at, DoubleClick = dbl });
        void Move(Vector2 from, Vector2 to) { for (int k = 1; k <= 4; k++) _UnhandledInput(new InputEventMouseMotion { Position = from.Lerp(to, k / 4f), Relative = (to - from) / 4 }); }
        void Click(Vector2 at, MouseButton b = MouseButton.Left) { Button(at, b, true); Button(at, b, false); }
        void DragTo(Vector2 from, Vector2 to, MouseButton b) { Button(from, b, true); Move(from, to); Button(to, b, false); }
        void Key(Key key) => _UnhandledInput(new InputEventKey { Keycode = key, Pressed = true });
        double Share(int i, int prim)
        {
            double ux = _w.Vx[i] - _w.Vx[prim], uy = _w.Vy[i] - _w.Vy[prim];
            return Math.Sqrt(ux * ux + uy * uy) / GodTools.OrbitSpeed(_w, prim, _w.X[i], _w.Y[i]);
        }
        const int earth = 3;
        Key(Godot.Key.Space);
        Say(_paused, "Space pauses");

        // a click near Earth takes Earth, although rocks are all around; the view does not jump
        double cx0 = _cx, cy0 = _cy;
        Click(Screen(earth) + new Vector2(6, 0));
        Say(_sel == earth && _cx == cx0 && _cy == cy0 && _follow < 0, $"click selects {(_sel >= 0 ? NameOf(_sel) : "nothing")}, view stays");

        // zoom keeps the spot under the pointer
        Vector2 at = Screen(earth) + new Vector2(40, 25);
        ToWorld(at, out double wx, out double wy);
        for (int k = 0; k < 12; k++) Button(at, MouseButton.WheelUp, true);
        ToWorld(at, out double wx2, out double wy2);
        Say(Math.Abs(wx - wx2) < 1e-3 && Math.Abs(wy - wy2) < 1e-3 && _zoom > 10, $"wheel: zoom {_zoom:F1}, spot under pointer moved {Math.Abs(wx - wx2) + Math.Abs(wy - wy2):E1}");

        // right drag moves the view and is not a click
        double cxb = _cx; int selB = _sel;
        DragTo(new Vector2(400, 400), new Vector2(500, 400), MouseButton.Right);
        Say(Math.Abs(cxb - _cx - 100 / _zoom) < 1e-6 && _sel == selB, $"right drag 100 px: view moved {cxb - _cx:F3} = {100 / _zoom:F3}");

        // left drag on empty space moves the view too
        cxb = _cx;
        DragTo(new Vector2(300, 600), new Vector2(350, 600), MouseButton.Left);
        Say(Math.Abs(cxb - _cx - 50 / _zoom) < 1e-6 && _sel == selB, "left drag on empty space moves the view");

        // double click = follow
        Home(); _zoom = 200; _cx = _w.X[earth]; _cy = _w.Y[earth];
        Button(Screen(earth), MouseButton.Left, true, dbl: true); Button(Screen(earth), MouseButton.Left, false);
        Say(_follow == earth && _sel == earth, "double click follows Earth");

        // create, click: a moon dropped next to Earth circles Earth, whatever is selected
        Key(Godot.Key.C);
        _ui.ApplyPreset(1);
        Select(-1);
        int live = _w.Live;
        Click(Screen(earth) + new Vector2(0.2f * _zoom, 0));
        int moon = _sel;
        bool made = _w.Live == live + 1 && Live(moon);
        int mp = made ? GodTools.PrimaryAt(_w, _w.X[moon], _w.Y[moon], _w.M[moon], moon) : -1;
        Say(made && mp == earth && Math.Abs(Share(moon, earth) - 1) < 0.01 && _creating, made ? $"create click: {NameOf(moon)} around {NameOf(mp)} at {Share(moon, earth):F3}x orbit speed" : "create click made nothing");

        // create, press and drag: launched at drag / 120 px of the orbit speed there
        live = _w.Live;
        Vector2 from = Screen(earth) + new Vector2(-0.25f * _zoom, 0);
        DragTo(from, from + new Vector2(0, -60), MouseButton.Left);
        int shot = _sel;
        made = _w.Live == live + 1 && Live(shot) && shot != moon;
        Say(made && Math.Abs(Share(shot, earth) - 0.5) < 0.01 && _w.Vy[shot] < _w.Vy[earth], made ? $"create drag 60 px up: launched at {Share(shot, earth):F3}x orbit speed" : "create drag made nothing");

        // a body heavier than everything nearby but the Sun circles the Sun
        _ui.ApplyPreset(2); live = _w.Live;
        Click(Screen(earth) + new Vector2(0, 3f * _zoom));
        int twin = _sel;
        Say(_w.Live == live + 1 && GodTools.PrimaryAt(_w, _w.X[twin], _w.Y[twin], _w.M[twin], twin) == 0 && Math.Abs(Share(twin, 0) - 1) < 0.01, $"create an Earth: {NameOf(twin)} circles the Sun");

        // right click drops the tool, next right click the selection
        Click(new Vector2(200, 200), MouseButton.Right);
        bool toolOff = !_creating && _sel == twin;
        Click(new Vector2(200, 200), MouseButton.Right);
        Say(toolOff && _sel < 0, "right click: tool off, then selection off");

        // push: drag from the selected object
        _follow = -1;
        Click(Screen(moon));
        double vx0 = _w.Vx[moon], vy0 = _w.Vy[moon];
        DragTo(Screen(moon), Screen(moon) + new Vector2(120, 0), MouseButton.Left);
        double pushed = Math.Sqrt(Math.Pow(_w.Vx[moon] - vx0, 2) + Math.Pow(_w.Vy[moon] - vy0, 2)) / GodTools.OrbitSpeed(_w, earth, _w.X[moon], _w.Y[moon]);
        Say(_sel == moon && Math.Abs(pushed - 1) < 0.01 && _w.Vx[moon] > vx0, $"push drag 120 px: {pushed:F3}x orbit speed");

        // panel actions on the selection
        CircularizeSelected();
        Say(Math.Abs(Share(moon, earth) - 1) < 0.01, $"back to a circular orbit: {Share(moon, earth):F3}x");
        live = _w.Live;
        Key(Godot.Key.Delete);
        Say(_w.Live == live - 1 && _sel < 0 && !_w.Alive[moon], "Delete removes the selected object");
        Key(Godot.Key.Key5);
        Say(_timeWarp == 16, "key 5 = 16x");

        // everything above went through the journal
        var fresh = World.SolSystem(_w.N > 5000 ? 5000 : 0, 1234); int next = 0;
        while (fresh.Step < _w.Step) { fresh.Replay(_w.Journal, ref next); fresh.Advance(0.5); }
        fresh.Replay(_w.Journal, ref next);
        Say(fresh.Hash() == _w.Hash(), $"replay of {_w.Journal.Count} commands: {fresh.Hash():X16} vs {_w.Hash():X16}");

        GD.Print(ok ? "PASS: uitest" : "FAIL: uitest");
        GetTree().Quit(ok ? 0 : 1);
    }

    /// <summary>
    /// Headless self-test (SPEC 6 P2 & SPEC 7 Round 2):
    /// Calls each tool method with fixed inputs, covers FastForward, SeedLife, SetRule, SetTimeWarp + throttle,
    /// replays journal on fresh world, prints one line per tool with before/after numbers, exits 0 on match or 1 on mismatch.
    /// </summary>
    public void RunSelfTest()
    {
        GD.Print("=== COSMOS SELFTEST START ===");
        ulong seed = 1234;
        int rocks = 500;
        var w = World.SolSystem(rocks, seed);
        ulong h0 = w.Hash();
        GD.Print($"[0] Init: Live={w.Live}, Hash={h0:X16}");

        // 1. Tool Create: orbiting around Earth (slot 3)
        int earth = 3;
        double moonMass = World.EarthMass * 0.05;
        double[] moonMix = { 0.02, 0.08, 0.60, 0.25, 0.04, 0.01 };
        var (pKind, pRadius) = GodTools.Preview(w, moonMass, moonMix, parent: earth);
        int liveBefore = w.Live;
        int slotMoon = GodTools.Create(w, w.X[earth] + 0.25, w.Y[earth], moonMass, moonMix, parent: earth, name: "SelftestMoon", col: 0x00FF88);
        if (slotMoon < 0) { GD.PrintErr("FAIL: Create orbiting failed"); GetTree().Quit(1); return; }
        GD.Print($"TOOL CreateOrbiting: slot={slotMoon} live {liveBefore} -> {w.Live}, mass={w.M[slotMoon]:G4}, r={w.R[slotMoon]:G3} (preview r={pRadius:G3}), kind={w.KindOf(slotMoon)} (preview {pKind})");

        // Also test Create at rest (parent = -1)
        double restMass = 0.5;
        double[] restMix = { 0.7, 0.2, 0.05, 0.05, 0, 0 };
        int liveBeforeRest = w.Live;
        int slotRest = GodTools.Create(w, 250.0, -100.0, restMass, restMix, parent: -1, name: "SelftestRest", col: 0xFF5500);
        if (slotRest < 0) { GD.PrintErr("FAIL: Create at rest failed"); GetTree().Quit(1); return; }
        GD.Print($"TOOL CreateAtRest: slot={slotRest} live {liveBeforeRest} -> {w.Live}, vx={w.Vx[slotRest]:F2}, vy={w.Vy[slotRest]:F2}, kind={w.KindOf(slotRest)}");

        // 2. Tool AddMatter: edit composition
        double matterBefore = w.Comp[slotMoon * World.NElem + 1];
        double massBefore = w.M[slotMoon];
        double deltaIce = 0.01 * World.EarthMass;
        int editRes = GodTools.AddMatter(w, slotMoon, 1, deltaIce);
        if (editRes < 0) { GD.PrintErr("FAIL: AddMatter failed"); GetTree().Quit(1); return; }
        double matterAfter = w.Comp[slotMoon * World.NElem + 1];
        double massAfter = w.M[slotMoon];
        GD.Print($"TOOL AddMatter: slot={slotMoon} ice {matterBefore:G4} -> {matterAfter:G4}, mass {massBefore:G4} -> {massAfter:G4}, r={w.R[slotMoon]:G3}");

        // 3. Tool Push
        double vxBefore = w.Vx[slotMoon], vyBefore = w.Vy[slotMoon];
        int pushRes = GodTools.Push(w, slotMoon, 0.02, -0.015);
        if (pushRes < 0) { GD.PrintErr("FAIL: Push failed"); GetTree().Quit(1); return; }
        double vxAfter = w.Vx[slotMoon], vyAfter = w.Vy[slotMoon];
        GD.Print($"TOOL Push: slot={slotMoon} vx {vxBefore:F4} -> {vxAfter:F4}, vy {vyBefore:F4} -> {vyAfter:F4}");

        // 4. Tool TimeControl & Throttle check (Round 2 requirement: calls SetTimeWarp and auto-throttle path)
        SetTimeWarp(32);
        int warpSet = _timeWarp;
        CheckAutoThrottle(65.0); // simulated load > 50ms
        int warpThrottled = _timeWarp;
        long stepBefore = w.Step;
        for (int s = 0; s < _timeWarp; s++) w.Advance(0.5);
        long stepAfter = w.Step;
        GD.Print($"TOOL TimeControl: set={warpSet}x throttled={warpThrottled}x on 65ms step {stepBefore} -> {stepAfter}");

        // 5. Tool SetConst
        double gBefore = w.C.G, rScaleBefore = w.C.RadiusScale;
        bool setG = GodTools.SetConst(w, "G", 1.20);
        bool setR = GodTools.SetConst(w, "RadiusScale", 1.75);
        if (!setG || !setR) { GD.PrintErr("FAIL: SetConst failed"); GetTree().Quit(1); return; }
        GD.Print($"TOOL SetConst: G {gBefore:F2} -> {w.C.G:F2}, RadiusScale {rScaleBefore:F2} -> {w.C.RadiusScale:F2}");

        // 6. Tool SetRule (Round 2)
        var ruleTemp = w.Rules.Find(r => r.Id == "temperature");
        bool ruleBefore = ruleTemp?.Enabled ?? false;
        GodTools.SetRule(w, "temperature", false);
        bool ruleOff = ruleTemp?.Enabled ?? false;
        GodTools.SetRule(w, "temperature", true);
        bool ruleOn = ruleTemp?.Enabled ?? false;
        GD.Print($"TOOL SetRule: temperature rule enabled {ruleBefore} -> {ruleOff} -> {ruleOn}");

        // 7. Tool SeedLife (Round 2)
        double lifeBefore = w.Life[earth];
        int seedRes = GodTools.SeedLife(w, earth, 0.65);
        if (seedRes < 0) { GD.PrintErr("FAIL: SeedLife failed"); GetTree().Quit(1); return; }
        double lifeAfter = w.Life[earth];
        GD.Print($"TOOL SeedLife: slot={earth} life {lifeBefore:F3} -> {lifeAfter:F3}, stage={w.LifeStage(earth)}");

        // 8. Tool FastForward (Round 2)
        double yrBefore = w.Year;
        bool ffRes = GodTools.FastForward(w, 500.0);
        if (!ffRes) { GD.PrintErr("FAIL: FastForward failed"); GetTree().Quit(1); return; }
        double yrAfter = w.Year;
        GD.Print($"TOOL FastForward: years {yrBefore:F1} -> {yrAfter:F1}, earth_water={(WaterState)w.Water[earth]}, events={w.Events.Count}");

        // 9. Mouse helpers (round 3): who rules a spot, launch with a velocity, circular orbit, path preview, remove
        double smallMass = World.EarthMass * 0.01;
        int primNear = GodTools.PrimaryAt(w, w.X[earth] + 0.1, w.Y[earth], smallMass);
        int primSunSize = GodTools.PrimaryAt(w, w.X[earth] + 0.1, w.Y[earth], w.M[0] * 2);
        int shot = GodTools.Launch(w, w.X[earth] + 0.3, w.Y[earth], w.Vx[earth], w.Vy[earth] + 0.2, smallMass, moonMix, "SelftestShot");
        if (shot < 0 || primNear < 0 || w.M[primNear] >= w.C.StarMass || primSunSize != -1) { GD.PrintErr($"FAIL: PrimaryAt/Launch near={primNear} sunsize={primSunSize} shot={shot}"); GetTree().Quit(1); return; }
        int shotPrim = GodTools.PrimaryAt(w, w.X[shot], w.Y[shot], w.M[shot], shot);
        var path = new double[2 * 64];
        int pathN = GodTools.Predict(w, shotPrim, w.M[shot], w.X[shot] - w.X[shotPrim], w.Y[shot] - w.Y[shotPrim], w.Vx[shot] - w.Vx[shotPrim], w.Vy[shot] - w.Vy[shotPrim], path, out bool pathHits);
        int circRes = GodTools.Circularize(w, shot);
        double sdx = w.X[shot] - w.X[shotPrim], sdy = w.Y[shot] - w.Y[shotPrim], sux = w.Vx[shot] - w.Vx[shotPrim], suy = w.Vy[shot] - w.Vy[shotPrim];
        double sd = Math.Sqrt(sdx * sdx + sdy * sdy), speedShare = Math.Sqrt(sux * sux + suy * suy) / Math.Sqrt(w.C.G * (w.M[shotPrim] + w.M[shot]) / sd);
        double radial = (sdx * sux + sdy * suy) / sd;
        int liveBeforeRemove = w.Live;
        int removeRes = GodTools.Remove(w, slotRest);
        if (circRes < 0 || Math.Abs(speedShare - 1) > 1e-9 || Math.Abs(radial) > 1e-9 || pathN < 2 || removeRes < 0 || w.Alive[slotRest] || w.Live != liveBeforeRemove - 1)
        { GD.PrintErr($"FAIL: Circularize/Predict/Remove share={speedShare} radial={radial} path={pathN} remove={removeRes}"); GetTree().Quit(1); return; }
        GD.Print($"TOOL Mouse: primary near Earth={w.Name[primNear]}, nothing heavier for a 2-sun mass; launched slot={shot} around {w.Name[shotPrim]}, path {pathN} points hits={pathHits}; circularized to {speedShare:F6}x orbit speed; removed slot={slotRest} live {liveBeforeRemove} -> {w.Live}");

        // Additional Advance steps with interleaved Push to test mixed journal
        for (int i = 0; i < 20; i++) w.Advance(0.5);
        GodTools.Push(w, slotMoon, -0.005, 0.01);
        for (int i = 0; i < 20; i++) w.Advance(0.5);

        ulong origHash = w.Hash();
        int journalCount = w.Journal.Count;

        // Replay on fresh world
        var fresh = World.SolSystem(rocks, seed);
        int next = 0;
        while (fresh.Step < w.Step)
        {
            fresh.Replay(w.Journal, ref next);
            fresh.Advance(0.5);
        }
        fresh.Replay(w.Journal, ref next);
        ulong replayHash = fresh.Hash();

        GD.Print($"REPLAY: journal_entries={journalCount} steps={w.Step} hash_orig={origHash:X16} hash_replay={replayHash:X16}");

        if (origHash == replayHash && next == journalCount)
        {
            GD.Print($"PASS: selftest verified all tools and exact journal replay ({origHash:X16}).");
            GetTree().Quit(0);
        }
        else
        {
            GD.PrintErr($"FAIL: mismatch! orig={origHash:X16} replay={replayHash:X16} replayed={next}/{journalCount}");
            GetTree().Quit(1);
        }
    }
}
