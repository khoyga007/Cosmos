// SPEC S2 (2): conservation measured from outside, on scenes this file builds itself.
// Nothing here reuses the audit's helpers or the single-body scenes StarChecks/StarEventChecks lean on:
// one ledger over the whole scene — mass, every element, momentum — plus whatever the star events threw out of
// it. A rule that moves matter between objects keeps the ledger flat; one that invents or drops it does not.
using System;
using System.Collections.Generic;
using System.Linq;
using Cosmos.Core;

static class ConserveChecks
{
    // These tallies are exact sums, not statistics: the band is floating-point rounding, not a physical fudge.
    const double Tight = 1e-11;
    // The stock system carries two sinks that no counter can see: a ship is made out of nothing and unmade with
    // no line (Civ.cs:151/161/194/249), and lost gas and sublimated ice shrink a body outright (Kinds.cs:203 and
    // Kinds.cs:267/274 are the only writes that take mass off a body without a merge, an ejection or a Kill).
    // Where they are live the scene is held to this band instead, and the exact deficit is printed with it.
    const double Loose = 1e-6;

    static bool ok;

    public static bool Run()
    {
        ok = true;
        void Check(bool pass, string line) { ok &= pass; Console.WriteLine($"{(pass ? "OK    " : "FAILED")} conserve: {line}"); }
        void Note(string line) => Console.WriteLine($"note: conserve: {line}");

        // A world of its own with the ejection recipe handed in: every mode the enum has, including the
        // transmutation branch no shipped row uses.
        var cases = new (string Label, EjectionRule Rule, IReadOnlyDictionary<string, double>? Mix, int Planets)[]
        {
            ("keeps half a sun, fixed", new EjectionRule(EjectionMode.RetainedSolarMass, Intercept: new StarParameter(Value: .5)), null, 0),
            ("slope .2 of birth, never under half a sun", new EjectionRule(EjectionMode.RetainedSolarMass, Slope: new StarParameter(Value: .2), Minimum: new StarParameter(Value: .5)), null, 2),
            ("ejects half by fraction", new EjectionRule(EjectionMode.Fraction, Fraction: new StarParameter(Value: .5)), null, 0),
            ("ejects everything by fraction", new EjectionRule(EjectionMode.Fraction, Fraction: new StarParameter(Value: 1)), null, 0),
            ("half out, split rock/metal", new EjectionRule(EjectionMode.Fraction, Fraction: new StarParameter(Value: .5)),
                new Dictionary<string, double>(StringComparer.Ordinal) { { "rock", .75 }, { "metal", .25 } }, 2),
        };
        int row = 0;
        foreach (var (label, rule, mix, planets) in cases)
        {
            row++;
            var w = Star(new[] { Death($"probe.eject.{row}", rule, mix) }, 50, planets);
            var before = Of(w);
            w.Do(new Command(CmdKind.FastForward, Amount: w.StarLifetime(50) * 1.2));
            var after = Of(w);
            Check(w.StellarEjectaMass > 0,
                $"{label}: the row fired — {w.StellarEjectaMass:G6} of {before.Mass:G6} left the star, {w.Live} body(ies) still alive");
            Check(Math.Abs(after.Mass - before.Mass) <= Tight * Scale(after.Mass, before.Mass),
                $"{label}: mass flat — {before.Mass:G10} → {after.Mass:G10} ({after.Mass - before.Mass:E3})");
            Check(Math.Abs(after.Total - before.Total) <= Tight * Scale(after.Total, before.Total),
                $"{label}: every element together flat — {after.Total - before.Total:E3} of {before.Total:G10}");
            Check(Math.Abs(after.Px - before.Px) <= Tight * Scale(after.Px, before.Px) && Math.Abs(after.Py - before.Py) <= Tight * Scale(after.Py, before.Py),
                $"{label}: momentum flat — dP ({after.Px - before.Px:E3}, {after.Py - before.Py:E3}) of ({before.Px:G6}, {before.Py:G6})");
        }

        // The shipped table is the one the game plays with. No row carries a mix, so the transmutation branch of
        // Eject has never run outside this probe — and a mix that is not a mix is refused before it can quietly
        // destroy part of the ejecta (StarEvents.cs:97).
        var stock = World.SolSystem(0, 1234);
        int mixed = stock.StarEvents.Count(r => r.Mix != null);
        Check(stock.StarEvents.All(r => r.Mix == null || Math.Abs(r.Mix.Values.Sum() - 1) <= 1e-11),
            $"every shipped ejecta mix is a mix: {stock.StarEvents.Count} row(s), {mixed} carrying one");
        Check(Refused(() => new World(16, 7, null, new[]
            {
                Death("probe.bad", new EjectionRule(EjectionMode.Fraction, Fraction: new StarParameter(Value: .5)),
                    new Dictionary<string, double>(StringComparer.Ordinal) { { "rock", .5 } }),
            })),
            "a mix summing to half is refused at construction, not absorbed as half the mass");

        // What a flat ledger is worth: prove it moves when mass really goes. A hand Remove takes a body out with
        // no tally at all, and the scene has to show exactly that much less.
        var three = new World(16, 3, null, new[] { Death("probe.never", new EjectionRule(EjectionMode.Fraction, Fraction: new StarParameter(Value: .5))) });
        three.Do(new Command(CmdKind.Create, Amount: 4, X: 2, Y: 0, Mix: Rocks));
        three.Do(new Command(CmdKind.Create, Amount: 7, X: -2, Y: 0, Mix: Rocks));
        int doomed = three.Do(new Command(CmdKind.Create, Amount: 3, X: 0, Y: 3, Mix: Rocks));
        var flat = Of(three);
        double taken = three.M[doomed];
        three.Do(new Command(CmdKind.Remove, Target: doomed));
        var torn = Of(three);
        Check(Math.Abs((torn.Mass - flat.Mass) + taken) <= 1e-12 && torn.Mass != flat.Mass,
            $"the ledger has teeth: removing one body of {taken:G6} shows as exactly that — {flat.Mass:G10} → {torn.Mass:G10}");

        // The stock system end to end: giant, engulfment, death, ejection. Same ledger, held to the stated band
        // because the two sinks above are live here.
        foreach (var (label, rocks, years) in new[]
        {
            ("sol, 0 rocks → giant", 0, 9.99e9),
            ("sol, 0 rocks → past the death", 0, 1.06e10),
            ("sol, 200 rocks → past the death", 200, 1.06e10),
        })
        {
            var w = World.SolSystem(rocks, 1234);
            var before = Of(w);
            w.Do(new Command(CmdKind.FastForward, Amount: years));
            var after = Of(w);
            int ships = 0;
            for (int i = 0; i < w.N; i++) if (w.Alive[i] && w.IsShip(i)) ships++;
            double band = Loose * before.Mass, made = ships * w.C.ShipMass;
            double dMass = after.Mass - before.Mass, dElem = after.Total - before.Total;
            double dPx = after.Px - before.Px, dPy = after.Py - before.Py;
            Check(dMass <= made + 1e-15 && dMass >= -band,
                $"{label}: the ledger only loses — {dMass:E3} of {before.Mass:G10} inside ±{band:E2} (ejecta {w.StellarEjectaMass:G6}, {ships} ship(s) adding {made:E2}, year {w.Year:E6})");
            Check(Math.Abs(dElem - made) <= band,
                $"{label}: the elements follow the mass — {dElem:E3} against {made:E2} for ships");
            Check(Math.Abs(dPx) <= band * before.Gmax && Math.Abs(dPy) <= band * before.Gmax,
                $"{label}: momentum inside the same band — dP ({dPx:E3}, {dPy:E3}), fastest body {before.Gmax:G4}");
            if (years > 1e10)
                Check(w.StellarEjectaMass > 0,
                    $"{label}: the scene reached the death and threw {w.StellarEjectaMass:G6} out of the star");
        }

        // The same stock scene with the two sinks switched off at the Const: now nothing may leave the ledger.
        var closed = World.SolSystem(200, 1234);
        closed.Do(new Command(CmdKind.SetConst, Name: "GasLossRate", Amount: 0));
        closed.Do(new Command(CmdKind.SetConst, Name: "CometSublimationRate", Amount: 0));
        var cb = Of(closed);
        closed.Do(new Command(CmdKind.FastForward, Amount: 1.06e10));
        var ca = Of(closed);
        Check(Math.Abs(ca.Mass - cb.Mass) <= Tight * Scale(cb.Mass, ca.Mass),
            $"sol, 200 rocks, both volatile sinks at 0: the whole stock scene is closed — {cb.Mass:G12} → {ca.Mass:G12} ({ca.Mass - cb.Mass:E3}) over {closed.Year:E6} years, {closed.Live} body(ies)");
        Check(Math.Abs(ca.Total - cb.Total) <= Tight * Scale(cb.Total, ca.Total),
            $"sol, 200 rocks, both volatile sinks at 0: every element together closed — {ca.Total - cb.Total:E3}");
        Check(Math.Abs(ca.Px - cb.Px) <= Loose * cb.Mass * cb.Gmax && Math.Abs(ca.Py - cb.Py) <= Loose * cb.Mass * cb.Gmax,
            $"sol, 200 rocks, both volatile sinks at 0: momentum inside the same band — dP ({ca.Px - cb.Px:E3}, {ca.Py - cb.Py:E3}), fastest body {cb.Gmax:G4}, band {Loose * cb.Mass * cb.Gmax:E2}");
        Note($"momentum is the one quantity the closed scene does not hold at rounding level: {Math.Sqrt((ca.Px - cb.Px) * (ca.Px - cb.Px) + (ca.Py - cb.Py) * (ca.Py - cb.Py)):E3} of {cb.Mass * cb.Gmax:G6} (mass x fastest body) over {closed.Year:E6} years, {closed.Merges} merge(s), {closed.Live} body(ies) — momentum is conserved inside Merge itself (World.cs:188-190), so the path that drops this is not named here [chưa kiểm: chưa tách được Gone() khỏi sai số tích phân]");

        // Which sink took the earlier deficit: one at a time.
        foreach (var (label, name) in new[] { ("gas loss off", "GasLossRate"), ("ice sublimation off", "CometSublimationRate") })
        {
            var w = World.SolSystem(200, 1234);
            w.Do(new Command(CmdKind.SetConst, Name: name, Amount: 0));
            var before = Of(w);
            w.Do(new Command(CmdKind.FastForward, Amount: 1.06e10));
            Note($"sol, 200 rocks, {label}: {Of(w).Mass - before.Mass:E3} of {before.Mass:G10} left the ledger");
        }
        return ok;
    }

