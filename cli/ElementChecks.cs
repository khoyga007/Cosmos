using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Runtime.Loader;
using Cosmos.Core;

static class ElementChecks
{
    // Captured from 236b4db before replacing the material layout; includes real stepping, layers, edits and remnants.
    static readonly ulong[] Legacy = { 0xE914BDC4B7FB0F4B, 0x63B5C44B467131D2, 0xE6AF00AE612FD0F1, 0x27291AD7151463EB,
        0xF119D94EA5D83720, 0xE97790037CF3B6BC, 0x5FB9F47D0BB39F04, 0xB812A74856695F03, 0x6C866F3AA02536AC,
        0x461923819FDE010D, 0x8D7EC8B03F5BE4C2 };

    public static bool Run()
    {
        bool ok = true;
        void Check(bool pass, string line) { ok &= pass; Console.WriteLine($"{(pass ? "OK    " : "FAILED")} elements: {line}"); }
        Check(Probe().Values.SequenceEqual(Legacy), "all eleven default-table hashes remain bit-identical (stars: 236b4db; Sol: since the scene lost its net momentum)");
        var source = ElementCatalog.Elements.ToList();
        source.Add(new Element("test", "Test", 4, 0x123456, ElementRole.None));
        var table = source.ToArray(); var w = new World(16, 5, source); source.Clear();
        Check(w.ElementCount == 7 && w.Elements.Count == 7 && w.C.Density.Length == 7 && w.StellarEjectaMatter.Length == 7,
            "seventh group sizes matter, density and stellar ledger; World snapshots its table");
        double[] gas = { 1, 0, 0, 0, 0, 0 }, planet = { 0, .01, .66, .32, .005, .005 };
        int sun = w.Do(new Command(CmdKind.Create, Amount: 50, Mix: gas));
        int p = w.Do(new Command(CmdKind.CreateOrbiting, Target: sun, X: 45, Amount: World.EarthMass, Mix: planet));
        Check(sun == 0 && p == 1 && w.Comp[p * w.ElementCount + w.Elem("test")] == 0,
            "old six-cell Create/CreateOrbiting mixes zero-fill the seventh group");
        w.Do(new Command(CmdKind.AddMatter, Target: p, Name: "test", Amount: 6e-5));
        int fragment = w.Do(new Command(CmdKind.Create, X: w.X[p], Y: w.Y[p], Vx: w.Vx[p], Vy: w.Vy[p],
            Amount: 2e-5, Mix: w.Mix(("test", 1))));
        double mass = Enumerable.Range(0, w.N).Where(i => w.Alive[i]).Sum(i => w.M[i]);
        w.Advance(0);
        double radius = w.R[p];
        Check(w.Do(new Command(CmdKind.SetConst, Name: "Density[6]", Amount: 2)) == -2 && w.R[p] > radius,
            "Density[k] edits the corresponding row of this World's table");
        w.Do(new Command(CmdKind.FastForward, Amount: 1000));
        Check(w.Alive[p] && !w.Alive[fragment] && Math.Abs(w.Comp[p * w.ElementCount + w.Elem("test")] - 8e-5) < 1e-15
            && Math.Abs(Enumerable.Range(0, w.N).Where(i => w.Alive[i]).Sum(i => w.M[i]) + w.StellarEjectaMass - mass) < 1e-12,
            "seventh-group AddMatter, merge and jump preserve the added matter and total mass");
        var r = new World(16, 5, table); int next = 0;
        r.Replay(w.Journal, ref next); r.Advance(0); r.Replay(w.Journal, ref next);
        Check(w.Hash() == r.Hash() && w.Comp.SequenceEqual(r.Comp), $"seventh-group replay {w.Hash():X16}/{r.Hash():X16}");
        var normal = World.SolSystem(200, 9); var extended = World.SolSystem(200, 9, table);
        for (int i = 0; i < 40; i++) { normal.Advance(.5); extended.Advance(.5); }
        Check(normal.N == extended.N && Enumerable.Range(0, normal.N).All(i => normal.Alive[i] == extended.Alive[i]
                && normal.X[i] == extended.X[i] && normal.Y[i] == extended.Y[i] && normal.M[i] == extended.M[i]
                && normal.R[i] == extended.R[i] && extended.Comp[i * extended.ElementCount + extended.Elem("test")] == 0),
            "appending an unused group leaves the belts, ring and physical motion identical; it cannot become the 70% main group");
        var reordered = World.SolSystem(0, 1234, ElementCatalog.Elements.Reverse());
        var original = World.SolSystem(0, 1234);
        Check(Enumerable.Range(0, original.N).All(i => original.ClassOf(i) == reordered.ClassOf(i))
            && original.Share(3, ElementRole.Metal) == reordered.Share(3, ElementRole.Metal),
            "rules and scene mixes follow ids/roles even when table rows are reordered");
        int journal = w.Journal.Count, live = w.Live;
        Check(w.Do(new Command(CmdKind.Create, Amount: 1, Mix: new[] { .25 })) == -1
            && w.Do(new Command(CmdKind.Create, Amount: 1, Mix: new[] { double.NaN })) == -1
            && w.Do(new Command(CmdKind.Create, Amount: 1, Mix: new double[8])) == -1 && w.Live == live && w.Journal.Count == journal,
            "invalid sums, NaN and oversized mixes are refused without normalization or state changes");
        var tagged = table.ToArray(); tagged[^1] = tagged[^1] with { Roles = ElementRole.Metal };
        Check(new World(4, 1, table).Hash() != new World(4, 1, tagged).Hash(), "custom roles participate in the state hash");
        var icy = table.ToArray(); icy[^1] = icy[^1] with { Roles = ElementRole.Ice };
        var multi = new World(4, 1, icy); int iceWorld = multi.Do(new Command(CmdKind.Create, Amount: .01, Mix: multi.Mix(("ice", .25), ("test", .75))));
        Check(multi.Share(iceWorld, ElementRole.Ice) == 1, "a role reads all groups tagged with it, including the added row");
        return ok;
    }

