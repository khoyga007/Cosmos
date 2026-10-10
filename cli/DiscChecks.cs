using System;
using Cosmos.Core;

// Heat Law slice 1: hot Roche debris becomes a disc, the disc drains into its host. `cli -- disc`.
static class DiscChecks
{
    static int _ok, _failed;

    static void Check(string name, bool pass, string detail = "")
    {
        if (pass) _ok++; else _failed++;
        Console.WriteLine($"{(pass ? "OK    " : "FAILED")} disc {name}{(detail.Length > 0 ? ": " + detail : "")}");
    }

    // A gas giant and an ice body placed just inside its Roche limit on a circular orbit: it breaks at once into a
    // ring. vapor = 0 calls every ring hot, so the ring goes to the disc.
    static World Scene(double vapor)
    {
        var w = new World(128, 7);
        foreach (string rule in new[] { "stars", "life", "civ", "ships", "water", "temperature", "comets" })
            w.Do(new Command(CmdKind.SetRule, Name: rule, Amount: 0));
        w.C.DiscVaporEnergy = vapor;
        int p = w.Do(new Command(CmdKind.Create, X: 3, Y: -2, Vx: .2, Vy: -.1, Amount: .05, Mix: w.Mix(("gas", 1))));
        int i = w.Do(new Command(CmdKind.Create, X: 5, Y: -2, Amount: 1e-6, Mix: w.Mix(("ice", 1))));
        double radius = .999 * w.RocheLimit(i, p);
        w.Do(new Command(CmdKind.Move, Target: i, X: w.X[p] + radius, Y: w.Y[p], Vx: w.Vx[p],
            Vy: w.Vy[p] + Math.Sqrt(w.C.G * (w.M[p] + w.M[i]) / radius), Index: 1));
        return w;
    }

    static (double Mass, double L, double Ice) Ledger(World w)
    {
        int ice = w.Elem("ice"), ne = w.ElementCount;
        double mass = w.EscapedMass, l = w.DiscAccretedAngular - w.DiscResidualAngular, matter = w.EscapedMatter[ice];
        for (int i = 0; i < w.N; i++) if (w.Alive[i])
        {
            double m = w.M[i] + w.DiscMassOn(i);
            mass += m; l += m * (w.X[i] * w.Vy[i] - w.Y[i] * w.Vx[i]); matter += w.Comp[i * ne + ice];
        }
        foreach (var d in w.Discs)
        {
            l += w.DiscAngular(d);
            for (int k = 0; k < d.Cells; k++) matter += w.DiscCellMatter(d, k, ice);
        }
        foreach (var r in w.RocheReservoirs) { mass += r.Mass; matter += r.Matter[ice]; }
        return (mass, l, matter);
    }

    static bool Close(double a, double b, double tolerance) => Math.Abs(a - b) <= tolerance * Math.Max(Math.Abs(a), Math.Abs(b));

    public static bool Run()
    {
        _ok = _failed = 0;

        // 1. hot debris: no fragments, one disc, ledgers close
        var w = Scene(0);
        var start = Ledger(w);
        int liveBefore = w.Live;
        w.Advance(.5);
        Check("hot ring makes no fragments", w.Live == liveBefore - 1 && w.Discs.Count == 1, $"live {liveBefore} -> {w.Live}, discs {w.Discs.Count}");
        var now = Ledger(w);
        Check("mass closes at formation", Close(start.Mass, now.Mass, 1e-12), $"{start.Mass:R} -> {now.Mass:R}");
        Check("ice closes at formation", Close(start.Ice, now.Ice, 1e-12), $"{start.Ice:R} -> {now.Ice:R}");
        Check("angular momentum closes at formation", Close(start.L, now.L, 1e-9), $"{start.L:R} -> {now.L:R}");
        if (w.Discs.Count == 0) { Console.WriteLine($"disc: {_ok} OK, {_failed} FAILED"); return false; }

        // 2. it drains: host only gains, light only grows, nothing is lost on the way
        int host = w.Discs[0].Host;
        double fed = w.Discs[0].Fed, hostMass = w.M[host], light = w.DiscRadiatedEnergy;
        bool hostGrows = true, lightGrows = true, finite = true;
        for (int step = 0; step < 4000; step++)
        {
            w.Advance(.5);
            if (w.M[host] < hostMass) hostGrows = false;
            if (w.DiscRadiatedEnergy < light) lightGrows = false;
            if (!double.IsFinite(w.M[host]) || !double.IsFinite(w.DiscRadiatedEnergy)) finite = false;
            hostMass = w.M[host]; light = w.DiscRadiatedEnergy;
        }
        now = Ledger(w);
        Check("host mass never falls", hostGrows);
        Check("radiated energy never falls", lightGrows && light >= 0, $"{light:R}");
        Check("numbers stay finite", finite);
        Check("mass closes after 4000 steps", Close(start.Mass, now.Mass, 1e-12), $"{start.Mass:R} -> {now.Mass:R}");
        Check("ice closes after 4000 steps", Close(start.Ice, now.Ice, 1e-10), $"{start.Ice:R} -> {now.Ice:R}");
        Check("angular momentum closes after 4000 steps", Close(start.L, now.L, 1e-9), $"{start.L:R} -> {now.L:R}");
        Console.WriteLine($"      year {w.Year:G6}: swallowed {w.DiscAccretedMass / fed:P2} of the debris, disc holds {(w.Discs.Count > 0 ? w.Discs[0].Total / fed : 0):P2}, rings {(w.Discs.Count > 0 ? w.Discs[0].Cells : 0)}");

        // 3. a long jump uses the same law
        w.Do(new Command(CmdKind.FastForward, Amount: 1000));
        w.Advance(.5);
        now = Ledger(w);
        Check("mass closes after a 1000 year jump", Close(start.Mass, now.Mass, 1e-12), $"{start.Mass:R} -> {now.Mass:R}");
        Check("angular momentum closes after the jump", Close(start.L, now.L, 1e-9), $"{start.L:R} -> {now.L:R}");
        Check("most of the debris is swallowed after 1000 years", w.DiscAccretedMass > .5 * fed, $"{w.DiscAccretedMass / fed:P2}");
        Console.WriteLine($"      year {w.Year:G6}: swallowed {w.DiscAccretedMass / fed:P2}, disc holds {(w.Discs.Count > 0 ? w.Discs[0].Total / fed : 0):P2}, radiated {w.DiscRadiatedEnergy:G6}, grid residual E {w.DiscResidualEnergy:G6}");

        // 4. cold debris is untouched by this law, and so is its hash
        var cold = Scene(0.0125); var off = Scene(0.0125); off.C.DiscOn = 0;
        cold.Advance(.5); off.Advance(.5);
        Check("cold ring stays fragments", cold.Discs.Count == 0 && cold.Live > 1, $"live {cold.Live}, discs {cold.Discs.Count}");
        Check("cold ring runs as with the law off", cold.Live == off.Live && cold.M[0] == off.M[0]);
        var plain = World.SolSystem(200, 1234); var same = World.SolSystem(200, 1234);
        for (int step = 0; step < 50; step++) { plain.Advance(.5); same.Advance(.5); }
        Check("stock scene repeats", plain.Hash() == same.Hash() && plain.Discs.Count == 0, $"{plain.Hash():X16}");

        Console.WriteLine($"disc: {_ok} OK, {_failed} FAILED");
        return _failed == 0;
    }
}
