// Headless runner: step cost per object count, then the checks (merge, moon, orbits, constants, repeat).
using System;
using System.Diagnostics;
using Cosmos.Core;

const double H = 0.5;
if (args.Length > 0 && args[0] == "elements-probe") { ElementChecks.PrintProbe(); return 0; }
if (args.Length > 0 && args[0] == "elements-bench") { ElementChecks.Bench(); return 0; }
if (args.Length > 0 && args[0] == "elements") return ElementChecks.Run() ? 0 : 1;
if (args.Length > 1 && args[0] == "elements-paired") return ElementChecks.PairedBench(args[1]) ? 0 : 1;
if (args.Length > 0 && args[0] == "rails") return RailChecks.Run() ? 0 : 1;
if (args.Length > 0 && args[0] == "layers") return LayerChecks.Run() ? 0 : 1;
if (args.Length > 0 && args[0] == "stars") return StarChecks.Run() ? 0 : 1;
if (args.Length > 0 && args[0] == "events") return StarEventChecks.Run() ? 0 : 1;
if (args.Length > 0 && args[0] == "star-state") return StarStateChecks.Run() ? 0 : 1;
if (args.Length > 0 && args[0] == "contact") return ContactChecks.Run() ? 0 : 1;
if (args.Length > 0 && args[0] == "giant") return GiantChecks.Run() ? 0 : 1;
if (args.Length > 0 && args[0] == "contact-bench") { ContactChecks.Bench(); return 0; }
if (args.Length > 1 && args[0] == "jump-bench")
{
    // cost of one god jump of args[1] years on the stock scene, and what the Sun is afterwards
    var jw = World.SolSystem(args.Length > 2 ? int.Parse(args[2]) : 500, 1234);
    var jt = Stopwatch.StartNew();
    jw.Do(new Command(CmdKind.FastForward, Amount: double.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture)));
    Console.WriteLine($"jump {args[1]} yr: {jt.Elapsed.TotalMilliseconds:F0} ms, live {jw.Live}, Sun {jw.StarPhaseOf(0)} {jw.StarSpectralClass(0)}, events {jw.Events.Count}");
    Console.WriteLine($"  Sun R {jw.R[0]:G4} StarRadius {jw.StarRadius(0):G4} M {jw.M[0]/World.EarthMass:G5} RadiusScale {jw.C.RadiusScale}");
    if (args.Length > 3) foreach (var e in jw.Events) Console.WriteLine($"  {e.Year:G6} #{e.ObjectSlot} {jw.Name[e.ObjectSlot]} {e.RuleId} {e.Change} {e.A:G4} {e.B:G4} {e.C:G4}");
    return 0;
}
if (args.Length > 0 && args[0] == "kinds") return KindChecks.Run() ? 0 : 1;
if (args.Length > 0 && args[0] == "guards") return GuardChecks.Run() ? 0 : 1;
if (args.Length > 0 && args[0] == "civtable") return CivTableChecks.Run() ? 0 : 1;
bool allOk = true;
void Check(bool ok, string line) { allOk &= ok; Console.WriteLine($"{(ok ? "OK    " : "FAILED")} {line}"); }

foreach (int n in new[] { 2_000, 5_000, 20_000, 50_000 })
{
    var w = World.SolSystem(n, 1234);
    for (int i = 0; i < 20; i++) w.Advance(H); // warm up the JIT
    var sw = Stopwatch.StartNew();
    const int steps = 100;
    for (int i = 0; i < steps; i++) w.Advance(H);
    double ms = sw.Elapsed.TotalMilliseconds / steps;
    Console.WriteLine($"{n,7} rocks + 10 bodies: {ms:F3} ms/step  ({1000 / ms:F0} steps/s)");
}

// merge: two bodies fall onto each other; mass, momentum and matter must be kept
{
    var w = new World(8, 1);
    int a = w.Add(-1, 0, 0.02, 0, 3e-4, new double[] { 0, 0, 1, 0, 0, 0 }, "A");
    int b = w.Add(1, 0, -0.02, 0, 1e-4, new double[] { 0, 1, 0, 0, 0, 0 }, "B");
    double px = w.M[a] * w.Vx[a] + w.M[b] * w.Vx[b], py = w.M[a] * w.Vy[a] + w.M[b] * w.Vy[b];
    for (int i = 0; i < 2000 && w.Live == 2; i++) w.Advance(H);
    bool ok = w.Live == 1 && w.Alive[a] && !w.Alive[b]
        && Math.Abs(w.M[a] - 4e-4) < 1e-15 && Math.Abs(w.M[a] * w.Vx[a] - px) < 1e-12 && Math.Abs(w.M[a] * w.Vy[a] - py) < 1e-12
        && Math.Abs(w.Comp[a * World.NElem + 2] - 3e-4) < 1e-15 && Math.Abs(w.Comp[a * World.NElem + 1] - 1e-4) < 1e-15;
    Check(ok, $"merge: {w.Live} object left after {w.Step} steps, mass {w.M[a]:E3}, rock {w.Comp[a * World.NElem + 2]:E1} ice {w.Comp[a * World.NElem + 1]:E1}");
}

