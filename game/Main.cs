// Shell: steps the core once per frame and draws it 2D from straight above (T tilts the picture, draw only).
// Everything is built in code; the scene file only attaches this script.
//   run:  godot --path game -- --rocks=5000
//   Every change to the world goes through World.Do(Command) (SPEC 3): never write world arrays from here.
// Mouse (SPEC 7 round 3):
//   wheel = zoom at the pointer · drag with right / middle button, or left on empty space = move the view
//   left click = select (bodies before rocks) · double click = select and follow · right click = drop tool / selection
//   left drag starting on the selected object = push it (the line shows the orbit it would get)
//   tools (bar at the bottom, or keys): V vector = press an object and drag, the arrow IS its new velocity;
//   M move = drag an object to a new place; A pull / R push away = hold the button, everything inside the ring feels it
//   tool "create" (C): the pointer carries the new object; click = circular orbit around whatever rules that
//   spot, press and drag = launch it with that velocity
// Keys: Space pause · 1..7 speed · C create · V vector · M move · A pull · R push away · F follow · Delete remove · Esc cancel · H whole system · Tab panel · T tilt · [ ] G
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using Cosmos.Core;

namespace Cosmos.Game;

public partial class Main : Node2D
{
    public record struct QueuedGodAction(Action<World> Action, int TargetSlot = -1, int ExpectedGen = -1);
    public record JumpRequest(double Years, string? Label, int HeavySlot, int HeavyGen, double HeavyX, double HeavyY);
    public class WorkerResult
    {
        public bool IsJump;
        public string? JumpLabel;
        public int EventsBefore;
        public int EventsAfter;
        public int Steps;
        public double ElapsedMs;
        public double CamDeltaX;
        public double CamDeltaY;
        public Exception? Error;
    }

    World _w = null!;
    readonly RenderSnapshot _snapshot = new();
    public RenderSnapshot Snapshot => _snapshot;

    readonly ConcurrentQueue<QueuedGodAction> _pendingActions = new();
    readonly ConcurrentQueue<JumpRequest> _pendingJumps = new();
    Task<WorkerResult>? _workerTask;
    readonly CancellationTokenSource _cts = new();

    public static Func<Task>? WorkerBarrier;
    public static Action<World>? TestActionHook;
    public static Action? WorkerStepFinished;

    MultiMesh _mm = null!;
    float[] _buf = null!;
    Label _hud = null!, _toast = null!;
    RichTextLabel _panel = null!;
    GodUi _ui = null!;

    int _sel = -1, _follow = -1, _frame, _createdCount;
    int _selSeen = -1, _selGen, _followSeen = -1, _followGen;
    public int Selected => _sel;

    double _cx, _cy;
    float _zoom = 0.75f, _tilt = 1f;
    bool _paused;
    public enum Tool { Select, Create, Vector, Move, Pull, Shove }
    Tool _tool;
    bool _creating => _tool == Tool.Create;
    int _grab = -1, _downSlot = -1, _downGen = -1;
    int _timeWarp = 1;
    double _toastLeft;

    // one mouse gesture at a time
    enum Drag { None, Maybe, Pan, Push, Place, Aim, Carry, Hand }
    Drag _drag;
    MouseButton _dragBtn;
    Vector2 _downPos, _mouse;
    bool _downOnSel;
    double _placeX, _placeY;
    const float DragStart = 6, LaunchStart = 8, DragPerOrbitSpeed = 120; // pixels
    readonly double[] _path = new double[2 * 220];
    readonly Vector2[] _pathPts = new Vector2[220];

    double _stepMs, _fillMs, _snapMs;
    int _bench; double _benchT; int _benchFrames;
    bool _selftest, _uitest, _workertest;
    int _rocks;
    int _mainThreadId;
    const int Stride = 12;

    static readonly Color[] ElemCol = Array.ConvertAll(ElementCatalog.Colours, c => new Color(((c >> 16) & 255) / 255f, ((c >> 8) & 255) / 255f, (c & 255) / 255f));
    static readonly Color AimCol = new(1f, 0.9f, 0.2f, 0.95f);