    public static Dictionary<string, ulong> Probe()
    {
        var hashes = new Dictionary<string, ulong>();
        void Save(string id, World w) => hashes.Add(id, w.Hash());
        var sol = World.SolSystem(0, 1234); Save("sol0", sol);
        for (int i = 0; i < 100; i++) sol.Advance(.5); Save("sol0-step", sol);
        sol.Do(new Command(CmdKind.SeedLife, Target: 3, Amount: .2));
        sol.Do(new Command(CmdKind.FastForward, Amount: 1e6)); Save("sol0-life", sol);
        sol.Do(new Command(CmdKind.Move, Target: 3, X: sol.X[3] + 30, Y: sol.Y[3] - 7, Vx: .1, Vy: .2, Index: 1));
        sol.Do(new Command(CmdKind.SetConst, Name: "Density[2]", Amount: 2.5));
        sol.Do(new Command(CmdKind.AddMatter, Target: 4, Index: 3, Amount: 1e-6));
        sol.Do(new Command(CmdKind.FastForward, Amount: 1e4)); Save("sol0-edit", sol);
        var rocks = World.SolSystem(2000, 9); Save("sol2000", rocks);
        for (int i = 0; i < 50; i++) rocks.Advance(.5); Save("sol2000-step", rocks);
        rocks.Do(new Command(CmdKind.FastForward, Amount: 1e6)); Save("sol2000-jump", rocks);
        foreach (double suns in new[] { 1.0, 2.0, 10.0, 30.0 })
        {
            var star = new World(16, 1);
            star.Do(new Command(CmdKind.Create, Amount: suns * 50, Mix: new double[] { 1, 0, 0, 0, 0, 0 }, Vx: .02, Vy: -.03));
            star.Do(new Command(CmdKind.SetConst, Name: "StarLifeScale", Amount: .01));
            star.Do(new Command(CmdKind.FastForward, Amount: star.StarLifetime(suns * 50) * 1.2)); Save($"star-{suns}", star);
        }
        return hashes;
    }

    public static void PrintProbe() { foreach (var (id, hash) in Probe()) Console.WriteLine($"{id} {hash:X16}"); }

    public static void Bench()
    {
        var samples = new double[7];
        for (int k = 0; k < samples.Length; k++)
        {
            var w = World.SolSystem(5000, 1234);
            for (int i = 0; i < 100; i++) w.Advance(.5);
            var timer = Stopwatch.StartNew();
            for (int i = 0; i < 500; i++) w.Advance(.5);
            samples[k] = timer.Elapsed.TotalMilliseconds / 500;
        }
        Array.Sort(samples);
        Console.WriteLine($"elements bench: 5000 rocks, step median7 {samples[3]:F6} ms ({string.Join(",", Array.ConvertAll(samples, x => x.ToString("F6")))})");
    }

    public static bool PairedBench(string baselineDll)
    {
        var context = new AssemblyLoadContext("elements-baseline", isCollectible: true);
        try
        {
            var type = context.LoadFromAssemblyPath(Path.GetFullPath(baselineDll)).GetType("Cosmos.Core.World")!;
            var make = type.GetMethod("SolSystem")!; var advance = type.GetMethod("Advance")!;
            var before = new double[7]; var after = new double[7]; var ratios = new double[7];
            double Measure(Action<double> step)
            {
                var timer = Stopwatch.StartNew();
                for (int i = 0; i < 500; i++) step(.5);
                return timer.Elapsed.TotalMilliseconds / 500;
            }
            for (int k = 0; k < before.Length; k++)
            {
                object old = make.Invoke(null, new object[] { 5000, 1234UL })!;
                Action<double> oldStep = advance.CreateDelegate<Action<double>>(old);
                var current = World.SolSystem(5000, 1234); Action<double> newStep = current.Advance;
                for (int i = 0; i < 100; i++) { oldStep(.5); newStep(.5); }
                if (k % 2 == 0) { before[k] = Measure(oldStep); after[k] = Measure(newStep); }
                else { after[k] = Measure(newStep); before[k] = Measure(oldStep); }
                ratios[k] = after[k] / before[k];
                Console.WriteLine($"elements paired {k}: before {before[k]:F6}, after {after[k]:F6} ms, delta {100 * (ratios[k] - 1):F2}%");
            }
            Array.Sort(before); Array.Sort(after); Array.Sort(ratios);
            bool pass = ratios[3] <= 1.05;
            Console.WriteLine($"{(pass ? "OK    " : "FAILED")} elements paired: same-process median7 before {before[3]:F6}/after {after[3]:F6} ms, median paired delta {100 * (ratios[3] - 1):F2}% (ceiling +5%)");
            return pass;
        }
        finally { context.Unload(); }
    }
}
