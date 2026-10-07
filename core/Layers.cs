// Layers (SPEC 2): parameters that grow on an object when conditions fit. Water sits on temperature, life on water,
// civilisation on life. Each layer is a rule in the table; nothing here is scripted per object, and nothing knows
// "Earth": a planet gets a layer because its numbers fit, and loses it when they stop fitting.
// Every number below is a PLACEHOLDER [P] chosen by Claire (Yang 06/10: "em tu nghi"); all are Consts, so the god
// can turn them at run time. Clock rules use World.RuleYears, so one run after a jump stands for all years skipped.
using System;
using System.Collections.Generic;

namespace Cosmos.Core;

public enum PlanetarySource
{
    None = 0,
    LiquidWater = 1,
    Biosphere = 2,
    Starlight = 3
}

public sealed record StageNeed(
    ElementRole Element = ElementRole.None,
    PlanetarySource Planetary = PlanetarySource.None,
    double MinShare = 0,
    double ConsumeRate = 0,
    bool BecomesWaste = false
)
{
    public static StageNeed ForElement(ElementRole elem, double minShare = 0, double consumeRate = 0, bool becomesWaste = false) =>
        new(Element: elem, Planetary: PlanetarySource.None, MinShare: minShare, ConsumeRate: consumeRate, BecomesWaste: becomesWaste);

    public static StageNeed ForPlanetary(PlanetarySource source, double minShare = 0, double consumeRate = 0) =>
        new(Element: ElementRole.None, Planetary: source, MinShare: minShare, ConsumeRate: consumeRate, BecomesWaste: false);
}

public sealed record Stage(
    string Id,
    string NameVi,
    double TechThreshold,
    double EarthYears = 1000,
    double WattsPerCapita = 100,
    double MaxPopulationOnEarth = 1e10,
    IReadOnlyList<StageNeed>? Needs = null,
    bool CanLaunchShips = false,
    bool CanDome = false
)
{
    public IReadOnlyList<StageNeed> Needs { get; init; } = Needs ?? Array.Empty<StageNeed>();
}

public sealed partial class Consts
{
    public double JumpSamples = 200;       // most chunks one fast-forward is cut into

    public double WaterFreeze = 273;       // K: below = ice
    public double WaterBoil = 373;         // K: above = vapour
    public double WaterIceMin = 0.001;     // share of the mass that must be ice for the planet to count as having water
    public double WaterHoldMass = 4.5e-5;  // lighter than this (0.3 Earths) cannot hold liquid water: it boils off

    public double LifeSparkYears = 2e5;    // years of liquid water in a row before life starts by itself
    public double LifeCarbonMin = 0.001;   // share of carbon needed
    public double LifeSolidMin = 0.5;      // share of rock + metal needed (a surface to live on)
    public double LifeSeed = 0.001;        // level life starts at
    public double LifeGrowth = 2e-5;       // per year, while conditions fit (0.001 -> 0.5 in about 350 000 years)
    public double LifeDecayYears = 2e4;    // life falls by e in this many years once conditions fail

    public double CivLifeMin = 0.5;        // a biosphere this rich, kept for CivRiseYears, raises a civilisation
    public double CivRiseYears = 1e5;
    public double CivSeed = 1e-4;          // population a civilisation starts at (1 = the planet is full)
    public double CivGrowth = 1e-3;        // per year, toward what the biosphere can feed
    public double CivDecayYears = 500;     // population falls by e in this many years once the biosphere fails
    public double CivTechRate = 1.0;       // global tech progression rate multiplier [P]
    public double CivMetalRef = 0.3;       // share of metal that gives the full tech rate
    public double EarthRadiusKm = 6371.0;  // km: WGS84 mean Earth radius [P]
    public double EarthMaxPopulation = 1e10;// people: fallback carrying capacity [P]
    public double StarlightTempMin = 50.0; // K: minimum star-lit warmth to count as usable starlight [P]
    public double TechCeilingMargin = 0.5; // margin above final stage tech threshold for mastery [P]
    public double FootprintBioWeight = 0.5;// weight of biosphere pressure in footprint [P]
    public double FootprintMatterWeight = 0.5;// weight of mined matter in footprint [P]
    public double FootprintMatterScale = 0.01;// share of planetary mass consumed that gives full matter footprint [P]

