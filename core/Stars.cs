// Stellar evolution, SPEC 8. No stored type: phase is read from mass, initial mass and burnt fuel.
// NASA overview/bounds: https://science.nasa.gov/universe/stars/types/
// Toy IFMR: https://vice-astro.readthedocs.io/en/latest/science_documentation/SSPs/index.html
// All fits are macro approximations, not a stellar-structure solver. Symmetric ejecta leave the simulated
// domain: their matter and momentum remain in a hashed ledger, rather than becoming a new massive "star".
using System;

namespace Cosmos.Core;

public sealed partial class Consts
{
    public double StarSolarMass = 50;             // [P] units: 1 solar mass
    public double StarLifeYears = 1e10;           // [P] real Sun main-sequence lifetime ~10 Gyr
    public double StarLifeExponent = 2.6;         // [P] real toy fits ~2.5..2.8; 10 Suns live ~20 Myr
    public double StarLifeScale = 1;              // [P] 1 = real years; god's clock dial
    public double StarGiantFraction = 0.05;       // [P] post-MS duration varies; toy 5% of main sequence
    public double StarGiantRadius = 100;          // [P] red giants reach ~100 solar radii or more
    public double StarGiantLight = 1000;          // [P] luminous giants ~10^2..10^4 solar luminosities
    public double StarGiantTemperature = 3500;    // [P] red giant/supergiant surface ~3000..4000 K
    public double StarGiantMinimumBoost = 1.1;    // [P] macro lower bound on luminosity increase
    public double StarSolarTemperature = 5772;    // [P] Sun effective temperature ~5772 K
    public double StarRadiusPower = 0.8;          // [P] approximate MS radius-mass relation
    public double StarSolarRadius = 1.5 * Math.Cbrt(50 / 0.3); // [P] existing squeezed radius; real Sun ~696340 km
    public double StarLowMass = 0.43;             // [P] low-mass luminosity fit boundary, solar masses
    public double StarLowLightScale = 0.23;       // [P] approximate L/Lsun = 0.23 m^2.3 at low mass
    public double StarLowLightPower = 2.3;
    public double StarBrownMin = 13 * 317.8 * 1.5e-4; // [P] conventional deuterium boundary ~13 Jupiters
    public double StarBrownRadius = 0.1;          // [P] brown dwarfs ~1 Jupiter radius = 0.1 solar radius
    public double StarBrownLight = 1e-4;          // [P] faint BD reference at 0.05 solar masses
    public double StarBrownReference = 0.05;
    public double StarBrownCoolYears = 1e9;       // [P] brown dwarfs cool over Gyr
    public double StarCoolingPower = 1.3;         // [P] macro cooling fit, not a detailed cooling track
    public double StarWhiteLimit = 8;             // [P] initial mass below ~8 Suns -> white dwarf
    public double StarNeutronLimit = 20;          // [P] ~8..20 Suns -> NS; heavier -> BH (metallicity ignored)
    public double StarWhiteSlope = 0.109;         // [P] empirical initial-final mass fit (solar units)
    public double StarWhiteIntercept = 0.394;
    public double StarWhiteRadius = 0.00916;      // [P] Earth radius / Sun radius
    public double StarWhiteLight = 0.01;          // [P] young WD cooling normalization
    public double StarWhiteCoolYears = 1e9;
    public double StarNeutronMass = 1.4;          // [P] typical neutron star mass ~1.4 Suns
    public double StarNeutronRadius = 20.0 / 696340; // [P] NS radius ~10..20 km
    public double StarNeutronLight = 0.02;         // [P] young NS thermal cooling normalization
    public double StarNeutronCoolYears = 1e6;
    public double StarBlackFraction = 0.25;       // [P] retained mass strongly depends on progenitor/winds
    public double StarBlackMin = 3;              // [P] stellar BH typically >=~3 solar masses
    public double StarSchwarzschild = 2.953 / 696340; // [P] 2GM/c^2: ~2.953 km per solar mass
    public double StarNovaRange = World.SolDist(10); // [P] real SN biosphere danger tens of pc; toy range 10 AU
    public double StarNovaDamage = 20;            // [P] geometric impact-like attenuation in toy units
    public double StarRedTemperature = 3700;      // [P] M/K boundary, Kelvin
    public double StarOrangeTemperature = 5200;   // [P] K/G
    public double StarYellowTemperature = 6000;   // [P] G/F
    public double StarWhiteTemperature = 7500;    // [P] F/A
    public double StarBlueWhiteTemperature = 10000; // [P] A/B
    public double StarBlueTemperature = 30000;    // [P] B/O
}

