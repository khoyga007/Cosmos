using System;
using System.Collections.Generic;

namespace Cosmos.Core;

public sealed partial class Consts
{
    public double HypernovaMinSolar = 25; // [P] Nomoto 2006 collapsar eligibility, not sufficient without spin.
    // [P] Guetta & Della Valle 2007: HN ~7% of Ibc; Cappellaro1999 table4 Sbc-Sd: Ibc .14, II .86 SNu.
    // Salpeter 8..100 Suns: fraction >=25 ~0.188. Conditional proxy ~0.052; NOT a measured mass-bin rate.
    public double HypernovaChance = .07 * .14 / (.14 + .86) * (Math.Pow(8, -1.35) - Math.Pow(100, -1.35)) / (Math.Pow(25, -1.35) - Math.Pow(100, -1.35));
    public double HypernovaDamage = 200; // [P] 10 x SN, 1e52 vs 1e51 erg (Nomoto 2006); toy impact scale.
    public double HypernovaRange = World.SolDist(10 * Math.Sqrt(10)); // [P] same fluence, physical radius x sqrt(10).
    public double NeutronTovSolar = 2.2; // [P] Rezzolla2017 ~2.16 (+.17/-.15), cold nonrotating proxy.
    public double KilonovaEjectaSolar = .03; // [P] GW170817 .01-.05 Suns; grouped dynamical+wind model.
    public double KilonovaRange = World.SolDist(1); // [P] kinetic ~1e49 erg, .1 x SN physical range at same dose.
    public double KilonovaDamage = .2; // [P] kinetic-energy ratio ~.01 x SN; no radiative transport.
    public double NeutronPhysicalRadiusKm = 12; // [P] distinct from drawn radius, Foucart2018 calibrated C=.13..182.
    public double EjectaSpeedKmS = 10000; // [P] SN shell ~1e4 km/s; white-envelope speed below.
    public double EnvelopeSpeedKmS = 20; // [P] AGB/planetary nebula wind ~10..30 km/s.
    public double KilonovaSpeedKmS = 60000; // [P] GW170817 blue ejecta ~.2c, grouped representative speed.
    public double GrbLongEnergyIsoJ = 5e45; // [P] ~5e52 erg, typical long GRB energy; Thomas2005 order 1e53 erg.
    public double GrbShortEnergyIsoJ = 1e43; // [P] ~1e50 erg; Piran/Jimenez2014 short population is weaker.
    public double GrbHalfAngleDeg = 7.5; // [P] representative narrow jet, 5..10 degrees.
    public double GrbFluenceJm2 = 1e5; // [P] 100 kJ/m2, Piran/Jimenez2014 and Thomas2005; atmospheric proxy.
}

public sealed record MergerRule(StarPhase First, StarPhase Second, bool RequiresDisruption = false);
public sealed record BurstRule(string Id, StarParameter EnergyIso);
public readonly record struct GammaBurst(double Year, int Source, int Generation, string Id, double X, double Y,
    double AxisX, double AxisY, double AxisZ, double HalfAngleRad, double EnergyIsoJ, double FluenceJm2)
{
    public double DoseRadiusMetres => Math.Sqrt(EnergyIsoJ / (4 * Math.PI * FluenceJm2));
    public double FluenceAt(double metres) => metres > 0 ? EnergyIsoJ / (4 * Math.PI * metres * metres) : double.PositiveInfinity;
}

public sealed partial class World
{
    readonly List<GammaBurst> _gammaBursts = new();
    public IReadOnlyList<GammaBurst> GammaBursts => _gammaBursts.AsReadOnly();

    // Foucart2018 eq4/6, chi=0 -> ISCO=6. Assumes gravitational mass ~= baryonic mass;
    // outside mass caps a fiducial .03-Sun ejecta recipe; this is not a calibrated unbound-mass fit.
    double DisruptedMass(StarPhase a, double ma, StarPhase b, double mb)
    {
        double ns = a == StarPhase.NeutronStar ? ma : mb, bh = a == StarPhase.BlackHole ? ma : mb;
        if (!(ns > 0) || !(bh > 0) || !(C.NeutronPhysicalRadiusKm > 0)) return 0;
        double q = bh / ns, eta = q / ((1 + q) * (1 + q));
        double compact = 1.4765 * (ns / C.StarSolarMass) / C.NeutronPhysicalRadiusKm;
        return ns * Math.Pow(Math.Max(0, .406 * (1 - 2 * compact) / Math.Cbrt(eta) - .139 * 6 * compact / eta + .255), 1.761);
    }