    public double ImpactScale = 1e-3;      // a hit by this share of the planet's mass cuts life and population by e
}

public enum WaterState { None, Ice, Liquid, Vapour }

public sealed partial class World
{
    public int[] Water = null!;         // WaterState per object; -1 = not looked at yet
    public double[] WaterYears = null!; // years of liquid water in a row
    public double[] Life = null!;       // 0 = none .. 1 = the planet is full of it
    public double[] RichYears = null!;  // years in a row with Life >= CivLifeMin
    public double[] Pop = null!;        // 0 = no civilisation .. 1 = the planet is full
    public double[] Tech = null!;       // 0 upward; stage determined by Stages table
    public double[] Touched = null!;    // year life or population was last changed from outside the rules (seed, impact)
    public double[] ConsumedMatter = null!; // cumulative mined/consumed matter per world slot [P]
    // Scratch for the life -> civ handoff within one rule pass; never read on a later pass.
    double[] _lifeBefore = null!, _lifeDecaySpan = null!;
    bool _lifeCurveReady;

    public double EarthRadiusRef => CalcEarthRadiusRef();
    public double CalcEarthRadiusRef()
    {
        double sumInvDensity = 0;
        for (int k = 0; k < EarthMix.Length; k++)
        {
            int e = Elem(EarthMix[k].Id);
            double d = e >= 0 ? C.Density[e] : 3.0;
            sumInvDensity += EarthMix[k].Share / d;
        }
        return C.RadiusScale * Math.Cbrt(EarthMass * sumInvDensity);
    }

