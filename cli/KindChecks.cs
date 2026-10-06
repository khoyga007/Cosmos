// Checks for body classification, snow line, comets and atmosphere retention (SPEC §8, Round 5).
// Owner: Ariel. Alone: `cli -- kinds`.
using System;
using System.Diagnostics;
using System.Linq;
using Cosmos.Core;

static class KindChecks
{
    const double H = 0.5;
    static bool _ok;
    static void Check(bool ok, string line)
    {
        _ok &= ok;
        Console.WriteLine($"{(ok ? "OK    " : "FAILED")} kinds: {line}");
    }

    public static bool Run()
    {
        _ok = true;

        // 1. Sol textbook classification
        {
            var sol = World.SolSystem(0, 1234);
            const int sun = 0, merc = 1, venus = 2, earth = 3, mars = 4, jup = 5, sat = 6, uran = 7, nept = 8, moon = 9;

            bool matchSun = sol.ClassOf(sun) == BodyClass.Star;
            bool matchMerc = sol.ClassOf(merc) == BodyClass.Rocky;
            bool matchVenus = sol.ClassOf(venus) == BodyClass.Rocky;
            bool matchEarth = sol.ClassOf(earth) == BodyClass.Ocean || sol.ClassOf(earth) == BodyClass.Rocky;
            bool matchMars = sol.ClassOf(mars) == BodyClass.Rocky;
            bool matchJup = sol.ClassOf(jup) == BodyClass.GasGiant;
            bool matchSat = sol.ClassOf(sat) == BodyClass.GasGiant;
            bool matchUran = sol.ClassOf(uran) == BodyClass.IceGiant;
            bool matchNept = sol.ClassOf(nept) == BodyClass.IceGiant;
            bool matchMoon = sol.ClassOf(moon) == BodyClass.Rocky;

            bool allMatched = matchSun && matchMerc && matchVenus && matchEarth && matchMars
                && matchJup && matchSat && matchUran && matchNept && matchMoon;

            Check(allMatched, $"textbook Sol: Sun={sol.ClassOf(sun)}, Mercury={sol.ClassOf(merc)}, Venus={sol.ClassOf(venus)}, Earth={sol.ClassOf(earth)}, Mars={sol.ClassOf(mars)}, Jupiter={sol.ClassOf(jup)}, Saturn={sol.ClassOf(sat)}, Uranus={sol.ClassOf(uran)}, Neptune={sol.ClassOf(nept)}, Moon={sol.ClassOf(moon)}");
        }

        // 2. Snow line of Sol is between Mars and Jupiter
        {
            var sol = World.SolSystem(0, 1234);
            double snowLine = sol.SnowLine(0);
            double dMars = Math.Sqrt(Math.Pow(sol.X[4] - sol.X[0], 2) + Math.Pow(sol.Y[4] - sol.Y[0], 2));
            double dJup = Math.Sqrt(Math.Pow(sol.X[5] - sol.X[0], 2) + Math.Pow(sol.Y[5] - sol.Y[0], 2));
            bool between = snowLine > dMars && snowLine < dJup;
            Check(between, $"Sol snow line {snowLine:F1} is between Mars {dMars:F1} and Jupiter {dJup:F1}");
        }

        // 3. Earth moved to 0.2 AU reads lava
        {
            var w = World.SolSystem(0, 1234);
            const int earth = 3;
            double d02 = World.SolDist(0.2); // ~16.56
            w.Do(new Command(CmdKind.Move, Target: earth, X: d02, Y: 0, Vx: 0, Vy: 1.74));
            for (int s = 0; s < 10; s++) w.Advance(H);
            bool isLava = w.ClassOf(earth) == BodyClass.Lava;
            Check(isLava && w.Temp[earth] >= w.C.LavaTemp,
                $"Earth moved to 0.2 AU (dist {d02:F1}, Temp {w.Temp[earth]:F1} K >= LavaTemp {w.C.LavaTemp}) reads {w.ClassOf(earth)}");
        }

        // 4. Atmosphere keeping: HoldsGas
        {
            var sol = World.SolSystem(0, 1234);
            const int earth = 3, jup = 5, moon = 9;
            bool jupHolds = sol.HoldsGas(jup);
            bool earthHolds = sol.HoldsGas(earth);
            bool moonHolds = sol.HoldsGas(moon);
            Check(jupHolds && earthHolds && !moonHolds,
                $"HoldsGas: Jupiter={jupHolds}, Earth={earthHolds}, Moon={moonHolds}");
        }

        // 5. Kuiper rock moved to 1 AU: becomes comet with tail > 0, loses ice, 1 cut == 200 cuts
        {
            var w1 = World.SolSystem(100, 1234);
            var w2 = World.SolSystem(100, 1234);

            int cometSlot = -1;
            for (int i = 10; i < w1.N; i++)
            {
                if (w1.Alive[i] && w1.Share(i, 1) > 0.5) { cometSlot = i; break; }
            }
            Debug.Assert(cometSlot >= 0, "Kuiper rock not found");

            double d1Au = World.SolDist(1.0);
            double vCirc = Math.Sqrt(w1.C.G * w1.M[0] / d1Au);

            w1.Do(new Command(CmdKind.Move, Target: cometSlot, X: d1Au, Y: 0, Vx: 0, Vy: vCirc));
            w2.Do(new Command(CmdKind.Move, Target: cometSlot, X: d1Au, Y: 0, Vx: 0, Vy: vCirc));

            w1.Advance(H);
            w2.Advance(H);

            bool isComet = w1.ClassOf(cometSlot) == BodyClass.Comet;
            double tail = w1.TailStrength(cometSlot);
            double initialIce = w1.Comp[cometSlot * World.NElem + 1];

            // 1 cut of 100 years
            w1.Do(new Command(CmdKind.FastForward, Amount: 100.0));
            double ice1Cut = w1.Comp[cometSlot * World.NElem + 1];

            // 200 cuts of 0.5 years
            for (int c = 0; c < 200; c++)
            {
                w2.Do(new Command(CmdKind.FastForward, Amount: 0.5));
            }
            double ice200Cuts = w2.Comp[cometSlot * World.NElem + 1];

            bool equalCuts = Math.Abs(ice1Cut - ice200Cuts) < 1e-12;
            bool lostIce = ice1Cut < initialIce;

            Check(isComet && tail > 0 && lostIce && equalCuts,
                $"Kuiper rock moved to 1 AU: class {w1.ClassOf(cometSlot)}, tail {tail:F2}, ice {initialIce:E2} -> {ice1Cut:E2} (1 cut {ice1Cut:E4} vs 200 cuts {ice200Cuts:E4}, diff {Math.Abs(ice1Cut - ice200Cuts):E2})");

            // Long jump until comet is spent
            w1.Do(new Command(CmdKind.FastForward, Amount: 2e6));
            bool spentEvent = w1.Events.Any(e => e.ObjectSlot == cometSlot && e.RuleId == "comets" && e.Change == "comet.spent");
            bool noIce = w1.Comp[cometSlot * World.NElem + 1] == 0;
            Check(spentEvent && noIce && w1.ClassOf(cometSlot) == BodyClass.Asteroid,
                $"comet spent: logged={spentEvent}, ice={w1.Comp[cometSlot * World.NElem + 1]}, transitioned to {w1.ClassOf(cometSlot)}");
        }

        // 6. Sol classes unchanged after +1e6 years
        {
            var sol = World.SolSystem(0, 1234);
            var classesBefore = Enumerable.Range(0, 10).Select(i => sol.ClassOf(i)).ToArray();
            sol.Do(new Command(CmdKind.FastForward, Amount: 1e6));
            var classesAfter = Enumerable.Range(0, 10).Select(i => sol.ClassOf(i)).ToArray();
            bool unchanged = classesBefore.SequenceEqual(classesAfter);
            Check(unchanged, $"Sol classes unchanged after 1e6 years: {string.Join(", ", classesAfter)}");
        }

        // 7. Replay hash
        {
            var w = World.SolSystem(200, 1234);
            for (int s = 0; s < 500; s++)
            {
                if (s == 20) w.Do(new Command(CmdKind.Move, Target: 15, X: 45.0, Y: 0, Vx: 0, Vy: 1.05));
                if (s == 50) w.Do(new Command(CmdKind.FastForward, Amount: 500.0));
                if (s == 100) w.Do(new Command(CmdKind.SetConst, Name: "SnowLineTemp", Amount: 220));
                w.Advance(H);
            }
            var r = World.SolSystem(200, 1234);
            int next = 0;
            for (int s = 0; s < 500; s++)
            {
                r.Replay(w.Journal, ref next);
                r.Advance(H);
            }
            bool hashMatch = w.Hash() == r.Hash();
            Check(hashMatch && w.Events.SequenceEqual(r.Events),
                $"replay hash {r.Hash():X16} vs live {w.Hash():X16}, events match {w.Events.Count}");
        }

        // 8. Rule overhead at 5000 rocks under +10%
        {
            var w = World.SolSystem(5000, 1234);
            for (int i = 0; i < 60; i++) w.Advance(H);

            w.Do(new Command(CmdKind.SetRule, Name: "comets", Amount: 0));
            for (int i = 0; i < 20; i++) w.Advance(H);
            var sw = Stopwatch.StartNew();
            const int steps = 100;
            for (int i = 0; i < steps; i++) w.Advance(H);
            double msDisabled = sw.Elapsed.TotalMilliseconds / steps;

            w.Do(new Command(CmdKind.SetRule, Name: "comets", Amount: 1));
            for (int i = 0; i < 20; i++) w.Advance(H);
            sw.Restart();
            for (int i = 0; i < steps; i++) w.Advance(H);
            double msEnabled = sw.Elapsed.TotalMilliseconds / steps;

            double overhead = msDisabled > 0 ? (msEnabled - msDisabled) / msDisabled : 0;
            Check(overhead < 0.10,
                $"5000-rock cost comets enabled {msEnabled:F3} / disabled {msDisabled:F3} ms/step, overhead {overhead * 100:F1}% (limit +10%)");
        }

        return _ok;
    }
}