// Sol: planets stay on their orbits, the Moon stays with Earth
{
    var w = World.SolSystem(5_000, 1234);
    const int earth = 3, moon = 9;
    var d0 = new double[10];
    for (int i = 1; i <= 8; i++) d0[i] = Math.Sqrt(w.X[i] * w.X[i] + w.Y[i] * w.Y[i]);
    int n0 = w.Live; double worst = 0, mLo = 1e9, mHi = 0, turn = 0, prev = 0;
    for (int s = 0; s < 20000; s++)
    {
        w.Advance(H);
        double mx = w.X[moon] - w.X[earth], my = w.Y[moon] - w.Y[earth], md = Math.Sqrt(mx * mx + my * my), ang = Math.Atan2(my, mx);
        mLo = Math.Min(mLo, md); mHi = Math.Max(mHi, md);
        if (s > 0) turn += Math.IEEERemainder(ang - prev, Math.Tau);
        prev = ang;
        for (int i = 1; i <= 8; i++) worst = Math.Max(worst, Math.Abs(Math.Sqrt(Math.Pow(w.X[i] - w.X[0], 2) + Math.Pow(w.Y[i] - w.Y[0], 2)) / d0[i] - 1));
    }
    for (int i = 1; i <= 9; i++) Console.WriteLine($"  {w.Name[i],-16} {w.KindOf(i),-6} mass {w.M[i] / World.EarthMass,8:F3} earths  radius {w.R[i]:F3}  zone {w.Hill(i):F3}");
    Check(w.Alive[moon] && mHi < 0.2, $"moon: {turn / Math.Tau:F1} turns around Earth, distance {mLo:F3}..{mHi:F3} (start 0.120)");
    Check(worst < 0.05, $"sol: worst planet radius drift {100 * worst:F2}% over 20000 steps; {n0 - w.Live} of 5000 rocks merged into bodies");
}

// constants: the god doubles G, Earth's orbit must tighten
{
    var w = World.SolSystem(0, 1234);
    w.C.G = 2;
    double lo = 1e9;
    for (int s = 0; s < 2000; s++) { w.Advance(H); lo = Math.Min(lo, Math.Sqrt(Math.Pow(w.X[3] - w.X[0], 2) + Math.Pow(w.Y[3] - w.Y[0], 2))); }
    Check(lo < 40, $"constants: G 1 -> 2, Earth dips from 45.0 to {lo:F1}");
}

// commands: the god acts through World.Do; the journal alone must rebuild the same world
{
    double[] rock = { 0, 0, 1, 0, 0, 0 };
    var w = World.SolSystem(500, 9);
    bool ok = true;
    for (int s = 0; s < 600; s++)
    {
        if (s == 50) ok &= w.Do(new Command(CmdKind.CreateOrbiting, Target: 3, X: w.X[3], Y: w.Y[3] + 0.2, Amount: 1e-6, Mix: rock, Name: "m")) >= 0;
        if (s == 100) ok &= w.Do(new Command(CmdKind.Push, Target: 4, Vx: 0.05, Vy: -0.02)) >= 0;
        if (s == 150) ok &= w.Do(new Command(CmdKind.AddMatter, Target: 4, Amount: 2e-5, Index: 1)) >= 0;
        if (s == 200) ok &= w.Do(new Command(CmdKind.SetConst, Name: "G", Amount: 1.2)) == -2;
        if (s == 250) ok &= w.Do(new Command(CmdKind.SetConst, Name: "Density[2]", Amount: 2.5)) == -2;
        if (s == 300) ok &= w.Do(new Command(CmdKind.Create, X: 300, Y: 10, Vx: 0, Vy: 0.3, Amount: 3e-4, Mix: rock)) >= 0;
        if (s == 350) ok &= w.Do(new Command(CmdKind.Remove, Target: 20)) >= 0;
        if (s == 400) ok &= w.Do(new Command(CmdKind.Remove, Target: 20)) < 0 && w.Do(new Command(CmdKind.SetConst, Name: "Nope", Amount: 1)) < 0; // refused, not recorded
        w.Advance(H);
    }
    var r = World.SolSystem(500, 9); int next = 0;
    for (int s = 0; s < 600; s++) { r.Replay(w.Journal, ref next); r.Advance(H); }
    Check(ok && w.Journal.Count == 7 && next == 7 && w.Hash() == r.Hash() && r.C.G == 1.2 && r.C.Density[2] == 2.5,
        $"commands: {w.Journal.Count} recorded, replay hash {r.Hash():X16} vs live {w.Hash():X16}");
}

ulong a1 = Run(), b1 = Run();
Check(a1 == b1, $"repeat: {a1:X16} / {b1:X16}");
allOk &= RailChecks.Run();
allOk &= RuleChecks.Run();
allOk &= LayerChecks.Run();
allOk &= StarChecks.Run();
allOk &= StarEventChecks.Run();
allOk &= StarStateChecks.Run();
allOk &= ContactChecks.Run();
allOk &= GiantChecks.Run();
allOk &= ElementChecks.Run();
allOk &= KindChecks.Run();
allOk &= GuardChecks.Run();
allOk &= CivTableChecks.Run();
allOk &= Audit.Run();
return allOk ? 0 : 1;

static ulong Run() { var w = World.SolSystem(3_000, 77); for (int i = 0; i < 1000; i++) w.Advance(H); return w.Hash(); }
