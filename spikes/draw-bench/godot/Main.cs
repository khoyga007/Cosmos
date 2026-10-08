using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;

public partial class Main : Node2D
{
    private string _sceneName = "A";
    private string _mode = "cpu";
    private string _outPath = "results_godot.json";
    private double _duration = 10.0;
    private double _warmup = 2.0;

    private int _pointCount = 100_000;
    private int _bodyCount = 0;
    private int _fpsCap = 0;
    private bool _hasBloomAndParticles = false;

    private MultiMeshInstance2D _mmInstance = null!;
    private MultiMesh _mm = null!;
    private float[] _buf = null!;
    private ShaderMaterial? _railMaterial;

    // Body objects (Scene D: major physical bodies)
    private MultiMeshInstance2D? _mmBodiesInstance;
    private MultiMesh? _mmBodies;
    private float[]? _bodyBuf;
    private float[]? _bodyRadii;
    private float[]? _bodyTheta0;
    private float[]? _bodyOmega;

    // Orbit parameters
    private float[] _radii = null!;
    private float[] _theta0 = null!;
    private float[] _omega = null!;
    private Color[] _colors = null!;

    // Telemetry
    private double _elapsedTotal = 0.0;
    private readonly List<double> _frameTimesMs = new();
    private readonly List<double> _cpuPushTimesMs = new();
    private long _lastFrameTimestamp = 0;

    public override void _Ready()
    {
        ParseArguments();

        if (_fpsCap > 0)
        {
            Engine.MaxFps = _fpsCap;
        }
        else
        {
            Engine.MaxFps = 0;
        }

        switch (_sceneName.ToUpperInvariant())
        {
            case "A":
                _pointCount = 100_000;
                _bodyCount = 0;
                _hasBloomAndParticles = false;
                break;
            case "B":
                _pointCount = 1_000_000;
                _bodyCount = 0;
                _hasBloomAndParticles = false;
                break;
            case "C":
                _pointCount = 1_000_000;
                _bodyCount = 0;
                _hasBloomAndParticles = true;
                break;
            case "D1":
            case "D_100K":
                _pointCount = 100_000;
                _bodyCount = 1_000;
                _hasBloomAndParticles = false;
                break;
            case "D2":
            case "D_300K":
                _pointCount = 300_000;
                _bodyCount = 1_000;
                _hasBloomAndParticles = false;
                break;
            case "D3":
            case "D_1M":
                _pointCount = 1_000_000;
                _bodyCount = 1_000;
                _hasBloomAndParticles = false;
                break;
            case "D":
                if (_pointCount == 100_000) _pointCount = 100_000;
                if (_bodyCount == 0) _bodyCount = 1_000;
                _hasBloomAndParticles = false;
                break;
            default:
                GD.PrintErr($"Unknown scene: {_sceneName}, defaulting to A (100k)");
                _pointCount = 100_000;
                _bodyCount = 0;
                break;
        }

        GD.Print($"[Godot DrawBench] Starting Scene {_sceneName} ({_pointCount:N0} points, {_bodyCount:N0} bodies), Mode {_mode}, FpsCap {_fpsCap}, Duration {_duration}s, Warmup {_warmup}s");

        InitOrbitData();
        InitMultiMesh();
        InitBodies();

        if (_hasBloomAndParticles)
        {
            InitBloomAndParticles();
        }

        _lastFrameTimestamp = Stopwatch.GetTimestamp();
    }

    private void ParseArguments()
    {
        string[] args = OS.GetCmdlineUserArgs();
        if (args.Length == 0)
        {
            args = OS.GetCmdlineArgs();
        }

        foreach (string arg in args)
        {
            if (arg.StartsWith("--scene="))
                _sceneName = arg.Substring("--scene=".Length).Trim();
            else if (arg.StartsWith("--mode="))
                _mode = arg.Substring("--mode=".Length).Trim().ToLowerInvariant();
            else if (arg.StartsWith("--out="))
                _outPath = arg.Substring("--out=".Length).Trim();
            else if (arg.StartsWith("--duration="))
                double.TryParse(arg.Substring("--duration=".Length).Trim(), System.Globalization.CultureInfo.InvariantCulture, out _duration);
            else if (arg.StartsWith("--warmup="))
                double.TryParse(arg.Substring("--warmup=".Length).Trim(), System.Globalization.CultureInfo.InvariantCulture, out _warmup);
            else if (arg.StartsWith("--fps-cap="))
                int.TryParse(arg.Substring("--fps-cap=".Length).Trim(), out _fpsCap);
            else if (arg.StartsWith("--points="))
                int.TryParse(arg.Substring("--points=".Length).Trim(), out _pointCount);
            else if (arg.StartsWith("--bodies="))
                int.TryParse(arg.Substring("--bodies=".Length).Trim(), out _bodyCount);
        }
    }