    static double[] Rocks => new double[] { 0, 0, 1, 0, 0, 0 };

    static double Scale(double a, double b) => Math.Max(1e-30, Math.Max(Math.Abs(a), Math.Abs(b)));

    static StarEvent Death(string id, EjectionRule? rule, IReadOnlyDictionary<string, double>? mix = null) =>
        new(id, StarTransition.Death,
            Array.AsReadOnly(new[] { new ProgenitorCondition(MassComparison.GreaterOrEqual, new StarParameter(Value: 1)) }),
            Range: new StarParameter(Value: .5), Damage: new StarParameter(Value: 1),
            Ejection: rule, Mix: mix, Payload: StarEventPayload.Remnant);

    static World Star(IEnumerable<StarEvent> rows, double mass, int planets)
    {
        var w = new World(16, 7, null, rows);
        w.Do(new Command(CmdKind.Create, Amount: mass, X: 0, Y: 0, Vx: .02, Vy: -.03, Mix: new double[] { 1, 0, 0, 0, 0, 0 }));
        for (int k = 0; k < planets; k++)
            w.Do(new Command(CmdKind.CreateOrbiting, Target: 0, X: 3 + 2 * k, Y: 0, Amount: 5 * (k + 1), Mix: Rocks));
        return w;
    }

    static bool Refused(Action build)
    {
        try { build(); return false; }
        catch (ArgumentException) { return true; }
    }

