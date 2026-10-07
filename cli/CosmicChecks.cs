using System;
using System.Linq;
using Cosmos.Core;

static class CosmicChecks
{
    public static bool Run()
    {
        bool ok = true;
        void Check(bool pass, string line) { ok &= pass; Console.WriteLine($"{(pass ? "OK    " : "FAILED")} cosmic: {line}"); }
        foreach (bool gas in new[] { false, true })
        {
            var elements = ElementCatalog.Elements.Append(new Element("extra", "Extra", 1, 0, gas ? ElementRole.Gas : ElementRole.Ice)).ToArray();
            var w = new World(8, 7, elements);
            w.Do(new Command(CmdKind.Create, Amount: 50, Mix: w.Mix(("gas", 1))));
            int i = w.Do(new Command(CmdKind.Create, X: 15, Amount: gas ? World.EarthMass : 1e-10,
                Vx: .02, Vy: -.03, Mix: w.Mix((gas ? "gas" : "ice", .4), ("extra", .4), ("rock", .2))));
            w.Do(new Command(CmdKind.SetConst, Name: "G", Amount: 0));
            w.Do(new Command(CmdKind.SetConst, Name: "GasHoldRatio", Amount: 1));
            w.Do(new Command(CmdKind.SetConst, Name: "GasLossRate", Amount: 1));
            var cells = w.Comp.Skip(i * w.ElementCount).Take(w.ElementCount).ToArray();
            double mass = w.M[i], px = mass * w.Vx[i], py = mass * w.Vy[i];
            w.Advance(0); w.Do(new Command(CmdKind.FastForward, Amount: 1));
            Check(w.EscapedMass > 0 && Math.Abs(w.M[i] + w.EscapedMass - mass) < mass * 1e-12,
                $"{(gas ? "gas" : "ice")} multi-role loss: live+escaped mass closes, escaped={w.EscapedMass:R}");
            Check(Enumerable.Range(0, w.ElementCount).All(e => Math.Abs(w.Comp[i * w.ElementCount + e] + w.EscapedMatter[e] - cells[e]) < mass * 1e-12)
                && Math.Abs(w.M[i] * w.Vx[i] + w.EscapedPx - px) < mass * 1e-12
                && Math.Abs(w.M[i] * w.Vy[i] + w.EscapedPy - py) < mass * 1e-12,
                "every role cell and bulk momentum close independently");
            Check(w.M[i] == w.Comp.Skip(i * w.ElementCount).Take(w.ElementCount).Sum(), "post-trim M equals sum of cells");
            var r = new World(8, 7, elements); int next = 0;
            // Advance(0) is not a journal command; repeat it at the same position in the sequence.
            r.Replay(w.Journal.Take(w.Journal.Count - 1).ToList(), ref next); r.Advance(0); r.Replay(w.Journal, ref next);
            Check(w.Hash() == r.Hash(), $"volatile replay {w.Hash():X16}/{r.Hash():X16}");
        }
        var tiny = new World(4, 7);
        tiny.Do(new Command(CmdKind.Create, Amount: 50, Mix: tiny.Mix(("gas", 1))));
        int grain = tiny.Do(new Command(CmdKind.Create, Amount: 1e-16, X: 15, Mix: tiny.Mix(("ice", .5), ("rock", .5))));
        tiny.Do(new Command(CmdKind.SetConst, Name: "G", Amount: 0));
        tiny.Do(new Command(CmdKind.SetConst, Name: "JumpSamples", Amount: 1));
        tiny.Advance(0);
        tiny.Do(new Command(CmdKind.SetConst, Name: "CometSublimationRate", Amount: (5e-17 - 1e-19) / ((tiny.Temp[grain] - tiny.C.SnowLineTemp) / 100)));
        tiny.Do(new Command(CmdKind.FastForward, Amount: 1));
        Check(tiny.Matter(grain, ElementRole.Ice) == 0 && tiny.M[grain] == 5e-17
            && Math.Abs(tiny.EscapedMass - 5e-17) < 1e-30
            && tiny.M[grain] == tiny.Comp.Skip(grain * tiny.ElementCount).Take(tiny.ElementCount).Sum(),
            $"tiny ice residual: M-sum(Comp)={tiny.M[grain] - tiny.Comp.Skip(grain * tiny.ElementCount).Take(tiny.ElementCount).Sum():E3}; erased residual enters escaped ledger");
        void Closed(World w, double mass, double px, double py, double[] before, string label)
        {
            double live = 0, x = 0, y = 0; var cells = (double[])w.EscapedMatter.Clone();
            for (int i = 0; i < w.N; i++) if (w.Alive[i])
            {
                live += w.M[i]; x += w.M[i] * w.Vx[i]; y += w.M[i] * w.Vy[i];
                for (int e = 0; e < w.ElementCount; e++) cells[e] += w.Comp[i * w.ElementCount + e];
            }
            Check(Math.Abs(live + w.EscapedMass - mass) < 1e-11 * mass
                && Math.Abs(x + w.EscapedPx - px) < 1e-11 * mass && Math.Abs(y + w.EscapedPy - py) < 1e-11 * mass
                && Enumerable.Range(0, w.ElementCount).All(e => Math.Abs(cells[e] - w.NucleosynthesisDelta[e] - before[e]) < 1e-11 * mass),
                $"{label}: live+shared escaped closes M/every transformed cell/Px/Py");
        }
        World Death(double solar, double chance, ulong seed = 7, int samples = 200)
        {
            var w = new World(4, seed);
            w.Do(new Command(CmdKind.Create, Amount: solar * 50, Vx: .02, Vy: -.03, Mix: w.Mix(("gas", 1))));
            w.Do(new Command(CmdKind.SetConst, Name: "HypernovaChance", Amount: chance));
            w.Do(new Command(CmdKind.SetConst, Name: "JumpSamples", Amount: samples));
            w.Do(new Command(CmdKind.FastForward, Amount: w.StarLifetime(solar * 50) * 1.1));
            return w;
        }
        foreach (double m in new[] { 7.999, 8, 24.999, 25, 30 })
        {
            var w = Death(m, 1); bool hn = m >= 25, sn = m >= 8 && !hn;
            Check(w.Events.Count(e => e.Change == "star.hypernova") == (hn ? 1 : 0)
                && w.Events.Count(e => e.Change == "star.nova") == (sn ? 1 : 0)
                && (!hn || w.StarPhaseOf(0) == StarPhase.BlackHole) && w.GammaBursts.Count == (hn ? 1 : 0),
                $"death threshold {m} Suns: exclusive SN/HN, expected remnant and burst");
            Closed(w, m * 50, m, -m * 1.5, w.Mix(("gas", m * 50)), $"death {m}");
            var replay = new World(4, 7); int next = 0; replay.Replay(w.Journal, ref next);
            Check(w.Hash() == replay.Hash(), $"death {m} replay {w.Hash():X16}");
        }
        var fallback = Death(30, 0);
        Check(fallback.Events.Any(e => e.Change == "star.nova") && fallback.GammaBursts.Count == 0
            && fallback.StarPhaseOf(0) == StarPhase.BlackHole, "failed spin draw falls through to normal SN/BH, no GRB");
        var beamWorld = Death(30, 1); var beam = beamWorld.GammaBursts.Single();
        Check(Math.Abs(beam.AxisX * beam.AxisX + beam.AxisY * beam.AxisY + beam.AxisZ * beam.AxisZ - 1) < 1e-14
            && beam.HalfAngleRad == 7.5 * Math.PI / 180 && beam.Id == "grb.long"
            && Math.Abs(beam.FluenceAt(beam.DoseRadiusMetres) / beam.FluenceJm2 - 1) < 1e-14
            && Math.Abs(beam.FluenceAt(2 * beam.DoseRadiusMetres) / beam.FluenceJm2 - .25) < 1e-14,
            $"two opposed unit-axis beams; inverse-square dose radius={beam.DoseRadiusMetres / 3.085677581e19:F4} kpc");
        Check(beamWorld.Chronicle.Any(c => c.Event.Change == "grb.long") && beamWorld.GammaBursts.SequenceEqual(Death(30, 1).GammaBursts)
            && beam.AxisX != Death(30, 1, 8).GammaBursts.Single().AxisX, "Chronicle, repeatable seed and independent random directions");
        int oldGen = beam.Generation; beamWorld.Do(new Command(CmdKind.Remove, Target: 0));
        beamWorld.Do(new Command(CmdKind.Create, Amount: 1, Mix: beamWorld.Mix(("rock", 1))));
        Check(beamWorld.Gen[0] != oldGen && beamWorld.GammaBursts.Single() == beam, "recycled slot cannot rewrite persistent burst identity");
        // A star seconds from death: a genuinely small integrator step versus rails, no orbital approximation involved.
        World NearDeath(bool jump)
        {
            var w = new World(4, 1); w.Do(new Command(CmdKind.Create, Amount: 1500, Mix: w.Mix(("gas", 1))));
            w.Do(new Command(CmdKind.SetConst, Name: "HypernovaChance", Amount: .5));
            double rate = 1 / w.StarLifetime(1500);
            w.Do(new Command(CmdKind.SetStarState, Target: 0, StarState: new StellarState(0, 1 + w.C.StarGiantFraction - rate * .001, 1500)));
            if (jump) w.Do(new Command(CmdKind.FastForward, Amount: .002)); else w.Advance(.002 * w.C.YearTime);
            return w;
        }
        var small = NearDeath(false); var large = NearDeath(true);
        Check(small.GammaBursts.SequenceEqual(large.GammaBursts) && small.M[0] == large.M[0]
            && small.Events.Where(e => e.RuleId == "stars").Select(e => e.Change).SequenceEqual(large.Events.Where(e => e.RuleId == "stars").Select(e => e.Change))
            && small.Next() == large.Next(), "small Advance vs long jump: same spin outcome, axis, next RNG and ejected mass");
        var cut1 = Death(30, .5, 1, 1); var cut200 = Death(30, .5, 1, 200);
        var b1 = cut1.GammaBursts.Single(); var b200 = cut200.GammaBursts.Single();
        Check(b1.AxisX == b200.AxisX && b1.AxisY == b200.AxisY && b1.AxisZ == b200.AxisZ && cut1.Next() == cut200.Next()
            && Math.Abs(b1.Year - b200.Year) < 1e-7 && Math.Abs(b1.X - b200.X) < 1e-6 && Math.Abs(b1.Y - b200.Y) < 1e-6,
            $"1/200 samples: spin/axis/next RNG exact; boundary rounding dYear={b1.Year - b200.Year:E3}");
        World Compact(double a, StarPhase ap, double b, StarPhase bp, bool merge = true)
        {
            var w = new World(4, 7);
            foreach (var item in new[] { (a, ap), (b, bp) })
            {
                int s = w.Do(new Command(CmdKind.Create, Amount: item.Item1 * 50, Vx: .02, Vy: -.03, Mix: w.Mix(("gas", 1))));
                double birth = item.Item2 == StarPhase.WhiteDwarf ? 3 : item.Item2 == StarPhase.BlackHole ? 30 : 10;
                w.Do(new Command(CmdKind.SetStarState, Target: s, StarState: new StellarState(0, 1 + w.C.StarGiantFraction, birth * 50)));
            }
            if (merge) w.Advance(0);
            return w;
        }
        foreach (var (a, b, phase) in new[] { (1.1, 1.1, StarPhase.NeutronStar), (1.1, 1.13, StarPhase.NeutronStar), (1.2, 1.2, StarPhase.BlackHole) })
        {
            var w = Compact(a, StarPhase.NeutronStar, b, StarPhase.NeutronStar);
            int survivor = w.Heaviest();
            Check(w.Live == 1 && w.StarPhaseOf(survivor) == phase && w.Events.Count(e => e.Change == "star.kilonova.nsns") == 1
                && w.GammaBursts.Single().Id == "grb.short" && Math.Abs(w.StellarEjectaMass / 50 - .03) < 1e-14,
                $"NSNS {a}+{b}: eject .03 Suns, retained {(w.M[survivor] / 50):R}, {phase}");
            Closed(w, (a + b) * 50, (a + b), -(a + b) * 1.5, w.Mix(("gas", (a + b) * 50)), "NSNS");
            var r = new World(4, 7); int next = 0; r.Replay(w.Journal, ref next); r.Advance(0);
            Check(w.Hash() == r.Hash(), $"merger replay {w.Hash():X16}");
            w.Advance(0); Check(w.GammaBursts.Count == 1, "retired partner cannot fire merger again");
        }
        foreach (double bh in new[] { 3.0, 5.0 })
        {
            var w = Compact(1.4, StarPhase.NeutronStar, bh, StarPhase.BlackHole);
            double q = bh / 1.4, eta = q / Math.Pow(1 + q, 2), c = 1.4765 * 1.4 / 12;
            double outside = 1.4 * Math.Pow(Math.Max(0, .406 * (1 - 2 * c) / Math.Cbrt(eta) - .139 * 6 * c / eta + .255), 1.761);
            Check(w.StarPhaseOf(1) == StarPhase.BlackHole && w.GammaBursts.Count == (outside > 0 ? 1 : 0)
                && Math.Abs(w.StellarEjectaMass / 50 - Math.Min(.03, outside)) < 1e-14,
                $"NSBH 1.4+{bh}: Foucart C={c:F4}, available={outside:R}, ejected={w.StellarEjectaMass / 50:R}");
            Closed(w, (1.4 + bh) * 50, (1.4 + bh), -(1.4 + bh) * 1.5, w.Mix(("gas", (1.4 + bh) * 50)), "NSBH");
        }
        Check(Compact(.6, StarPhase.WhiteDwarf, .6, StarPhase.WhiteDwarf).GammaBursts.Count == 0
            && Compact(3, StarPhase.BlackHole, 3, StarPhase.BlackHole).GammaBursts.Count == 0, "WDWD/BHBH pairs have no hidden kilonova");
        // Independent geometry check: no slots available, supplied real Earth radius / physical 100 AU.
        var shell = new World(2, 7, starEvents: new[] { new StarEvent("probe.shell", StarTransition.Death,
            Ejection: new EjectionRule(EjectionMode.Fraction, Fraction: new StarParameter(.5)), Mix: new System.Collections.Generic.Dictionary<string, double> { ["metal"] = 1 }, SpeedKmS: new(10000)) });
        shell.Do(new Command(CmdKind.Create, Amount: 50, Mix: shell.Mix(("gas", 1))));
        int world = shell.Do(new Command(CmdKind.Create, X: World.SolDist(100), Amount: World.EarthMass,
            Mix: shell.Mix(("ice", .01), ("rock", .66), ("metal", .32), ("carbon", .005), ("radio", .005))));
        shell.Do(new Command(CmdKind.SetConst, Name: "G", Amount: 0));
        shell.Do(new Command(CmdKind.SetConst, Name: "GasLossRate", Amount: 0));
        var beforeCells = new double[shell.ElementCount]; for (int i = 0; i < 2; i++) for (int e = 0; e < shell.ElementCount; e++) beforeCells[e] += shell.Comp[i * shell.ElementCount + e];
        double oldMetal = shell.Matter(world, ElementRole.Metal);
        shell.Do(new Command(CmdKind.FastForward, Amount: shell.StarLifetime(50) * 1.1));
        double expect = 25 * Math.Pow(6371 / (100 * 149597870.7), 2) / 4;
        Check(Math.Abs((shell.Matter(world, ElementRole.Metal) - oldMetal) / expect - 1) < 1e-7
            && shell.N == 2 && shell.Live == 2 && shell.EscapedTransfers.Count == 1,
            $"shell solid angle at100AU, full capacity: captured={shell.Matter(world, ElementRole.Metal) - oldMetal:E6}, expected={expect:E6}, no packet slots");
        Closed(shell, 50 + World.EarthMass, 0, 0, beforeCells, "analytic shell with radial momentum");
        // Default SN mix follows catalogue IDs even when all six material rows are reversed.
        var order = new World(4, 7, ElementCatalog.Elements.Reverse());
        order.Do(new Command(CmdKind.Create, Amount: 500, Mix: order.Mix(("gas", 1))));
        order.Do(new Command(CmdKind.FastForward, Amount: order.StarLifetime(500) * 1.1));
        Check(order.EscapedMatter[order.Elem("metal")] > 0 && order.EscapedMatter[order.Elem("radio")] > 0
            && Math.Abs(order.NucleosynthesisDelta.Sum()) < 1e-11 && order.EscapedTransfers.Single().Cause == "star.nova",
            "reordered six-group yields, signed delta mass-neutral, escaped transfer names original event");
        // Custom merger row proves dispatch reads pair and payload columns rather than built-in identifiers.
        var pairRow = new StarEvent("probe.pair", StarTransition.Merger, Merger: new(StarPhase.WhiteDwarf, StarPhase.WhiteDwarf),
            Ejection: new(EjectionMode.ExpelledSolarMass, Intercept: new(.01)), Mix: new System.Collections.Generic.Dictionary<string, double> { ["carbon"] = 1 });
        var custom = new World(4, 7, starEvents: new[] { pairRow });
        for (int s = 0; s < 2; s++)
        {
            int i = custom.Do(new Command(CmdKind.Create, Amount: 30, Mix: custom.Mix(("gas", 1))));
            custom.Do(new Command(CmdKind.SetStarState, Target: i, StarState: new(0, 1 + custom.C.StarGiantFraction, 150)));
        }
        custom.Advance(0);
        Check(custom.Events.Any(e => e.Change == "probe.pair") && custom.StellarEjectaMass == .5,
            "one added arbitrary WD pair row dispatches without editing the core");
        return ok;
    }
}
