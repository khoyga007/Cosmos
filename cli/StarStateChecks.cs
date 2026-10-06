using System;
using System.Linq;
using Cosmos.Core;

static class StarStateChecks
{
    public static bool Run()
    {
        bool ok = true;
        void Check(bool pass, string line) { ok &= pass; Console.WriteLine($"{(pass ? "OK    " : "FAILED")} star-state: {line}"); }
        foreach (var (birth, mass, phase) in new[] { (2.0, 30.6, StarPhase.WhiteDwarf), (10.0, 70.0, StarPhase.NeutronStar), (30.0, 375.0, StarPhase.BlackHole) })
        {
            var w = new World(8, 1);
            int star = w.Do(new Command(CmdKind.Create, Amount: mass, Mix: w.Mix(("gas", 1)), Vx: .02, Vy: -.03));
            var state = new StellarState(w.StarLifetime(birth * 50) * (1 + w.C.StarGiantFraction) + 1e6,
                1 + w.C.StarGiantFraction, birth * 50, 1e6);
            int r = w.Do(new Command(CmdKind.SetStarState, Target: star, StarState: state));
            Check(r == star && w.StarPhaseOf(star) == phase && w.M[star] == mass && w.Comp[star * w.ElementCount] == mass
                && w.Vx[star] == .02 && w.Vy[star] == -.03 && w.StellarEjectaMass == 0 && w.Events.Count == 0,
                $"direct {phase} from numeric snapshot: mass/matter/momentum unchanged, no historical nova/ejecta");
            double light = w.StarLuminosity(star);
            w.Do(new Command(CmdKind.FastForward, Amount: 1e6));
            Check(w.Alive[star] && w.StarPhaseOf(star) == phase && w.StarCoolingAge[star] == 2e6
                && w.StarLuminosity(star) <= light && w.StellarEjectaMass == 0 && w.Events.All(e => e.RuleId != "stars"),
                $"{phase} continues cooling without firing death a second time");
            var replay = new World(8, 1); int next = 0; replay.Replay(w.Journal, ref next);
            Check(replay.Hash() == w.Hash(), $"{phase} snapshot + cooling replay {w.Hash():X16}/{replay.Hash():X16}");
        }
        var burning = new World(8, 1);
        burning.Do(new Command(CmdKind.Create, Amount: 50, Mix: burning.Mix(("gas", 1))));
        burning.Do(new Command(CmdKind.SetConst, Name: "StarLifeScale", Amount: 1e-8));
        burning.Do(new Command(CmdKind.FastForward, Amount: 20));
        burning.Do(new Command(CmdKind.SetStarState, Target: 0, StarState: new(80, .8, 50)));
        burning.Advance(burning.C.YearTime * 20.1);
        Check(burning.StarPhaseOf(0) == StarPhase.RedGiant && burning.Events.Any(e => e.Change == "star.giant" && Math.Abs(e.Year - 40) < 1e-12),
            "edited burning fuel reschedules an ordinary Advance boundary from the current year");
        burning.Do(new Command(CmdKind.FastForward, Amount: 5));
        Check(burning.StarPhaseOf(0) == StarPhase.WhiteDwarf && burning.Events.Count(e => e.Change == "star.remnant.white") == 1
            && Math.Abs(burning.Events.Single(e => e.Change == "star.remnant.white").Year - 45) < 1e-12,
            "future death follows the event table exactly once at the rescheduled year");
        var burningReplay = new World(8, 1); int burningNext = 0;
        burningReplay.Replay(burning.Journal, ref burningNext); burningReplay.Advance(burningReplay.C.YearTime * 20.1);
        burningReplay.Replay(burning.Journal, ref burningNext);
        Check(burningReplay.Hash() == burning.Hash(), $"snapshot replay across Advance and jump {burning.Hash():X16}/{burningReplay.Hash():X16}");
        var invalid = new World(8, 1);
        int sun = invalid.Do(new Command(CmdKind.Create, Amount: 50, Mix: invalid.Mix(("gas", 1))));
        int planet = invalid.Do(new Command(CmdKind.Create, X: 100, Amount: World.EarthMass, Mix: invalid.Mix(("rock", 1))));
        ulong hash = invalid.Hash(); int journal = invalid.Journal.Count;
        var states = new[] { new StellarState(double.NaN, 0, 50), new(-1, 0, 50), new(10, -.1, 50), new(10, 2, 50),
            new(10, 0, double.PositiveInfinity), new(10, .5, 100), new(10, 1.05, 49), new(10, 0, 50, 1), new(10, 1.05, 50, 11) };
        Check(states.All(state => invalid.Do(new Command(CmdKind.SetStarState, Target: sun, StarState: state)) == -1)
            && invalid.Do(new Command(CmdKind.SetStarState, Target: planet, StarState: new(10, 1.05, 50))) == -1
            && invalid.Do(new Command(CmdKind.SetStarState, Target: -1, StarState: new(10, 1.05, 50))) == -1
            && invalid.Do(new Command(CmdKind.SetStarState, Target: sun)) == -1
            && invalid.Hash() == hash && invalid.Journal.Count == journal, "invalid snapshots/targets are refused atomically without journal or hash changes");
        var brown = new World(4, 1); brown.Do(new Command(CmdKind.Create, Amount: 2.5, Mix: brown.Mix(("gas", 1))));
        double young = brown.StarLuminosity(0);
        Check(brown.Do(new Command(CmdKind.SetStarState, Target: 0, StarState: new(1e9, 0, 2.5))) == 0
            && brown.StarPhaseOf(0) == StarPhase.BrownDwarf && brown.StarLuminosity(0) < young, "brown-dwarf age edit changes cooling without inventing hydrogen burning");
        return ok;
    }
}