    public static readonly Stage[] DefaultStages = new[]
    {
        // 1. prehistoric: hunter-gatherers, ~2000 kcal/day ≈ 100 W/person (Smil 2017), ~5-10M Earth total (McEvedy & Jones 1978), ~3e5 yr duration
        new Stage("prehistoric", "Thời kỳ Tiền sử", 0.0, EarthYears: 300000, WattsPerCapita: 100, MaxPopulationOnEarth: 1e7,
            Needs: new[] { StageNeed.ForPlanetary(PlanetarySource.LiquidWater, 0.001), StageNeed.ForPlanetary(PlanetarySource.Biosphere, 0.01) }),
        // 2. stone_age: fire control, early food processing ~150 W (Cook 1971), Neolithic transition ~50M (Biraben 1979), ~7000 yr duration
        new Stage("stone_age", "Thời kỳ Đồ Đá", 0.5, EarthYears: 7000, WattsPerCapita: 150, MaxPopulationOnEarth: 5e7,
            Needs: new[] { StageNeed.ForElement(ElementRole.Rock, 0.05), StageNeed.ForPlanetary(PlanetarySource.Biosphere, 0.05) }),
        // 3. bronze_age: metallurgy, draft animals ~250 W (Smil 2017), 2nd millennium BC ~150M, ~2000 yr duration
        new Stage("bronze_age", "Thời kỳ Đồ Đồng", 1.0, EarthYears: 2000, WattsPerCapita: 250, MaxPopulationOnEarth: 1.5e8,
            Needs: new[] { StageNeed.ForElement(ElementRole.Metal, 0.02, 1e-14, becomesWaste: true), StageNeed.ForElement(ElementRole.Rock, 0.05) }),
        // 4. iron_age: iron smelting, charcoal, intensive farming ~350 W (Cook 1971), Greco-Roman / Han era ~250M, ~1700 yr duration
        new Stage("iron_age", "Thời kỳ Đồ Sắt", 1.5, EarthYears: 1700, WattsPerCapita: 350, MaxPopulationOnEarth: 2.5e8,
            Needs: new[] { StageNeed.ForElement(ElementRole.Metal, 0.05, 2e-14, becomesWaste: true), StageNeed.ForElement(ElementRole.Carbon, 0.001, 1e-14) }),
        // 5. medieval: water/wind mills ~500 W (Smil 2017), 13th-14th century ~400M, ~1000 yr duration
        new Stage("medieval", "Thời kỳ Trung Cổ", 2.0, EarthYears: 1000, WattsPerCapita: 500, MaxPopulationOnEarth: 4e8,
            Needs: new[] { StageNeed.ForElement(ElementRole.Metal, 0.05), StageNeed.ForPlanetary(PlanetarySource.Biosphere, 0.1) }),
        // 6. renaissance: early blast furnaces, mining ~800 W (Cook 1971), 16th-17th century ~700M, ~300 yr duration
        new Stage("renaissance", "Thời kỳ Phục Hưng", 2.3, EarthYears: 300, WattsPerCapita: 800, MaxPopulationOnEarth: 7e8,
            Needs: new[] { StageNeed.ForElement(ElementRole.Metal, 0.08), StageNeed.ForElement(ElementRole.Carbon, 0.002, 1e-14) }),
        // 7. industrial: coal, steam ~1500 W (IEA historical), year 1900 ~1.6B, ~190 yr duration
        new Stage("industrial", "Thời kỳ Công nghiệp", 2.6, EarthYears: 190, WattsPerCapita: 1500, MaxPopulationOnEarth: 1.6e9,
            Needs: new[] { StageNeed.ForElement(ElementRole.Metal, 0.10, 5e-13, becomesWaste: true), StageNeed.ForElement(ElementRole.Carbon, 0.003, 1e-13) }),
        // 8. atomic_age: power grids, oil, nuclear; Earth 2020: 19 TW / 8B people ≈ 2400 W/person -> K ≈ 0.73 (BP / Sagan 1973), ~6B at year 1999, ~75 yr duration
        new Stage("atomic_age", "Thời kỳ Nguyên tử", 2.85, EarthYears: 75, WattsPerCapita: 2400, MaxPopulationOnEarth: 6e9,
            Needs: new[] { StageNeed.ForElement(ElementRole.Metal, 0.10, 5e-13, becomesWaste: true), StageNeed.ForElement(ElementRole.Radio, 0.0005, 1e-14, becomesWaste: true) }),
        // 9. space: orbital infrastructure, automated energy ~8000 W (Sagan 1973), UN peak population projection ~10B, ~100 yr duration
        new Stage("space", "Kỷ nguyên Tiền Vũ trụ", 3.0, EarthYears: 100, WattsPerCapita: 8000, MaxPopulationOnEarth: 1e10,
            Needs: new[] { StageNeed.ForElement(ElementRole.Metal, 0.10, 5e-13, becomesWaste: true), StageNeed.ForElement(ElementRole.Ice, 0.001) },
            CanLaunchShips: true, CanDome: true),
        // 10. interplanetary: fusion, early Dyson swarm ~50000 W -> K ≈ 0.94 near Type I (Kardashev 1964), multi-world domes ~50B, ~500 yr duration
        new Stage("interplanetary", "Kỷ nguyên Vũ trụ", 3.5, EarthYears: 500, WattsPerCapita: 50000, MaxPopulationOnEarth: 5e10,
            Needs: new[] { StageNeed.ForElement(ElementRole.Metal, 0.10, 5e-13, becomesWaste: true), StageNeed.ForElement(ElementRole.Radio, 0.001, 2e-14, becomesWaste: true) },
            CanLaunchShips: true, CanDome: true)
    };

    public readonly List<Stage> Stages = new();
    public static int MaxTechStage => DefaultStages.Length - 1; // Stages
    static readonly double[] LifeStages = { 0.01, 0.1, 0.5 }; // Stages

    void InitLayers(int capacity)
    {
        Water = new int[capacity]; WaterYears = new double[capacity]; Life = new double[capacity];
        RichYears = new double[capacity]; Pop = new double[capacity]; Tech = new double[capacity]; Touched = new double[capacity];
        ConsumedMatter = new double[capacity];
        _lifeBefore = new double[capacity]; _lifeDecaySpan = new double[capacity];
        Stages.Clear(); Stages.AddRange(DefaultStages);
        InitCiv(capacity);
    }

