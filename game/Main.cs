// Shell: steps the core once per frame and draws it 2D from straight above (T tilts the picture, draw only).
// Everything is built in code; the scene file only attaches this script.
//   run:  godot --path game -- --rocks=5000
//   Every change to the world goes through World.Do(Command) (SPEC 3): never write world arrays from here.
//   left click = select an object (camera follows it); drag from selection = push;
//   right click = create object with parameters configured in GodUi;
//   wheel = zoom, T = tilt, Space = pause, [ and ] = turn G down / up, 1..7 = time warp (1x..64x)
using System;
using Godot;
using Cosmos.Core;

namespace Cosmos.Game;

public partial class Main : Node2D
{
    World _w = null!;
    MultiMesh _mm = null!;
    float[] _buf = null!;
    Label _hud = null!;
    RichTextLabel _panel = null!;
    GodUi _ui = null!;

    int _sel = -1, _frame, _createdCount;
    public int Selected => _sel;

    double _cx, _cy; // world point at screen centre; rides selected object
    float _zoom = 0.75f, _tilt = 1f;
    bool _paused;
    int _timeWarp = 1;

    // Push drag state
    bool _pushDragging;
    Vector2 _pushDragStart, _pushDragCurrent;

    double _stepMs, _fillMs;
    int _bench; double _benchT; int _benchFrames;
    bool _selftest;
    const int Stride = 12; // per small object: 8 floats transform + 4 colour

    static readonly Color[] ElemCol = { new(0.72f, 0.78f, 1f), new(0.3f, 0.9f, 1f), new(0.9f, 0.5f, 0.25f), new(1f, 0.85f, 0.4f), new(0.75f, 0.4f, 0.95f), new(0.4f, 1f, 0.3f) };
    static readonly string[] ElemVi = { "Khí nhẹ", "Băng", "Đá", "Kim loại", "Carbon", "Phóng xạ" };
    static readonly string[] KindVi = { "Sao", "Hành tinh", "Vệ tinh", "Tiểu hành tinh" };