    public override void _Ready()
    {
        _mainThreadId = System.Environment.CurrentManagedThreadId;
        // numbers are typed and shown with a dot whatever the machine's language (vi-VN reads "1.5" as 15)
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        int rocks = 5000;
        var allArgs = OS.GetCmdlineArgs().Concat(OS.GetCmdlineUserArgs());
        foreach (string a in allArgs)
        {
            if (a == "--selftest") _selftest = true;
            if (a == "--uitest") _uitest = true;
            if (a == "--workertest") _workertest = true;
            if (a.StartsWith("--rocks=")) rocks = int.Parse(a[8..]);
            if (a.StartsWith("--bench=")) _bench = int.Parse(a[8..]);
        }

        if (_uitest)
        {
            GetTree().Root.Size = new Vector2I(1280, 800);
        }

        if (_selftest)
        {
            SetProcess(false);
            RunSelfTest();
            return;
        }

        _w = World.SolSystem(rocks, 1234); _rocks = rocks;
        _snapshot.CopyFrom(_w);

        int cap = _w.X.Length;
        _mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform2D, UseColors = true, Mesh = new QuadMesh { Size = new Vector2(2.5f, 2.5f) }, InstanceCount = cap };
        _buf = new float[cap * Stride];
        for (int i = 0; i < cap; i++) { _buf[i * Stride] = 1; _buf[i * Stride + 5] = 1; _buf[i * Stride + 11] = 1; }
        AddChild(new MultiMeshInstance2D { Multimesh = _mm, Modulate = new Color(1, 1, 1, 0.8f), Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add } });

        var layer = new CanvasLayer(); AddChild(layer);
        _hud = new Label { Position = new Vector2(12, 8), MouseFilter = Control.MouseFilterEnum.Ignore }; layer.AddChild(_hud);
        _panel = new RichTextLabel { Position = new Vector2(12, 36), Size = new Vector2(420, 420), BbcodeEnabled = true, ScrollActive = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        layer.AddChild(_panel);
        _toast = new Label { MouseFilter = Control.MouseFilterEnum.Ignore, AnchorTop = 1, AnchorBottom = 1, OffsetLeft = 14, OffsetTop = -146, OffsetBottom = -118, Modulate = new Color(1, 1, 1, 0) };
        _toast.AddThemeColorOverride("font_color", new Color(1f, 0.92f, 0.5f));
        layer.AddChild(_toast);

        _ui = new GodUi(this, _w);
        AddChild(_ui);
        _ui.UpdateTimeWarp(_timeWarp); // AddChild has run its _Ready already

        if (_workertest)
        {
            SetProcess(false);
            RunWorkerTest();
            return;
        }
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
    public void SetCreating(bool on) => SetTool(on ? Tool.Create : Tool.Select);

    public Tool CurrentTool => _tool;
    public void SetTool(Tool tool)
    {
        _tool = tool; _drag = Drag.None; _grab = -1;
        _ui?.UpdateCreating(_creating);
        _ui?.UpdateTool(tool);
        string? say = tool switch
        {
            Tool.Create => "Đặt vật thể: bấm = quỹ đạo tròn, giữ và kéo = phóng đi.",
            Tool.Vector => "Vector: giữ chuột trên một vật thể rồi kéo — mũi tên chính là vận tốc mới của nó.",
            Tool.Move => "Di dời: giữ chuột trên một vật thể, kéo tới chỗ mới rồi thả.",
            Tool.Pull => "Hút: giữ chuột trái, mọi thứ trong vòng tròn bị kéo về con trỏ.",
            Tool.Shove => "Đẩy ra: giữ chuột trái, mọi thứ trong vòng tròn bị hất khỏi con trỏ.",
            _ => null
        };
        if (say != null) Toast(say + " Chuột phải hoặc Esc để thôi.");
    }

    public void Toast(string text)
    {
        if (Thread.CurrentThread.ManagedThreadId != _mainThreadId)
        {
            Callable.From(() => Toast(text)).CallDeferred();
            return;
        }
        if (_toast != null) { _toast.Text = text; _toastLeft = 4; }
    }

    public void QueueGodAction(Action<World> action, int targetSlot = -1, int expectedGen = -1)
    {
        if (_uitest)
        {
            if (targetSlot >= 0)
            {
                if (targetSlot >= _w.N || !_w.Alive[targetSlot] || _w.Gen[targetSlot] != expectedGen)
                    return;
            }
            action(_w);
            _snapshot.CopyFrom(_w);
            return;
        }
        _pendingActions.Enqueue(new QueuedGodAction(action, targetSlot, expectedGen));
    }

    public void Select(int i, bool follow = false)
    {
        if (Thread.CurrentThread.ManagedThreadId != _mainThreadId)
        {
            Callable.From(() => Select(i, follow)).CallDeferred();
            return;
        }
        _sel = i;
        if (follow) _follow = i;
        _ui?.RefreshSelection();
        if (_panel != null) _panel.Text = PanelText();
    }

    public void JumpTo(int i)
    {
        if (!Live(i)) return;
        _follow = -1;
        _cx = _snapshot.X[i];
        _cy = _snapshot.Y[i];
        Select(i);
        Toast($"Đã chuyển tới {NameOf(i)}");
    }

    public void FollowSelected() { if (Live(_sel)) { _follow = _sel; Toast($"Bám theo {NameOf(_sel)}"); } }

    public void RemoveSelected()
    {
        if (!Live(_sel)) return;
        int target = _sel;
        int gen = _snapshot.Gen[target];
        string name = NameOf(target);
        QueueGodAction(w => GodTools.Remove(w, target), target, gen);
        Toast($"Đã xoá {name}");
        Select(-1);
    }

    public void CircularizeSelected()
    {
        if (!Live(_sel)) return;
        int target = _sel;
        int gen = _snapshot.Gen[target];
        string name = NameOf(target);
        QueueGodAction(w =>
        {
            int res = GodTools.Circularize(w, target);
            if (res < 0) Toast("Không có vật nào nặng hơn để quay quanh");
        }, target, gen);
        Toast($"{name}: đã đưa vào hàng đợi quỹ đạo tròn");
    }

    public void Home()
    {
        _follow = -1;
        int h = _snapshot.Heaviest();
        if (h >= 0) { _cx = _snapshot.X[h]; _cy = _snapshot.Y[h]; }
        _zoom = 0.75f;
    }

    // PLACEHOLDER: faint grid in the orbit plane, to give the eye a sense of place and scale. G switches it.
    // Lines every power of ten of world units that comes out 60..600 px apart; every tenth one brighter and numbered.
    bool _grid = true;

    // PLACEHOLDER: the zone each attracting object rules (World.Hill), as a faint ring. Z switches it.
    // The heaviest object rules everything left over in the core (no galaxy to pull against yet), so its ring is
    // display only: where the galaxy's tide would take over, the Sun's real figure (about 230 000 AU) scaled by mass^(1/3).
    const double SunTidalAU = 230000, SunMass = 50;
    bool _zones;

    void DrawZones()
    {
        float far = GetViewportRect().Size.Length() * 4;
        for (int i = 0; i < _snapshot.N; i++)
        {
            if (!_snapshot.Alive[i] || !_snapshot.Attracts[i]) continue;
            double h = _snapshot.Hill[i];
            if (h == double.MaxValue) h = World.SolDist(SunTidalAU * Math.Cbrt(_snapshot.M[i] / SunMass));
            h *= _zoom;
            if (!(h > 6) || h > far) continue;
            DrawSetTransform(Screen(i), 0, new Vector2(1, _tilt));
            DrawArc(Vector2.Zero, (float)h, 0, MathF.Tau, 96, new Color(0.45f, 0.75f, 1f, i == _sel ? 0.55f : 0.22f), 1f);
            DrawSetTransform(Vector2.Zero);
        }
    }

    void DrawGrid(Font font)
    {
        Vector2 size = GetViewportRect().Size;
        double step = Math.Pow(10, Math.Ceiling(Math.Log10(60 / _zoom)));
        ToWorld(Vector2.Zero, out double x0, out double y0); ToWorld(size, out double x1, out double y1);
        if (!(step > 0) || (x1 - x0) / step > 400 || (y1 - y0) / step > 400) return;
        Color thin = new(0.55f, 0.65f, 0.9f, 0.07f), thick = new(0.55f, 0.65f, 0.9f, 0.16f), text = new(0.55f, 0.65f, 0.9f, 0.45f);
        for (double k = Math.Floor(x0 / step); k * step <= x1; k++)
        {
            bool main = Math.Abs(k % 10) < 0.5; float sx = ToScreen(k * step, 0).X;
            DrawLine(new Vector2(sx, 0), new Vector2(sx, size.Y), main ? thick : thin, 1);
            if (main) DrawString(font, new Vector2(sx + 3, size.Y - 96), (k * step).ToString("G4"), HorizontalAlignment.Left, -1, 10, text);
        }
        for (double k = Math.Floor(y0 / step); k * step <= y1; k++)
        {
            bool main = Math.Abs(k % 10) < 0.5; float sy = ToScreen(0, k * step).Y;
            DrawLine(new Vector2(0, sy), new Vector2(size.X, sy), main ? thick : thin, 1);
            if (main) DrawString(font, new Vector2(4, sy - 3), (k * step).ToString("G4"), HorizontalAlignment.Left, -1, 10, text);
        }
    }

    /// A jump with the view riding along: a system can drift through space over the ages (the stock scene has no
    /// net momentum, but anything the god adds or throws gives it some), and after a long jump it would be far outside a view left where it was.
    public void JumpKeepingView(double years, string? label = null)
    {
        int h = _snapshot.Heaviest();
        int hGen = (h >= 0 && _snapshot.IsAlive(h)) ? _snapshot.Gen[h] : -1;
        double hx = h >= 0 ? _snapshot.X[h] : 0, hy = h >= 0 ? _snapshot.Y[h] : 0;
        _pendingJumps.Enqueue(new JumpRequest(years, label, h, hGen, hx, hy));
    }
    public void JumpKeepingView(double years) => JumpKeepingView(years, null);

    bool Live(int i) => _snapshot.IsAlive(i);

    // ---- view ----

    Vector2 ToScreen(double x, double y)
    {
        Vector2 c = GetViewportRect().Size / 2;
        return new Vector2(c.X + (float)((x - _cx) * _zoom), c.Y + (float)((y - _cy) * _zoom * _tilt));
    }

    Vector2 Screen(int i) => ToScreen(_snapshot.X[i], _snapshot.Y[i]);

    void ToWorld(Vector2 p, out double x, out double y)
    {
        Vector2 c = GetViewportRect().Size / 2;
        x = _cx + (p.X - c.X) / _zoom; y = _cy + (p.Y - c.Y) / (_zoom * _tilt);
    }

    static float KindPx(Kind k) => k switch { Kind.Star => 9f, Kind.Planet => 4f, Kind.Moon => 2.5f, _ => 1.5f };

    float Px(int i) => MathF.Max(KindPx(_snapshot.Kind[i]), (float)_snapshot.R[i] * _zoom);

    string NameOf(int i) => _snapshot.NameOf(i);

    // How many small objects each planet or moon holds close by (within 4 of its radii), and how far the furthest is.
    const int RingMin = 30;
    const float RingBandPx = 14; // the ring spans fewer pixels than this = its rocks cannot be told apart
    int[] _ringCount = Array.Empty<int>();
    double[] _ringFar = Array.Empty<double>();
    readonly List<int> _ringHosts = new();

    void CountRings()
    {
        if (_ringCount.Length < _snapshot.N) { _ringCount = new int[_snapshot.X.Length]; _ringFar = new double[_snapshot.X.Length]; }
        _ringHosts.Clear();
        for (int i = 0; i < _snapshot.N; i++)
        {
            _ringCount[i] = 0; _ringFar[i] = 0;
            if (_snapshot.Alive[i] && _snapshot.Attracts[i] && _snapshot.Kind[i] != Kind.Star) _ringHosts.Add(i);
        }
        for (int i = 0; i < _snapshot.N; i++)
        {
            if (!_snapshot.Alive[i] || _snapshot.Attracts[i] || _snapshot.IsShip[i]) continue;
            foreach (int h in _ringHosts)
            {
                double dx = _snapshot.X[i] - _snapshot.X[h], dy = _snapshot.Y[i] - _snapshot.Y[h], d2 = dx * dx + dy * dy, reach = _snapshot.R[h] * 4;
                if (d2 >= reach * reach) continue;
                _ringCount[h]++; if (d2 > _ringFar[h]) _ringFar[h] = d2;
                break;
            }
        }
    }

    public void RingSelected()
    {
        if (!Live(_sel)) return;
        int target = _sel;
        int gen = _snapshot.Gen[target];
        string name = NameOf(target);
        var mix = _ui.GetCreateMix();
        ulong seed = (ulong)_snapshot.Step * 31 + (ulong)target;
        QueueGodAction(w =>
        {
            int made = GodTools.MakeRing(w, target, 200, seed, mix);
            Toast(made > 0 ? $"Đã tạo vành đai {made} mảnh quanh {name}" : made == 0 ? "Thế giới đã đầy" : "Vật này không giữ được vành đai");
        }, target, gen);
    }

    // PLACEHOLDER LOOK. Everything an object looks like goes through BodyCol + DrawBody, so that textures
    // can replace these two later without touching what is shown or where.
    Color BodyCol(int i)
    {
        uint star = _snapshot.StarColour[i];
        if (star != 0) return new Color((star << 8) | 0xFF);
        return _snapshot.StarPhase[i] switch
        {
            StarPhase.BlackHole => new Color(0.02f, 0.02f, 0.03f),
            StarPhase.BrownDwarf => new Color(0.42f, 0.2f, 0.14f),
            StarPhase.WhiteDwarf or StarPhase.NeutronStar => new Color(0.45f, 0.47f, 0.55f), // cooled down
            _ => _snapshot.Col[i] != 0 ? new Color((_snapshot.Col[i] << 8) | 0xFF) : MixCol(i)
        };
    }

    void DrawBody(int i, Vector2 p, float r)
    {
        StarPhase phase = _snapshot.StarPhase[i];
        Color col = BodyCol(i);
        if (phase is StarPhase.MainSequence or StarPhase.RedGiant) DrawCircle(p, r * 1.35f, new Color(col.R, col.G, col.B, 0.18f));
        DrawCircle(p, r, col);
        if (phase == StarPhase.BlackHole) DrawArc(p, r + 2, 0, MathF.Tau, 32, new Color(1f, 0.7f, 0.3f, 0.9f), 1.5f);
        if (phase == StarPhase.NeutronStar) DrawArc(p, r + 3, 0, MathF.Tau, 24, new Color(0.7f, 0.85f, 1f, 0.8f), 1f);
    }

    Color MixCol(int i)
    {
        Color col = new(0, 0, 0);
        double m = _snapshot.M[i];
        if (m > 0)
        {
            for (int e = 0; e < World.NElem; e++)
                col += ElemCol[e] * (float)(_snapshot.Comp[i * World.NElem + e] / m);
        }
        col.A = 1; return col;
    }

    // Bodies win over rocks: with 5000 rocks on screen a click near a planet must not land on a pebble.
    int Pick(Vector2 at)
    {
        int best = -1; float bestD = 8;
        for (int i = 0; i < _snapshot.N; i++)
        {
            if (!_snapshot.Alive[i] || !_snapshot.IsShip[i]) continue;
            float d = Screen(i).DistanceTo(at);
            if (d < bestD) { bestD = d; best = i; }
        }
        if (best >= 0) return best;
        bestD = 10;
        for (int i = 0; i < _snapshot.N; i++)
        {
            if (!_snapshot.Alive[i] || !_snapshot.Attracts[i]) continue;
            float d = MathF.Max(0, Screen(i).DistanceTo(at) - Px(i));
            if (d < bestD) { bestD = d; best = i; }
        }
        if (best >= 0) return best;
        bestD = 5;
        for (int i = 0; i < _snapshot.N; i++)
        {
            if (!_snapshot.Alive[i] || _snapshot.Attracts[i]) continue;
            float d = Screen(i).DistanceTo(at);
            if (d < bestD) { bestD = d; best = i; }
        }
        return best;
    }

    public override void _ExitTree()
    {
        try
        {
            if (!_cts.IsCancellationRequested)
                _cts.Cancel();
        }
        catch { }
        try { _workerTask?.Wait(200); } catch { }
    }

    Task<WorkerResult> StartWorker(bool doJump, JumpRequest? jumpReq, int stepsToRun)
    {
        var token = _cts.Token;
        return Task.Run(async () =>
        {
            var res = new WorkerResult();
            try
            {
                if (WorkerBarrier != null)
                    await WorkerBarrier();

                long t0 = Stopwatch.GetTimestamp();

                // 1. Drain pending actions with stale generation check
                while (_pendingActions.TryDequeue(out var act))
                {
                    if (act.TargetSlot >= 0)
                    {
                        if (act.TargetSlot >= _w.N || !_w.Alive[act.TargetSlot] || _w.Gen[act.TargetSlot] != act.ExpectedGen)
                            continue; // Slot was recycled / dead: drop stale action
                    }
                    act.Action(_w);
                }

                if (TestActionHook != null)
                {
                    TestActionHook(_w);
                }

                // 2. Perform Jump or Advance steps
                if (doJump && jumpReq != null)
                {
                    res.IsJump = true;
                    res.JumpLabel = jumpReq.Label;
                    res.EventsBefore = _w.Events.Count;

                    GodTools.FastForward(_w, jumpReq.Years);

                    res.EventsAfter = _w.Events.Count;
                    int h = jumpReq.HeavySlot;
                    if (h >= 0 && h < _w.N && _w.Alive[h] && _w.Gen[h] == jumpReq.HeavyGen)
                    {
                        res.CamDeltaX = _w.X[h] - jumpReq.HeavyX;
                        res.CamDeltaY = _w.Y[h] - jumpReq.HeavyY;
                    }
                }
                else
                {
                    for (int s = 0; s < stepsToRun; s++)
                    {
                        if (token.IsCancellationRequested) break;
                        _w.Advance(0.5);
                        res.Steps++;
                    }
                }

                long t1 = Stopwatch.GetTimestamp();
                res.ElapsedMs = (t1 - t0) * (1000.0 / Stopwatch.Frequency);
            }
            catch (Exception ex)
            {
                res.Error = ex;
            }
            return res;
        }, token);
    }

    public override void _Process(double delta)
    {
        if (_w == null) return;
        if (_uitest && _frame == 3) { _frame++; RunUiTest(); return; }

        if (_drag == Drag.Hand) UseHand(delta);

        // Check background sim worker status
        if (_workerTask != null)
        {
            if (_workerTask.IsCompleted)
            {
                WorkerResult? res = null;
                if (_workerTask.IsFaulted)
                {
                    GD.PrintErr($"Sim Worker Task Faulted: {_workerTask.Exception}");
                    Toast("Lỗi mô phỏng nền! Đã tạm dừng.");
                    _paused = true;
                    _ui?.UpdatePaused(_paused);
                }
                else
                {
                    res = _workerTask.Result;
                    if (res.Error != null)
                    {
                        GD.PrintErr($"Sim Worker Error: {res.Error}");
                        Toast("Lỗi mô phỏng nền! Đã tạm dừng.");
                        _paused = true;
                        _ui?.UpdatePaused(_paused);
                    }
                    else
                    {
                        _stepMs += (res.ElapsedMs - _stepMs) * 0.05;
                        if (!res.IsJump) CheckAutoThrottle(res.ElapsedMs);
                        else
                        {
                            if (_follow < 0)
                            {
                                _cx += res.CamDeltaX;
                                _cy += res.CamDeltaY;
                            }
                            if (res.JumpLabel != null)
                            {
                                int news = res.EventsAfter - res.EventsBefore;
                                Toast(news > 0 ? $"Đã nhảy {res.JumpLabel.TrimStart('+')}: {news} sự kiện mới, xem tab Nhật ký" : $"Đã nhảy {res.JumpLabel.TrimStart('+')}: không có gì đáng kể xảy ra");
                                _ui?.RefreshEvents();
                                _ui?.RefreshSelection();
                            }
                        }
                    }
                }

                // EXCLUSIVE SYNC POINT: Worker is idle, Main thread owns exclusive access to _w
                long tSnap0 = Stopwatch.GetTimestamp();
                _snapshot.CopyFrom(_w);
                long tSnap1 = Stopwatch.GetTimestamp();
                _snapMs = (tSnap1 - tSnap0) * (1000.0 / Stopwatch.Frequency);

                if (_sel >= 0 && !Live(_sel)) Select(-1);
                if (_follow >= 0 && !Live(_follow)) _follow = -1;
                if (_sel != _selSeen) { _selSeen = _sel; _selGen = Live(_sel) ? _snapshot.Gen[_sel] : 0; }
                else if (Live(_sel) && _snapshot.Gen[_sel] != _selGen) _sel = _selSeen = -1;
                if (_follow != _followSeen) { _followSeen = _follow; _followGen = Live(_follow) ? _snapshot.Gen[_follow] : 0; }
                else if (Live(_follow) && _snapshot.Gen[_follow] != _followGen) _follow = _followSeen = -1;
                if (_follow >= 0 && Live(_follow)) { _cx = _snapshot.X[_follow]; _cy = _snapshot.Y[_follow]; }

                WorkerStepFinished?.Invoke();

                // Launch next worker stream iteration
                if (_pendingJumps.TryDequeue(out var jump))
                {
                    _workerTask = StartWorker(true, jump, 0);
                }
                else if (!_paused)
                {
                    _workerTask = StartWorker(false, null, _timeWarp);
                }
                else if (_pendingActions.Count > 0)
                {
                    _workerTask = StartWorker(false, null, 0);
                }
                else
                {
                    _workerTask = null;
                }
            }
        }
        else
        {
            // Initial worker launch or resume from idle
            if (_pendingJumps.TryDequeue(out var jump))
            {
                _workerTask = StartWorker(true, jump, 0);
            }
            else if (!_paused)
            {
                _workerTask = StartWorker(false, null, _timeWarp);
            }
            else if (_pendingActions.Count > 0)
            {
                _workerTask = StartWorker(false, null, 0);
            }
        }

        // Render MultiMesh completely from _snapshot (main thread never touches _w while worker runs)
        long tRender0 = Stopwatch.GetTimestamp();
        Vector2 c = GetViewportRect().Size / 2;
        int n = 0; int attract = 0;
        for (int i = 0; i < _snapshot.N; i++)
        {
            if (!_snapshot.Alive[i]) continue;
            if (_snapshot.Attracts[i]) { attract++; continue; }
            int o = n++ * Stride;
            _buf[o + 3] = c.X + (float)((_snapshot.X[i] - _cx) * _zoom);
            _buf[o + 7] = c.Y + (float)((_snapshot.Y[i] - _cy) * _zoom * _tilt);
            Color k = MixCol(i);
            _buf[o + 8] = k.R; _buf[o + 9] = k.G; _buf[o + 10] = k.B;
        }
        _mm.VisibleInstanceCount = n;
        RenderingServer.MultimeshSetBuffer(_mm.GetRid(), _buf);
        long tRender1 = Stopwatch.GetTimestamp();
        _fillMs += ((tRender1 - tRender0) * (1000.0 / Stopwatch.Frequency) - _fillMs) * 0.05;

        _hud.Text = $"Năm {_snapshot.Year:N1}   {(_paused ? "TẠM DỪNG" : $"tốc độ {_timeWarp}×")}   {_snapshot.Live} vật thể ({attract} có lực hút)   va chạm {_snapshot.Merges}   {Engine.GetFramesPerSecond():F0} fps   [snap {_snapMs:F2}ms]";
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
                if (3 < _snapshot.N && _snapshot.Alive[3])
                {
                    _sel = 3;
                    GD.Print(PanelText());
                }
                int testSlot = Math.Min(500, _snapshot.N - 1);
                while (testSlot >= 0 && !_snapshot.Alive[testSlot]) testSlot--;
                _sel = testSlot;
                GD.Print(PanelText());
                GD.Print($"BENCH objects={_snapshot.Live} fps={_benchFrames / _benchT:F1} step_ms={_stepMs:F2} fill_ms={_fillMs:F2} snap_ms={_snapMs:F2} renderer={RenderingServer.GetVideoAdapterName()}");
                GetTree().Quit(0);
            }
        }
    }

    static readonly Color ShipCol = new(1f, 0.92f, 0.45f);
    const int ChronicleLines = 9;
    readonly System.Collections.Generic.List<int> _lines = new();

    string PanelText()
    {
        var sb = new System.Text.StringBuilder();
        if (!Live(_sel))
        {
            sb.Append("[color=#9aa4c0]Lăn chuột: phóng to tại con trỏ\nKéo chuột (phải, giữa, hoặc trái trên khoảng trống): di chuyển khung nhìn\nBấm: chọn · bấm đúp: chọn và bám theo\nKéo từ vật đang chọn: đẩy nó\nC: đặt vật thể mới · Space: tạm dừng · H: về toàn hệ · G: bật tắt lưới · Z: vùng hấp dẫn[/color]\n\n");
            for (int e = 0; e < World.NElem; e++) sb.Append($"[color=#{ElemCol[e].ToHtml(false)}]■[/color] {GodUi.ElemVi[e]}  ");
            return sb.ToString();
        }
        int i = _sel; double m = _snapshot.M[i];
        sb.Append($"[b]{NameOf(i)}[/b]   {(_snapshot.IsShip[i] ? "Tàu vũ trụ" : GodUi.KindName(_snapshot, i))}{(_follow == i ? "   [color=#9aa4c0](đang bám theo)[/color]" : "")}\n");
        sb.Append($"khối lượng {m / World.EarthMass:G4} Trái Đất   bán kính {_snapshot.R[i]:G3}\n");
        if (_snapshot.IsShip[i])
        {
            int to = _snapshot.ShipTo[i], from = _snapshot.ShipFrom[i];
            sb.Append($"[color=#ffd700]Tàu của văn minh {_snapshot.Civs[_snapshot.ShipCiv[i]].Name}[/color]\n");
            if (Live(from)) sb.Append($"rời {NameOf(from)} năm {_snapshot.ShipBorn[i]:N1}\n");
            if (Live(to))
            {
                double tx = _snapshot.X[to] - _snapshot.X[i], ty = _snapshot.Y[to] - _snapshot.Y[i];
                sb.Append($"đang tới {NameOf(to)}, còn cách {Math.Sqrt(tx * tx + ty * ty):G3}\n");
                sb.Append(_snapshot.Pop[to] > 0 ? "chở tri thức và tiếp tế cho thuộc địa\n" : "chở người đi lập thuộc địa\n");
            }
            sb.Append("[color=#9aa4c0]Tàu tự lái bằng động cơ. Anh vẫn đẩy, hút, di dời, xoá nó được.[/color]\n");
            return sb.ToString();
        }
        sb.Append(GodUi.StarLines(_snapshot, i));
        int p = _snapshot.PrimaryAt(_snapshot.X[i], _snapshot.Y[i], m, i);
        if (p >= 0)
        {
            double dx = _snapshot.X[i] - _snapshot.X[p], dy = _snapshot.Y[i] - _snapshot.Y[p], ux = _snapshot.Vx[i] - _snapshot.Vx[p], uy = _snapshot.Vy[i] - _snapshot.Vy[p];
            double d = Math.Sqrt(dx * dx + dy * dy);
            sb.Append($"quay quanh {NameOf(p)}, cách {d:G4}, tốc độ {Math.Sqrt(ux * ux + uy * uy) / _snapshot.OrbitSpeed(p, _snapshot.X[i], _snapshot.Y[i]):F2}× tốc độ quỹ đạo tròn\n");
        }

        if (_snapshot.IsWorld(i))
        {
            string bandName = !double.IsNaN(_snapshot.Temp[i]) ? GodUi.BandVi[(int)_snapshot.BandOf(_snapshot.Temp[i])] : "Chưa rõ";
            sb.Append($"[color=#ffaa55]Nhiệt độ:[/color] {_snapshot.Temp[i]:F1} K ({bandName})   ");
            int wState = Math.Clamp((int)_snapshot.Water[i], 0, 3);
            sb.Append($"[color=#44ccff]Nước:[/color] {GodUi.WaterVi[wState]}\n");

            if (_snapshot.Life[i] > 0)
                sb.Append($"[color=#55ff88]Sự sống:[/color] {_snapshot.Life[i] * 100:F1}% ({GodUi.LifeStageVi[_snapshot.LifeStage(i)]})\n");
            else
                sb.Append($"[color=#888888]Sự sống:[/color] Chưa có (nước lỏng {_snapshot.WaterYears[i]:N0} năm)\n");

            int civ = _snapshot.Civ[i];
            if (civ >= 0)
            {
                CivInfo info = _snapshot.Civs[civ];
                bool home = info.Home == i;
                if (_snapshot.Pop[i] > 0)
                {
                    sb.Append($"[color=#ffd700]Văn minh {info.Name}[/color] — {(home ? "quê hương" : $"thuộc địa (quê hương: {(Live(info.Home) ? NameOf(info.Home) : "đã mất")})")}\n");
                    int stage = _snapshot.TechStage[i];
                    string stageName = stage < GodUi.TechStageVi.Length ? GodUi.TechStageVi[stage] : $"Cấp {stage}";
                    double people = _snapshot.PeopleCount[i];
                    double watts = _snapshot.PowerWatts[i];
                    double kScale = _snapshot.KardashevScale[i];
                    double footprint = _snapshot.CivFootprint[i];
                    double civLifeMin = _snapshot.Consts.TryGetValue("CivLifeMin", out double clm) ? clm : 0.4;
                    bool isDome = _snapshot.Life[i] < civLifeMin / 5;

                    sb.Append($"[color=#aaccff]Thời đại:[/color] [b]{stageName}[/b]   [color=#aaccff]Công nghệ:[/color] {_snapshot.Tech[i]:F2}/4.0\n");
                    sb.Append($"[color=#aaccff]Dân số:[/color] {GodUi.FormatPeople(people)} ({_snapshot.Pop[i] * 100:F1}%){(isDome ? " [color=#88ccff](vòm kín)[/color]" : "")}\n");
                    sb.Append($"[color=#aaccff]Năng lượng:[/color] {GodUi.FormatWatts(watts)} (Kardashev {kScale:F2})   [color=#aaccff]Tác động:[/color] {footprint * 100:F1}%\n");
                    if (isDome && !home)
                    {
                        bool supplyOk = info.Home >= 0 && Live(info.Home) && _snapshot.Civ[info.Home] == civ && _snapshot.Pop[info.Home] > 0;
                        sb.Append(supplyOk ? "[color=#66ff66]Tiếp tế quê hương: Ổn định[/color]\n" : "[color=#ff5555]Mất nguồn tiếp tế: Thuộc địa đang lụi tàn[/color]\n");
                    }
                }
                else sb.Append($"[color=#888888]Văn minh {info.Name} từng sống ở đây.[/color]\n");
                sb.Append($"ra đời năm {info.BornYear:N0}, đang sống trên {_snapshot.WorldsOf(civ)} thế giới\n");
                sb.Append("[color=#ffd700]Niên biểu:[/color]\n");
                // oldest first; when it is long, the birth and then the latest lines
                _lines.Clear();
                for (int k = 0; k < _snapshot.Chronicle.Count; k++) if (_snapshot.Chronicle[k].Civ == civ) _lines.Add(k);
                for (int k = 0; k < _lines.Count; k++)
                {
                    if (_lines.Count > ChronicleLines && k == 1) { sb.Append("  …\n"); k = _lines.Count - ChronicleLines + 1; }
                    sb.Append("  ").Append(GodUi.CivLine(_snapshot, _snapshot.Chronicle[_lines[k]].Event)).Append('\n');
                }
            }
        }

        int g = _snapshot.Grp[i];
        if (g > 0)
        {
            int cnt = 0; double tot = 0;
            for (int j = 0; j < _snapshot.N; j++) if (_snapshot.Alive[j] && _snapshot.Grp[j] == g) { cnt++; tot += _snapshot.M[j]; }
            sb.Append($"thuộc {_snapshot.Groups[g]}: {cnt} vật thể, tổng {tot / World.EarthMass:G3} Trái Đất\n");
        }
        for (int e = 0; e < World.NElem; e++)
            sb.Append($"[color=#{ElemCol[e].ToHtml(false)}]■[/color] {GodUi.ElemVi[e]}   {(m > 0 ? 100 * _snapshot.Comp[i * World.NElem + e] / m : 0):F1}%\n");
        return sb.ToString();
    }

    // Pull / push away: once a frame while the button is down. Strength is counted in orbit speeds per second of
    // holding, at the pointer; the ring is a size on screen, so the hand is as big as it looks at any zoom.
    void UseHand(double seconds)
    {
        ToWorld(_mouse, out double x, out double y);
        int p = _snapshot.PrimaryAt(x, y, 0);
        double amount = _ui.HandStrength * _snapshot.OrbitSpeed(p, x, y) * Math.Min(seconds, 0.1);
        double forceAmt = _tool == Tool.Shove ? -amount : amount;
        double r = _ui.HandRadiusPx / _zoom;
        QueueGodAction(w => GodTools.Force(w, x, y, r, forceAmt));
    }

    // velocity of object i as its primary sees it, and that primary
    int RelVelocity(int i, out double ux, out double uy)
    {
        int p = _snapshot.PrimaryAt(_snapshot.X[i], _snapshot.Y[i], _snapshot.M[i], i);
        ux = _snapshot.Vx[i] - (p >= 0 ? _snapshot.Vx[p] : 0);
        uy = _snapshot.Vy[i] - (p >= 0 ? _snapshot.Vy[p] : 0);
        return p;
    }

    // drag on screen -> velocity seen from the primary: DragPerOrbitSpeed pixels = one circular-orbit speed there
    void DragVelocity(Vector2 drag, int primary, double x, double y, out double vx, out double vy, out double share)
    {
        double v = _snapshot.OrbitSpeed(primary, x, y);
        vx = drag.X / DragPerOrbitSpeed * v;
        vy = drag.Y / (DragPerOrbitSpeed * _tilt) * v;
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
        int n = GodTools.Predict(_snapshot, primary, mass, rx, ry, rvx, rvy, _path, out bool hits);
        if (n < 2) return;
        for (int k = 0; k < n; k++)
            _pathPts[k] = ToScreen(_snapshot.X[primary] + _path[k * 2], _snapshot.Y[primary] + _path[k * 2 + 1]);
        DrawPolyline(_pathPts.AsSpan(0, n).ToArray(), hits ? new Color(1f, 0.35f, 0.3f, 0.9f) : new Color(1f, 0.9f, 0.2f, 0.6f), 1.5f);
    }

    public override void _Draw()
    {
        if (_w == null) return;
        Font font = ThemeDB.FallbackFont;

        if (_grid) DrawGrid(font);
        if (_zones) DrawZones();

        // Orbit lines
        for (int i = 0; i < _snapshot.N; i++)
        {
            int par = _snapshot.Par[i];
            if (!_snapshot.Alive[i] || par < 0 || par >= _snapshot.N || !_snapshot.Alive[par]) continue;
            double dx = _snapshot.X[i] - _snapshot.X[par], dy = _snapshot.Y[i] - _snapshot.Y[par];
            float rad = (float)(Math.Sqrt(dx * dx + dy * dy) * _zoom);
            if (rad < 6) continue;
            DrawSetTransform(Screen(par), 0, new Vector2(1, _tilt));
            DrawArc(Vector2.Zero, rad, 0, MathF.Tau, 128, new Color(1, 1, 1, 0.10f), 1);
        }
        if (Live(_sel) && _snapshot.Attracts[_sel] && _snapshot.Kind[_sel] != Kind.Star)
        {
            DrawSetTransform(Screen(_sel), 0, new Vector2(1, _tilt));
            DrawArc(Vector2.Zero, (float)(_snapshot.Hill[_sel] * _zoom), 0, MathF.Tau, 96, new Color(0.5f, 1f, 0.6f, 0.35f), 1);
        }
        DrawSetTransform(Vector2.Zero);

        CountRings();

        // a moon's labels are dropped while it sits on top of its planet on screen
        bool far(int i, Vector2 p) => !(_snapshot.Par[i] >= 0 && _snapshot.Par[i] < _snapshot.N && _snapshot.Alive[_snapshot.Par[i]]) || Screen(_snapshot.Par[i]).DistanceTo(p) > 30;

        for (int i = 0; i < _snapshot.N; i++)
        {
            if (!_snapshot.Alive[i] || !_snapshot.Attracts[i]) continue;
            Vector2 p = Screen(i);
            float r = Px(i);
            Kind kind = _snapshot.Kind[i];
            // PLACEHOLDER: a ring too small on screen to show its own rocks is drawn as one band
            if (_ringCount[i] >= RingMin && Math.Sqrt(_ringFar[i]) * _zoom < RingBandPx)
            {
                DrawSetTransform(p, 0, new Vector2(1, _tilt));
                DrawArc(Vector2.Zero, r * 2.1f, 0, MathF.Tau, 48, new Color(0.9f, 0.82f, 0.6f, 0.7f), MathF.Max(1.5f, r * 0.45f));
                DrawSetTransform(Vector2.Zero);
            }
            DrawBody(i, p, r);

            // Visual marks on bodies that carry life or civilisation (SPEC 7 Round 2)
            if (_snapshot.IsWorld(i))
            {
                if (_snapshot.Pop[i] > 0)
                {
                    DrawArc(p, r + 4, 0, MathF.Tau, 28, new Color(1f, 0.85f, 0.2f, 0.95f), 1.5f);
                    int civ = _snapshot.Civ[i];
                    string tag = civ < 0 ? "văn minh" : _snapshot.Civs[civ].Home == i ? _snapshot.Civs[civ].Name : $"thuộc địa {_snapshot.Civs[civ].Name}";
                    if (kind != Kind.Moon || far(i, p))
                        DrawString(font, p + new Vector2(r + 6, -10), tag, HorizontalAlignment.Left, -1, 11, new Color(1f, 0.85f, 0.2f, 0.95f));
                }
                else if (_snapshot.Life[i] > 0)
                {
                    DrawArc(p, r + 3, 0, MathF.Tau, 24, new Color(0.2f, 1f, 0.45f, 0.9f), 1.2f);
                    DrawString(font, p + new Vector2(r + 6, -10), "sự sống", HorizontalAlignment.Left, -1, 11, new Color(0.2f, 1f, 0.45f, 0.9f));
                }
            }

            if (kind != Kind.Star && (kind != Kind.Moon || far(i, p)))
                DrawString(font, p + new Vector2(r + 6, 4), NameOf(i), HorizontalAlignment.Left, -1, 13, new Color(1, 1, 1, 0.75f));
        }

        // Ships: a small diamond, and for the selected one a line to where it is going
        for (int i = 0; i < _snapshot.N; i++)
        {
            if (!_snapshot.Alive[i] || !_snapshot.IsShip[i]) continue;
            Vector2 p = Screen(i);
            DrawColoredPolygon(new[] { p + new Vector2(0, -5), p + new Vector2(4, 0), p + new Vector2(0, 5), p + new Vector2(-4, 0) }, ShipCol);
            if (i == _sel && Live(_snapshot.ShipTo[i])) DrawDashedLine(p, Screen(_snapshot.ShipTo[i]), new Color(ShipCol.R, ShipCol.G, ShipCol.B, 0.5f), 1, 6);
        }

        // Selection ring
        if (Live(_sel)) DrawArc(Screen(_sel), Px(_sel) + 5, 0, MathF.Tau, 32, Colors.White, 1.5f);

        // Push: arrow from the object, and the orbit it would get
        if (_drag == Drag.Push && Live(_sel))
        {
            int i = _sel;
            int p = _snapshot.PrimaryAt(_snapshot.X[i], _snapshot.Y[i], _snapshot.M[i], i);
            DragVelocity(_mouse - _downPos, p, _snapshot.X[i], _snapshot.Y[i], out double dvx, out double dvy, out double share);
            DrawArrow(Screen(i), Screen(i) + (_mouse - _downPos));
            if (p >= 0) DrawPath(p, _snapshot.M[i], _snapshot.X[i] - _snapshot.X[p], _snapshot.Y[i] - _snapshot.Y[p], _snapshot.Vx[i] - _snapshot.Vx[p] + dvx, _snapshot.Vy[i] - _snapshot.Vy[p] + dvy);
            DrawString(font, _mouse + new Vector2(14, 18), $"đẩy {share:F2}× tốc độ quỹ đạo", HorizontalAlignment.Left, -1, 13, AimCol);
        }

        // Vector tool: the selected object's velocity as an arrow (same scale as a drag); while aiming, the new one
        if (_tool == Tool.Vector)
        {
            int i = _drag == Drag.Aim ? _grab : _sel;
            if (Live(i))
            {
                int p = RelVelocity(i, out double ux, out double uy);
                double v = _snapshot.OrbitSpeed(p, _snapshot.X[i], _snapshot.Y[i]);
                Vector2 now = new((float)(ux / v * DragPerOrbitSpeed), (float)(uy / v * DragPerOrbitSpeed * _tilt));
                if (_drag == Drag.Aim)
                {
                    DrawLine(Screen(i), Screen(i) + now, new Color(1, 1, 1, 0.35f), 1.5f);
                    DragVelocity(_mouse - Screen(i), p, _snapshot.X[i], _snapshot.Y[i], out double nvx, out double nvy, out double share);
                    DrawArrow(Screen(i), _mouse);
                    if (p >= 0) DrawPath(p, _snapshot.M[i], _snapshot.X[i] - _snapshot.X[p], _snapshot.Y[i] - _snapshot.Y[p], nvx, nvy);
                    DrawString(font, _mouse + new Vector2(14, 18), $"vận tốc mới {share:F2}× tốc độ quỹ đạo{(p >= 0 ? $" quanh {NameOf(p)}" : "")}", HorizontalAlignment.Left, -1, 13, AimCol);
                }
                else DrawArrow(Screen(i), Screen(i) + now);
            }
        }

        // Move tool: the object's shadow under the pointer and the orbit it would land on
        if (_drag == Drag.Carry && Live(_grab))
        {
            ToWorld(_mouse, out double x, out double y);
            int p = _snapshot.PrimaryAt(x, y, _snapshot.M[_grab], _grab);
            DrawLine(Screen(_grab), _mouse, new Color(1, 1, 1, 0.3f), 1);
            string say = "giữ vận tốc cũ";
            if (_ui.MoveCircular)
            {
                say = p >= 0 ? $"quỹ đạo tròn quanh {NameOf(p)}" : "đứng yên";
                if (p >= 0)
                {
                    double dx = x - _snapshot.X[p], dy = y - _snapshot.Y[p];
                    DrawSetTransform(Screen(p), 0, new Vector2(1, _tilt));
                    DrawArc(Vector2.Zero, (float)(Math.Sqrt(dx * dx + dy * dy) * _zoom), 0, MathF.Tau, 128, new Color(1f, 0.9f, 0.2f, 0.45f), 1.5f);
                    DrawSetTransform(Vector2.Zero);
                }
            }
            Color shade = MixCol(_grab); shade.A = 0.7f;
            DrawCircle(_mouse, Px(_grab), shade);
            DrawArc(_mouse, Px(_grab) + 3, 0, MathF.Tau, 24, new Color(1, 1, 1, 0.6f), 1);
            DrawString(font, _mouse + new Vector2(Px(_grab) + 12, 18), $"{NameOf(_grab)} — {say}", HorizontalAlignment.Left, -1, 13, AimCol);
        }

        // Pull / push away: the ring is the reach of the hand
        if (_tool == Tool.Pull || _tool == Tool.Shove)
        {
            bool on = _drag == Drag.Hand;
            Color ring = _tool == Tool.Pull ? new Color(0.4f, 0.8f, 1f, on ? 0.9f : 0.45f) : new Color(1f, 0.5f, 0.3f, on ? 0.9f : 0.45f);
            DrawArc(_mouse, _ui.HandRadiusPx, 0, MathF.Tau, 64, ring, on ? 2.5f : 1.5f);
            if (on) DrawCircle(_mouse, _ui.HandRadiusPx, new Color(ring.R, ring.G, ring.B, 0.08f));
            if (on && _paused) DrawString(font, _mouse + new Vector2(_ui.HandRadiusPx + 8, 4), "đang tạm dừng: vận tốc đã đổi, chạy tiếp mới thấy", HorizontalAlignment.Left, -1, 13, AimCol);
        }

        // Create: the pointer carries the new object and shows what it will do
        if (_creating && _ui != null)
        {
            bool placing = _drag == Drag.Place;
            double x, y;
            if (placing) { x = _placeX; y = _placeY; } else ToWorld(_mouse, out x, out y);
            double mass = _ui.GetCreateMass(); double[] mix = _ui.GetCreateMix();
            int p = _snapshot.PrimaryAt(x, y, mass);
            var (kind, radius) = GodTools.Preview(_snapshot, mass, mix, p);
            Vector2 at = ToScreen(x, y);
            Vector2 drag = placing ? _mouse - at : Vector2.Zero;
            string say;
            if (p < 0) say = drag.Length() < LaunchStart ? "đứng yên (không có vật nào nặng hơn ở đây)" : "phóng đi";
            else if (drag.Length() < LaunchStart && !_ui.CreateCircular) say = "đứng yên";
            else if (drag.Length() < LaunchStart)
            {
                double dx = x - _snapshot.X[p], dy = y - _snapshot.Y[p];
                DrawSetTransform(Screen(p), 0, new Vector2(1, _tilt));
                DrawArc(Vector2.Zero, (float)(Math.Sqrt(dx * dx + dy * dy) * _zoom), 0, MathF.Tau, 128, new Color(1f, 0.9f, 0.2f, 0.45f), 1.5f);
                DrawSetTransform(Vector2.Zero);
                say = $"quỹ đạo tròn quanh {NameOf(p)}";
            }
            else
            {
                DragVelocity(drag, p, x, y, out double rvx, out double rvy, out double share);
                DrawPath(p, mass, x - _snapshot.X[p], y - _snapshot.Y[p], rvx, rvy);
                say = $"phóng {share:F2}× tốc độ quỹ đạo quanh {NameOf(p)}";
            }
            if (drag.Length() >= LaunchStart) DrawArrow(at, _mouse);
            Color ghost = new(0, 0, 0);
            double sum = 0; for (int e = 0; e < World.NElem; e++) sum += mix[e];
            for (int e = 0; e < World.NElem; e++) ghost += ElemCol[e] * (float)(sum > 0 ? mix[e] / sum : e == _snapshot.ElemRock ? 1 : 0);
            ghost.A = 0.75f;
            float gr = MathF.Max(KindPx(kind), (float)radius * _zoom);
            DrawCircle(at, gr, ghost);
            DrawArc(at, gr + 3, 0, MathF.Tau, 24, new Color(1, 1, 1, 0.6f), 1);
            DrawString(font, (placing ? _mouse : at) + new Vector2(gr + 12, 18), $"{_ui.CreateRemnantName ?? GodUi.KindVi[(int)kind]} — {say}", HorizontalAlignment.Left, -1, 13, AimCol);
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
        int p = _snapshot.PrimaryAt(x, y, mass);
        Vector2 drag = _mouse - ToScreen(x, y);
        Kind kind = GodTools.Preview(_snapshot, mass, mix, p).Kind;
        double birthSuns = _ui.CreateBirthSuns;
        string name = $"{_ui.CreateRemnantName ?? GodUi.KindVi[(int)kind]} mới {++_createdCount}";
        bool placingDrag = drag.Length() >= LaunchStart;
        double rvx = 0, rvy = 0, share = 0;
        if (placingDrag)
            DragVelocity(drag, p, x, y, out rvx, out rvy, out share);
        bool orbit = p >= 0 && _ui.CreateCircular;
        string primName = p >= 0 ? NameOf(p) : "";

        QueueGodAction(w =>
        {
            int slot; string did;
            if (!placingDrag)
            {
                slot = GodTools.Create(w, x, y, mass, mix, parent: orbit ? p : -1, name: name);
                did = orbit ? $"quay quanh {primName}" : "đứng yên";
            }
            else
            {
                slot = GodTools.Launch(w, x, y, (p >= 0 ? w.Vx[p] : 0) + rvx, (p >= 0 ? w.Vy[p] : 0) + rvy, mass, mix, name);
                did = $"phóng {share:F2}× tốc độ quỹ đạo";
            }
            bool stillBurning = slot >= 0 && birthSuns > 0 && GodTools.MakeRemnant(w, slot, birthSuns) < 0;
            if (stillBurning)
            {
                Select(slot);
                Toast($"Đã tạo {name} nhưng nó vẫn là sao thường: khối lượng không hợp với xác của sao {birthSuns:0.#} Mặt Trời");
            }
            else if (slot >= 0)
            {
                Select(slot);
                Toast($"Đã tạo {name}: {did}");
            }
            else
            {
                Toast("Không tạo được: hết chỗ hoặc thông số sai");
            }
        });
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true } k)
        {
            if (k.Keycode == Key.Bracketright)
            {
                QueueGodAction(w =>
                {
                    GodTools.SetConst(w, "G", w.C.G * 1.05);
                    Toast($"G = {w.C.G:F2}");
                });
                _ui?.RefreshConsts();
            }
            if (k.Keycode == Key.Bracketleft)
            {
                QueueGodAction(w =>
                {
                    GodTools.SetConst(w, "G", w.C.G / 1.05);
                    Toast($"G = {w.C.G:F2}");
                });
                _ui?.RefreshConsts();
            }
            if (k.Echo) return;
            if (k.Keycode == Key.T) _tilt = _tilt < 1 ? 1f : 0.5f;
            if (k.Keycode == Key.Space) TogglePause();
            if (k.Keycode == Key.C) SetCreating(!_creating);
            if (k.Keycode == Key.V) SetTool(_tool == Tool.Vector ? Tool.Select : Tool.Vector);
            if (k.Keycode == Key.M) SetTool(_tool == Tool.Move ? Tool.Select : Tool.Move);
            if (k.Keycode == Key.A) SetTool(_tool == Tool.Pull ? Tool.Select : Tool.Pull);
            if (k.Keycode == Key.R) SetTool(_tool == Tool.Shove ? Tool.Select : Tool.Shove);
            if (k.Keycode == Key.F) FollowSelected();
            if (k.Keycode == Key.H) Home();
            if (k.Keycode == Key.G) _grid = !_grid;
            if (k.Keycode == Key.Z) _zones = !_zones;
            if (k.Keycode == Key.Delete) RemoveSelected();
            if (k.Keycode == Key.Tab) _ui?.TogglePanel();
            if (k.Keycode == Key.Escape)
            {
                if (_drag != Drag.None) _drag = Drag.None;
                else if (_tool != Tool.Select) SetTool(Tool.Select);
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
            else if (m.ButtonIndex == MouseButton.Left && (_tool == Tool.Pull || _tool == Tool.Shove)) _drag = Drag.Hand;
            else if (m.ButtonIndex == MouseButton.Left && (_tool == Tool.Vector || _tool == Tool.Move) && Pick(m.Position) is int held and >= 0)
            {
                _grab = held;
                _downSlot = held;
                _downGen = _snapshot.IsAlive(held) ? _snapshot.Gen[held] : -1;
                Select(held);
                _drag = _tool == Tool.Vector ? Drag.Aim : Drag.Carry;
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
                _downSlot = _sel;
                _downGen = Live(_sel) ? _snapshot.Gen[_sel] : -1;
            }
            return;
        }

        if (m.ButtonIndex != _dragBtn || _drag == Drag.None) return;
        Drag was = _drag; _drag = Drag.None;
        if (was == Drag.Place) PlaceObject();
        else if (was == Drag.Hand) _ui?.RefreshSelection();
        else if (was == Drag.Aim && Live(_grab))
        {
            int i = _grab;
            int gen = _downGen;
            if ((_mouse - _downPos).Length() > DragStart) // a plain click only selects
            {
                int p = RelVelocity(i, out double ux, out double uy);
                DragVelocity(_mouse - Screen(i), p, _snapshot.X[i], _snapshot.Y[i], out double nvx, out double nvy, out double share);
                double dvx = nvx - ux, dvy = nvy - uy;
                string name = NameOf(i);
                QueueGodAction(w => GodTools.Push(w, i, dvx, dvy), i, gen);
                Toast($"{name}: vận tốc mới {share:F2}× tốc độ quỹ đạo");
                _ui?.RefreshSelection();
            }
        }
        else if (was == Drag.Carry && Live(_grab))
        {
            int i = _grab;
            int gen = _downGen;
            if ((_mouse - _downPos).Length() > DragStart)
            {
                ToWorld(_mouse, out double x, out double y);
                bool moveCircular = _ui.MoveCircular;
                bool moveAlone = _ui.MoveAlone;
                string name = NameOf(i);
                QueueGodAction(w =>
                {
                    int res = GodTools.Move(w, i, x, y, moveCircular, moveAlone);
                    if (res < 0) Toast("Không dời được tới đó");
                }, i, gen);
                Toast($"Đã dời {name}");
                _ui?.RefreshSelection();
            }
        }
        else if (was == Drag.Push && Live(_sel))
        {
            int i = _sel;
            int gen = _downGen;
            int p = _snapshot.PrimaryAt(_snapshot.X[i], _snapshot.Y[i], _snapshot.M[i], i);
            DragVelocity(_mouse - _downPos, p, _snapshot.X[i], _snapshot.Y[i], out double dvx, out double dvy, out double share);
            string name = NameOf(i);
            QueueGodAction(w => GodTools.Push(w, i, dvx, dvy), i, gen);
            Toast($"Đã đẩy {name}: {share:F2}× tốc độ quỹ đạo");
            _ui?.RefreshSelection();
        }
        else if (was == Drag.Maybe)
        {
            if (m.ButtonIndex == MouseButton.Left) Select(Pick(m.Position));
            else if (m.ButtonIndex == MouseButton.Right) { if (_tool != Tool.Select) SetTool(Tool.Select); else Select(-1); }
        }
    }

    // Headless check of the mouse and keyboard paths (`-- --uitest`): feeds the same events a hand would, on the
    // real panels, and reads back what the world got. No window, nobody has to click through it.
    async void RunUiTest()
    {
        _snapshot.CopyFrom(_w);
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

        // vector tool: the arrow is the new velocity, whatever it was before
        Home(); _zoom = 200; _cx = _w.X[earth]; _cy = _w.Y[earth];
        Key(Godot.Key.V);
        DragTo(Screen(shot), Screen(shot) + new Vector2(0, 120), MouseButton.Left);
        int sp = RelVelocity(shot, out double sux, out double suy);
        Say(_tool == Tool.Vector && _sel == shot && sp == earth && Math.Abs(Share(shot, earth) - 1) < 0.01 && Math.Abs(sux) < 1e-9 && suy > 0, $"vector drag 120 px down: {Share(shot, earth):F3}x orbit speed, straight down");

        // move tool: dropped next to Earth on the other side, on a circular orbit
        Key(Godot.Key.M);
        Vector2 drop = Screen(earth) + new Vector2(0.15f * _zoom, 0.1f * _zoom);
        ToWorld(drop, out double dropX, out double dropY);
        DragTo(Screen(shot), drop, MouseButton.Left);
        Say(_tool == Tool.Move && Math.Abs(_w.X[shot] - dropX) < 1e-9 && Math.Abs(_w.Y[shot] - dropY) < 1e-9 && Math.Abs(Share(shot, earth) - 1) < 0.01, $"move: dropped where the pointer was, {Share(shot, earth):F3}x orbit speed around Earth");
        _ui.SetMoveCircular(false);
        double keepVx = _w.Vx[shot];
        DragTo(Screen(shot), Screen(shot) + new Vector2(30, 0), MouseButton.Left);
        Say(_w.Vx[shot] == keepVx, "move with 'keep velocity': velocity untouched");

        // pull and push away: a rock inside the ring gains speed toward / away from the pointer, one outside does not
        Home();
        int rockIn = -1, rockOut = -1; Vector2 hand = Vector2.Zero;
        for (int i = 0; i < _w.N && rockIn < 0; i++) if (_w.Alive[i] && !_w.Attracts(i)) { rockIn = i; hand = Screen(i) + new Vector2(20, 0); }
        for (int i = 0; i < _w.N; i++) if (_w.Alive[i] && !_w.Attracts(i) && Screen(i).DistanceTo(hand) > _ui.HandRadiusPx * 2) { rockOut = i; break; }
        if (rockIn >= 0 && rockOut >= 0)
        {
            double ivx = _w.Vx[rockIn], ovx = _w.Vx[rockOut];
            Key(Godot.Key.A);
            Button(hand, MouseButton.Left, true); UseHand(0.1); UseHand(0.1); Button(hand, MouseButton.Left, false);
            double pulled = _w.Vx[rockIn] - ivx;
            Key(Godot.Key.R);
            Button(hand, MouseButton.Left, true); UseHand(0.1); UseHand(0.1); UseHand(0.1); UseHand(0.1); Button(hand, MouseButton.Left, false);
            Say(pulled > 0 && _w.Vx[rockIn] < ivx && _w.Vx[rockOut] == ovx && _drag == Drag.None, $"hand: pull gave the rock {pulled:E2} toward the pointer, push away took {ivx + pulled - _w.Vx[rockIn]:E2}; rock outside the ring untouched");
        }
        else Say(false, "hand: no rocks to try on (run with --rocks)");
        Key(Godot.Key.Escape);
        Say(_tool == Tool.Select, "Esc drops the tool");

        // dead stars: each preset drops the remnant itself, no burning star and no explosion on the way
        Home();
        for (int k = 0; k < 3; k++)
        {
            Key(Godot.Key.C);
            _ui.ApplyPreset(5 + k);
            int events = _w.Events.Count, n0 = _w.Live;
            Click(new Vector2(60, 60));
            int dead = _sel;
            StarPhase want = StarPhase.WhiteDwarf + k;
            Say(_w.Live == n0 + 1 && Live(dead) && _w.StarPhaseOf(dead) == want && _w.Events.Count == events && PanelText().Contains(GodUi.StarPhaseVi[(int)want]),
                $"create preset {5 + k}: {(Live(dead) ? _w.StarPhaseOf(dead).ToString() : "nothing")}, {(Live(dead) ? _w.M[dead] / _w.C.StarSolarMass : 0):F2} Suns, panel names it");
            Key(Godot.Key.Escape);
            if (Live(dead)) GodTools.Remove(_w, dead);
        }
        _ui.ApplyPreset(2);

        // object list: clicking Neptune jumps view, selects it; deleting removes row; slot reuse guarded
        _ui.ShowCelestialTab();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var rowNep = _ui.FindObjectButton("Sao Hải Vương");
        Say(rowNep != null, "object list: Sao Hải Vương present in list");
        if (rowNep != null)
        {
            // negative check: click empty space beside list leaves selection unchanged
            int selBefore = _sel;
            Vector2 emptyAt = new Vector2(rowNep.GetGlobalRect().Position.X - 4, rowNep.GetGlobalRect().GetCenter().Y);
            GetViewport().PushInput(new InputEventMouseMotion { Position = emptyAt, GlobalPosition = emptyAt });
            GetViewport().PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = emptyAt, GlobalPosition = emptyAt });
            GetViewport().PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = emptyAt, GlobalPosition = emptyAt });
            Say(_sel == selBefore, "object list: click empty space beside list leaves selection unchanged");

            // click row: real mouse events via PushInput only (motion + down + up)
            Vector2 rowAt = rowNep.GetGlobalRect().GetCenter();
            GetViewport().PushInput(new InputEventMouseMotion { Position = rowAt, GlobalPosition = rowAt });
            GetViewport().PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = rowAt, GlobalPosition = rowAt });
            GetViewport().PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = rowAt, GlobalPosition = rowAt });

            bool nepJumping = _sel == 8 && Math.Abs(_cx - _w.X[8]) < 1e-6 && Math.Abs(_cy - _w.Y[8]) < 1e-6;
            Say(nepJumping, $"object list click: camera centered at Sao Hải Vương ({_cx:F2}, {_cy:F2}) and selected");

            // delete Neptune -> row disappears
            int liveBeforeNepDel = _w.Live;
            Key(Godot.Key.Delete);
            _ui.RefreshObjectListIfNeeded();
            bool nepDeleted = _w.Live == liveBeforeNepDel - 1 && !_w.Alive[8] && _ui.FindObjectButton("Sao Hải Vương") == null;
            Say(nepDeleted, "object list: Delete removes Sao Hải Vương and row disappears");

            // slot reuse: create a new object taking slot 8 with Gen > 0, stale handler with old Gen must not jump
            _ui.ApplyPreset(0);
            int newSlot = GodTools.Create(_w, 555.0, 777.0, 1e-4 * World.EarthMass, _ui.GetCreateMix(), name: "Thiên thạch test");
            bool reusedSlot8 = newSlot == 8 && _w.Gen[8] > 0;
            double preCx = _cx, preCy = _cy; int preSel = _sel;
            _ui.OnObjectRowClicked(8, gen: 0); // old gen = 0
            bool guardOk = reusedSlot8 && _cx == preCx && _cy == preCy && _sel == preSel;
            Say(guardOk, $"object list: reused slot {newSlot} with stale gen did not jump or select");
            if (newSlot >= 0 && Live(newSlot)) { GodTools.Remove(_w, newSlot); _snapshot.CopyFrom(_w); }
        }
        _ui.ApplyPreset(2);

        // civilisation: after the jump Earth carries a named people with a chronicle; in stepping its ships are
        // objects on screen that a click takes before the planet next to them
        double lifeGrow = Math.Log((1.0 / _w.C.LifeSeed - 1.0) / (1.0 / _w.C.CivLifeMin - 1.0)) / _w.C.LifeGrowth;
        double civStart = _w.C.LifeSparkYears + lifeGrow + _w.C.CivRiseYears;
        double eraToSpace = _w.Stages.TakeWhile(s => !s.CanLaunchShips).Sum(s => s.EarthYears);
        double jumpToSpace = civStart + eraToSpace + 1.5e4;
        GodTools.FastForward(_w, jumpToSpace);
        _snapshot.CopyFrom(_w);
        Home(); Click(Screen(earth));
        string civPanel = PanelText();
        int civ = _w.Civ[earth];
        Say(_sel == earth && civ >= 0 && civPanel.Contains($"Văn minh {_w.Civs[civ].Name}") && civPanel.Contains("Niên biểu") && civPanel.Contains("trỗi dậy"),
            $"Earth panel after {jumpToSpace:G3} years: people {(civ >= 0 ? _w.Civs[civ].Name : "none")}, {_w.WorldsOf(civ)} worlds, chronicle shown");
        int ship = -1;
        for (int k = 0; k < 6000 && ship < 0; k++)
        {
            _w.Advance(0.5);
            _snapshot.CopyFrom(_w);
            for (int i = 0; i < _w.N; i++) if (_w.Alive[i] && _w.IsShip(i) && _w.ShipTo[i] != 9 && Screen(i).DistanceTo(Screen(earth)) > 12) ship = i;
        }
        if (ship >= 0)
        {
            Click(Screen(ship) + new Vector2(3, 0));
            string shipPanel = PanelText();
            int n0 = _w.Live;
            Key(Godot.Key.Delete);
            Say(shipPanel.Contains("Tàu của văn minh") && shipPanel.Contains("đang tới") && _w.Live == n0 - 1, $"ship: click took it ({shipPanel.Split((char)10)[0]}), Delete removed it");
        }
        else Say(false, "ship: none seen in 6000 steps");

        // everything above went through the journal
        var fresh = World.SolSystem(_rocks, 1234); int next = 0;
        while (fresh.Step < _w.Step) { fresh.Replay(_w.Journal, ref next); fresh.Advance(0.5); }
        fresh.Replay(_w.Journal, ref next);
        Say(fresh.Hash() == _w.Hash(), $"replay of {_w.Journal.Count} commands: {fresh.Hash():X16} vs {_w.Hash():X16}");

        // resize: panel must stay inside viewport at multiple sizes
        var testSizes = new[] { new Vector2I(900, 600), new Vector2I(1280, 720), new Vector2I(1920, 1080) };
        foreach (var sz in testSizes)
        {
            GetTree().Root.Size = sz;
            // Allow the layout callback to fire
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Rect2 vp = new(Vector2.Zero, sz);
            Rect2 side = _ui.GetSideRect();
            bool inside = side.Position.X >= -1 && side.Position.Y >= -1 && side.End.X <= sz.X + 1 && side.End.Y <= sz.Y + 1;
            bool minWidth = side.Size.X >= 250; // SideMin - some margin
            Say(inside && minWidth, $"resize {sz.X}x{sz.Y}: panel rect ({side.Position.X:F0},{side.Position.Y:F0})-({side.End.X:F0},{side.End.Y:F0}), inside={inside}, width={side.Size.X:F0} >= 250");
        }
        GetTree().Root.Size = new Vector2I(1280, 800); // restore

        // event text audit: every event emitted by core must have VN text (no raw ids)
        {
            var testCases = new (string RuleId, string Change)[]
            {
                ("contact", "merge"),
                ("temperature", "band.0.1"),
                ("water", "water.1.2"),
                ("life", "life.start"),
                ("life", "life.end"),
                ("life", "life.stage.0.1"),
                ("impact", "impact"),
                ("comets", "comet.spent"),
                ("roche", "roche.ring"),
                ("roche", "roche.stream"),
                ("stars", "star.giant"),
                ("stars", "star.nova"),
                ("stars", "star.hypernova"),
                ("stars", "star.remnant.white"),
                ("stars", "star.remnant.neutron"),
                ("stars", "star.remnant.black"),
                ("stars", "star.kilonova.nsns"),
                ("stars", "star.kilonova.nsbh"),
                ("stars", "grb.long"),
                ("stars", "grb.short"),
                ("civ", "civ.start"),
                ("civ", "civ.end"),
                ("civ", "civ.stage.0.1"),
                ("civ", "civ.ship.first"),
                ("civ", "civ.colony"),
                ("civ", "civ.ship.turned"),
                ("civ", "civ.ship.lost"),
            };
            var missing = new System.Collections.Generic.List<string>();
            foreach (var (rId, ch) in testCases)
            {
                var ev = new RuleEvent(1000.0, 0, rId, ch, 1.0, 2.0, 3.0);
                string text = GodUi.TranslateEvent(_w, ev);
                if (text.Contains($"[{rId}] {ch}"))
                    missing.Add($"{rId}/{ch}");
            }
            // Also audit runtime logged events
            foreach (var ev in _w.Events)
            {
                string text = GodUi.TranslateEvent(_w, ev);
                if (text.Contains($"[{ev.RuleId}] {ev.Change}"))
                    missing.Add($"{ev.RuleId}/{ev.Change}");
            }
            var unique = missing.Distinct().ToArray();
            Say(unique.Length == 0, $"core event audit ({testCases.Length} catalog cases + {_w.Events.Count} runtime): {(unique.Length == 0 ? "100% translated" : $"{unique.Length} missing: {string.Join(", ", unique)}")}");
        }

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
        double matterBefore = w.Comp[slotMoon * w.ElementCount + w.Elem("ice")];
        double massBefore = w.M[slotMoon];
        double deltaIce = 0.01 * World.EarthMass;
        int editRes = GodTools.AddMatter(w, slotMoon, 1, deltaIce);
        if (editRes < 0) { GD.PrintErr("FAIL: AddMatter failed"); GetTree().Quit(1); return; }
        double matterAfter = w.Comp[slotMoon * w.ElementCount + w.Elem("ice")];
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
        int liveBeforeRemove = w.Live; // the object made at rest earlier fell into the Sun during the 500-year jump (jumps collide now)
        int removeRes = GodTools.Remove(w, shot);
        if (circRes < 0 || Math.Abs(speedShare - 1) > 1e-9 || Math.Abs(radial) > 1e-9 || pathN < 2 || removeRes < 0 || w.Alive[shot] || w.Live != liveBeforeRemove - 1)
        { GD.PrintErr($"FAIL: Circularize/Predict/Remove share={speedShare} radial={radial} path={pathN} remove={removeRes}"); GetTree().Quit(1); return; }
        GD.Print($"TOOL Mouse: primary near Earth={w.Name[primNear]}, nothing heavier for a 2-sun mass; launched slot={shot} around {w.Name[shotPrim]}, path {pathN} points hits={pathHits}; circularized to {speedShare:F6}x orbit speed; removed slot={shot} live {liveBeforeRemove} -> {w.Live}");

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

    /// <summary>
    /// Headless worker test suite running strictly on the PRODUCTION worker path.
    /// Covers barrier concurrency, queue draining during block, serialized jumps,
    /// stale generation rejection, worker exception resilience, and snapshot scaling benchmark.
    /// </summary>
    public async void RunWorkerTest()
    {
        GD.Print("=== COSMOS WORKER TEST START ===");
        bool ok = true;
        void Say(bool good, string line)
        {
            ok &= good;
            GD.Print($"{(good ? "OK    " : "FAILED")} worker: {line}");
        }

        // Initialize production world and snapshot
        int rocks = 500;
        _w = World.SolSystem(rocks, 1234);
        _snapshot.CopyFrom(_w);
        _paused = false;

        async Task DrainToIdle()
        {
            _paused = true;
            while (_workerTask != null && !_workerTask.IsCompleted)
            {
                await Task.Delay(10);
            }
            if (_workerTask != null && _workerTask.IsCompleted)
            {
                _Process(0.016); // final sync point
            }
            _workerTask = null;
        }

        async Task<bool> WaitForWorkerStep(int maxWaitMs = 2000)
        {
            bool stepFinished = false;
            Action hook = () => stepFinished = true;
            WorkerStepFinished += hook;
            try
            {
                int elapsed = 0;
                while (!stepFinished && elapsed < maxWaitMs)
                {
                    _Process(0.016);
                    if (stepFinished) return true;
                    await Task.Delay(10);
                    elapsed += 10;
                }
                return stepFinished;
            }
            finally
            {
                WorkerStepFinished -= hook;
            }
        }

        // ----------------------------------------------------
        // Test 1: Worker Barrier Concurrency Test (~300ms barrier)
        // ----------------------------------------------------
        {
            await DrainToIdle();
            var tcs = new TaskCompletionSource<bool>();
            WorkerBarrier = async () => await tcs.Task;
            _paused = false;

            // Trigger worker startup
            _Process(0.016);
            bool workerStarted = _workerTask != null && !_workerTask.IsCompleted;
            Say(workerStarted, "Test 1: Worker launched and waiting at barrier");

            // Loop frames while worker is blocked behind barrier
            bool stayedBlocked = true;
            for (int f = 0; f < 6; f++)
            {
                _Process(0.016);
                if (_workerTask == null || _workerTask.IsCompleted) stayedBlocked = false;
                await Task.Delay(20);
            }
            Say(stayedBlocked, "Test 1: Worker stayed blocked behind 300ms barrier while Main thread looped");

            // Release barrier
            tcs.SetResult(true);
            WorkerBarrier = null;
            bool step1Done = await WaitForWorkerStep();
            Say(step1Done, "Test 1: Worker completed cleanly after barrier release");
        }

        // ----------------------------------------------------
        // Test 2: Concurrent God Actions Queue During Barrier
        // ----------------------------------------------------
        {
            await DrainToIdle();
            var tcs = new TaskCompletionSource<bool>();
            WorkerBarrier = async () => await tcs.Task;
            _paused = false;

            // Start worker cycle
            _Process(0.016);

            // Queue actions while worker is blocked
            double origG = _snapshot.G;
            QueueGodAction(w => GodTools.SetConst(w, "G", 2.22));
            QueueGodAction(w => GodTools.SetRule(w, "temperature", false));
            int earth = 3;
            int earthGen = _snapshot.Gen[earth];
            QueueGodAction(w => GodTools.AddMatter(w, earth, 1, 100.0), earth, earthGen);

            Say(_pendingActions.Count == 3, "Test 2: 3 God actions queued while worker is blocked");
            Say(Math.Abs(_w.C.G - 2.22) > 1e-4, "Test 2: World.C.G untouched while actions are in queue");

            // Release barrier and await processing
            tcs.SetResult(true);
            WorkerBarrier = null;
            await WaitForWorkerStep();

            Say(_pendingActions.IsEmpty, "Test 2: All actions drained by worker");
            Say(Math.Abs(_snapshot.Consts["G"] - 2.22) < 1e-4, $"Test 2: Snapshot Const G updated to 2.22 (got {_snapshot.Consts["G"]})");
            Say(_snapshot.Rules.Find(r => r.Id == "temperature").Enabled == false, "Test 2: Snapshot temperature rule disabled");
        }

        // ----------------------------------------------------
        // Test 3: Serialized Jumps & View Shift on Production Stream
        // ----------------------------------------------------
        {
            double yr0 = _snapshot.Year;

            // Queue 2 jumps
            JumpKeepingView(50.0, "+50 năm");
            JumpKeepingView(50.0, "+50 năm");
            Say(_pendingJumps.Count == 2, "Test 3: 2 jumps queued in stream");

            // Process until jumps execute
            int loop = 0;
            while ((!_pendingJumps.IsEmpty || (_workerTask != null && !_workerTask.IsCompleted)) && loop++ < 200)
            {
                _Process(0.016);
                await Task.Delay(10);
            }
            _Process(0.016); // Sync point

            double deltaYr = _snapshot.Year - yr0;
            Say(deltaYr >= 99.0, $"Test 3: Serialized jumps executed in order, +{deltaYr:F1} years (expected ~100)");
        }

        // ----------------------------------------------------
        // Test 4: Stale Generation Rejection on Recycled Slot
        // ----------------------------------------------------
        {
            int testSlot = 8;
            int oldGen = _snapshot.Gen[testSlot];

            // Action A: Recycles slot 8 -> removes it and creates a replacement, bumping Gen[8]
            QueueGodAction(w =>
            {
                GodTools.Remove(w, testSlot);
                GodTools.Create(w, 888.0, 999.0, 10.0, new double[] { 1, 0, 0, 0, 0, 0 }, parent: -1, name: "NewSlot8");
            });

            // Action B: Stale action intended for oldGen of slot 8 -> must be DROPPED
            QueueGodAction(w =>
            {
                GodTools.Push(w, testSlot, 555.0, 777.0);
            }, targetSlot: testSlot, expectedGen: oldGen);

            while (!_pendingActions.IsEmpty)
            {
                await WaitForWorkerStep();
            }
            await WaitForWorkerStep(); // sync point copied to snapshot

            // Verify: slot 8 was recycled and Gen increased, but the Push was NOT applied
            bool genBumped = _snapshot.Gen[testSlot] > oldGen;
            bool pushDropped = Math.Abs(_snapshot.Vx[testSlot] - 555.0) > 1e-4;
            Say(genBumped && pushDropped, $"Test 4: Stale action for slot 8 gen {oldGen} dropped (new gen={_snapshot.Gen[testSlot]}, vx={_snapshot.Vx[testSlot]:F2})");
        }

        // ----------------------------------------------------
        // Test 5: Worker Exception Resilience & ExitTree Clean Exit
        // ----------------------------------------------------
        {
            await DrainToIdle();
            _paused = false;
            TestActionHook = w => throw new InvalidOperationException("Injected Worker Fault Test");

            _Process(0.016);
            while (_workerTask != null && !_workerTask.IsCompleted)
            {
                await Task.Delay(10);
            }
            _Process(0.016); // Process completed worker task with exception

            Say(_paused, "Test 5: Worker exception gracefully caught, simulation paused without process crash");
            TestActionHook = null;

            // Test ExitTree cleanup
            _ExitTree();
            Say(_cts.IsCancellationRequested, "Test 5: ExitTree canceled background worker CTS cleanly");
        }

        // ----------------------------------------------------
        // Test 6: Snapshot Copy Scaling Benchmark
        // ----------------------------------------------------
        {
            var snapBench = new RenderSnapshot();

            // 5K objects
            var w5k = World.SolSystem(5000, 1234);
            var sw = new Stopwatch();
            double[] times5k = new double[11];
            for (int i = 0; i < 11; i++)
            {
                sw.Restart();
                snapBench.CopyFrom(w5k);
                sw.Stop();
                times5k[i] = sw.Elapsed.TotalMilliseconds;
            }
            Array.Sort(times5k);
            double median5k = times5k[5];
            Say(median5k < 2.0, $"Test 6: Snapshot CopyFrom 5K objects median = {median5k:F3} ms (< 2.0ms)");

            // 50K objects scaling test
            var w50k = World.SolSystem(50000, 1234);
            double[] times50k = new double[7];
            for (int i = 0; i < 7; i++)
            {
                sw.Restart();
                snapBench.CopyFrom(w50k);
                sw.Stop();
                times50k[i] = sw.Elapsed.TotalMilliseconds;
            }
            Array.Sort(times50k);
            double median50k = times50k[3];
            Say(median50k < 6.0, $"Test 6: Snapshot CopyFrom 50K objects median = {median50k:F3} ms (< 6.0ms)");
        }

        GD.Print(ok ? "PASS: workertest" : "FAIL: workertest");
        GetTree().Quit(ok ? 0 : 1);
    }
}