    private void InitOrbitData()
    {
        var rng = new Random(1234);
        _radii = new float[_pointCount];
        _theta0 = new float[_pointCount];
        _omega = new float[_pointCount];
        _colors = new Color[_pointCount];

        for (int i = 0; i < _pointCount; i++)
        {
            float r = 50.0f + (float)rng.NextDouble() * 450.0f;
            float t0 = (float)(rng.NextDouble() * Math.PI * 2.0);
            float w = 25.0f / (float)Math.Sqrt(r); // Keplerian-like speed

            _radii[i] = r;
            _theta0[i] = t0;
            _omega[i] = w;

            // Gradient color based on radius
            float normR = (r - 50.0f) / 450.0f;
            _colors[i] = new Color(
                0.4f + 0.6f * (1.0f - normR),
                0.5f + 0.5f * normR,
                0.8f + 0.2f * (float)rng.NextDouble(),
                0.85f
            );
        }
    }

    private void InitMultiMesh()
    {
        bool isShader = _mode == "shader";

        _mm = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform2D,
            UseColors = true,
            UseCustomData = isShader,
            Mesh = new QuadMesh { Size = new Vector2(2.0f, 2.0f) },
            InstanceCount = _pointCount
        };

        _mmInstance = new MultiMeshInstance2D
        {
            Multimesh = _mm
        };

        if (isShader)
        {
            var shader = GD.Load<Shader>("res://orbit_rail.gdshader");
            _railMaterial = new ShaderMaterial { Shader = shader };
            _railMaterial.SetShaderParameter("center", new Vector2(960.0f, 540.0f));
            _mmInstance.Material = _railMaterial;

            // Stride for Transform2D + Color + CustomData = 8 + 4 + 4 = 16 floats
            const int stride = 16;
            _buf = new float[_pointCount * stride];

            for (int i = 0; i < _pointCount; i++)
            {
                int o = i * stride;
                // Transform2D identity
                _buf[o + 0] = 1.0f; // x.x
                _buf[o + 1] = 0.0f; // x.y
                _buf[o + 2] = 0.0f;
                _buf[o + 3] = 0.0f; // origin.x (calculated in shader)
                _buf[o + 4] = 0.0f; // y.x
                _buf[o + 5] = 1.0f; // y.y
                _buf[o + 6] = 0.0f;
                _buf[o + 7] = 0.0f; // origin.y (calculated in shader)

                // Color
                Color c = _colors[i];
                _buf[o + 8] = c.R;
                _buf[o + 9] = c.G;
                _buf[o + 10] = c.B;
                _buf[o + 11] = c.A;

                // Custom Data: r, theta0, omega, unused
                _buf[o + 12] = _radii[i];
                _buf[o + 13] = _theta0[i];
                _buf[o + 14] = _omega[i];
                _buf[o + 15] = 0.0f;
            }

            RenderingServer.MultimeshSetBuffer(_mm.GetRid(), _buf);
        }
        else
        {
            _mmInstance.Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };

            // Stride for Transform2D + Color = 8 + 4 = 12 floats
            const int stride = 12;
            _buf = new float[_pointCount * stride];