    public override void _Ready()
    {
        int rocks = 5000;
        foreach (string a in OS.GetCmdlineUserArgs())
        {
            if (a == "--selftest") _selftest = true;
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
        _hud = new Label { Position = new Vector2(12, 8) }; layer.AddChild(_hud);
        _panel = new RichTextLabel { Position = new Vector2(12, 36), Size = new Vector2(420, 320), BbcodeEnabled = true, ScrollActive = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        layer.AddChild(_panel);

        _ui = new GodUi(this, _w);
        AddChild(_ui);
    }

    public void SetTimeWarp(int speed)
    {
        _timeWarp = Math.Clamp(speed, 1, 64);
        _ui?.UpdateTimeWarp(_timeWarp);
    }

    Color MixCol(int i)
    {
        Color col = new(0, 0, 0);
        for (int e = 0; e < World.NElem; e++) col += ElemCol[e] * (float)(_w.Comp[i * World.NElem + e] / _w.M[i]);
        col.A = 1; return col;
    }

    public override void _Process(double delta)
    {
        if (_w == null) return;
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

        // Auto-throttle: drop back time warp if stepping takes over 50 ms
        if (advanceElapsedMs > 50.0 && _timeWarp > 1)
        {
            SetTimeWarp(Math.Max(1, _timeWarp / 2));
        }

        if (_sel >= 0 && (_sel >= _w.N || !_w.Alive[_sel]))
        {
            _sel = -1;
            _ui?.RefreshSelection();
        }
        if (_sel >= 0) { _cx = _w.X[_sel]; _cy = _w.Y[_sel]; }

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
        _hud.Text = $"{Engine.GetFramesPerSecond():F0} fps   {_w.Live} vật thể ({attract} có lực hút)   tốc độ {_timeWarp}× (tính {_stepMs:F2} ms)   draw-fill {_fillMs:F2} ms   G = {_w.C.G:F2}  ([ ] chỉnh G)   va chạm: {_w.Merges}";
        if (_frame++ % 15 == 0) _panel.Text = PanelText();
        QueueRedraw();

        if (_bench > 0)
        {
            _benchT += delta; _benchFrames++;
            if (_benchT >= _bench)
            {
                if (3 < _w.N && _w.Alive[3])
                {
                    _sel = 3;
                    GodTools.Create(_w, _w.X[3], _w.Y[3] + 0.2, _w.M[3] * 0.01, new[] { 0, 0, 1.0, 0, 0, 0 }, parent: 3, name: "bench_test");
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

    Vector2 Screen(int i)
    {
        Vector2 c = GetViewportRect().Size / 2;
        return new Vector2(c.X + (float)((_w.X[i] - _cx) * _zoom), c.Y + (float)((_w.Y[i] - _cy) * _zoom * _tilt));
    }

    float Px(int i) => MathF.Max(_w.KindOf(i) switch { Kind.Star => 9f, Kind.Planet => 4f, Kind.Moon => 2.5f, _ => 1.5f }, (float)_w.R[i] * _zoom);

    string NameOf(int i) => _w.Name[i] ?? $"{(_w.Grp[i] > 0 ? _w.Groups[_w.Grp[i]] : "Vật thể")} #{i}";

    string PanelText()
    {
        var sb = new System.Text.StringBuilder();
        if (_sel < 0 || _sel >= _w.N || !_w.Alive[_sel])
        {
            for (int e = 0; e < World.NElem; e++) sb.Append($"[color=#{ElemCol[e].ToHtml(false)}]■[/color] {ElemVi[e]}\n");
            return sb.Append("Bấm trái vào vật thể: xem tham số, camera bám theo\nGiữ chuột trái kéo ra: đẩy vận tốc (mũi tên lực)\nBấm phải: tạo vật thể mới (thông số cấu hình ở bảng phải)").ToString();
        }
        int i = _sel; double m = _w.M[i];
        sb.Append($"[b]{NameOf(i)}[/b]   {KindVi[(int)_w.KindOf(i)]}\n");
        sb.Append($"khối lượng {m / World.EarthMass:G4} Trái Đất   bán kính {_w.R[i]:G3}\n");
        sb.Append($"tốc độ {Math.Sqrt(_w.Vx[i] * _w.Vx[i] + _w.Vy[i] * _w.Vy[i]):F3}   hút vật khác: {(_w.Attracts(i) ? "có" : "không")}\n");
        int g = _w.Grp[i];
        if (g > 0)
        {
            int cnt = 0; double tot = 0;
            for (int j = 0; j < _w.N; j++) if (_w.Alive[j] && _w.Grp[j] == g) { cnt++; tot += _w.M[j]; }
            sb.Append($"thuộc {_w.Groups[g]}: {cnt} vật thể, tổng {tot / World.EarthMass:G3} Trái Đất\n");
        }
        for (int e = 0; e < World.NElem; e++)
            sb.Append($"[color=#{ElemCol[e].ToHtml(false)}]■[/color] {ElemVi[e]}   {100 * _w.Comp[i * World.NElem + e] / m:F1}%\n");
        return sb.ToString();
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
        if (_sel >= 0 && _sel < _w.N && _w.Alive[_sel] && _w.Attracts(_sel) && _w.KindOf(_sel) != Kind.Star)
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
            bool far = true;
            if (kind == Kind.Moon && _w.Par[i] >= 0 && _w.Par[i] < _w.N && _w.Alive[_w.Par[i]])
                far = Screen(_w.Par[i]).DistanceTo(p) > 30;
            if (kind != Kind.Star && far)
                DrawString(font, p + new Vector2(r + 6, 4), NameOf(i), HorizontalAlignment.Left, -1, 13, new Color(1, 1, 1, 0.75f));
        }

        // Selection ring
        if (_sel >= 0 && _sel < _w.N && _w.Alive[_sel])
        {
            DrawArc(Screen(_sel), Px(_sel) + 5, 0, MathF.Tau, 32, Colors.White, 1.5f);
        }

        // Push tool arrow during drag
        if (_pushDragging && _sel >= 0 && _sel < _w.N && _w.Alive[_sel])
        {
            Vector2 from = Screen(_sel);
            Vector2 to = _pushDragCurrent;
            if (from.DistanceTo(to) > 4)
            {
                Color arrowCol = new Color(1f, 0.9f, 0.2f, 0.95f);
                DrawLine(from, to, arrowCol, 2.5f);
                Vector2 dir = (to - from).Normalized();
                Vector2 perp = new Vector2(-dir.Y, dir.X);
                Vector2 p1 = to - dir * 14 + perp * 7;
                Vector2 p2 = to - dir * 14 - perp * 7;
                DrawColoredPolygon(new[] { to, p1, p2 }, arrowCol);
            }
        }
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true } k)
        {
            if (k.Keycode == Key.Bracketright)
            {
                GodTools.SetConst(_w, "G", _w.C.G * 1.05);
                _ui?.RefreshConsts();
            }
            if (k.Keycode == Key.Bracketleft)
            {
                GodTools.SetConst(_w, "G", _w.C.G / 1.05);
                _ui?.RefreshConsts();
            }
            if (k.Echo) return;
            if (k.Keycode == Key.T) _tilt = _tilt < 1 ? 1f : 0.5f;
            if (k.Keycode == Key.Space) _paused = !_paused;

            // Time warp shortcuts: 1..7
            if (k.Keycode == Key.Key1) SetTimeWarp(1);
            if (k.Keycode == Key.Key2) SetTimeWarp(2);
            if (k.Keycode == Key.Key3) SetTimeWarp(4);
            if (k.Keycode == Key.Key4) SetTimeWarp(8);
            if (k.Keycode == Key.Key5) SetTimeWarp(16);
            if (k.Keycode == Key.Key6) SetTimeWarp(32);
            if (k.Keycode == Key.Key7) SetTimeWarp(64);
        }

        if (e is InputEventMouseMotion mm && _pushDragging)
        {
            _pushDragCurrent = mm.Position;
            QueueRedraw();
        }

        if (e is InputEventMouseButton m)
        {
            if (m.Pressed)
            {
                if (m.ButtonIndex == MouseButton.WheelUp) _zoom *= 1.25f;
                if (m.ButtonIndex == MouseButton.WheelDown) _zoom /= 1.25f;

                if (m.ButtonIndex == MouseButton.Left)
                {
                    // Check if clicking near the selected object to initiate push drag
                    if (_sel >= 0 && _sel < _w.N && _w.Alive[_sel] && Screen(_sel).DistanceTo(m.Position) <= Px(_sel) + 20)
                    {
                        _pushDragging = true;
                        _pushDragStart = m.Position;
                        _pushDragCurrent = m.Position;
                    }
                    else
                    {
                        // Select nearest object under cursor
                        _sel = -1; float best = float.MaxValue;
                        for (int i = 0; i < _w.N; i++)
                        {
                            if (!_w.Alive[i]) continue;
                            float d = Screen(i).DistanceTo(m.Position) - Px(i);
                            if (d < 12 && d < best) { best = d; _sel = i; }
                        }
                        _ui?.RefreshSelection();
                        _panel.Text = PanelText();
                    }
                }

                if (m.ButtonIndex == MouseButton.Right)
                {
                    // Create tool: place configured object at cursor
                    Vector2 c = GetViewportRect().Size / 2;
                    double x = _cx + (m.Position.X - c.X) / _zoom;
                    double y = _cy + (m.Position.Y - c.Y) / (_zoom * _tilt);
                    double mass = _ui != null ? _ui.GetCreateMass() : World.EarthMass;
                    double[] mix = _ui != null ? _ui.GetCreateMix() : new double[] { 0, 0.02, 0.70, 0.27, 0.005, 0.005 };
                    string name = $"Tạo #{++_createdCount}";

                    int parent = (_sel >= 0 && _sel < _w.N && _w.Alive[_sel]) ? _sel : -1;
                    int newSlot = GodTools.Create(_w, x, y, mass, mix, parent: parent, name: name);
                    if (newSlot >= 0)
                    {
                        _ui?.RefreshSelection();
                        _panel.Text = PanelText();
                    }
                }
            }
            else // Mouse button released
            {
                if (m.ButtonIndex == MouseButton.Left && _pushDragging)
                {
                    if (_sel >= 0 && _sel < _w.N && _w.Alive[_sel])
                    {
                        Vector2 drag = _pushDragCurrent - _pushDragStart;
                        if (drag.Length() > 5)
                        {
                            double dvx = (drag.X / _zoom) * 0.05;
                            double dvy = (drag.Y / (_zoom * _tilt)) * 0.05;
                            GodTools.Push(_w, _sel, dvx, dvy);
                            _ui?.RefreshSelection();
                            _panel.Text = PanelText();
                        }
                    }
                    _pushDragging = false;
                    QueueRedraw();
                }
            }
        }
    }

    /// <summary>
    /// Headless self-test (SPEC 6, P2):
    /// Calls each tool method with fixed inputs, replays journal on fresh world,
    /// prints one line per tool with before/after numbers, exits 0 on match or 1 on mismatch.
    /// </summary>
    public void RunSelfTest()
    {
        GD.Print("=== COSMOS P2 SELFTEST START ===");
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
        double matterBefore = w.Comp[slotMoon * World.NElem + 1]; // ice
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

        // 4. Tool TimeControl: advance steps
        long stepBefore = w.Step;
        int warp = 8;
        for (int s = 0; s < warp; s++) w.Advance(0.5);
        long stepAfter = w.Step;
        GD.Print($"TOOL TimeControl: warp={warp}x step {stepBefore} -> {stepAfter}");

        // 5. Tool SetConst
        double gBefore = w.C.G, rScaleBefore = w.C.RadiusScale;
        bool setG = GodTools.SetConst(w, "G", 1.20);
        bool setR = GodTools.SetConst(w, "RadiusScale", 1.75);
        if (!setG || !setR) { GD.PrintErr("FAIL: SetConst failed"); GetTree().Quit(1); return; }
        GD.Print($"TOOL SetConst: G {gBefore:F2} -> {w.C.G:F2}, RadiusScale {rScaleBefore:F2} -> {w.C.RadiusScale:F2}");

        // Additional stepping with interleaved push to test multiple steps replay
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
