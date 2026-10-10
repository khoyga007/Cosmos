using System;
using System.Globalization;
using Cosmos.Core;

// Vong 5, viec 1 (phase 2): tim nguong va kiem tra gia thuyet chunking.
static class JumpAnomaly2
{
    static int DropNS(World w)
    {
        int ns = w.Do(new Command(CmdKind.Create, X: 100, Amount: 466700 * World.EarthMass, Mix: w.Mix(("gas", 1))));
        double birth = 10 * w.C.StarSolarMass, end = 1 + w.C.StarGiantFraction;
        w.Do(new Command(CmdKind.SetStarState, Target: ns, StarState: new StellarState(w.StarLifetime(birth) * end, end, birth)));
        return ns;
    }

    static double TestJump(double years, double jumpSamples)
    {
        var w = World.SolSystem(5000, 1002);
        w.Threads = 1;
        w.C.DiscOn = 1.0;
        w.C.JumpSamples = jumpSamples;
        for (int k = 0; k < 96; k++) w.Advance(.5);
        DropNS(w);
        for (int s = 0; s < 500; s++) w.Advance(.5);
        double accBefore = w.DiscAccretedMass;
        int liveBefore = w.Live;
        w.Do(new Command(CmdKind.FastForward, Amount: years));
        double dAcc = (w.DiscAccretedMass - accBefore) / World.EarthMass;
        Console.WriteLine($"  jump {years:E0} yr, samples={jumpSamples}: swallowed {dAcc:F2} Earths, live {liveBefore}->{w.Live}");
        return dAcc;
    }

    public static bool Run()
    {
        Console.WriteLine("== Binary search threshold (samples=200) ==");
        foreach (double y in new[] { 1.5e4, 2e4, 3e4, 5e4, 7e4 })
            TestJump(y, 200);
        Console.WriteLine("== Chunking hypothesis: 1e6 yr with more samples ==");
        foreach (double js in new[] { 200.0, 1000.0, 5000.0 })
            TestJump(1e6, js);
        return true;
    }
}