public enum StarPhase { None, BrownDwarf, MainSequence, RedGiant, WhiteDwarf, NeutronStar, BlackHole }
public enum StarSpectrum { None, M, K, G, F, A, B, O }

public sealed partial class World
{
    public double[] StarAge = null!, StarFuel = null!, StarInitialMass = null!, StarCoolingAge = null!;
    double[] _starUpdated = null!, _starRate = null!;
    Rule _starRule = null!;
    bool _updatingStars;
    public double StellarEjectaMass { get; private set; }
    public double StellarEjectaPx { get; private set; }
    public double StellarEjectaPy { get; private set; }
    public readonly double[] StellarEjectaMatter = new double[NElem];

    double GiantEnd => 1 + Math.Max(0, C.StarGiantFraction);
    double SolarRadius => C.StarSolarRadius * C.RadiusScale / 1.5;

    void InitStars(int capacity)
    {
        StarAge = new double[capacity]; StarFuel = new double[capacity]; StarInitialMass = new double[capacity];
        StarCoolingAge = new double[capacity]; _starUpdated = new double[capacity]; _starRate = new double[capacity];
    }

    void InitStarRule()
    {
        _starRule = new Rule("stars", "M,StarAge,StarFuel,Star*,Comp", "StarAge,StarFuel,M,R,Comp,ejecta,Life,Pop", 1000, w => w.UpdateStars());
        Rules.Add(_starRule); // before temperature: new light/geometry is visible to the following rules
    }

    void ResetStars(int i)
    {
        StarAge[i] = StarFuel[i] = StarCoolingAge[i] = 0;
        StarInitialMass[i] = M[i] >= Math.Min(C.StarBrownMin, C.StarMass) ? M[i] : 0;
        _starUpdated[i] = Year;
        _starRate[i] = M[i] >= C.StarMass ? 1 / StarLifetime(M[i]) : 0;
    }

    public double StarLifetime(double mass) => mass > 0 && C.StarLifeScale > 0 && C.StarSolarMass > 0
        ? C.StarLifeYears * C.StarLifeScale * Math.Pow(mass / C.StarSolarMass, -C.StarLifeExponent) : double.PositiveInfinity;

    public StarPhase StarPhaseOf(int i)
    {
        if (!Alive[i] || IsShip(i)) return StarPhase.None;
        if (StarInitialMass[i] >= C.StarMass && StarFuel[i] >= GiantEnd)
        {
            double birth = StarInitialMass[i] / C.StarSolarMass;
            return birth < C.StarWhiteLimit ? StarPhase.WhiteDwarf : birth <= C.StarNeutronLimit ? StarPhase.NeutronStar : StarPhase.BlackHole;
        }
        if (M[i] >= C.StarMass) return StarFuel[i] >= 1 ? StarPhase.RedGiant : StarPhase.MainSequence;
        return M[i] >= C.StarBrownMin ? StarPhase.BrownDwarf : StarPhase.None;
    }

    double MainLight(double mass)
    {
        double m = mass / C.StarSolarMass;
        return m < C.StarLowMass ? C.StarLowLightScale * Math.Pow(m, C.StarLowLightPower) : Math.Pow(m, C.LuminosityExponent);
    }

    double GiantLight(double mass) => Math.Max(MainLight(mass) * C.StarGiantMinimumBoost,
        C.StarGiantLight * Math.Pow(mass / C.StarSolarMass, 2 * C.StarRadiusPower));

    public double StarLuminosity(int i) => StarPhaseOf(i) switch
    {
        StarPhase.MainSequence => MainLight(M[i]),
        StarPhase.RedGiant => GiantLight(M[i]),
        StarPhase.BrownDwarf => C.StarBrownLight * Math.Pow(M[i] / (C.StarSolarMass * C.StarBrownReference), 2)
            * Cool(StarAge[i], C.StarBrownCoolYears),
        StarPhase.WhiteDwarf => C.StarWhiteLight * Cool(StarCoolingAge[i], C.StarWhiteCoolYears),
        StarPhase.NeutronStar => C.StarNeutronLight * Cool(StarCoolingAge[i], C.StarNeutronCoolYears),
        _ => 0
    };

    double Cool(double age, double scale) => scale > 0 ? Math.Pow(1 + Math.Max(0, age) / scale, -C.StarCoolingPower) : 0;

