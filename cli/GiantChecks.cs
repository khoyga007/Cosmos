using System;
using System.Linq;
using Cosmos.Core;

static class GiantChecks
{
    static int Jump(World w, double years) => w.Do(new Command(CmdKind.FastForward, Amount: years));

    public static bool Run()
    {
        bool ok = true;
        void Check(bool pass, string line) { ok &= pass; Console.WriteLine($"{(pass ? "OK    " : "FAILED")} giant: {line}"); }
        var before = World.SolSystem(0, 1234); Jump(before, 9.9e9);
        Check(before.Live == 10 && before.StarPhaseOf(0) == StarPhase.MainSequence, "Sol at 9.9 Gyr: ten bodies, Sun still on the main sequence");
        foreach (double solarRadii in new[] { 100.0, 200.0 })
        {
            var sol = World.SolSystem(0, 1234);
            sol.Do(new Command(CmdKind.SetConst, Name: "StarGiantRadius", Amount: solarRadii));
            Jump(sol, 1e10);
            double expected = World.SolDist(solarRadii / 215);
            Check(sol.StarPhaseOf(0) == StarPhase.RedGiant && Math.Abs(sol.R[0] / expected - 1) < .01
                && sol.R[0] == sol.StarRadius(0) && sol.StarSurfaceTemperature(0) <= sol.C.StarGiantTemperature,
                $"{solarRadii} solar radii -> {sol.R[0]:F3} orbital units (expected {expected:F3}), drawn/contact radius agree, surface {sol.StarSurfaceTemperature(0):F0}K");
            Check(!sol.Alive[1] && sol.Alive[3] && Enumerable.Range(4, 6).All(i => sol.Alive[i])
                && (solarRadii == 100 ? sol.Alive[2] : !sol.Alive[2]),
                $"Sol +10 Gyr: {sol.Live} live, Mercury gone, Venus {(sol.Alive[2] ? "alive" : "gone")}, Earth and outer worlds survive");
            int[] lost = Enumerable.Range(1, 9).Where(i => !sol.Alive[i]).ToArray();
            Check(lost.All(i => sol.Events.Count(e => e.ObjectSlot == i && e.RuleId == "contact" && e.Change == "merge") == 1),
                $"Sol giant logs each of {lost.Length} swallowed worlds");
        }
        foreach (bool jump in new[] { false, true })
        {
            var w = World.SolSystem(0, 1234); Jump(w, 1e6);
            double[] oldLife = (double[])w.Life.Clone(), oldPop = (double[])w.Pop.Clone();
            int[] oldCiv = (int[])w.Civ.Clone();
            bool[] wasAlive = (bool[])w.Alive.Clone();
            int eventsBefore = w.Events.Count, chronicleBefore = w.Chronicle.Count;
            foreach (int i in new[] { 1, 3 }) w.Do(new Command(CmdKind.Move, Target: i, X: w.X[0], Y: w.Y[0]));
            if (jump) Jump(w, 1); else w.Advance(0);
            int[] lost = Enumerable.Range(0, w.N).Where(i => wasAlive[i] && !w.Alive[i]).ToArray();
            var events = w.Events.Skip(eventsBefore).ToArray();
            var deaths = w.Chronicle.Skip(chronicleBefore).ToArray();
            Check(lost.Length >= 2 && lost.All(i => events.Count(e => e.ObjectSlot == i && e.RuleId == "contact" && e.Change == "merge"
                    && e.A == 0 && e.B > 0 && e.C == w.Gen[i]) == 1),
                $"{(jump ? "jump" : "Advance")}: {lost.Length} swallowed objects, exactly one merge event per victim");
            Check(lost.Where(i => oldLife[i] > 0).All(i => events.Count(e => e.ObjectSlot == i && e.Change == "life.end") == 1)
                && lost.Where(i => oldPop[i] > 0).All(i => events.Count(e => e.ObjectSlot == i && e.Change == "civ.end") == 1
                    && deaths.Count(e => e.Civ == oldCiv[i] && e.Event.ObjectSlot == i && e.Event.Change == "civ.end") == 1)
                && lost.All(i => w.Life[i] == 0 && w.Pop[i] == 0 && w.Tech[i] == 0),
                $"{(jump ? "jump" : "Advance")}: swallowed life and population end once, civilisation chronicles retain the deaths");
            Check(lost.All(i => events.Where(e => e.ObjectSlot == i && (e.Change == "merge" || e.Change == "civ.end" || e.Change == "life.end"))
                    .All(e => e.Year == events.Single(x => x.ObjectSlot == i && x.Change == "merge").Year)),
                "each victim's layer deaths use its merge's sampled contact year");
            var r = World.SolSystem(0, 1234); int next = 0; r.Replay(w.Journal, ref next);
            if (!jump) r.Advance(0);
            Check(w.Hash() == r.Hash() && w.Chronicle.SequenceEqual(r.Chronicle),
                $"{(jump ? "jump" : "Advance")} death replay {w.Hash():X16}/{r.Hash():X16}");
        }
        return ok;
    }
}
