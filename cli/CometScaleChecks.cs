using System;
using System.Linq;
using Cosmos.Core;

static class CometScaleChecks
{
    public static bool Run()
    {
        bool ok = true;
        void Check(bool pass, string message) { ok &= pass; Console.WriteLine($"{(pass ? "OK    " : "FAILED")} comet-scale: {message}"); }
        foreach (int stars in new[] { 9, 200 })
        {
            var w = new World(stars + 8, 1234);
            double[] gas = w.Mix(("gas", 1)), ice = w.Mix(("ice", .8), ("rock", .2));
            int last = -1;
            for (int i = 0; i < stars; i++) last = w.Do(new Command(CmdKind.Create,
                X: i * 10000, Amount: 50, Mix: gas, Name: "S" + i));
            int hot = w.Do(new Command(CmdKind.CreateOrbiting, Target: last,
                X: w.X[last] + 45, Amount: 1e-8, Mix: ice, Name: "hot-last-star"));
            int cold = w.Do(new Command(CmdKind.CreateOrbiting, Target: last,
                X: w.X[last] + 1000, Amount: 1e-8, Mix: ice, Name: "cold-last-star"));
            w.Do(new Command(CmdKind.SetConst, Name: "CometSublimationRate", Amount: .1));
            int element = w.Elem("ice");
            double before = w.Comp[hot * w.ElementCount + element], coldBefore = w.Comp[cold * w.ElementCount + element];
            w.Advance(.1 * w.C.YearTime);
            double after = w.Comp[hot * w.ElementCount + element], coldAfter = w.Comp[cold * w.ElementCount + element];
            Check(after < before && w.EscapedMatter[element] > 0,
                $"{stars} stars: ice at LAST star decreases {before:R}->{after:R}; escaped={w.EscapedMatter[element]:R}, temp={w.Temp[hot]:R}");
            Check(coldAfter == coldBefore, $"{stars} stars: distant cold ice stays unchanged {coldAfter:R}");
            double accounted = w.EscapedMatter[element];
            for (int i = 0; i < w.N; i++) if (w.Alive[i]) accounted += w.Comp[i * w.ElementCount + element];
            foreach (var reservoir in w.RocheReservoirs) accounted += reservoir.Matter[element];
            Check(Math.Abs(accounted - before - coldBefore) <= (before + coldBefore) * 1e-12,
                $"{stars} stars: resolved+escaped+reservoir ice ledger closes {accounted:R}");
            var replay = new World(stars + 8, 1234); int next = 0;
            replay.Replay(w.Journal, ref next); replay.Advance(.1 * replay.C.YearTime);
            Check(replay.Hash() == w.Hash(), $"{stars} stars: exact command replay {w.Hash():X16}/{replay.Hash():X16}");
        }
        return ok;
    }
}
