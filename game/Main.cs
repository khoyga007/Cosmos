// Shell: steps the core once per frame and draws it tilted (2.5D = the flat world seen from above at an angle).
// Everything is built in code; the scene file only attaches this script.
//   run:  godot --path game -- --rocks=5000
//   left click = select an object (camera follows it), right click = put a moon there around the selection,
//   wheel = zoom, T = tilt, Space = pause, [ and ] = turn the universe constant G down / up
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
    int _sel = -1, _frame, _moons;
    double _cx, _cy; // world point at the screen centre; rides the selected object
    float _zoom = 0.75f, _tilt = 0.5f; // tilt = cos of the view angle: 1 = straight down
    bool _paused;
    double _stepMs, _fillMs;
    int _bench; double _benchT; int _benchFrames;
    const int Stride = 12; // per small object: 8 floats transform + 4 colour
    // one colour and one Vietnamese label per element group, order = World.ElemName
    static readonly Color[] ElemCol = { new(0.72f, 0.78f, 1f), new(0.3f, 0.9f, 1f), new(0.9f, 0.5f, 0.25f), new(1f, 0.85f, 0.4f), new(0.75f, 0.4f, 0.95f), new(0.4f, 1f, 0.3f) };
    static readonly string[] ElemVi = { "Khí nhẹ", "Băng", "Đá", "Kim loại", "Carbon", "Phóng xạ" };
    static readonly string[] KindVi = { "Sao", "Hành tinh", "Vệ tinh", "Tiểu hành tinh" };

    public override void _Ready()
    {
        int rocks = 5000;
        foreach (string a in OS.GetCmdlineUserArgs())
        {
            if (a.StartsWith("--rocks=")) rocks = int.Parse(a[8..]);
            if (a.StartsWith("--bench=")) _bench = int.Parse(a[8..]); // seconds, then print and quit
        }
        _w = World.SolSystem(rocks, 1234);

        int cap = _w.X.Length;
        _mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform2D, UseColors = true, Mesh = new QuadMesh { Size = new Vector2(2.5f, 2.5f) }, InstanceCount = cap };
        _buf = new float[cap * Stride];
        for (int i = 0; i < cap; i++) { _buf[i * Stride] = 1; _buf[i * Stride + 5] = 1; _buf[i * Stride + 11] = 1; } // identity basis, alpha 1
        AddChild(new MultiMeshInstance2D { Multimesh = _mm, Modulate = new Color(1, 1, 1, 0.8f), Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add } });

        var layer = new CanvasLayer(); AddChild(layer);
        _hud = new Label { Position = new Vector2(12, 8) }; layer.AddChild(_hud);
        _panel = new RichTextLabel { Position = new Vector2(12, 36), Size = new Vector2(420, 320), BbcodeEnabled = true, ScrollActive = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        layer.AddChild(_panel);
    }

    Color MixCol(int i)
    {
        Color col = new(0, 0, 0);
        for (int e = 0; e < World.NElem; e++) col += ElemCol[e] * (float)(_w.Comp[i * World.NElem + e] / _w.M[i]);
        col.A = 1; return col;
    }

    public override void _Process(double delta)
    {
        long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        if (!_paused) _w.Advance(0.5);
        long t1 = System.Diagnostics.Stopwatch.GetTimestamp();

        if (_sel >= 0 && !_w.Alive[_sel]) _sel = -1; // it was swallowed
        if (_sel >= 0) { _cx = _w.X[_sel]; _cy = _w.Y[_sel]; }
        Vector2 c = GetViewportRect().Size / 2;
        // small objects (the ones that do not pull) go through one MultiMesh; the heavy ones are drawn in _Draw
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

        double f = 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        _stepMs += ((t1 - t0) * f - _stepMs) * 0.05; _fillMs += ((t2 - t1) * f - _fillMs) * 0.05;
        _hud.Text = $"{Engine.GetFramesPerSecond():F0} fps   {_w.Live} vật thể ({attract} có lực hút)   step {_stepMs:F2} ms   draw-fill {_fillMs:F2} ms   G = {_w.C.G:F2}  ([ ] để chỉnh)   va chạm: {_w.Merges}";
        if (_frame++ % 15 == 0) _panel.Text = PanelText();
        QueueRedraw();

        if (_bench > 0)
        {
            _benchT += delta; _benchFrames++;
            if (_benchT >= _bench)
            {
                _sel = 3; _w.AddOrbiting(3, _w.X[3], _w.Y[3] + 0.2, _w.M[3] * 0.01, new[] { 0, 0, 1.0, 0, 0, 0 }, "test");
                GD.Print(PanelText()); _sel = 500; GD.Print(PanelText());
                GD.Print($"BENCH objects={_w.Live} fps={_benchFrames / _benchT:F1} step_ms={_stepMs:F2} fill_ms={_fillMs:F2} renderer={RenderingServer.GetVideoAdapterName()}");
                GetTree().Quit();
            }
        }
    }

    Vector2 Screen(int i)
    {
        Vector2 c = GetViewportRect().Size / 2;
        return new Vector2(c.X + (float)((_w.X[i] - _cx) * _zoom), c.Y + (float)((_w.Y[i] - _cy) * _zoom * _tilt));
    }

    // true size once it is big enough to see, a fixed dot before that
    float Px(int i) => MathF.Max(_w.KindOf(i) switch { Kind.Star => 9f, Kind.Planet => 4f, Kind.Moon => 2.5f, _ => 1.5f }, (float)_w.R[i] * _zoom);

    string NameOf(int i) => _w.Name[i] ?? $"{(_w.Grp[i] > 0 ? _w.Groups[_w.Grp[i]] : "Vật thể")} #{i}";

    string PanelText()
    {
        var sb = new System.Text.StringBuilder();
        if (_sel < 0)
        {
            for (int e = 0; e < World.NElem; e++) sb.Append($"[color=#{ElemCol[e].ToHtml(false)}]■[/color] {ElemVi[e]}\n");
            return sb.Append("Bấm trái vào vật thể: xem tham số, camera bám theo\nBấm phải: đặt vệ tinh quanh vật thể đang chọn").ToString();
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
        Font font = ThemeDB.FallbackFont;
        // orbit lines: circle around the parent, in the world plane, squashed by the tilt like everything else
        for (int i = 0; i < _w.N; i++)
        {
            int par = _w.Par[i];
            if (!_w.Alive[i] || par < 0 || !_w.Alive[par]) continue;
            double dx = _w.X[i] - _w.X[par], dy = _w.Y[i] - _w.Y[par];
            float rad = (float)(Math.Sqrt(dx * dx + dy * dy) * _zoom);
            if (rad < 6) continue;
            DrawSetTransform(Screen(par), 0, new Vector2(1, _tilt));
            DrawArc(Vector2.Zero, rad, 0, MathF.Tau, 128, new Color(1, 1, 1, 0.10f), 1);
        }
        if (_sel >= 0 && _w.Attracts(_sel) && _w.KindOf(_sel) != Kind.Star) // zone where the selection, not its primary, rules: a moon put inside about half of it stays
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
            if (_w.Name[i] == "Sao Thổ") { DrawSetTransform(p, 0, new Vector2(1, _tilt)); DrawArc(Vector2.Zero, r * 2.1f, 0, MathF.Tau, 48, new Color(0.9f, 0.82f, 0.6f, 0.7f), MathF.Max(1.5f, r * 0.45f)); DrawSetTransform(Vector2.Zero); } // ponytail: drawn ring, not objects
            DrawCircle(p, r, _w.Col[i] != 0 ? new Color((_w.Col[i] << 8) | 0xFF) : MixCol(i));
            bool far = true; // moon names only once the moon has left its parent's dot
            if (kind == Kind.Moon) far = Screen(_w.Par[i]).DistanceTo(p) > 30;
            if (kind != Kind.Star && far) DrawString(font, p + new Vector2(r + 6, 4), NameOf(i), HorizontalAlignment.Left, -1, 13, new Color(1, 1, 1, 0.75f));
        }
        if (_sel >= 0) DrawArc(Screen(_sel), Px(_sel) + 5, 0, MathF.Tau, 32, Colors.White, 1.5f);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true } k)
        {
            if (k.Keycode == Key.Bracketright) _w.C.G *= 1.05; // the god turns a constant: every object answers at once
            if (k.Keycode == Key.Bracketleft) _w.C.G /= 1.05;
            if (k.Echo) return;
            if (k.Keycode == Key.T) _tilt = _tilt < 1 ? 1f : 0.5f;
            if (k.Keycode == Key.Space) _paused = !_paused;
        }
        if (e is InputEventMouseButton { Pressed: true } m)
        {
            if (m.ButtonIndex == MouseButton.WheelUp) _zoom *= 1.25f;
            if (m.ButtonIndex == MouseButton.WheelDown) _zoom /= 1.25f;
            if (m.ButtonIndex == MouseButton.Left) // nearest object under the cursor, empty space clears
            {
                _sel = -1; float best = float.MaxValue;
                for (int i = 0; i < _w.N; i++)
                {
                    if (!_w.Alive[i]) continue;
                    float d = Screen(i).DistanceTo(m.Position) - Px(i);
                    if (d < 10 && d < best) { best = d; _sel = i; }
                }
                _panel.Text = PanelText();
            }
            if (m.ButtonIndex == MouseButton.Right && _sel >= 0) // the god puts a moon here, circling the selection
            {
                Vector2 c = GetViewportRect().Size / 2;
                double x = _cx + (m.Position.X - c.X) / _zoom, y = _cy + (m.Position.Y - c.Y) / (_zoom * _tilt);
                double mass = _w.M[_sel] * 0.01; // ponytail: fixed 1% of the parent, rock; mass and matter become choices when the god tools exist
                if (Math.Sqrt(Math.Pow(x - _w.X[_sel], 2) + Math.Pow(y - _w.Y[_sel], 2)) > _w.R[_sel] * 1.5)
                    _w.AddOrbiting(_sel, x, y, mass, new[] { 0, 0.02, 0.70, 0.27, 0.005, 0.005 }, "Vệ tinh " + ++_moons, 0xB8B8C8);
            }
        }
    }
}
