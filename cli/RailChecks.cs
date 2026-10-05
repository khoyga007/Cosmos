// Checks for fast-forward on rails (core/Rails.cs). Owner: Claire. `dotnet run -c Release --project cli -- rails` runs these alone.
using System;
using System.Diagnostics;
using Cosmos.Core;

static class RailChecks
{
    const double H = 0.5;
    static bool _ok;
    static void Check(bool ok, string line) { _ok &= ok; Console.WriteLine($"{(ok ? "OK    " : "FAILED")} {line}"); }
    static double Dist(World w, int i, int j) => Math.Sqrt(Math.Pow(w.X[i] - w.X[j], 2) + Math.Pow(w.Y[i] - w.Y[j], 2));
    static double Angle(World w, int i, int j) => Math.Atan2(w.Y[i] - w.Y[j], w.X[i] - w.X[j]);
    static int Jump(World w, double years) => w.Do(new Command(CmdKind.FastForward, Amount: years));

    public static bool Run()
    {
        _ok = true;
        const int sun = 0, earth = 3, moon = 9;
        double year = new Consts().YearTime;

        // same stretch of time, stepped against jumped: what do the rails lose?
        foreach (int steps in new[] { 2_000, 20_000 })
        {
            var a = World.SolSystem(0, 1234); var b = World.SolSystem(0, 1234);
            for (int s = 0; s < steps; s++) a.Advance(H);
            Jump(b, steps * H / year);
            double worstR = 0, worstPh = 0;
            for (int i = 1; i <= 8; i++)
            {
                worstR = Math.Max(worstR, Math.Abs(Dist(b, i, sun) / Dist(a, i, sun) - 1));
                worstPh = Math.Max(worstPh, Math.Abs(Math.IEEERemainder(Angle(b, i, sun) - Angle(a, i, sun), Math.Tau)));
            }
            Check(worstR < 0.02 && b.OffRails == 0 && Math.Abs(Dist(b, moon, earth) / Dist(a, moon, earth) - 1) < 0.1,
                $"rails vs stepping, {steps * H / year:F1} years: worst planet distance {100 * worstR:F3}% apart, worst angle {worstPh:F4} rad apart; Moon-Earth {Dist(b, moon, earth):F4} vs {Dist(a, moon, earth):F4}; off rails {b.OffRails}");
        }

        // one jump of a million years: cost, and the system must come out the same shape
        foreach (int rocks in new[] { 0, 5_000, 50_000 })
        {
            var w = World.SolSystem(rocks, 1234);
            for (int s = 0; s < 200; s++) w.Advance(H);
            var d0 = new double[10];
            for (int i = 1; i <= 8; i++) d0[i] = Dist(w, i, sun);
            double m0 = Dist(w, moon, earth);
            var sw = Stopwatch.StartNew();
            double y0 = w.Year; Jump(w, 1e6);
            double ms = sw.Elapsed.TotalMilliseconds, worst = 0;
            for (int i = 1; i <= 8; i++) worst = Math.Max(worst, Math.Abs(Dist(w, i, sun) / d0[i] - 1));
            int live = w.Live, off = w.OffRails;
            // and it must still be a working system when real pull takes over again
            double lo = 1e9, hi = 0;
            for (int s = 0; s < 4000; s++) { w.Advance(H); double d = Dist(w, moon, earth); lo = Math.Min(lo, d); hi = Math.Max(hi, d); }
            double after = 0;
            for (int i = 1; i <= 8; i++) after = Math.Max(after, Math.Abs(Dist(w, i, sun) / d0[i] - 1));
            Check(Math.Abs(w.Year - y0 - 1e6 - 4000 * H / year) < 1e-3 && worst < 0.02 && after < 0.05 && hi < 0.2 && w.Alive[moon],
                $"jump 1e6 years, {rocks} rocks: {ms:F2} ms, year now {w.Year:F1}, off rails {off}; planet distance changed {100 * worst:F3}%, Moon-Earth {m0:F4} -> {lo:F4}..{hi:F4} over the next 4000 steps (planets {100 * after:F3}%), {live - w.Live} merged after");
        }

        // a jump is a command: the journal must rebuild the same world
        {
            var w = World.SolSystem(500, 9);
            for (int s = 0; s < 300; s++) { if (s == 100) Jump(w, 1234.5); if (s == 200) Jump(w, 0.37); w.Advance(H); }
            var r = World.SolSystem(500, 9); int next = 0;
            for (int s = 0; s < 300; s++) { r.Replay(w.Journal, ref next); r.Advance(H); }
            Check(w.Journal.Count == 2 && w.Hash() == r.Hash() && Jump(w, -1) < 0 && Jump(w, double.NaN) < 0 && w.Journal.Count == 2,
                $"jump replay: hash {r.Hash():X16} vs live {w.Hash():X16}; bad jumps refused");
        }

        // switching a rule is a command too: replay must land on the same temperatures
        {
            var w = World.SolSystem(0, 5);
            for (int s = 0; s < 200; s++) { if (s == 20) w.Do(new Command(CmdKind.SetRule, Name: "temperature", Amount: 0)); if (s == 150) w.Do(new Command(CmdKind.SetRule, Name: "temperature", Amount: 1)); w.Advance(H); }
            var r = World.SolSystem(0, 5); int next = 0;
            for (int s = 0; s < 200; s++) { r.Replay(w.Journal, ref next); r.Advance(H); }
            Check(w.Journal.Count == 2 && w.Hash() == r.Hash() && w.Do(new Command(CmdKind.SetRule, Name: "nope", Amount: 1)) < 0,
                $"rule switch replay: hash {r.Hash():X16} vs live {w.Hash():X16}; unknown rule refused");
        }

        // not bound = straight line, counted
        {
            var w = new World(8, 1);
            double[] rock = { 0, 0, 1, 0, 0, 0 };
            w.Add(0, 0, 0, 0, 50, new double[] { 1, 0, 0, 0, 0, 0 });
            int p = w.Do(new Command(CmdKind.CreateOrbiting, Target: 0, X: 45, Y: 0, Amount: 1.5e-4, Mix: rock));
            int q = w.Add(100, 0, 0, 5, 1e-9, rock); // far over escape speed
            Jump(w, 10 / year);
            Check(w.OffRails == 1 && Math.Abs(w.Y[q] - 50) < 1e-6 && Math.Abs(Dist(w, p, 0) - 45) < 1e-6,
                $"escaping object: off rails {w.OffRails}, moved straight to y {w.Y[q]:F3}; planet still at {Dist(w, p, 0):F6}");
        }
        return _ok;
    }
}