    readonly struct Ledger
    {
        public readonly double Mass, Px, Py, Gmax;
        public readonly double[] Matter;
        public Ledger(double mass, double[] matter, double px, double py, double gmax) { Mass = mass; Matter = matter; Px = px; Py = py; Gmax = gmax; }
        public double Total => Matter.Sum();
    }

    // Everything the world holds, plus everything the star events have thrown out of it. A transfer between
    // objects moves entries around inside this sum and leaves it alone; only an invention or a loss moves it.
    static Ledger Of(World w)
    {
        double mass = w.StellarEjectaMass, px = w.StellarEjectaPx, py = w.StellarEjectaPy, gmax = 0;
        var matter = (double[])w.StellarEjectaMatter.Clone();
        for (int i = 0; i < w.N; i++)
        {
            if (!w.Alive[i]) continue;
            mass += w.M[i];
            px += w.M[i] * w.Vx[i];
            py += w.M[i] * w.Vy[i];
            double g = Math.Sqrt(w.Vx[i] * w.Vx[i] + w.Vy[i] * w.Vy[i]);
            if (g > gmax) gmax = g;
            for (int e = 0; e < w.ElementCount; e++) matter[e] += w.Comp[i * w.ElementCount + e];
        }
        return new Ledger(mass, matter, px, py, gmax);
    }
}
