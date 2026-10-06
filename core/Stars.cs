// Stellar evolution, SPEC 8. No stored type: phase is read from mass, initial mass and burnt fuel.
// NASA overview/bounds: https://science.nasa.gov/universe/stars/types/
// Toy IFMR: https://vice-astro.readthedocs.io/en/latest/science_documentation/SSPs/index.html
// All fits are macro approximations, not a stellar-structure solver. Symmetric ejecta leave the simulated
// domain: their matter and momentum remain in a hashed ledger, rather than becoming a new massive "star".
using System;

namespace Cosmos.Core;

// ProgenitorMass uses world mass units (50 = one Sun); both ages use years. Fuel is the burnt fraction, 0..GiantEnd.
public readonly record struct StellarState(double AgeYears, double Fuel, double ProgenitorMass, double CoolingAgeYears = 0);

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
    public double StarSolarRadiusAU = 1.0 / 215; // [P] real solar radius ~0.00465 AU; giant envelope mapped onto the orbital distance scale
    public double StarLowMass = 0.43;             // [P] low-mass luminosity fit boundary, solar masses
    public double StarLowLightScale = 0.23;       // [P] approximate L/Lsun = 0.23 m^2.3 at low mass
    public double StarLowLightPower = 2.3;         // [P] low-mass luminosity fit exponent
    public double StarBrownMin = 13 * 317.8 * 1.5e-4; // [P] conventional deuterium boundary ~13 Jupiters
    public double StarBrownRadius = 0.1;          // [P] brown dwarfs ~1 Jupiter radius = 0.1 solar radius
    public double StarBrownLight = 1e-4;          // [P] faint BD reference at 0.05 solar masses
    public double StarBrownReference = 0.05;      // [P] mid-band brown dwarf, solar masses
    public double StarBrownCoolYears = 1e9;       // [P] brown dwarfs cool over Gyr
    public double StarCoolingPower = 1.3;         // [P] macro cooling fit, not a detailed cooling track
    public double StarWhiteLimit = 8;             // [P] initial mass below ~8 Suns -> white dwarf
    public double StarNeutronLimit = 20;          // [P] ~8..20 Suns -> NS; heavier -> BH (metallicity ignored)
    public double StarWhiteSlope = 0.109;         // [P] empirical initial-final mass fit (solar units)
    public double StarWhiteIntercept = 0.394;     // [P] empirical IFMR intercept, solar masses
    public double StarWhiteRadius = 0.00916;      // [P] Earth radius / Sun radius
    public double StarWhiteLight = 0.00916 * 0.00916 * Math.Pow(200000.0 / 5772, 4); // [P] hot WD birth, reference radius, Stefan-Boltzmann
    public double StarWhiteCoolYears = 1.05e8 / 14; // [P] Mestel coefficient for a C/O core (mean ion mass 14)
    public double StarNeutronMass = 1.4;          // [P] typical neutron star mass ~1.4 Suns
    public double StarNeutronRadius = 20.0 / 696340; // [P] NS radius ~10..20 km
    public double StarNeutronLight = Math.Pow(20.0 / 696340, 2) * Math.Pow(2e6 / 5772, 4) * Math.Pow(2, 1.3); // [P] 2 MK at 330 yr, Cas A-like thermal normalization
    public double StarNeutronCoolYears = 330;     // [P] finite hot origin; effective cooling fit, not universal NS microphysics
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
    public readonly double[] StellarEjectaMatter;

    double GiantEnd => 1 + Math.Max(0, C.StarGiantFraction);
    double SolarRadius => C.StarSolarRadius * C.RadiusScale / 1.5;
    bool StarsEnabled => _starRule != null && _starRule.Enabled && Rules.Contains(_starRule);

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

    public double StarLifetime(double mass) => mass > 0 && mass >= C.StarMass && C.StarLifeYears > 0 && C.StarLifeScale > 0 && C.StarSolarMass > 0
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
        StarPhase.WhiteDwarf or StarPhase.NeutronStar => RemnantLight(i, StarCoolingAge[i]),
        _ => 0
    };

    double Cool(double age, double scale) => scale > 0 ? Math.Pow(1 + Math.Max(0, age) / scale, -C.StarCoolingPower) : 0;

    // Pols 2011 sec.12.3: tau=(1.05e8/Aion)*(L/M)^(-5/7). Shift its age origin to a finite hot birth.
    // https://inpp.ohio.edu/~meisel/ASTR4201/file/StellarStructureAndEvolution_OnnoPols2011.pdf
    // NS normalization: https://chandra.cfa.harvard.edu/photo/2009/cassio/index.html
    // Its effective power-law is a macro fit; envelopes, neutrino processes and superfluidity vary between NSs.
    double RemnantLight(int i, double age)
    {
        if (StarPhaseOf(i) == StarPhase.NeutronStar) return C.StarNeutronLight * Cool(age, C.StarNeutronCoolYears);
        double birth = C.StarWhiteLight * Math.Pow(C.StarWhiteIntercept * C.StarSolarMass / M[i], 2.0 / 3);
        if (!(birth > 0) || !(C.StarWhiteCoolYears > 0)) return 0;
        double origin = C.StarWhiteCoolYears * Math.Pow(birth / (M[i] / C.StarSolarMass), -5.0 / 7);
        return birth * Math.Pow(1 + Math.Max(0, age) / origin, -7.0 / 5);
    }

    // During a jump only cooling remnants vary continuously. Find the next heat/water edge
    // using the same orbit-average geometry as UpdateTemperature; no invented warm plateau.
    double NextCoolingBoundary(double target)
    {
        if (!StarsEnabled || !Rules.Exists(r => r.Enabled && r.Id == "temperature")) return double.PositiveInfinity;
        bool cooling = false;
        for (int s = 0; s < N && !cooling; s++) cooling = Alive[s] && StarPhaseOf(s) is StarPhase.WhiteDwarf or StarPhase.NeutronStar;
        if (!cooling) return double.PositiveInfinity;
        double next = double.PositiveInfinity;
        foreach (double edge in new[] { C.ScorchedEdge, C.FrozenEdge, C.WaterBoil, C.WaterFreeze })
        {
            if (!(edge > C.CosmicBackground) || !(C.TemperatureScale > 0)) continue;
            double need = Math.Pow(edge / C.TemperatureScale, 4);
            for (int i = 0; i < N; i++)
            {
                if (!Alive[i] || !IsWorld(i)) continue;
                int host = RailStar(i, out double mean);
                double Flux(double at)
                {
                    double flux = 0;
                    for (int s = 0; s < N; s++)
                    {
                        if (!Alive[s] || s == i) continue;
                        double light = StarPhaseOf(s) is StarPhase.WhiteDwarf or StarPhase.NeutronStar
                            ? RemnantLight(s, StarCoolingAge[s] + Math.Max(0, at - _starUpdated[s])) : StarLuminosity(s);
                        if (!(light > 0)) continue;
                        double dx = X[i] - X[s], dy = Y[i] - Y[s], d2 = dx * dx + dy * dy;
                        if (s == host) flux += light * 45 * 45 * mean;
                        else if (d2 > 0) flux += light * 45 * 45 / d2;
                    }
                    return flux;
                }
                double hi = Math.Min(target, next), lo = Year;
                if (Flux(lo) <= need || Flux(hi) >= need) continue;
                for (int k = 0; k < 64; k++) { double mid = lo + (hi - lo) / 2; if (Flux(mid) > need) lo = mid; else hi = mid; }
                // A single ULP in year need not change the rounded fourth-root temperature.
                double crossed = Math.BitIncrement(Math.BitIncrement(Math.BitIncrement(Math.BitIncrement(hi))));
                next = Math.Min(next, Math.Max(crossed, hi + 1e-6));
            }
        }
        return next;
    }

    double GiantRadiusFactor(int i) => Math.Max(Math.Pow(M[i] / C.StarSolarMass, C.StarRadiusPower) * C.StarGiantRadius,
        Math.Sqrt(GiantLight(M[i])) * Math.Pow(C.StarSolarTemperature / C.StarGiantTemperature, 2));

    public double StarRadius(int i) => StarPhaseOf(i) switch
    {
        StarPhase.MainSequence => SolarRadius * Math.Pow(M[i] / C.StarSolarMass, C.StarRadiusPower),
        // R is both the drawn and contacting envelope: convert real stellar radius to the same squeezed AU scale as orbits.
        StarPhase.RedGiant => SolDist(GiantRadiusFactor(i) * C.StarSolarRadiusAU) * C.RadiusScale / 1.5,
        StarPhase.BrownDwarf => SolarRadius * C.StarBrownRadius,
        StarPhase.WhiteDwarf => SolarRadius * C.StarWhiteRadius * Math.Cbrt(C.StarWhiteIntercept * C.StarSolarMass / M[i]),
        StarPhase.NeutronStar => SolarRadius * C.StarNeutronRadius,
        StarPhase.BlackHole => SolarRadius * C.StarSchwarzschild * M[i] / C.StarSolarMass * Math.Max(0, C.G),
        _ => 0
    };

    public double StarSurfaceTemperature(int i)
    {
        // Stefan-Boltzmann uses stellar solar radii, not a draw/orbital distance stretched by the simulation.
        double r = StarPhaseOf(i) == StarPhase.RedGiant ? GiantRadiusFactor(i) : StarRadius(i) / SolarRadius, light = StarLuminosity(i);
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
        // Progenitor = the main-sequence mass before envelope loss. A god's mass edit while the core
        // is still burning rewrites that progenitor, without assigning a type or resetting burnt fuel.
        if (M[i] >= C.StarMass && StarFuel[i] < 1) StarInitialMass[i] = M[i];
        if (!StarsEnabled) return;
        if (StarInitialMass[i] > 0)
        {
            EvolveStar(i, Year);
            // A new mass or clock dial can bring death before the old calendar tick. Reschedule now,
            // so Advance sees it too; Jump already cuts at physical boundaries independently.
            if (_starRule != null && _starRate[i] > 0 && StarFuel[i] < GiantEnd)
            {
                double mark = StarFuel[i] < 1 ? 1 : GiantEnd;
                _starRule.NextYear = Math.Min(_starRule.NextYear, _starUpdated[i] + (mark - StarFuel[i]) / _starRate[i]);
            }
        }
    }

    void MergeStars(int k, int d)
    {
        SyncStar(k); SyncStar(d);
        double m = M[k] + M[d];
        if (!(m > 0) || StarInitialMass[k] <= 0 && StarInitialMass[d] <= 0) return;
        // Accretion cannot restore the surviving core; an exhausted swallowed core cannot extinguish it either.
        double coreFloor = StarFuel[k] >= GiantEnd ? GiantEnd : StarFuel[k] >= 1 ? 1 : 0;
        StarAge[k] = (StarAge[k] * M[k] + StarAge[d] * M[d]) / m;
        StarFuel[k] = Math.Max(coreFloor, (StarFuel[k] * M[k] + StarFuel[d] * M[d]) / m);
        StarCoolingAge[k] = (StarCoolingAge[k] * M[k] + StarCoolingAge[d] * M[d]) / m;
        // Limitation: mergers/MS sync rewrite the progenitor proxy, so later remnant logs may not retain the original birth mass.
        StarInitialMass[k] = (StarInitialMass[k] > 0 ? StarInitialMass[k] : M[k]) + (StarInitialMass[d] > 0 ? StarInitialMass[d] : M[d]);
        _starUpdated[k] = Year;
    }

    void UpdateStars()
    {
        for (int i = 0; i < N; i++) if (Alive[i] && !IsShip(i) && StarInitialMass[i] > 0) EvolveStar(i, Year);
    }

    double NextStarBoundary()
    {
        if (!StarsEnabled) return double.PositiveInfinity;
        double next = double.PositiveInfinity;
        for (int i = 0; i < N; i++)
        {
            if (!Alive[i] || IsShip(i) || !(_starRate[i] > 0) || StarFuel[i] >= GiantEnd) continue;
            double mark = StarFuel[i] < 1 ? 1 : GiantEnd;
            next = Math.Min(next, _starUpdated[i] + (mark - StarFuel[i]) / _starRate[i]);
        }
        return next;
    }

    bool SetStellarState(int i, StellarState state)
    {
        if (IsShip(i) || StarPhaseOf(i) == StarPhase.None
            || !double.IsFinite(state.AgeYears) || state.AgeYears < 0
            || !double.IsFinite(state.Fuel) || state.Fuel < 0 || state.Fuel > GiantEnd
            || !double.IsFinite(state.ProgenitorMass) || state.ProgenitorMass < M[i]
            || !double.IsFinite(state.CoolingAgeYears) || state.CoolingAgeYears < 0 || state.CoolingAgeYears > state.AgeYears
            || state.Fuel < GiantEnd && state.CoolingAgeYears != 0
            || state.Fuel < 1 && state.ProgenitorMass != M[i]
            || state.Fuel > 0 && state.ProgenitorMass < C.StarMass
            || state.Fuel < GiantEnd && state.Fuel > 0 && M[i] < C.StarMass) return false;
        double rate = M[i] >= C.StarMass && state.Fuel < GiantEnd ? 1 / StarLifetime(M[i]) : 0;
        if (!double.IsFinite(rate)) return false;
        var before = (StarAge[i], StarFuel[i], StarInitialMass[i], StarCoolingAge[i]);
        StarAge[i] = state.AgeYears; StarFuel[i] = state.Fuel;
        StarInitialMass[i] = state.ProgenitorMass; StarCoolingAge[i] = state.CoolingAgeYears;
        double radius = StarRadius(i);
        if (!double.IsFinite(radius) || radius < 0)
        {
            (StarAge[i], StarFuel[i], StarInitialMass[i], StarCoolingAge[i]) = before;
            return false;
        }
        // This is a snapshot, not a historical jump: mass, matter, velocity and the ejecta ledger stay as supplied.
        R[i] = radius; _starUpdated[i] = Year; _starRate[i] = rate;
        if (StarsEnabled && _starRule != null)
            _starRule.NextYear = Math.Min((Math.Floor(Year / _starRule.RhythmYears) + 1) * _starRule.RhythmYears, NextStarBoundary());
        return true;
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
                ApplyStarEvents(i, StarTransition.Giant);
            }
            if (death)
            {
                Year = deathAt; StarFuel[i] = end;
                ApplyStarEvents(i, StarTransition.Death);
                StarCoolingAge[i] += Math.Max(0, now - Year);
            }
            else if (before >= end) StarCoolingAge[i] += dt;
            StarFuel[i] = after; _starUpdated[i] = now;
            _starRate[i] = M[i] >= C.StarMass && after < end ? 1 / StarLifetime(M[i]) : 0;
            SetRadius(i);
        }
        finally { Year = savedYear; _updatingStars = false; }
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