    void EmitBurst(int i, BurstRule burst)
    {
        double z = 2 * Next() - 1, phi = Math.Tau * Next(), r = Math.Sqrt(Math.Max(0, 1 - z * z));
        var record = new GammaBurst(Year, i, Gen[i], burst.Id, X[i], Y[i], r * Math.Cos(phi), r * Math.Sin(phi), z,
            C.GrbHalfAngleDeg * Math.PI / 180, burst.EnergyIso.Read(C), C.GrbFluenceJm2);
        _gammaBursts.Add(record); // axis +/- gives TWO jets; never damage the home system here.
        LogEvent(i, "stars", burst.Id, record.EnergyIsoJ, record.HalfAngleRad, record.DoseRadiusMetres);
        if (Chronicle.Count == ChronicleCapacity) Chronicle.RemoveAt(0);
        Chronicle.Add((-1, new RuleEvent(Year, i, "stars", burst.Id, record.EnergyIsoJ, record.HalfAngleRad, record.DoseRadiusMetres)));
    }

    // Shell interception uses physical solid angle, not the enlarged draw radii or 2D arc length.
    // ponytail: instantaneous optically thin shell; transit time, shielding and hydrodynamics need a later solver.
    void DistributeEjecta(int source, string cause, double expelled, double[] matter, double speedKmS)
    {
        double px = expelled * Vx[source], py = expelled * Vy[source], remaining = expelled;
        var left = (double[])matter.Clone();
        double[] weights = new double[N]; double sum = 0;
        for (int j = 0; j < N; j++)
        {
            if (j == source || !Alive[j] || IsShip(j)) continue;
            double dx = X[j] - X[source], dy = Y[j] - Y[source], d = Math.Sqrt(dx * dx + dy * dy);
            if (!(d > 0)) continue;
            double distanceAU = Math.Pow(d / 45, 1 / .62);
            double radiusKm = StarPhaseOf(j) switch
            {
                StarPhase.RedGiant => GiantRadiusFactor(j) * 696340,
                StarPhase.None => C.EarthRadiusKm * R[j] / EarthRadiusRef,
                _ => StarRadius(j) / SolarRadius * 696340
            };
            double ratio = radiusKm / (149597870.7 * distanceAU);
            weights[j] = Math.Min(1, ratio * ratio / 4); sum += weights[j];
        }
        for (int j = 0; j < N; j++)
        {
            double f = weights[j] / Math.Max(1, sum); if (!(f > 0)) continue;
            double taken = expelled * f, old = M[j], dx = X[j] - X[source], dy = Y[j] - Y[source], d = Math.Sqrt(dx * dx + dy * dy);
            double au = Math.Pow(d / 45, 1 / .62);
            // Physical km/s -> AU/year -> squeezed distance/time via the local map derivative.
            double speed = speedKmS * 31557600 / 149597870.7 * 45 * .62 * Math.Pow(au, -.38) / C.YearTime;
            double vx = Vx[source] + speed * dx / d, vy = Vy[source] + speed * dy / d;
            Vx[j] = (old * Vx[j] + taken * vx) / (old + taken); Vy[j] = (old * Vy[j] + taken * vy) / (old + taken);
            for (int e = 0; e < ElementCount; e++) { double cell = matter[e] * f; Comp[j * ElementCount + e] += cell; left[e] -= cell; }
            M[j] += taken; remaining -= taken; px -= taken * vx; py -= taken * vy;
            // Ejecta add matter without retroactively evolving another star at a historical event year.
            SetRadius(j);
        }
        Escape(source, cause, Math.Max(0, remaining), left, px, py);
    }

    void HashBursts(Action<ulong> mix)
    {
        void Number(double x) => mix(BitConverter.DoubleToUInt64Bits(x));
        mix((ulong)_gammaBursts.Count);
        foreach (var b in _gammaBursts)
        {
            Number(b.Year); mix((ulong)b.Source); mix((ulong)b.Generation);
            mix((ulong)b.Id.Length); foreach (char c in b.Id) mix(c);
            Number(b.X); Number(b.Y); Number(b.AxisX); Number(b.AxisY); Number(b.AxisZ);
            Number(b.HalfAngleRad); Number(b.EnergyIsoJ); Number(b.FluenceJm2);
        }
    }
}
