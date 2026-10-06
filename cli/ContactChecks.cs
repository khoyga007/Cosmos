using System;
using System.Diagnostics;
using Cosmos.Core;

static class ContactChecks
{
    static readonly double[] Rock = { 0, 0, 1, 0, 0, 0 };
    static int Jump(World w, double time) => w.Do(new Command(CmdKind.FastForward, Amount: time / w.C.YearTime));

    public static bool Run()
    {
        bool ok = true;
        void Check(bool pass, string line) { ok &= pass; Console.WriteLine($"{(pass ? "OK    " : "FAILED")} contact: {line}"); }
        foreach (double projectileMass in new[] { .0123 * World.EarthMass, 1e-9 })
        {
            int hits = 0, misses = 0;
            for (int n = 0; n < 40; n++)
            {
                double a = n * Math.Tau / 40, speed = 40 + n * 11, ux = Math.Cos(a), uy = Math.Sin(a);
                World Shot(double offset)
                {
                    var w = new World(8, 1);
                    w.Add(0, 0, 3, -2, World.EarthMass, Rock);
                    double r = w.R[0] + w.C.RadiusScale * Math.Cbrt(projectileMass / w.C.Density[2]);
                    w.Add(-ux * speed / 32 - uy * offset * r, -uy * speed / 32 + ux * offset * r,
                        3 + ux * speed, -2 + uy * speed, projectileMass, Rock);
                    w.Advance(.5);
                    return w;
                }
                if (Shot(0).Live == 1) hits++;
                if (Shot(3).Live == 2) misses++;
            }
            Check(hits == 40, $"swept {(projectileMass > 1e-7 ? "Moon" : "rock")}: {hits}/40 centre hits at h=.5, moving target");
            Check(misses == 40, $"swept near misses: {misses}/40 survive");
        }

        foreach (double mass in new[] { World.EarthMass, 1e-9 })
        foreach (double samples in new[] { 1.0, 200.0 })
        {
            var w = new World(8, 1);
            w.Do(new Command(CmdKind.SetConst, Name: "JumpSamples", Amount: samples));
            w.Add(0, 0, 0, 0, 50, new double[] { 1, 0, 0, 0, 0, 0 });
            w.Add(45, 0, 0, .2, mass, Rock);
            Jump(w, w.C.YearTime * 10);
            Check(w.Live == 1 && Math.Abs(w.M[0] - 50 - mass) < 1e-12,
                $"jump periapsis contact: mass {mass:E1}, {samples} samples, {w.Live} live");
        }
        {
            var w = new World(8, 1); w.C.G = 0;
            w.Add(-1, 0, 1, 0, World.EarthMass, Rock); w.Add(1, 0, -1, 0, World.EarthMass, Rock);
            Jump(w, 1);
            Check(w.Live == 1, $"jump body pair endpoint: {w.Live} live");
        }
        {
            var w = new World(8, 1);
            w.Add(0, 0, 0, 0, 50, new double[] { 1, 0, 0, 0, 0, 0 });
            w.AddOrbiting(0, 45, 0, World.EarthMass, Rock);
            Jump(w, w.C.YearTime / 2);
            Check(w.Live == 2, "half-circle orbit does not collide through its chord");
        }
        {
            var w = new World(8, 1);
            w.Add(0, 0, 0, 0, 50, new double[] { 1, 0, 0, 0, 0, 0 });
            int p = w.Add(45, 0, 0, 2, World.EarthMass, Rock);
            w.Rules.Clear();
            w.Rules.Add(new Rule("capture", "", "", 1, x =>
            {
                double dx = x.X[p] - x.X[0], dy = x.Y[p] - x.Y[0], r = Math.Sqrt(dx * dx + dy * dy);
                double v = Math.Sqrt(x.C.G * (x.M[0] + x.M[p]) / r);
                x.Vx[p] = x.Vx[0] - dy / r * v; x.Vy[p] = x.Vy[0] + dx / r * v;
            }));
            Jump(w, 3 * w.C.YearTime);
            Check(w.OffRails == 1, $"OffRails union across escape then capture: {w.OffRails}");
        }
        {
            var w = new World(8, 1); w.Add(0, 0, 1, 0, 1e-9, Rock); w.Add(0, 10, 0, 1, 1e-9, Rock);
            w.Rules.Add(new Rule("observer", "", "", 1, _ => { }));
            Jump(w, 5 * w.C.YearTime);
            Check(w.OffRails == 2, $"OffRails counts each free rock once: {w.OffRails}");
        }
        {
            var w = World.SolSystem(100, 7);
            w.Do(new Command(CmdKind.Move, Target: 4, X: 45, Y: 0, Vx: 0, Vy: .2));
            Jump(w, 10 * w.C.YearTime); w.Advance(.5);
            var r = World.SolSystem(100, 7); int next = 0;
            r.Replay(w.Journal, ref next); r.Advance(.5);
            Check(w.Hash() == r.Hash(), $"collision replay {w.Hash():X16}/{r.Hash():X16}");
        }
        return ok;
    }

    // Paired runs of this same fixture in the before/after DLLs; median suppresses scheduling noise.
    public static void Bench()
    {
        var step = new double[7]; var jump = new double[7];
        for (int k = 0; k < step.Length; k++)
        {
            var w = World.SolSystem(5000, 1234);
            for (int i = 0; i < 100; i++) w.Advance(.5);
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 500; i++) w.Advance(.5);
            step[k] = sw.Elapsed.TotalMilliseconds / 500;
            sw.Restart(); Jump(w, 1e6 * w.C.YearTime); jump[k] = sw.Elapsed.TotalMilliseconds;
        }
        Array.Sort(step); Array.Sort(jump);
        Console.WriteLine($"contact bench: 5000 rocks+10 bodies step {step[3]:F6} ms; jump1e6yr {jump[3]:F3} ms (median7)");
    }
}