    void ResetLayers(int i) { Water[i] = -1; WaterYears[i] = Life[i] = RichYears[i] = Pop[i] = Tech[i] = Touched[i] = ConsumedMatter[i] = 0; _lifeDecaySpan[i] = double.NaN; ResetCiv(i); }

    void InitLayerRules()
    {
        Rules.Add(new Rule("water", "Temp,Comp,M,WaterFreeze,WaterBoil,WaterIceMin,WaterHoldMass", "Water,WaterYears", 1, w => w.UpdateWater())
            { Boundary = RuleBoundary.BeforeStar | RuleBoundary.BeforeCooling | RuleBoundary.After });
        Rules.Add(new Rule("life", "Water,WaterYears,Comp,Life*", "Life,RichYears", 1000, w => w.UpdateLife())
            { Boundary = RuleBoundary.BeforeStar | RuleBoundary.BeforeCooling });
        Rules.Add(new Rule("civ", "Life,RichYears,Comp,Civ*", "Pop,Tech,Comp,Civ", 100, w => w.UpdateCiv())
            { Boundary = RuleBoundary.BeforeStar | RuleBoundary.BeforeCooling });
        Rules.Add(new Rule("ships", "Pop,Tech,Civ,X,Y,Ship*", "Pop,Tech,Civ,objects", ShipRhythmYears, w => w.UpdateShips()));
    }

    /// A planet or a moon: pulls others, is not a star. Only these carry layers.
    public bool IsWorld(int i) => M[i] >= C.AttractMass && M[i] < C.StarMass && !IsShip(i) && StarPhaseOf(i) == StarPhase.None;

    public int LifeStage(int i) { int s = 0; while (s < LifeStages.Length && Life[i] >= LifeStages[s]) s++; return s; }
    public int TechStage(int i)
    {
        if (Pop[i] <= 0 || Stages.Count == 0) return 0;
        double t = Tech[i];
        for (int k = Stages.Count - 1; k >= 0; k--)
            if (t >= Stages[k].TechThreshold) return k;
        return 0;
    }

    public double Share(int i, int elem) => Comp[i * ElementCount + elem] / M[i];

    public double ResourceLevel(int i, StageNeed need)
    {
        if (i < 0 || i >= N || !Alive[i]) return 0;
        if (need.Element != ElementRole.None) return Share(i, need.Element);
        return need.Planetary switch
        {
            PlanetarySource.LiquidWater => Water[i] == (int)WaterState.Liquid ? Share(i, ElementRole.Ice) : 0.0,
            PlanetarySource.Biosphere => Life[i],
            PlanetarySource.Starlight => !double.IsNaN(Temp[i]) && Temp[i] >= C.StarlightTempMin ? Math.Min(1.0, Temp[i] / C.CivDomeTemp) : 0.0,
            _ => 0.0
        };
    }

    public bool HasNeedsForStage(int i, int stageIndex)
    {
        if (stageIndex < 0 || stageIndex >= Stages.Count) return false;
        var needs = Stages[stageIndex].Needs;
        if (needs == null || needs.Count == 0) return true;
        for (int k = 0; k < needs.Count; k++)
        {
            var need = needs[k];
            if (need.MinShare > 0 && ResourceLevel(i, need) < need.MinShare)
                return false;
        }
        return true;
    }

    public double PeopleCount(int i)
    {
        if (!Alive[i] || Pop[i] <= 0) return 0;
        int stage = TechStage(i);
        double capEarth = stage < Stages.Count ? Stages[stage].MaxPopulationOnEarth : C.EarthMaxPopulation;
        double rRef = EarthRadiusRef;
        double areaRatio = rRef > 0 ? Math.Pow(R[i] / rRef, 2.0) : 1.0;
        return Pop[i] * capEarth * areaRatio;
    }

