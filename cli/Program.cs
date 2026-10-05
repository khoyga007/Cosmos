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
ulong a = Run(), b = Run();
Console.WriteLine(a == b ? $"repeat check OK  {a:X16}" : $"repeat check FAILED  {a:X16} != {b:X16}");
return a == b ? 0 : 1;

static ulong Run() { var w = World.Solar(20_000, 8, 77); for (int i = 0; i < 500; i++) w.Advance(H); return w.Hash(); }
