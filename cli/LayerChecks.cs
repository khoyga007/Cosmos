// Checks for the layers water -> life -> civilisation (core/Layers.cs). Owner: Claire. Alone: `cli -- layers`.
using System;
using System.Linq;
using Cosmos.Core;

static class LayerChecks
{
    const double H = 0.5;
    static bool _ok;
    static void Check(bool ok, string line) { _ok &= ok; Console.WriteLine($"{(ok ? "OK    " : "FAILED")} layers: {line}"); }
    static void Jump(World w, double years) => w.Do(new Command(CmdKind.FastForward, Amount: years));
    static string Story(World w, int slot) => string.Join(", ", w.Events.Where(e => e.ObjectSlot == slot && e.RuleId != "temperature").Select(e => $"{e.Change}@{e.Year:F0}"));

    public static bool Run()
    {
        _ok = true;
        const int earth = 3, mars = 4;
        double[] rock = { 0, 0, 1, 0, 0, 0 };

        // start: of the nine bodies only Earth has liquid water
        var sol = World.SolSystem(0, 1234);
        string waters = string.Join(" ", Enumerable.Range(1, 9).Select(i => $"{sol.Name[i]}={(WaterState)sol.Water[i]}"));
        Check(Enumerable.Range(1, 9).All(i => (sol.Water[i] == (int)WaterState.Liquid) == (i == earth)) && sol.Events.Count == 0, waters);

        // a million years, untouched: life then a civilisation on Earth; life nowhere else, people only where ships took them
        double metal0 = sol.Share(earth, 3), mass0 = sol.M[earth];
        Jump(sol, 1e6);
        bool onlyEarth = Enumerable.Range(0, sol.N).All(i => i == earth || sol.Life[i] == 0 && (sol.Pop[i] == 0 || sol.IsSolid(i) && sol.Civ[i] == sol.Civ[earth]));
        Check(onlyEarth && sol.Life[earth] > 0.9 && sol.Pop[earth] > 0.5 && sol.TechStage(earth) == World.MaxTechStage
            && Story(sol, earth).StartsWith("life.start@") && Story(sol, earth).Contains("civ.start@"),
            $"Sol + 1e6 years: Earth life {sol.Life[earth]:F3} pop {sol.Pop[earth]:F3} tech {sol.Tech[earth]:F1}; story: {Story(sol, earth)}");

        // the civilisation: a name, metal eaten (mass kept), colonies on every solid world, a chronicle of its own
        {
            int civ = sol.Civ[earth];
            var same = World.SolSystem(0, 1234); Jump(same, 1e6);
            int[] solid = Enumerable.Range(0, sol.N).Where(i => i != earth && sol.IsSolid(i)).ToArray();
            string colonies = string.Join(" ", solid.Select(i => $"{sol.Name[i]}={sol.Pop[i]:F3}"));
            string chron = string.Join(", ", sol.Chronicle.Where(c => c.Civ == civ).Select(c => $"{c.Event.Change}@{c.Event.Year:F0}/{sol.Name[c.Event.ObjectSlot]}"));
            Check(sol.Civs.Count == 1 && civ == 0 && sol.Civs[0].Home == earth && sol.Civs[0].Name.Length >= 4 && same.Civs[0].Name == sol.Civs[0].Name,
                $"one civilisation, named {sol.Civs[0].Name}, home {sol.Name[sol.Civs[0].Home]}, born year {sol.Civs[0].BornYear:F0}; same seed, same name");
            Check(sol.Share(earth, 3) < metal0 - 0.005 && sol.Share(earth, 3) > 0 && sol.M[earth] == mass0,
                $"industry ate metal: share {metal0:F3} -> {sol.Share(earth, 3):F3}, mass unchanged");
            Check(solid.Length == 4 && solid.All(i => sol.Pop[i] > 0 && sol.Pop[i] <= sol.C.CivDome * 1.001 && sol.Civ[i] == civ) && sol.WorldsOf(civ) == 5 && sol.Live == 10,
                $"colonies under domes, no ship objects left by a jump: {colonies}");
            Check(chron.Contains("civ.start@") && chron.Contains("civ.ship.first@") && sol.Chronicle.Count(c => c.Event.Change == "civ.colony") == 4,
                $"chronicle: {chron}");
        }

        // the same million years in ten jumps or as stepping-size pieces must tell the same story
        var ten = World.SolSystem(0, 1234);
        for (int k = 0; k < 10; k++) Jump(ten, 1e5);
        Check(Math.Abs(ten.Life[earth] - sol.Life[earth]) < 0.02 && Math.Abs(ten.Pop[earth] - sol.Pop[earth]) < 0.02 && ten.TechStage(earth) == sol.TechStage(earth),
            $"ten jumps of 1e5: life {ten.Life[earth]:F3} pop {ten.Pop[earth]:F3} tech {ten.Tech[earth]:F1}; story: {Story(ten, earth)}");

        // the god throws Earth outward: water freezes, life dies; a space-age people lives on under domes
        double dx = sol.X[earth] - sol.X[0], dy = sol.Y[earth] - sol.Y[0], d = Math.Sqrt(dx * dx + dy * dy);
        sol.Do(new Command(CmdKind.Push, Target: earth, Vx: -dy / d * 0.25, Vy: dx / d * 0.25));
        int before = sol.Events.Count;
        for (int s = 0; s < 2000; s++) sol.Advance(H);
        Jump(sol, 5e5);
        string after = string.Join(", ", sol.Events.Skip(before).Where(e => e.ObjectSlot == earth && e.RuleId != "temperature").Select(e => $"{e.Change}@{e.Year:F0}"));
        Check(sol.Life[earth] == 0 && Math.Abs(sol.Pop[earth] - sol.C.CivDome) < 1e-3 && !after.Contains("civ.end") && after.Contains("life.end"),
            $"Earth pushed out: {sol.Temp[earth]:F0} K, water {(WaterState)sol.Water[earth]}, pop {sol.Pop[earth]:F3} under domes; then: {after}");

        // without domes the same throw ends them: on Earth, and on every colony
        {
            var w = World.SolSystem(0, 1234);
            w.Do(new Command(CmdKind.SetConst, Name: "CivDome", Amount: 0));
            Jump(w, 1e6);
            int colonised = w.Chronicle.Count(c => c.Event.Change == "civ.colony");
            double ex = w.X[earth] - w.X[0], ey = w.Y[earth] - w.Y[0], ed = Math.Sqrt(ex * ex + ey * ey);
            w.Do(new Command(CmdKind.Push, Target: earth, Vx: -ey / ed * 0.25, Vy: ex / ed * 0.25));
            for (int s = 0; s < 2000; s++) w.Advance(H);
            Jump(w, 5e5);
            Check(w.Pop[earth] == 0 && Story(w, earth).Contains("civ.end") && w.WorldsOf(0) == 0 && colonised == 0,
                $"CivDome 0: Earth pop {w.Pop[earth]:F3}, worlds left {w.WorldsOf(0)}, colonies ever founded {colonised}, ends logged {w.Chronicle.Count(c => c.Event.Change == "civ.end")}");
        }

        // stepping: ships are objects. They fly, land, found colonies; the god can remove one; all of it replays.
        {
            var w = World.SolSystem(0, 1234);
            w.Do(new Command(CmdKind.SetConst, Name: "ShipPop", Amount: 2)); // no ships during the jump
            Jump(w, 6.95e5); // just into the space age
            w.Do(new Command(CmdKind.SetConst, Name: "ShipPop", Amount: 0.03));
            int civ = w.Civ[earth], had = w.WorldsOf(civ), seen = 0, maxFlying = 0, removed = -1; double removedAt = 0;
            for (int s = 0; s < 40000; s++)
            {
                w.Advance(H);
                int flying = 0, first = -1;
                for (int i = 0; i < w.N; i++) if (w.Alive[i] && w.IsShip(i)) { flying++; if (first < 0) first = i; }
                maxFlying = Math.Max(maxFlying, flying); if (flying > 0) seen++;
                if (removed < 0 && first >= 0 && s > 600) { removed = w.Do(new Command(CmdKind.Remove, Target: first)); removedAt = w.Year; }
            }
            string where = string.Join(" ", Enumerable.Range(1, 9).Where(i => w.Pop[i] > 0).Select(i => $"{w.Name[i]}={w.Pop[i]:F4}"));
            Check(seen > 0 && maxFlying <= 2 * 5 && removed >= 0 && had == 1 && w.WorldsOf(civ) == 5,
                $"ships flown over {40000 * H / w.C.YearTime:F0} years: worlds {had} -> {w.WorldsOf(civ)} ({where}), most in flight {maxFlying}, steps with a ship up {seen}, god removed one in year {removedAt:F1}");
            var r = World.SolSystem(0, 1234); int next = 0;
            for (int s = 0; s < 40000; s++) { r.Replay(w.Journal, ref next); r.Advance(H); }
            r.Replay(w.Journal, ref next);
            Check(r.Hash() == w.Hash() && r.Chronicle.SequenceEqual(w.Chronicle), $"ships replay from the journal: {w.Hash():X16} / {r.Hash():X16}");
        }

        // seeding: life put on frozen Mars dies out; put on a fitting planet it takes hold without the long wait
        var seed = World.SolSystem(0, 1234);
        int twin = seed.Do(new Command(CmdKind.CreateOrbiting, Target: 0, X: -seed.X[earth] * 41 / 45, Y: -seed.Y[earth] * 41 / 45, Amount: World.EarthMass, Mix: new[] { 0, 0.01, 0.66, 0.32, 0.005, 0.005 }, Name: "Twin"));
        bool did = seed.Do(new Command(CmdKind.SeedLife, Target: mars, Amount: 0.3)) >= 0 && seed.Do(new Command(CmdKind.SeedLife, Target: twin, Amount: 0.3)) >= 0
            && seed.Do(new Command(CmdKind.SeedLife, Target: 0, Amount: 0.3)) < 0; // a star takes no life
        Jump(seed, 2e5);
        Check(did && seed.Life[mars] == 0 && Story(seed, mars).Contains("life.end") && seed.Life[twin] > 0.9 && seed.Life[earth] < 0.01,
            $"seeded 0.3: Mars {seed.Life[mars]:F3} ({Story(seed, mars)}), twin Earth slot {twin} {seed.Life[twin]:F3} water {(WaterState)seed.Water[twin]} {seed.Temp[twin]:F0} K ({Story(seed, twin)}), Earth by itself {seed.Life[earth]:F3} after 2e5 years");

        // impact: a body of 0.1% of the planet's mass lands on a living planet
        {
            var w = new World(8, 1);
            w.Add(0, 0, 0, 0, 50, new double[] { 1, 0, 0, 0, 0, 0 });
            int p = w.Do(new Command(CmdKind.CreateOrbiting, Target: 0, X: 45, Y: 0, Amount: World.EarthMass, Mix: new[] { 0, 0.01, 0.66, 0.32, 0.005, 0.005 }));
            w.Do(new Command(CmdKind.SeedLife, Target: p, Amount: 0.8));
            w.Do(new Command(CmdKind.Create, X: 45.01, Y: 0, Vx: w.Vx[p], Vy: w.Vy[p], Amount: World.EarthMass * 1e-3, Mix: rock));
            w.Advance(H);
            var e = w.Events.LastOrDefault();
            Check(w.Live == 2 && Math.Abs(w.Life[p] - 0.8 * Math.Exp(-1)) < 1e-6 && e.RuleId == "impact",
                $"impact of 0.1% mass: life 0.800 -> {w.Life[p]:F3}, event {e.RuleId} share {e.A:E1}");
        }

        // all of it replays from the journal
        {
            var w = World.SolSystem(200, 9);
            for (int s = 0; s < 900; s++)
            {
                if (s == 50) w.Do(new Command(CmdKind.SeedLife, Target: earth, Amount: 0.2));
                if (s == 100) Jump(w, 4e5);
                if (s == 200) w.Do(new Command(CmdKind.SetConst, Name: "WaterFreeze", Amount: 300));
                w.Advance(H);
            }
            var r = World.SolSystem(200, 9); int next = 0;
            for (int s = 0; s < 900; s++) { r.Replay(w.Journal, ref next); r.Advance(H); }
            Check(w.Hash() == r.Hash() && w.Events.SequenceEqual(r.Events) && w.Water[earth] == (int)WaterState.Ice,
                $"replay: hash {r.Hash():X16} vs live {w.Hash():X16}, {w.Events.Count} events equal; WaterFreeze 300 froze Earth");
        }

        // Celine's review, round 2: seven inputs that used to give wrong or broken state
        {
            void Set(World w, string name, double value) => w.Do(new Command(CmdKind.SetConst, Name: name, Amount: value));
            World Seeded(double samples, double life)
            {
                var w = World.SolSystem(0, 1234); Set(w, "JumpSamples", samples);
                w.Do(new Command(CmdKind.SeedLife, Target: earth, Amount: life)); return w;
            }
            bool near(double a, double b, double rel) => Math.Abs(a - b) <= rel * Math.Max(Math.Abs(a), Math.Abs(b));

            // 1 + 2: one history, cut into 1 chunk or 200, ends the same
            var a = Seeded(1, .001); var b = Seeded(200, .001);
            Jump(a, 4e5); Jump(b, 4e5);
            Check(near(a.RichYears[earth], b.RichYears[earth], 1e-6) && near(a.Pop[earth], b.Pop[earth], 0.05) && near(a.Tech[earth], b.Tech[earth], 0.05) && b.Pop[earth] == 0,
                $"cut 1 / 200: rich years {a.RichYears[earth]:F1} / {b.RichYears[earth]:F1}, pop {a.Pop[earth]:G4} / {b.Pop[earth]:G4}");
            Jump(a, 3e5); Jump(b, 3e5);
            Check(near(a.RichYears[earth], b.RichYears[earth], 1e-6) && near(a.Pop[earth], b.Pop[earth], 0.05) && near(a.Tech[earth], b.Tech[earth], 0.05) && b.Pop[earth] > 0.5,
                $"cut 1 / 200, 3e5 years on: pop {a.Pop[earth]:F4} / {b.Pop[earth]:F4}, tech {a.Tech[earth]:F3} / {b.Tech[earth]:F3}");
            a = Seeded(1, .8); b = Seeded(200, .8);
            foreach (var w in new[] { a, b }) { Set(w, "LifeGrowth", 0); Set(w, "CivRiseYears", 0); Jump(w, 100); }
            Jump(a, 1e4); Jump(b, 1e4);
            Check(near(a.Pop[earth], b.Pop[earth], 1e-9) && near(a.Tech[earth], b.Tech[earth], 1e-6),
                $"cut 1 / 200, fixed biosphere: pop {a.Pop[earth]:F6} / {b.Pop[earth]:F6}, tech {a.Tech[earth]:F6} / {b.Tech[earth]:F6}");

            // 3: a seed grows only for the years it has lived
            var s3 = World.SolSystem(0, 1234); Jump(s3, 999);
            s3.Do(new Command(CmdKind.SeedLife, Target: earth, Amount: .1)); Jump(s3, 2);
            double lived = s3.Rules.Find(r => r.Id == "life")!.LastYear - 999; // the life rule last ran at year 1000
            double want = 1 / (1 + 9 * Math.Exp(-s3.C.LifeGrowth * lived));
            Check(lived > 0 && lived <= 2 && Math.Abs(s3.Life[earth] - want) < 1e-9, $"seed 0.1 at year 999, life rule ran {lived:F2} years later: {s3.Life[earth]:F7}, expected {want:F7}");

            // 4: CivMetalRef 0 on a planet without metal
            var s4 = Seeded(200, .8); Set(s4, "CivRiseYears", 0); Set(s4, "LifeGrowth", 0); Set(s4, "CivMetalRef", 0);
            s4.Do(new Command(CmdKind.AddMatter, Target: earth, Index: 3, Amount: -s4.Comp[earth * World.NElem + 3]));
            Jump(s4, 1e4);
            Check(double.IsFinite(s4.Tech[earth]) && s4.Tech[earth] > 0, $"CivMetalRef 0, no metal: tech {s4.Tech[earth]:F4}");

            // 5: two objects on one spot, then a jump
            var s5 = new World(3, 1);
            s5.Do(new Command(CmdKind.Create, Amount: 50, Mix: new double[] { 1, 0, 0, 0, 0, 0 }));
            s5.Do(new Command(CmdKind.Create, Amount: World.EarthMass, Mix: new[] { 0, .01, .66, .32, .005, .005 }));
            Jump(s5, 1);
            Check(s5.X.Take(s5.N).Concat(s5.Y.Take(s5.N)).All(double.IsFinite), $"two objects on one spot + jump: x {s5.X[0]:G3}, {s5.X[1]:G3}; off rails {s5.OffRails}");

            // 6: the hash sees a push and a changed constant before any step
            var s6 = World.SolSystem(0, 1234); ulong h0 = s6.Hash();
            s6.Do(new Command(CmdKind.Push, Target: earth, Vx: 1)); ulong h1 = s6.Hash();
            Set(s6, "LifeGrowth", 1); ulong h2 = s6.Hash();
            Check(h0 != h1 && h1 != h2, $"hash after push / after SetConst: {h0:X16} / {h1:X16} / {h2:X16}");

            // 7: an impact that breaks the rich biosphere clears its count at once
            var s7 = Seeded(200, 1); Jump(s7, 1e6);
            s7.Do(new Command(CmdKind.Create, X: s7.X[earth], Y: s7.Y[earth], Vx: s7.Vx[earth], Vy: s7.Vy[earth], Amount: World.EarthMass * .002, Mix: new[] { 0, .01, .66, .32, .005, .005 }));
            s7.Advance(0);
            Check(s7.Life[earth] < s7.C.CivLifeMin && s7.RichYears[earth] == 0, $"impact 0.2%: life {s7.Life[earth]:F3}, rich years {s7.RichYears[earth]:F0}");
        }

        // cost of a long jump with the whole rule table
        {
            var w = World.SolSystem(5_000, 1234);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            Jump(w, 1e6);
            Console.WriteLine($"       layers: jump 1e6 years, 5000 rocks, {w.C.JumpSamples} chunks: {sw.Elapsed.TotalMilliseconds:F0} ms");
        }
        return _ok;
    }
}
