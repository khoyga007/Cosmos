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

        // a million years, untouched: life then a civilisation on Earth, nowhere else
        Jump(sol, 1e6);
        bool onlyEarth = Enumerable.Range(0, sol.N).All(i => i == earth || sol.Life[i] == 0 && sol.Pop[i] == 0);
        Check(onlyEarth && sol.Life[earth] > 0.9 && sol.Pop[earth] > 0.5 && sol.TechStage(earth) == World.MaxTechStage
            && Story(sol, earth).StartsWith("life.start@") && Story(sol, earth).Contains("civ.start@"),
            $"Sol + 1e6 years: Earth life {sol.Life[earth]:F3} pop {sol.Pop[earth]:F3} tech {sol.Tech[earth]:F1}; story: {Story(sol, earth)}");

        // the same million years in ten jumps or as stepping-size pieces must tell the same story
        var ten = World.SolSystem(0, 1234);
        for (int k = 0; k < 10; k++) Jump(ten, 1e5);
        Check(Math.Abs(ten.Life[earth] - sol.Life[earth]) < 0.02 && Math.Abs(ten.Pop[earth] - sol.Pop[earth]) < 0.02 && ten.TechStage(earth) == sol.TechStage(earth),
            $"ten jumps of 1e5: life {ten.Life[earth]:F3} pop {ten.Pop[earth]:F3} tech {ten.Tech[earth]:F1}; story: {Story(ten, earth)}");

        // the god throws Earth outward: water freezes, life dies, the civilisation with it
        double dx = sol.X[earth] - sol.X[0], dy = sol.Y[earth] - sol.Y[0], d = Math.Sqrt(dx * dx + dy * dy);
        sol.Do(new Command(CmdKind.Push, Target: earth, Vx: -dy / d * 0.25, Vy: dx / d * 0.25));
        int before = sol.Events.Count;
        for (int s = 0; s < 2000; s++) sol.Advance(H);
        Jump(sol, 5e5);
        string after = string.Join(", ", sol.Events.Skip(before).Where(e => e.ObjectSlot == earth && e.RuleId != "temperature").Select(e => $"{e.Change}@{e.Year:F0}"));
        Check(sol.Life[earth] == 0 && sol.Pop[earth] == 0 && after.Contains("civ.end") && after.Contains("life.end"),
            $"Earth pushed out: {sol.Temp[earth]:F0} K, water {(WaterState)sol.Water[earth]}; then: {after}");

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
