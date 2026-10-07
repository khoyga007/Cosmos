using System;
using System.Collections.Generic;
using System.Linq;
using Cosmos.Core;

static class StarEventChecks
{
    public static bool Run()
    {
        bool ok = true;
        void Check(bool pass, string line) { ok &= pass; Console.WriteLine($"{(pass ? "OK    " : "FAILED")} star-events: {line}"); }
        // S1 re-capture 2026-10-06; A2 re-capture 2026-10-07; A3 re-capture 2026-10-08: new Consts fields hashed by reflection.
        double[] births = { 1.23, 2.0000000000000004, 7.999999999, 8.0, 20.0, 20.0000001, 31.123 };
        // C5 capture: shared ledger, physical constants, transformed shells and stochastic high-mass channel.
        // C6 capture: Roche constants, rule and persistent state; see C6-REPORT.md.
        ulong[] legacy = { 0x3111EC43BE1F8CC3, 0x86CA1273DE11B663, 0x74CF37E143ED8D4D, 0x78816E46EAD729B2,
            0x6ABE1F6A11F178F1, 0x72E2504BE76F0DC3, 0x27C9F85B6BA0D221 };
        bool exact = true; var got = new List<string>();
        for (int n = 0; n < births.Length; n++)
        {
            var w = new World(4, 1);
            w.Do(new Command(CmdKind.Create, Amount: births[n] * 50, Mix: w.Mix(("gas", 1)), Vx: .02, Vy: -.03));
            w.Do(new Command(CmdKind.SetConst, Name: "StarLifeScale", Amount: .01));
            w.Do(new Command(CmdKind.FastForward, Amount: w.StarLifetime(births[n] * 50) * 1.2));
            exact &= w.Hash() == legacy[n]; got.Add($"0x{w.Hash():X16}");
        }
        Check(exact, "seven C6 arbitrary/boundary progenitor regression hashes match" + (exact ? "" : $"; now: {string.Join(", ", got)}"));
        bool Refused(IEnumerable<StarEvent> rows)
        {
            try { _ = new World(4, 1, starEvents: rows); return false; }
            catch (ArgumentException) { return true; }
        }
        var elements = ElementCatalog.Elements.Append(new Element("test", "Test", 4, 0x123456, ElementRole.None)).ToArray();
        var conditions = new[] { new ProgenitorCondition(MassComparison.GreaterOrEqual, new(9)), new ProgenitorCondition(MassComparison.Less, new(11)) };
        var mix = new Dictionary<string, double> { ["test"] = 1 };
        var test = new StarEvent("test", StarTransition.Death, conditions,
            Ejection: new(EjectionMode.Fraction, Fraction: new(.1)), Mix: mix);
        var rows = StarEventCatalog.Events.Append(test).ToList();
        var live = new World(16, 3, elements, rows);
        conditions[0] = new(MassComparison.GreaterOrEqual, new(100)); mix["test"] = 0; rows.Clear();
        Check(live.StarEvents.Count == StarEventCatalog.Events.Count + 1 && live.StarEvents[^1].Conditions![0].Threshold.Value == 9
            && live.StarEvents[^1].Mix!["test"] == 1, "World snapshots rows, progenitor conditions and ejecta mix");
        int star = live.Do(new Command(CmdKind.Create, Amount: 500, Mix: live.Mix(("gas", 1)), Vx: .02, Vy: -.03));
        double death = live.StarLifetime(500) * (1 + live.C.StarGiantFraction);
        live.Do(new Command(CmdKind.FastForward, Amount: death * 1.1));
        var fired = live.Events.Where(e => e.Change == "test").ToArray();
        Check(fired.Length == 1 && Math.Abs(fired[0].Year - death) < 1e-7 && Math.Abs(live.M[star] - 63) < 1e-12,
            $"one added row fires once at physical death year {death:R}; remnant mass={live.M[star]:R}");
        Check(Math.Abs(live.StellarEjectaMatter[live.Elem("test")] - 7) < 1e-12
            && Math.Abs(live.M[star] + live.StellarEjectaMass - 500) < 1e-12
            && Math.Abs(live.Comp.Take(live.ElementCount).Sum() + live.StellarEjectaMatter.Sum() - 500) < 1e-12
            && Math.Abs(live.M[star] * live.Vx[star] + live.StellarEjectaPx - 10) < 1e-12
            && Math.Abs(live.M[star] * live.Vy[star] + live.StellarEjectaPy + 15) < 1e-12,
            "ejecta use the seventh material id; total mass, matter and both momentum components remain conserved");
        var cleanTest = test with { Conditions = new[] { new ProgenitorCondition(MassComparison.GreaterOrEqual, new(9)), new ProgenitorCondition(MassComparison.Less, new(11)) }, Mix = new Dictionary<string, double> { ["test"] = 1 } };
        var cleanRows = StarEventCatalog.Events.Append(cleanTest).ToArray();
        var replay = new World(16, 3, elements, cleanRows); int next = 0; replay.Replay(live.Journal, ref next);
        Check(next == live.Journal.Count && replay.Hash() == live.Hash(), $"custom-table journal replay {live.Hash():X16}/{replay.Hash():X16}");
        foreach (double mass in new[] { 8.0, 11.0 })
        {
            var outside = new World(4, 1, elements, cleanRows);
            outside.Do(new Command(CmdKind.Create, Amount: mass * 50, Mix: outside.Mix(("gas", 1))));
            outside.Do(new Command(CmdKind.FastForward, Amount: outside.StarLifetime(mass * 50) * 1.1));
            Check(outside.Events.All(e => e.Change != "test"), $"progenitor {mass} Suns lies outside the row's [9,11) conditions");
        }
        var sampled = new World(16, 3, elements, cleanRows);
        sampled.Do(new Command(CmdKind.Create, Amount: 500, Mix: sampled.Mix(("gas", 1)), Vx: .02, Vy: -.03));
        sampled.Do(new Command(CmdKind.SetConst, Name: "JumpSamples", Amount: 1));
        sampled.Do(new Command(CmdKind.FastForward, Amount: death * 1.1));
        double sampledYear = sampled.Events.Single(e => e.Change == "test").Year;
        Check(Math.Abs(sampledYear - fired[0].Year) < 1e-7 && sampled.M[0] == live.M[0]
            && sampled.StellarEjectaMatter.SequenceEqual(live.StellarEjectaMatter), $"1/default samples: event years {sampledYear:R}/{fired[0].Year:R}, identical ejection");
        // A low-mass progenitor has no default nova, isolating the added row's blast.
        var blastRow = new StarEvent("test.blast", StarTransition.Death, Range: new(40000), Damage: new(1));
        var blast = new World(8, 1, starEvents: StarEventCatalog.Events.Append(blastRow));
        blast.Do(new Command(CmdKind.Create, Amount: 150, Mix: blast.Mix(("gas", 1))));
        int planet = blast.Do(new Command(CmdKind.CreateOrbiting, Target: 0, X: 20000, Amount: World.EarthMass,
            Mix: blast.Mix(("ice", .01), ("rock", .66), ("metal", .32), ("carbon", .005), ("radio", .005))));
        blast.Do(new Command(CmdKind.SeedLife, Target: planet, Amount: .8));
        blast.Do(new Command(CmdKind.SetRule, Name: "life", Amount: 0)); blast.Do(new Command(CmdKind.SetRule, Name: "civ", Amount: 0));
        blast.Do(new Command(CmdKind.FastForward, Amount: blast.StarLifetime(150) * 1.1));
        Check(blast.Alive[planet] && blast.Life[planet] < .8 && blast.Events.Any(e => e.Change == "test.blast")
            && blast.Events.All(e => e.Change != "star.nova"), "added range/damage row damages a nearby biosphere without a default nova");
        Check(Refused(new[] { new StarEvent("bad", StarTransition.Death, Ejection: new(EjectionMode.Fraction, Fraction: new(.1)), Mix: new Dictionary<string, double> { ["missing"] = 1 }) })
            && Refused(new[] { new StarEvent("bad", StarTransition.Death, Ejection: new(EjectionMode.Fraction, Fraction: new(.1)), Mix: new Dictionary<string, double> { ["gas"] = .25 }) })
            && Refused(new[] { new StarEvent("bad", StarTransition.Death, Ejection: new(EjectionMode.Fraction, Fraction: new(1.1))) })
            && Refused(new[] { blastRow, blastRow }), "unknown ids, bad sums/fractions and duplicate event ids are refused at construction");
        Check(new World(4, 1).Hash() == new World(4, 1, starEvents: StarEventCatalog.Events.ToArray()).Hash()
            && new World(4, 1, starEvents: Array.Empty<StarEvent>()).Hash() != new World(4, 1).Hash()
            && new World(4, 1, starEvents: new[] { blastRow }).Hash() != new World(4, 1, starEvents: new[] { blastRow with { Damage = new(.5) } }).Hash(),
            "default catalog keeps legacy hashes; custom rows and their parameters participate in Hash");
        var empty = new World(4, 1, starEvents: Array.Empty<StarEvent>());
        empty.Do(new Command(CmdKind.Create, Amount: 100, Mix: empty.Mix(("gas", 1))));
        empty.Do(new Command(CmdKind.FastForward, Amount: empty.StarLifetime(100) * 1.1));
        Check(empty.StarFuel[0] >= 1 + empty.C.StarGiantFraction && empty.M[0] == 100 && empty.StellarEjectaMass == 0
            && empty.Events.All(e => e.RuleId != "stars"), "empty event table still advances the physical phase clock without hidden default events");
        return ok;
    }
}
