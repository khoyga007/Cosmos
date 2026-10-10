using System; using Cosmos.Core;
static class Diag {
    public static bool Run() {
        foreach (double jy in new[]{1e4, 2e4}) {
            var w = World.SolSystem(5000, 1002); w.Threads = 1; w.C.DiscOn = 1.0;
            for (int k = 0; k < 96; k++) w.Advance(.5);
            int ns = w.Do(new Command(CmdKind.Create, X: 100, Amount: 466700 * World.EarthMass, Mix: w.Mix(("gas", 1))));
            double birth = 10 * w.C.StarSolarMass, end = 1 + w.C.StarGiantFraction;
            w.Do(new Command(CmdKind.SetStarState, Target: ns, StarState: new StellarState(w.StarLifetime(birth) * end, end, birth)));
            for (int s = 0; s < 500; s++) w.Advance(.5);
            int disrBefore = (int)w.RocheDisruptions.Count, mergBefore = (int)w.Merges;
            double accBefore = w.DiscAccretedMass;
            w.Do(new Command(CmdKind.FastForward, Amount: jy));
            Console.WriteLine($"jump {jy:E0}: disr {disrBefore}->{w.RocheDisruptions.Count} (+{w.RocheDisruptions.Count-disrBefore}), " +
                $"merges {mergBefore}->{w.Merges} (+{w.Merges-mergBefore}), " +
                $"acc +{(w.DiscAccretedMass-accBefore)/World.EarthMass:F1} Earths, live={w.Live}");
            // dem formation cua disruption moi
            int nd=0,nr=0,ns2=0;
            for (int i = disrBefore; i < (int)w.RocheDisruptions.Count; i++) {
                var d = w.RocheDisruptions[i];
                if (d.Formation=="disc") nd++; else if (d.Formation=="ring") nr++; else ns2++;
            }
            Console.WriteLine($"  new disruptions: to-disc={nd} ring={nr} stream={ns2}");
        }
        return true;
    }
}