    public double PowerWatts(int i)
    {
        if (!Alive[i] || Pop[i] <= 0) return 0;
        int stage = TechStage(i);
        double wpc = stage < Stages.Count ? Stages[stage].WattsPerCapita : 100.0;
        return PeopleCount(i) * wpc;
    }

    public double KardashevScale(int i)
    {
        double p = PowerWatts(i);
        if (!(p > 1e6)) return 0;
        return (Math.Log10(p) - 6.0) / 10.0;
    }

    public double CivFootprint(int i)
    {
        if (!Alive[i] || Pop[i] <= 0) return 0;
        double massShare = (M[i] > 0 && C.FootprintMatterScale > 0) ? (ConsumedMatter[i] / (M[i] * C.FootprintMatterScale)) : 0;
        double bioLoad = Life[i] > 0 ? Math.Clamp(Pop[i] / Life[i], 0, 1) : 1.0;
        return Math.Clamp(C.FootprintBioWeight * bioLoad + C.FootprintMatterWeight * Math.Min(1.0, massShare), 0, 1);
    }

    void UpdateWater()
    {
        for (int i = 0; i < N; i++)
        {
            if (!Alive[i] || !IsWorld(i) || double.IsNaN(Temp[i])) continue;
            double ice = Share(i, ElementRole.Ice), t = Temp[i];
            WaterState now = ice < C.WaterIceMin ? WaterState.None
                : t < C.WaterFreeze ? WaterState.Ice
                : t > C.WaterBoil || M[i] < C.WaterHoldMass ? WaterState.Vapour : WaterState.Liquid;
            int old = Water[i];
            WaterYears[i] = now == WaterState.Liquid && old == (int)WaterState.Liquid ? WaterYears[i] + RuleYears : 0;
            Water[i] = (int)now;
            if (old >= 0 && old != (int)now) LogEvent(i, "water", $"water.{old}.{(int)now}", t, ice, M[i] / EarthMass);
        }
    }

    // One run of a clock rule stands for all the years since its last run. Whatever happens inside that stretch
    // (life starts, a threshold is crossed, the god seeds a planet) is placed at its own moment and only the years
    // after it count; otherwise the same history would end differently depending on how a jump was cut.
    void UpdateLife()
    {
        _lifeCurveReady = true;
        for (int i = 0; i < N; i++)
        {
            _lifeDecaySpan[i] = double.NaN;
            if (!Alive[i] || !IsWorld(i)) continue;
            double dt = Math.Min(RuleYears, Year - Touched[i]);
            bool fits = Water[i] == (int)WaterState.Liquid && Share(i, ElementRole.Rock) + Share(i, ElementRole.Metal) >= C.LifeSolidMin && Share(i, ElementRole.Carbon) >= C.LifeCarbonMin;
            double l = Life[i];
            if (l <= 0)
            {
                if (!fits || WaterYears[i] < C.LifeSparkYears) continue;
                Life[i] = l = C.LifeSeed;
                dt = Math.Min(dt, WaterYears[i] - C.LifeSparkYears); // it has been alive since the wait was over
                LogEvent(i, "life", "life.start", WaterYears[i], Temp[i], Share(i, ElementRole.Carbon), Year - dt);
            }
            int stage = LifeStage(i);
            double l0 = l;
            if (!fits) { _lifeBefore[i] = l0; _lifeDecaySpan[i] = dt; }
            // logistic growth toward 1, written so that any dt gives the same curve
            l = fits ? 1 / (1 + (1 / l - 1) * Math.Exp(-C.LifeGrowth * dt)) : l * Math.Exp(-dt / C.LifeDecayYears);
            if (l < C.LifeSeed / 10)
            {
                Life[i] = 0; RichYears[i] = 0;
                double at = Math.Clamp(C.LifeDecayYears * Math.Log(l0 / (C.LifeSeed / 10)), 0, dt);
                LogEvent(i, "life", "life.end", Temp[i], Water[i], stage, Year - dt + at);
                continue;
            }
            Life[i] = l;
            if (l < C.CivLifeMin) RichYears[i] = 0;
            else if (l0 >= C.CivLifeMin) RichYears[i] += dt;
            else
            {
                // crossed the mark inside this stretch: count from the moment of crossing on the growth curve
                double at = Math.Log((1 / l0 - 1) / (1 / C.CivLifeMin - 1)) / C.LifeGrowth;
                RichYears[i] = at >= 0 && at <= dt ? dt - at : 0;
            }
            int after = LifeStage(i);
            if (after > stage && fits && C.LifeGrowth > 0)
                // each mark it grew past gets its own line, at the year the growth curve crossed it
                for (int k = stage; k < after; k++)
                {
                    double mark = LifeStages[k], at = Math.Clamp(Math.Log((1 / l0 - 1) / (1 / mark - 1)) / C.LifeGrowth, 0, dt);
                    LogEvent(i, "life", $"life.stage.{k}.{k + 1}", k + 1 == after ? l : mark, Temp[i], Water[i], Year - dt + at);
                }
            else if (after != stage) LogEvent(i, "life", $"life.stage.{stage}.{after}", l, Temp[i], Water[i]);
        }
    }

