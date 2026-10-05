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
// Sol: planets must stay on their orbits (radius drift after 20000 steps)
{
    var w = World.SolSystem(20_000, 1234);
    var d0 = new double[w.Nb];
    for (int i = 0; i < w.Nb; i++) d0[i] = Math.Sqrt(w.Bx[i] * w.Bx[i] + w.By[i] * w.By[i]);
    int n0 = w.Np; double worst = 0;
    for (int s = 0; s < 20000; s++)
    {
        w.Advance(H);
        for (int i = 1; i < w.Nb; i++) worst = Math.Max(worst, Math.Abs(Math.Sqrt(Math.Pow(w.Bx[i] - w.Bx[0], 2) + Math.Pow(w.By[i] - w.By[0], 2)) / d0[i] - 1));
    }
    for (int i = 1; i < w.Nb; i++) Console.WriteLine($"  {w.Bname[i],-16} d {d0[i],6:F1}  mass {w.Bm[i] / World.EarthMass,8:F3} earths");
    Console.WriteLine($"sol: worst orbit radius drift {100 * worst:F2}% over 20000 steps, {n0 - w.Np} grains absorbed");
}
ulong a = Run(), b = Run();
Console.WriteLine(a == b ? $"repeat check OK  {a:X16}" : $"repeat check FAILED  {a:X16} != {b:X16}");
return a == b ? 0 : 1;

static ulong Run() { var w = World.Solar(20_000, 8, 77); for (int i = 0; i < 500; i++) w.Advance(H); return w.Hash(); }