    public double StarRadius(int i) => StarPhaseOf(i) switch
    {
        StarPhase.MainSequence => SolarRadius * Math.Pow(M[i] / C.StarSolarMass, C.StarRadiusPower),
        StarPhase.RedGiant => SolarRadius * Math.Max(Math.Pow(M[i] / C.StarSolarMass, C.StarRadiusPower) * C.StarGiantRadius,
            Math.Sqrt(GiantLight(M[i])) * Math.Pow(C.StarSolarTemperature / C.StarGiantTemperature, 2)),
        StarPhase.BrownDwarf => SolarRadius * C.StarBrownRadius,
        StarPhase.WhiteDwarf => SolarRadius * C.StarWhiteRadius * Math.Cbrt(C.StarWhiteIntercept * C.StarSolarMass / M[i]),
        StarPhase.NeutronStar => SolarRadius * C.StarNeutronRadius,
        StarPhase.BlackHole => SolarRadius * C.StarSchwarzschild * M[i] / C.StarSolarMass * Math.Max(0, C.G),
        _ => 0
    };

    public double StarSurfaceTemperature(int i)
    {
        double r = StarRadius(i) / SolarRadius, light = StarLuminosity(i);
        return r > 0 && light > 0 ? C.StarSolarTemperature * Math.Sqrt(Math.Sqrt(light / (r * r))) : 0;
    }

    public StarSpectrum StarSpectralClass(int i)
    {
        double t = StarSurfaceTemperature(i);
        return t <= 0 ? StarSpectrum.None : t < C.StarRedTemperature ? StarSpectrum.M : t < C.StarOrangeTemperature ? StarSpectrum.K
            : t < C.StarYellowTemperature ? StarSpectrum.G : t < C.StarWhiteTemperature ? StarSpectrum.F
            : t < C.StarBlueWhiteTemperature ? StarSpectrum.A : t < C.StarBlueTemperature ? StarSpectrum.B : StarSpectrum.O;
    }

    public uint StarColour(int i) => StarSpectralClass(i) switch
    {
        StarSpectrum.M => 0xFF7755, StarSpectrum.K => 0xFFB36D, StarSpectrum.G => 0xFFF0C2,
        StarSpectrum.F => 0xFFF8ED, StarSpectrum.A => 0xEEF3FF, StarSpectrum.B => 0xBACFFF, StarSpectrum.O => 0x8EB5FF, _ => 0
    };

    // Radius recalculation also settles the previous fuel rate before a god's mass/constant edit takes effect.
    void SyncStar(int i)
    {
        if (_updatingStars || !Alive[i] || IsShip(i)) return;
        if (StarInitialMass[i] <= 0 && M[i] >= C.StarBrownMin) ResetStars(i);
        if (_starRule != null && !_starRule.Enabled) return;
        if (StarInitialMass[i] > 0) EvolveStar(i, Year);
    }

    void MergeStars(int k, int d)
    {
        SyncStar(k); SyncStar(d);
        double m = M[k] + M[d];
        if (!(m > 0) || StarInitialMass[k] <= 0 && StarInitialMass[d] <= 0) return;
        // Envelope accretion cannot restore an exhausted core. Unevolved stars still mix fuel by mass.
        double coreFloor = Math.Max(StarFuel[k] >= GiantEnd ? GiantEnd : StarFuel[k] >= 1 ? 1 : 0,
            StarFuel[d] >= GiantEnd ? GiantEnd : StarFuel[d] >= 1 ? 1 : 0);
        StarAge[k] = (StarAge[k] * M[k] + StarAge[d] * M[d]) / m;
        StarFuel[k] = Math.Max(coreFloor, (StarFuel[k] * M[k] + StarFuel[d] * M[d]) / m);
        StarCoolingAge[k] = (StarCoolingAge[k] * M[k] + StarCoolingAge[d] * M[d]) / m;
        StarInitialMass[k] = (StarInitialMass[k] > 0 ? StarInitialMass[k] : M[k]) + (StarInitialMass[d] > 0 ? StarInitialMass[d] : M[d]);
        _starUpdated[k] = Year;
    }

    void UpdateStars()
    {
        for (int i = 0; i < N; i++) if (Alive[i] && !IsShip(i) && StarInitialMass[i] > 0) EvolveStar(i, Year);
    }

