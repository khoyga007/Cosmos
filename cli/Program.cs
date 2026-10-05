// Headless runner: step cost per grain count + the repeat check (two runs, same seed, same hash).
using System;
using System.Diagnostics;
using Cosmos.Core;

const double H = 0.5;
foreach (int n in new[] { 10_000, 30_000, 100_000, 300_000 })
{
    var w = World.Solar(n, 8, 1234);
    for (int i = 0; i < 20; i++) w.Advance(H); // warm up the JIT
    var sw = Stopwatch.StartNew();
    const int steps = 100;
    for (int i = 0; i < steps; i++) w.Advance(H);
    double ms = sw.Elapsed.TotalMilliseconds / steps;
    Console.WriteLine($"{n,7} grains, 9 bodies: {ms:F3} ms/step  ({1000 / ms:F0} steps/s)");
}
// matter check: nothing appears or vanishes, and each body's table adds up to its mass
{
    var w = World.Solar(100_000, 8, 1234);
    double Total() { double t = w.Np * World.GrainMass; for (int i = 0; i < w.Nb; i++) t += w.Bm[i]; return t; }
    double before = Total(); int n0 = w.Np;
    for (int i = 0; i < 4000; i++) w.Advance(H);
    bool ok = Math.Abs(Total() - before) < 1e-9;
    for (int i = 0; i < w.Nb; i++)
    {
        double sum = 0; string row = "";
        for (int e = 0; e < World.NElem; e++) { double c = w.Bcomp[i * World.NElem + e]; sum += c; row += $" {World.ElemName[e]} {100 * c / w.Bm[i]:F1}%"; }
        ok &= Math.Abs(sum - w.Bm[i]) < 1e-9 * w.Bm[i] + 1e-12;
        Console.WriteLine($"  body {i}: mass {w.Bm[i] / World.GrainMass,10:F0} grains |{row}");
    }
    Console.WriteLine($"matter check {(ok ? "OK" : "FAILED")}: {n0 - w.Np} grains absorbed in 4000 steps");
    if (!ok) return 1;
}
ulong a = Run(), b = Run();
Console.WriteLine(a == b ? $"repeat check OK  {a:X16}" : $"repeat check FAILED  {a:X16} != {b:X16}");
return a == b ? 0 : 1;

static ulong Run() { var w = World.Solar(20_000, 8, 77); for (int i = 0; i < 500; i++) w.Advance(H); return w.Hash(); }
