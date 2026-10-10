using System;
using System.Globalization;
using Cosmos.Core;

// Vong 5, viec 1: tai hien ca seed 1002 — tua 1e4 nam nuot 95 Trai Dat,
// nhung tua 1e6/1e8 nam nuot 0. In theo tung moc: khoi luong dia, da nuot,
// sao chu cua dia (slot+Gen), so dia. Tach: loi do hay loi duong Jump.
static class JumpAnomaly
{
    static int DropNS(World w)
    {
        int ns = w.Do(new Command(CmdKind.Create, X: 100, Amount: 466700 * World.EarthMass, Mix: w.Mix(("gas", 1))));
        double birth = 10 * w.C.StarSolarMass, end = 1 + w.C.StarGiantFraction;
        w.Do(new Command(CmdKind.SetStarState, Target: ns, StarState: new StellarState(w.StarLifetime(birth) * end, end, birth)));
        return ns;
    }

    static void PrintState(string tag, World w)
    {
        var iv = CultureInfo.InvariantCulture;
        Console.WriteLine($"[{tag}] year={w.Year:F1} live={w.Live} discs={w.Discs.Count} " +
            $"accreted={w.DiscAccretedMass / World.EarthMass:F2} Earths accAng={w.DiscAccretedAngular:E3}");
        foreach (var d in w.Discs)
        {
            string host = d.Host >= 0 ? $"slot={d.Host} gen={(d.Host < w.N ? w.Gen[d.Host].ToString() : "?")} alive={w.Alive[d.Host]}" : "host=-1 FROZEN";
            Console.WriteLine($"    disc: {host} cells={d.Cells} total={d.Total:E4} fed={d.Fed:E4} cold={d.Cold} L={w.DiscAngular(d):E4}");
        }
        int heavy = w.Heaviest();
        Console.WriteLine($"    heavy: slot={heavy} phase={w.StarPhaseOf(heavy)} mass={w.M[heavy]:E4}");
    }

    public static bool Run()
    {
        var iv = CultureInfo.InvariantCulture;
        double[] jumps = { 1e2, 1e3, 1e4, 1e5, 1e6, 1e7, 1e8 };
        foreach (double jy in jumps)
        {
            Console.WriteLine($"===== seed 1002, jump {jy:E0} years =====");
            var w = World.SolSystem(5000, 1002);
            w.Threads = 1;
            w.C.DiscOn = 1.0;
            for (int k = 0; k < 96; k++) w.Advance(.5);
            DropNS(w);
            for (int s = 0; s < 500; s++) w.Advance(.5);
            PrintState("pre-jump", w);
            double accBefore = w.DiscAccretedMass;
            int discsBefore = w.Discs.Count;
            try
            {
                w.Do(new Command(CmdKind.FastForward, Amount: jy));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  EXCEPTION during jump: {ex.GetType().Name}: {ex.Message}");
            }
            PrintState("post-jump", w);
            double dAcc = (w.DiscAccretedMass - accBefore) / World.EarthMass;
            Console.WriteLine($"  => jump swallowed {dAcc:F2} Earths, discs {discsBefore}->{w.Discs.Count}");
            Console.WriteLine();
        }
        return true;
    }
}