    void UpdateCiv()
    {
        FindFed();
        for (int i = 0; i < N; i++)
        {
            if (!Alive[i] || !IsWorld(i)) continue;
            double dt = Math.Min(RuleYears, Year - Touched[i]);
            double p = Pop[i], room = CivRoom(i);
            if (p <= 0)
            {
                if (Life[i] < C.CivLifeMin || RichYears[i] < C.CivRiseYears) continue;
                Pop[i] = p = C.CivSeed; Tech[i] = 0; Civ[i] = NewCiv(i);
                // The wait was over before this run began whenever the rich years outran it: what dates the birth is
                // the whole wait since the mark, not the slice this run happens to cover. dt above is a window
                // (RuleYears), an age is not the same thing, and clamping one with the other pushed every birth
                // logged after a sparse run onto the window's own edge.
                dt = Math.Min(Year - Touched[i], RichYears[i] - C.CivRiseYears);
                CivEvent(i, Civ[i], "civ.start", RichYears[i], room, Share(i, ElementRole.Metal), Year - dt);
            }
            int stage = TechStage(i);
            double p0 = p, lived; // lived = population summed over the stretch (people * years)
            // the biosphere feeds the people: population follows what life there is, and starves when it fails
            bool decaying = _lifeCurveReady && _lifeDecaySpan[i] >= dt && C.LifeDecayYears > 0 && _lifeBefore[i] >= C.CivLifeMin / 5 && _lifeBefore[i] > 0;
            double life0 = decaying ? _lifeBefore[i] * Math.Exp(-(_lifeDecaySpan[i] - dt) / C.LifeDecayYears) : 0;
            double fedYears = decaying && C.CivLifeMin > 0 ? Math.Clamp(C.LifeDecayYears * Math.Log(life0 / (C.CivLifeMin / 5)), 0, dt) : dt;
            // Logistic population with exponentially falling carrying capacity: solve for 1/pop.
            // Once the biosphere drops below the feeding mark, only domes (or starvation) remain.
            double FedPop(double years)
            {
                if (C.CivGrowth == 0) return p0;
                double rate = C.CivGrowth + 1 / C.LifeDecayYears;
                return life0 * rate * Math.Exp(-years / C.LifeDecayYears)
                    / (C.CivGrowth + (life0 * rate / p0 - C.CivGrowth) * Math.Exp(-rate * years));
            }
            double pFed = decaying ? FedPop(fedYears) : p0;
            double PopAt(double years)
            {
                if (decaying && years <= fedYears) return FedPop(years);
                double startPop = decaying ? pFed : p0, elapsed = decaying ? years - fedYears : years;
                return room > 0 ? room / (1 + (room / startPop - 1) * Math.Exp(-C.CivGrowth * elapsed)) : startPop * Math.Exp(-elapsed / C.CivDecayYears);
            }
            double LivedTo(double years)
            {
                if (decaying)
                {
                    double fed = Math.Min(years, fedYears), elapsed = years - fed, qAfter = PopAt(years);
                    double total = IntegratePopulation(FedPop, fed);
                    if (elapsed > 0) total += room > 0 ? (C.CivGrowth > 0 ? room * elapsed + room / C.CivGrowth * Math.Log(pFed / qAfter) : pFed * elapsed)
                        : C.CivDecayYears * (pFed - qAfter);
                    return total;
                }
                double q = PopAt(years);
                return room > 0 ? (C.CivGrowth > 0 ? room * years + room / C.CivGrowth * Math.Log(p0 / q) : p0 * years) : C.CivDecayYears * (p0 - q);
            }
            p = PopAt(dt); lived = LivedTo(dt);
            if (p < C.CivSeed / 10)
            {
                Pop[i] = 0; Tech[i] = 0;
                // Locate the crossing on the same population curve, including a tiny but nonzero dome.
                double lo = 0, hi = dt;
                for (int it = 0; it < 50; it++) { double mid = 0.5 * (lo + hi); if (PopAt(mid) >= C.CivSeed / 10) lo = mid; else hi = mid; }
                CivEvent(i, Civ[i], "civ.end", room, Temp[i], stage, Year - dt + hi);
                continue;
            }
            Pop[i] = p;
            double metal = C.CivMetalRef > 0 ? Math.Min(1, Share(i, ElementRole.Metal) / C.CivMetalRef) : 1; // 0 = metal not needed
            double tech0 = Tech[i], effMult = metal * C.CivTechRate;
            double top = (Stages.Count > 0 ? Stages[^1].TechThreshold : 0.0) + C.TechCeilingMargin;
            double cap = top;
            for (int s = stage + 1; s < Stages.Count; s++)
            {
                if (!HasNeedsForStage(i, s))
                {
                    cap = Math.BitDecrement(Stages[s].TechThreshold);
                    break;
                }
            }
            if (tech0 < cap && effMult > 0 && lived > 0)
            {
                double cur = tech0, remLived = lived;
                while (cur < cap && remLived > 0)
                {
                    int s = 0;
                    for (int k = Stages.Count - 1; k >= 0; k--)
                        if (cur >= Stages[k].TechThreshold) { s = k; break; }
                    double next = s + 1 < Stages.Count ? Stages[s + 1].TechThreshold : top;
                    double stageCeil = Math.Min(cap, next);
                    double delta = next - Stages[s].TechThreshold;
                    double stageRate = Stages[s].EarthYears > 0 ? (delta / Stages[s].EarthYears) : 0;
                    if (!(stageRate > 0)) break;
                    double effRate = stageRate * effMult;
                    double neededLived = (stageCeil - cur) / effRate;
                    if (remLived >= neededLived)
                    {
                        cur = stageCeil;
                        remLived -= neededLived;
                    }
                    else
                    {
                        cur += remLived * effRate;
                        remLived = 0;
                    }
                }
                Tech[i] = cur;
            }
            ConsumeResources(i, lived, stage);
            int after = TechStage(i);
            _launchYear[i] = double.NaN;
            if (after > stage && effMult > 0)
                // each stage it passed gets its own line, at the year its tech got there (found on the same curves)
                for (int k = stage + 1; k <= after; k++)
                {
                    double need = LivedNeededToReach(tech0, Stages[k].TechThreshold, effMult), lo = 0, hi = dt;
                    for (int it = 0; it < 50; it++) { double mid = 0.5 * (lo + hi); if (LivedTo(mid) < need) lo = mid; else hi = mid; }
                    double when = Year - dt + hi;
                    CivEvent(i, Civ[i], $"civ.stage.{k - 1}.{k}", k == after ? Tech[i] : Stages[k].TechThreshold, PopAt(hi), Share(i, ElementRole.Metal), when);
                    if (Stages[k].CanLaunchShips && !Stages[k - 1].CanLaunchShips) _launchYear[i] = when;
                }
            else if (after != stage) CivEvent(i, Civ[i], $"civ.stage.{stage}.{after}", Tech[i], p, Share(i, ElementRole.Metal));
        }
    }

