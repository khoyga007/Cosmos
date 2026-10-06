using System;
using System.Linq;
using Cosmos.Core;

static class NovaStampChecks
{
    static void Jump(World w, double years) => w.Do(new Command(CmdKind.FastForward, Amount: years));
    static void Set(World w, string key, double value) => w.Do(new Command(CmdKind.SetConst, Name: key, Amount: value));

    public static bool Run()
    {
        bool ok = true;
        void Check(bool pass, string text) { ok &= pass; Console.WriteLine($"{(pass ? "OK    " : "FAILED")} nova-stamps: {text}"); }
        RuleEvent[]? firstEnds = null;
        foreach (double samples in new[] { 1.0, 200.0 })
        {
            var sol = World.SolSystem(0, 1234);
            Set(sol, "JumpSamples", samples);
            Jump(sol, 1e10);
            Check(sol.Alive[3] && sol.Life[3] > .9 && sol.Pop[3] > .5
                && sol.Events.All(e => e.ObjectSlot != 3 || e.Change != "life.end" && e.Change != "civ.end"),
                $"{samples} samples at giant: Earth life={sol.Life[3]:R}, pop={sol.Pop[3]:R}; no retroactive extinction");
            Check(sol.Events.Any(e => e.ObjectSlot == 3 && e.Change == "band.1.2" && Math.Abs(e.Year - 1e10) < 1e-3)
                && sol.Events.Any(e => e.ObjectSlot == 3 && e.Change == "water.2.3" && Math.Abs(e.Year - 1e10) < 1e-3)
                && sol.Events.Any(e => e.ObjectSlot == 1 && e.Change == "merge" && Math.Abs(e.Year - 1e10) < 1e-3),
                "heat/water and swallowed Mercury change at the giant boundary");
            Jump(sol, 1e6);
            var ends = sol.Events.Where(e => e.ObjectSlot == 3 && (e.Change == "life.end" || e.Change == "civ.end")).ToArray();
            Check(ends.Length == 2 && ends.All(e => e.Year > 1e10 && e.Year < 1e10 + 1e6),
                $"{samples} samples, giant +1e6: {string.Join(", ", ends.Select(e => $"{e.Change}@{e.Year:R}"))}");
            if (firstEnds == null) firstEnds = ends;
            else Check(firstEnds.Length == ends.Length && firstEnds.All(e => ends.Any(x => x.Change == e.Change && Math.Abs(x.Year - e.Year) < 1e-3)),
                "1/200 samples: both extinction years agree within 1e-3 year");
            var replay = World.SolSystem(0, 1234); int next = 0; replay.Replay(sol.Journal, ref next);
            Check(replay.Hash() == sol.Hash() && replay.Events.SequenceEqual(sol.Events), $"giant replay {sol.Hash():X16}/{replay.Hash():X16}");
        }

        RuleEvent[]? firstBands = null;
        foreach (double samples in new[] { 1.0, 200.0 })
        {
            var w = new World(8, 1);
            Set(w, "G", 0); Set(w, "JumpSamples", samples); // isolate thermal history from orbital mass loss
            int star = w.Do(new Command(CmdKind.Create, Amount: 25.15, Mix: w.Mix(("gas", 1))));
            w.Do(new Command(CmdKind.SetStarState, Target: star, StarState: new(1.05e10, 1.05, 50)));
            int planet = w.Do(new Command(CmdKind.Create, X: 45, Amount: World.EarthMass,
                Mix: w.Mix(("ice", .01), ("rock", .66), ("metal", .32), ("carbon", .005), ("radio", .005))));
            Jump(w, 1);
            Check(w.StarSurfaceTemperature(star) > 190000 && w.Temp[planet] > w.C.ScorchedEdge,
                $"new white dwarf: surface {w.StarSurfaceTemperature(star):F0}K, planet {w.Temp[planet]:F1}K, L={w.StarLuminosity(star):F3}");
            double mass = w.M[star];
            Jump(w, 1e8);
            var bands = w.Events.Where(e => e.ObjectSlot == planet && e.RuleId == "temperature").ToArray();
            Check(bands.Length == 2 && bands[0].Change == "band.2.1" && bands[1].Change == "band.1.0"
                && bands[0].Year < bands[1].Year && bands[1].Year < 1e8 && w.M[star] == mass,
                $"{samples} samples: physical cooling retains warm interval; {string.Join(",", bands.Select(e => $"{e.Change}@{e.Year:R}"))}");
            double birth = Math.Pow(w.StarRadius(star) / (w.C.StarSolarRadius * w.C.RadiusScale / 1.5), 2) * Math.Pow(200000.0 / 5772, 4);
            double origin = (1.05e8 / 14) * Math.Pow(birth / (mass / 50), -5.0 / 7);
            double Crossing(double edge) => (1.05e8 / 14) * Math.Pow((mass / 50) / Math.Pow(edge / 288, 4), 5.0 / 7) - origin;
            Check(bands.Length == 2 && Math.Abs(bands[0].Year - Crossing(350)) < 1e-3 && Math.Abs(bands[1].Year - Crossing(240)) < 1e-3,
                "both thermal dates satisfy the independently inverted Mestel age-luminosity law");
            if (firstBands == null) firstBands = bands;
            else Check(firstBands.Length == bands.Length && firstBands.Zip(bands).All(p => p.First.Change == p.Second.Change && Math.Abs(p.First.Year - p.Second.Year) < 1e-3),
                "1/200 thermal crossing years agree within 1e-3 year");
            var replay = new World(8, 1); int next = 0; replay.Replay(w.Journal, ref next);
            Check(replay.Hash() == w.Hash(), $"cooling replay {w.Hash():X16}/{replay.Hash():X16}");
        }
        var ns = new World(4, 1);
        ns.Do(new Command(CmdKind.Create, Amount: 70, Mix: ns.Mix(("gas", 1))));
        ns.Do(new Command(CmdKind.SetStarState, Target: 0, StarState: new(1e6, 1.05, 500, 330)));
        Check(Math.Abs(ns.StarSurfaceTemperature(0) - 2e6) < 1 && ns.StarLuminosity(0) > 1,
            $"young neutron-star thermal normalization: {ns.StarSurfaceTemperature(0):F0}K at 330 yr");

        // The equilibrium-light model has an actual discontinuity at death, not an unobserved warm interval.
        foreach (double suns in new[] { 1.0, 10.0, 30.0 })
        {
            var w = new World(16, 1);
            int star = w.Do(new Command(CmdKind.Create, Amount: suns * 50, Mix: w.Mix(("gas", 1))));
            double distance = 45 * Math.Sqrt(suns < 8 ? 1000 : 1000 * Math.Pow(suns, 1.6)) * Math.Pow(288.0 / 400, 2);
            int planet = w.Do(new Command(CmdKind.CreateOrbiting, Target: star, X: distance, Amount: World.EarthMass,
                Mix: w.Mix(("ice", .01), ("rock", .66), ("metal", .32), ("carbon", .005), ("radio", .005))));
            double death = w.StarLifetime(suns * 50) * (1 + w.C.StarGiantFraction);
            Jump(w, death - 10000);
            double hot = w.Temp[planet];
            int before = w.Events.Count;
            Jump(w, 10000);
            var bands = w.Events.Skip(before).Where(e => e.ObjectSlot == planet && e.RuleId == "temperature").ToArray();
            Console.WriteLine($"       nova-stamps: (a) {suns} Suns {hot:R}->{w.Temp[planet]:R}K; {string.Join(",", bands.Select(e => $"{e.Change}@{e.Year:R}"))}; death={death:R}");
            Check(w.Alive[planet] && w.BandOf(w.Temp[planet]) == TemperatureBand.Frozen,
                $"{suns} Suns remnant equilibrium is frozen; no invented intermediate thermal track");
        }
        return ok;
    }
}