    double NextStarBoundary()
    {
        if (!_starRule.Enabled) return double.PositiveInfinity;
        double next = double.PositiveInfinity;
        for (int i = 0; i < N; i++)
        {
            if (!Alive[i] || IsShip(i) || !(_starRate[i] > 0) || StarFuel[i] >= GiantEnd) continue;
            double mark = StarFuel[i] < 1 ? 1 : GiantEnd;
            next = Math.Min(next, _starUpdated[i] + (mark - StarFuel[i]) / _starRate[i]);
        }
        return next;
    }

    void EvolveStar(int i, double now)
    {
        double start = _starUpdated[i], dt = Math.Max(0, now - start), before = StarFuel[i], rate = _starRate[i], end = GiantEnd;
        double after = Math.Min(end, before + dt * rate);
        double giantAt = rate > 0 ? start + (1 - before) / rate : double.PositiveInfinity;
        double deathAt = rate > 0 ? start + (end - before) / rate : double.PositiveInfinity;
        bool giant = before < 1 && now >= giantAt, death = before < end && now >= deathAt;
        if (giant) after = Math.Max(1, after);
        if (death) after = end;
        _updatingStars = true;
        double savedYear = Year;
        try
        {
            StarAge[i] += dt;
            if (giant)
            {
                Year = giantAt; StarFuel[i] = 1; SetRadius(i);
                LogEvent(i, "stars", "star.giant", StarInitialMass[i] / C.StarSolarMass, R[i], StarLuminosity(i));
            }
            if (death)
            {
                Year = deathAt; StarFuel[i] = end;
                bool nova = StarInitialMass[i] / C.StarSolarMass >= C.StarWhiteLimit;
                if (nova)
                {
                    LogEvent(i, "stars", "star.nova", StarInitialMass[i] / C.StarSolarMass, M[i], C.StarNovaRange);
                    for (int j = 0; j < N; j++) if (j != i && Alive[j] && IsWorld(j))
                    {
                        double dx = X[j] - X[i], dy = Y[j] - Y[i], distance = Math.Sqrt(dx * dx + dy * dy);
                        if (distance < C.StarNovaRange && C.StarNovaRange > 0)
                            Impact(j, C.ImpactScale * C.StarNovaDamage * Math.Pow(1 - distance / C.StarNovaRange, 2));
                    }
                }
                ShedEnvelope(i); SetRadius(i);
                string code = StarPhaseOf(i) switch { StarPhase.WhiteDwarf => "star.remnant.white", StarPhase.NeutronStar => "star.remnant.neutron", _ => "star.remnant.black" };
                LogEvent(i, "stars", code, StarInitialMass[i] / C.StarSolarMass, M[i] / C.StarSolarMass, StellarEjectaMass);
                StarCoolingAge[i] += Math.Max(0, now - Year);
            }
            else if (before >= end) StarCoolingAge[i] += dt;
            StarFuel[i] = after; _starUpdated[i] = now;
            _starRate[i] = M[i] >= C.StarMass && after < end ? 1 / StarLifetime(M[i]) : 0;
            SetRadius(i);
        }
        finally { Year = savedYear; _updatingStars = false; }
    }

    void ShedEnvelope(int i)
    {
        double birth = StarInitialMass[i] / C.StarSolarMass;
        double final = birth < C.StarWhiteLimit ? C.StarWhiteIntercept + C.StarWhiteSlope * birth
            : birth <= C.StarNeutronLimit ? C.StarNeutronMass : Math.Max(C.StarBlackMin, birth * C.StarBlackFraction);
        double keepMass = Math.Clamp(final * C.StarSolarMass, 0, M[i]), lost = M[i] - keepMass, fraction = keepMass / M[i];
        StellarEjectaMass += lost; StellarEjectaPx += lost * Vx[i]; StellarEjectaPy += lost * Vy[i];
        for (int e = 0; e < NElem; e++)
        {
            int o = i * NElem + e; double kept = Comp[o] * fraction;
            StellarEjectaMatter[e] += Comp[o] - kept; Comp[o] = kept;
        }
        M[i] = keepMass; // isotropic impulsive loss: velocity unchanged, future gravity changes the orbit
    }

    void HashStars(Action<ulong> mix)
    {
        void number(double n) => mix(BitConverter.DoubleToUInt64Bits(n));
        for (int i = 0; i < N; i++) if (Alive[i])
        {
            number(StarAge[i]); number(StarFuel[i]); number(StarInitialMass[i]); number(StarCoolingAge[i]); number(_starUpdated[i]); number(_starRate[i]);
        }
        number(StellarEjectaMass); number(StellarEjectaPx); number(StellarEjectaPy);
        foreach (double m in StellarEjectaMatter) number(m);
    }
}