    double LivedNeededToReach(double fromTech, double targetTech, double effMult)
    {
        if (!(effMult > 0) || fromTech >= targetTech) return 0;
        double sum = 0, cur = fromTech;
        double top = (Stages.Count > 0 ? Stages[^1].TechThreshold : 0.0) + C.TechCeilingMargin;
        while (cur < targetTech)
        {
            int s = 0;
            for (int k = Stages.Count - 1; k >= 0; k--)
                if (cur >= Stages[k].TechThreshold) { s = k; break; }
            double next = s + 1 < Stages.Count ? Stages[s + 1].TechThreshold : top;
            double stageCeil = Math.Min(targetTech, next);
            double delta = next - Stages[s].TechThreshold;
            double stageRate = Stages[s].EarthYears > 0 ? (delta / Stages[s].EarthYears) : 0;
            if (!(stageRate > 0)) break;
            double effRate = stageRate * effMult;
            sum += (stageCeil - cur) / effRate;
            cur = stageCeil;
        }
        return sum;
    }

    // Only the changing biosphere needs quadrature; the fixed-room curves above retain their closed integral.
    static double IntegratePopulation(Func<double, double> at, double years)
    {
        if (!(years > 0)) return 0;
        double a = at(0), b = at(years / 2), c = at(years), whole = years * (a + 4 * b + c) / 6;
        double Split(double lo, double hi, double x, double y, double z, double estimate, double tolerance, int depth)
        {
            double mid = (lo + hi) / 2, l = at((lo + mid) / 2), r = at((mid + hi) / 2);
            double left = (mid - lo) * (x + 4 * l + y) / 6, right = (hi - mid) * (y + 4 * r + z) / 6;
            double error = left + right - estimate;
            if (depth == 0 || Math.Abs(error) <= 15 * tolerance) return left + right + error / 15;
            return Split(lo, mid, x, l, y, left, tolerance / 2, depth - 1) + Split(mid, hi, y, r, z, right, tolerance / 2, depth - 1);
        }
        return Split(0, years, a, b, c, whole, 1e-9 * Math.Max(1, years), 18);
    }
    // Names, metal, ships and colonies: core/Civ.cs.

