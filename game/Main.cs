// Spike shell: steps the core once per frame and draws it tilted (2.5D = the flat world seen from above at an angle).
// Everything is built in code; the scene file only attaches this script.
//   run:  godot --path game -- --grains=100000        keys: T tilt on/off, Space pause, wheel zoom
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
        _w = World.Solar(grains, 8, 1234);

        _mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform2D, Mesh = new QuadMesh { Size = new Vector2(2, 2) }, InstanceCount = grains };
        _buf = new float[grains * 8];
        for (int i = 0; i < grains; i++) { _buf[i * 8] = 1; _buf[i * 8 + 5] = 1; } // identity basis, origin filled per frame
        var inst = new MultiMeshInstance2D { Multimesh = _mm, Modulate = new Color(0.55f, 0.7f, 1f, 0.55f), Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add } };
        AddChild(inst);

        var layer = new CanvasLayer(); AddChild(layer);
        _hud = new Label { Position = new Vector2(12, 8) }; layer.AddChild(_hud);
    }

    public override void _Process(double delta)
    {
        long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        if (!_paused) _w.Advance(0.5);
        long t1 = System.Diagnostics.Stopwatch.GetTimestamp();

        Vector2 c = GetViewportRect().Size / 2;
        float[] px = _w.Px, py = _w.Py;
        for (int i = 0, n = _w.Np; i < n; i++) { _buf[i * 8 + 3] = c.X + px[i] * _zoom; _buf[i * 8 + 7] = c.Y + py[i] * _zoom * _tilt; }
        RenderingServer.MultimeshSetBuffer(_mm.GetRid(), _buf);
        long t2 = System.Diagnostics.Stopwatch.GetTimestamp();

        double f = 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        _stepMs += ((t1 - t0) * f - _stepMs) * 0.05; _fillMs += ((t2 - t1) * f - _fillMs) * 0.05;
        _hud.Text = $"{Engine.GetFramesPerSecond():F0} fps   {_w.Np} grains   step {_stepMs:F2} ms   draw-fill {_fillMs:F2} ms   tilt {(_tilt < 1 ? "on" : "off")}";
        QueueRedraw();

        if (_bench > 0)
        {
            _benchT += delta; _benchFrames++;
            if (_benchT >= _bench)
            {
                GD.Print($"BENCH grains={_w.Np} fps={_benchFrames / _benchT:F1} step_ms={_stepMs:F2} fill_ms={_fillMs:F2} renderer={RenderingServer.GetVideoAdapterName()}");
                GetTree().Quit();
            }
        }
    }

    public override void _Draw()
    {
        Vector2 c = GetViewportRect().Size / 2;
        for (int i = 0; i < _w.Nb; i++)
        {
            var p = new Vector2(c.X + (float)_w.Bx[i] * _zoom, c.Y + (float)_w.By[i] * _zoom * _tilt);
            if (i == 0) DrawCircle(p, 9 * _zoom + 3, new Color(1f, 0.86f, 0.55f));
            else DrawCircle(p, 3.5f, new Color(0.75f, 0.85f, 1f));
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
            if (m.ButtonIndex == MouseButton.WheelUp) _zoom *= 1.1f;
            if (m.ButtonIndex == MouseButton.WheelDown) _zoom /= 1.1f;
        }
    }
}
