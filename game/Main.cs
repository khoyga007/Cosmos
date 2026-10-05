// Spike shell: steps the core once per frame and draws it tilted (2.5D = the flat world seen from above at an angle).
// Everything is built in code; the scene file only attaches this script.
//   run:  godot --path game -- --grains=100000        keys: T tilt on/off, Space pause, wheel zoom, left click a body = matter table + camera follows it,
//         right click = put a moon there, in orbit around the selected body
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
    int _sel = -1, _np = -1, _frame, _moons;
    double _cx, _cy; // world point at the screen centre; rides the selected body
    const int Stride = 12; // per grain: 8 floats transform + 4 colour
    // one colour and one Vietnamese label per element group, order = World.ElemName
    static readonly Color[] ElemCol = { new(0.72f, 0.78f, 1f), new(0.3f, 0.9f, 1f), new(0.9f, 0.5f, 0.25f), new(1f, 0.85f, 0.4f), new(0.75f, 0.4f, 0.95f), new(0.4f, 1f, 0.3f) };
    static readonly string[] ElemVi = { "Khí nhẹ", "Băng", "Đá", "Kim loại", "Carbon", "Phóng xạ" };
    float _zoom = 0.75f, _tilt = 0.5f; // tilt = cos of the view angle: 1 = straight down
    bool _paused;
    double _stepMs, _fillMs;
    int _bench; double _benchT; int _benchFrames;

    public override void _Ready()
    {
        int grains = 30000;
        foreach (string a in OS.GetCmdlineUserArgs())
        {
            if (a.StartsWith("--grains=")) grains = int.Parse(a[9..]);
            if (a.StartsWith("--bench=")) _bench = int.Parse(a[8..]); // seconds, then print and quit
        }
        _w = World.SolSystem(grains, 1234);

        _mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform2D, UseColors = true, Mesh = new QuadMesh { Size = new Vector2(2, 2) }, InstanceCount = grains };
        _buf = new float[grains * Stride];
        for (int i = 0; i < grains; i++) { _buf[i * Stride] = 1; _buf[i * Stride + 5] = 1; } // identity basis, origin filled per frame
        var inst = new MultiMeshInstance2D { Multimesh = _mm, Modulate = new Color(1, 1, 1, 0.6f), Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add } };
        AddChild(inst);

        var layer = new CanvasLayer(); AddChild(layer);
        _hud = new Label { Position = new Vector2(12, 8) }; layer.AddChild(_hud);
        _panel = new RichTextLabel { Position = new Vector2(12, 36), Size = new Vector2(360, 260), BbcodeEnabled = true, ScrollActive = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        layer.AddChild(_panel);
    }

    public override void _Process(double delta)
    {
        long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        if (!_paused) _w.Advance(0.5);
        long t1 = System.Diagnostics.Stopwatch.GetTimestamp();

        if (_sel >= 0) { _cx = _w.Bx[_sel]; _cy = _w.By[_sel]; }
        Vector2 c = GetViewportRect().Size / 2;
        float[] px = _w.Px, py = _w.Py;
        float ox = c.X - (float)_cx * _zoom, oy = c.Y - (float)_cy * _zoom * _tilt, zt = _zoom * _tilt;
        for (int i = 0, n = _w.Np; i < n; i++) { _buf[i * Stride + 3] = ox + px[i] * _zoom; _buf[i * Stride + 7] = oy + py[i] * zt; }
        if (_w.Np != _np) // a grain was absorbed, slots moved: recolour
        {
            _np = _w.Np; _mm.VisibleInstanceCount = _np;
            for (int i = 0; i < _np; i++) { Color k = ElemCol[_w.Pe[i]]; int o = i * Stride + 8; _buf[o] = k.R; _buf[o + 1] = k.G; _buf[o + 2] = k.B; _buf[o + 3] = 1; }
        }
        RenderingServer.MultimeshSetBuffer(_mm.GetRid(), _buf);
        long t2 = System.Diagnostics.Stopwatch.GetTimestamp();

        double f = 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        _stepMs += ((t1 - t0) * f - _stepMs) * 0.05; _fillMs += ((t2 - t1) * f - _fillMs) * 0.05;
        _hud.Text = $"{Engine.GetFramesPerSecond():F0} fps   {_w.Np} grains   step {_stepMs:F2} ms   draw-fill {_fillMs:F2} ms   tilt {(_tilt < 1 ? "on" : "off")}";
        if (_frame++ % 15 == 0) _panel.Text = PanelText();
        QueueRedraw();

        if (_bench > 0)
        {
            _benchT += delta; _benchFrames++;
            if (_benchT >= _bench)
            {
                _sel = 3; _w.AddMoon(3, _w.Bx[3], _w.By[3] + 0.2, _w.Bm[3] * 0.01, new[] { 0, 0, 1.0, 0, 0, 0 }, "test", 0); GD.Print(PanelText(), " bodies=", _w.Nb);
                GD.Print($"BENCH grains={_w.Np} fps={_benchFrames / _benchT:F1} step_ms={_stepMs:F2} fill_ms={_fillMs:F2} renderer={RenderingServer.GetVideoAdapterName()}");
                GetTree().Quit();
            }
        }
    }

    Vector2 BodyPos(int i)
    {
        Vector2 c = GetViewportRect().Size / 2;
        return new Vector2(c.X + (float)((_w.Bx[i] - _cx) * _zoom), c.Y + (float)((_w.By[i] - _cy) * _zoom * _tilt));
    }

    // true size once it is big enough to see, a fixed dot before that
    float BodyPx(int i) => MathF.Max(i == 0 ? 9f : _w.Bpar[i] > 0 ? 2.5f : 4f, (float)_w.Br[i] * _zoom);

    string PanelText()
    {
        var sb = new System.Text.StringBuilder();
        if (_sel < 0)
        {
            for (int e = 0; e < World.NElem; e++) sb.Append($"[color=#{ElemCol[e].ToHtml(false)}]■[/color] {ElemVi[e]}\n");
            return sb.Append("Bấm trái vào thiên thể: xem thành phần, camera bám theo\nBấm phải: đặt vệ tinh quanh thiên thể đang chọn").ToString();
        }
        double m = _w.Bm[_sel];
        sb.Append($"[b]{_w.Bname[_sel]}[/b]   khối lượng {m / World.EarthMass:N3} Trái Đất\n");
        for (int e = 0; e < World.NElem; e++)
        {
            double k = _w.Bcomp[_sel * World.NElem + e];
            sb.Append($"[color=#{ElemCol[e].ToHtml(false)}]■[/color] {ElemVi[e]}   {100 * k / m:F1}%\n");
        }
        return sb.ToString();
    }

    public override void _Draw()
    {
        Vector2 c = GetViewportRect().Size / 2;
        Font font = ThemeDB.FallbackFont;
        // orbit lines: circle around the parent body, in the world plane, squashed by the tilt like everything else
        for (int i = 1; i < _w.Nb; i++)
        {
            int par = _w.Bpar[i];
            double dx = _w.Bx[i] - _w.Bx[par], dy = _w.By[i] - _w.By[par];
            float rad = (float)(Math.Sqrt(dx * dx + dy * dy) * _zoom);
            if (rad < 6) continue;
            DrawSetTransform(BodyPos(par), 0, new Vector2(1, _tilt));
            DrawArc(Vector2.Zero, rad, 0, MathF.Tau, 128, new Color(1, 1, 1, 0.10f), 1);
        }
        if (_sel > 0) // zone where the selected body, not the star, rules: a moon put inside about half of it stays
        {
            DrawSetTransform(BodyPos(_sel), 0, new Vector2(1, _tilt));
            DrawArc(Vector2.Zero, (float)(_w.Hill(_sel) * _zoom), 0, MathF.Tau, 96, new Color(0.5f, 1f, 0.6f, 0.35f), 1);
        }
        DrawSetTransform(Vector2.Zero);
        for (int i = 0; i < _w.Nb; i++)
        {
            Vector2 p = BodyPos(i);
            float r = BodyPx(i);
            Color col = new(0, 0, 0);
            if (_w.Bcol[i] != 0) col = new Color((_w.Bcol[i] << 8) | 0xFF);
            else for (int e = 0; e < World.NElem; e++) col += ElemCol[e] * (float)(_w.Bcomp[i * World.NElem + e] / _w.Bm[i]); // no own colour: its matter mix
            col.A = 1;
            if (_w.Bname[i] == "Sao Thổ") { DrawSetTransform(p, 0, new Vector2(1, _tilt)); DrawArc(Vector2.Zero, r * 2.1f, 0, MathF.Tau, 48, new Color(0.9f, 0.82f, 0.6f, 0.7f), MathF.Max(1.5f, r * 0.45f)); DrawSetTransform(Vector2.Zero); } // ponytail: drawn ring, not matter
            DrawCircle(p, r, col);
            if (i == _sel) DrawArc(p, r + 5, 0, MathF.Tau, 32, Colors.White, 1.5f);
            if (_w.Bpar[i] <= 0 || (_w.Bx[i] - _w.Bx[_w.Bpar[i]]) * _zoom is > 40 or < -40 || (_w.By[i] - _w.By[_w.Bpar[i]]) * _zoom is > 40 or < -40) // moon names only when zoomed in
            if (i > 0) DrawString(font, p + new Vector2(r + 6, 4), _w.Bname[i], HorizontalAlignment.Left, -1, 13, new Color(1, 1, 1, 0.75f));
        }
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Echo: false } k)
        {
            if (k.Keycode == Key.T) _tilt = _tilt < 1 ? 1f : 0.5f;
            if (k.Keycode == Key.Space) _paused = !_paused;
        }
        if (e is InputEventMouseButton { Pressed: true } m)
        {
            if (m.ButtonIndex == MouseButton.WheelUp) _zoom *= 1.25f;
            if (m.ButtonIndex == MouseButton.WheelDown) _zoom /= 1.25f;
            if (m.ButtonIndex == MouseButton.Left) // nearest body under the cursor, empty space clears
            {
                _sel = -1; float best = float.MaxValue;
                for (int i = 0; i < _w.Nb; i++)
                {
                    float d = BodyPos(i).DistanceTo(m.Position);
                    if (d < BodyPx(i) + 12 && d < best) { best = d; _sel = i; }
                }
                _panel.Text = PanelText();
            }
            if (m.ButtonIndex == MouseButton.Right && _sel >= 0 && _w.Nb < 64) // god puts a moon here, circling the selected body
            {
                Vector2 c = GetViewportRect().Size / 2;
                double x = _cx + (m.Position.X - c.X) / _zoom, y = _cy + (m.Position.Y - c.Y) / (_zoom * _tilt);
                double mass = _w.Bm[_sel] * 0.01; // ponytail: fixed 1% of the parent, rock; mass and matter become choices when the god tools exist
                if (Math.Sqrt(Math.Pow(x - _w.Bx[_sel], 2) + Math.Pow(y - _w.By[_sel], 2)) > _w.Br[_sel] * 1.5)
                    _w.AddMoon(_sel, x, y, mass, new[] { 0, 0.02, 0.70, 0.27, 0.005, 0.005 }, "Vệ tinh " + ++_moons, 0xB8B8C8);
            }
        }
    }
}