    // A swallowed world's layers stop here, before the dead-slot filters can hide their ending from the journal.
    void EndSwallowedWorld(int i)
    {
        if (Life[i] > 0) LogEvent(i, "life", "life.end", Temp[i], Water[i], LifeStage(i));
        if (Pop[i] > 0) CivEvent(i, Civ[i], "civ.end", -1, Temp[i], TechStage(i)); // room -1 = its world was swallowed, it did not starve
        Life[i] = RichYears[i] = Pop[i] = Tech[i] = 0;
        Touched[i] = Year;
    }

    // Called from Merge before the masses are summed: `share` = mass of what hit / mass of the one that stays.
    void Impact(int k, double share)
    {
        if (Life[k] <= 0 && Pop[k] <= 0) return;
        double keep = Math.Exp(-share / C.ImpactScale), before = Life[k];
        Life[k] *= keep; Pop[k] *= keep; Touched[k] = Year;
        if (Life[k] < C.CivLifeMin) RichYears[k] = 0; // the rich biosphere is gone now, not at the next life run
        if (share >= C.ImpactScale / 10) LogEvent(k, "impact", "impact", share, before, Life[k]);
    }

    void HashLayers(Action<ulong> mix)
    {
        void number(double n) => mix(BitConverter.DoubleToUInt64Bits(n));
        for (int i = 0; i < N; i++)
        {
            if (!Alive[i]) continue;
            mix((ulong)Water[i]); number(WaterYears[i]); number(Life[i]); number(RichYears[i]); number(Pop[i]); number(Tech[i]); number(Touched[i]);
            number(ConsumedMatter[i]);
        }
        HashCiv(mix);
    }
}