            for (int i = 0; i < _pointCount; i++)
            {
                int o = i * stride;
                _buf[o + 0] = 1.0f;
                _buf[o + 5] = 1.0f;
                Color c = _colors[i];
                _buf[o + 8] = c.R;
                _buf[o + 9] = c.G;
                _buf[o + 10] = c.B;
                _buf[o + 11] = c.A;
            }
        }

        AddChild(_mmInstance);
    }

    private void InitBodies()
    {
        if (_bodyCount <= 0) return;

        var rng = new Random(9999);
        _bodyRadii = new float[_bodyCount];
        _bodyTheta0 = new float[_bodyCount];
        _bodyOmega = new float[_bodyCount];
        Color[] bodyColors = new Color[_bodyCount];

        for (int i = 0; i < _bodyCount; i++)
        {
            float r = 80.0f + (float)rng.NextDouble() * 420.0f;
            float t0 = (float)(rng.NextDouble() * Math.PI * 2.0);
            float w = 22.0f / (float)Math.Sqrt(r);

            _bodyRadii[i] = r;
            _bodyTheta0[i] = t0;
            _bodyOmega[i] = w;

            // Distinct bright colors for major bodies (stars/planets/named asteroids §15)
            bodyColors[i] = new Color(
                0.8f + 0.2f * (float)rng.NextDouble(),
                0.6f + 0.4f * (float)rng.NextDouble(),
                0.3f + 0.5f * (float)rng.NextDouble(),
                1.0f
            );
        }

        _mmBodies = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform2D,
            UseColors = true,
            Mesh = new QuadMesh { Size = new Vector2(8.0f, 8.0f) },
            InstanceCount = _bodyCount
        };

        const int stride = 12;
        _bodyBuf = new float[_bodyCount * stride];
        for (int i = 0; i < _bodyCount; i++)
        {
            int o = i * stride;
            _bodyBuf[o + 0] = 1.0f;
            _bodyBuf[o + 5] = 1.0f;
            Color c = bodyColors[i];
            _bodyBuf[o + 8] = c.R;
            _bodyBuf[o + 9] = c.G;
            _bodyBuf[o + 10] = c.B;
            _bodyBuf[o + 11] = c.A;
        }

        RenderingServer.MultimeshSetBuffer(_mmBodies.GetRid(), _bodyBuf);
        _mmBodiesInstance = new MultiMeshInstance2D
        {
            Multimesh = _mmBodies,
            Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add }
        };
        AddChild(_mmBodiesInstance);
    }

    private void InitBloomAndParticles()
    {
        // 1. 200 bloom / glow spots
        var glowMm = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform2D,
            UseColors = true,
            Mesh = new QuadMesh { Size = new Vector2(64.0f, 64.0f) },
            InstanceCount = 200
        };

        float[] glowBuf = new float[200 * 12];
        var rng = new Random(5678);
        Vector2 center = new(960.0f, 540.0f);

        for (int i = 0; i < 200; i++)
        {
            int o = i * 12;
            float r = 50.0f + (float)rng.NextDouble() * 450.0f;
            float angle = (float)(rng.NextDouble() * Math.PI * 2.0);
            float gx = center.X + r * (float)Math.Cos(angle);
            float gy = center.Y + r * (float)Math.Sin(angle);

            glowBuf[o + 0] = 1.0f;
            glowBuf[o + 3] = gx;
            glowBuf[o + 5] = 1.0f;
            glowBuf[o + 7] = gy;
            glowBuf[o + 8] = 0.2f;
            glowBuf[o + 9] = 0.4f;
            glowBuf[o + 10] = 0.9f;
            glowBuf[o + 11] = 0.15f;
        }

        RenderingServer.MultimeshSetBuffer(glowMm.GetRid(), glowBuf);
        var glowInstance = new MultiMeshInstance2D
        {
            Multimesh = glowMm,
            Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add }
        };
        AddChild(glowInstance);

        // 2. 50k explosion particles
        var particles = new GpuParticles2D
        {
            Amount = 50_000,
            Lifetime = 2.5,
            Explosiveness = 0.6f,
            Position = center
        };

        var pMat = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere,
            EmissionSphereRadius = 150.0f,
            Spread = 180.0f,
            InitialVelocityMin = 50.0f,
            InitialVelocityMax = 200.0f,
            Gravity = Vector3.Zero,
            Color = new Color(1.0f, 0.6f, 0.2f, 0.8f)
        };
        particles.ProcessMaterial = pMat;
        AddChild(particles);
    }

    public override void _Process(double delta)
    {
        long now = Stopwatch.GetTimestamp();
        double frameMs = (now - _lastFrameTimestamp) * 1000.0 / Stopwatch.Frequency;
        _lastFrameTimestamp = now;

        _elapsedTotal += delta;

        long tPushStart = Stopwatch.GetTimestamp();

        if (_mode == "shader")
        {
            // Vertex shader evaluation: CPU only sets uniform time
            _railMaterial?.SetShaderParameter("time", (float)_elapsedTotal);
        }
        else
        {
            // CPU calculation + buffer push
            const int stride = 12;
            float t = (float)_elapsedTotal;
            float cx = 960.0f;
            float cy = 540.0f;

            for (int i = 0; i < _pointCount; i++)
            {
                float angle = _theta0[i] + _omega[i] * t;
                float r = _radii[i];
                float px = cx + r * (float)Math.Cos(angle);
                float py = cy + r * (float)Math.Sin(angle);

                int o = i * stride;
                _buf[o + 3] = px;
                _buf[o + 7] = py;
            }

            RenderingServer.MultimeshSetBuffer(_mm.GetRid(), _buf);
        }

        if (_bodyCount > 0 && _bodyBuf != null && _mmBodies != null)
        {
            const int strideB = 12;
            float t = (float)_elapsedTotal;
            float cx = 960.0f;
            float cy = 540.0f;
            for (int i = 0; i < _bodyCount; i++)
            {
                float angle = _bodyTheta0![i] + _bodyOmega![i] * t;
                float r = _bodyRadii![i];
                float px = cx + r * (float)Math.Cos(angle);
                float py = cy + r * (float)Math.Sin(angle);

                int o = i * strideB;
                _bodyBuf[o + 3] = px;
                _bodyBuf[o + 7] = py;
            }
            RenderingServer.MultimeshSetBuffer(_mmBodies.GetRid(), _bodyBuf);
        }

        long tPushEnd = Stopwatch.GetTimestamp();
        double cpuPushMs = (tPushEnd - tPushStart) * 1000.0 / Stopwatch.Frequency;

        // Collect stats only after warmup
        if (_elapsedTotal >= _warmup)
        {
            _frameTimesMs.Add(frameMs);
            _cpuPushTimesMs.Add(cpuPushMs);
        }

        if (_elapsedTotal >= _duration)
        {
            FinishBenchmark();
        }
    }

    private void FinishBenchmark()
    {
        SetProcess(false);

        double avgFrameMs = _frameTimesMs.Count > 0 ? _frameTimesMs.Average() : 0.0;
        double p50FrameMs = Percentile(_frameTimesMs, 0.50);
        double p95FrameMs = Percentile(_frameTimesMs, 0.95);
        double p99FrameMs = Percentile(_frameTimesMs, 0.99);
        double avgCpuPushMs = _cpuPushTimesMs.Count > 0 ? _cpuPushTimesMs.Average() : 0.0;

        double ramMb = Process.GetCurrentProcess().WorkingSet64 / (1024.0 * 1024.0);
        double vramMb = RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.VideoMemUsed) / (1024.0 * 1024.0);
        string adapter = RenderingServer.GetVideoAdapterName();
        string engineVer = Engine.GetVersionInfo()["string"].AsString();

        var result = new Dictionary<string, object>
        {
            ["engine"] = "godot",
            ["engine_version"] = engineVer,
            ["scene"] = _sceneName,
            ["points"] = _pointCount,
            ["bodies"] = _bodyCount,
            ["mode"] = _mode,
            ["fps_cap"] = _fpsCap,
            ["has_bloom_and_particles"] = _hasBloomAndParticles,
            ["frame_count_measured"] = _frameTimesMs.Count,
            ["frame_ms_avg"] = Math.Round(avgFrameMs, 4),
            ["frame_ms_p50"] = Math.Round(p50FrameMs, 4),
            ["frame_ms_p95"] = Math.Round(p95FrameMs, 4),
            ["frame_ms_p99"] = Math.Round(p99FrameMs, 4),
            ["fps_avg"] = avgFrameMs > 0 ? Math.Round(1000.0 / avgFrameMs, 2) : 0,
            ["cpu_push_ms"] = Math.Round(avgCpuPushMs, 4),
            ["ram_mb"] = Math.Round(ramMb, 2),
            ["vram_mb"] = Math.Round(vramMb, 2),
            ["gpu_adapter_name"] = adapter,
            ["duration_sec"] = _duration,
            ["warmup_sec"] = _warmup
        };

        string json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        GD.Print($"[Godot DrawBench Result]\n{json}");

        try
        {
            string fullPath = Path.IsPathRooted(_outPath) ? _outPath : ProjectSettings.GlobalizePath(_outPath);
            string? dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllText(fullPath, json);
            GD.Print($"Saved results to {fullPath}");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Failed to write result to {_outPath}: {ex.Message}");
        }

        GetTree().Quit(0);
    }

    private static double Percentile(List<double> sequence, double excelPercentile)
    {
        if (sequence.Count == 0) return 0.0;
        var sorted = sequence.OrderBy(x => x).ToList();
        int N = sorted.Count;
        double n = (N - 1) * excelPercentile + 1;
        if (n <= 1) return sorted[0];
        if (n >= N) return sorted[N - 1];
        int k = (int)n;
        double d = n - k;
        return sorted[k - 1] + d * (sorted[k] - sorted[k - 1]);
    }
}
