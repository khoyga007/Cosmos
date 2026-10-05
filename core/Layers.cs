// Layers (SPEC 2): parameters that grow on an object when conditions fit. Water sits on temperature, life on water,
// civilisation on life. Each layer is a rule in the table; nothing here is scripted per object, and nothing knows
// "Earth": a planet gets a layer because its numbers fit, and loses it when they stop fitting.
// Every number below is a PLACEHOLDER [P] chosen by Claire (Yang 06/10: "em tu nghi"); all are Consts, so the god
// can turn them at run time. Clock rules use World.RuleYears, so one run after a jump stands for all years skipped.
using System;

namespace Cosmos.Core;

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
    public double CivTechRate = 1e-4;      // tech per year at full population on a metal-rich planet
    public double CivMetalRef = 0.3;       // share of metal that gives the full tech rate

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
    public double[] Tech = null!;       // 0 upward; stage = whole part, capped at MaxTechStage

    public const int MaxTechStage = 3;  // 1 farming, 2 industry, 3 space [P]
    static readonly double[] LifeStageAt = { 0.01, 0.1, 0.5 }; // 1 microbes, 2 complex, 3 rich biosphere [P]

    void InitLayers(int capacity)
    {
        Water = new int[capacity]; WaterYears = new double[capacity]; Life = new double[capacity];
        RichYears = new double[capacity]; Pop = new double[capacity]; Tech = new double[capacity];
    }

    void ResetLayers(int i) { Water[i] = -1; WaterYears[i] = Life[i] = RichYears[i] = Pop[i] = Tech[i] = 0; }

    void InitLayerRules()
    {
        Rules.Add(new Rule("water", "Temp,Comp,M,WaterFreeze,WaterBoil,WaterIceMin,WaterHoldMass", "Water,WaterYears", 1, w => w.UpdateWater()));
        Rules.Add(new Rule("life", "Water,WaterYears,Comp,Life*", "Life,RichYears", 1000, w => w.UpdateLife()));
        Rules.Add(new Rule("civ", "Life,RichYears,Comp,Civ*", "Pop,Tech", 100, w => w.UpdateCiv()));
    }

    /// A planet or a moon: pulls others, is not a star. Only these carry layers.
    public bool IsWorld(int i) => M[i] >= C.AttractMass && M[i] < C.StarMass;

    public int LifeStage(int i) { int s = 0; while (s < LifeStageAt.Length && Life[i] >= LifeStageAt[s]) s++; return s; }
    public int TechStage(int i) => Pop[i] > 0 ? (int)Math.Min(Tech[i], MaxTechStage) : 0;

    double Share(int i, int elem) => Comp[i * NElem + elem] / M[i];

    void UpdateWater()
    {
        for (int i = 0; i < N; i++)
        {
            if (!Alive[i] || !IsWorld(i) || double.IsNaN(Temp[i])) continue;
            double ice = Share(i, 1), t = Temp[i];
            WaterState now = ice < C.WaterIceMin ? WaterState.None
                : t < C.WaterFreeze ? WaterState.Ice
                : t > C.WaterBoil || M[i] < C.WaterHoldMass ? WaterState.Vapour : WaterState.Liquid;
            int old = Water[i];
            WaterYears[i] = now == WaterState.Liquid && old == (int)WaterState.Liquid ? WaterYears[i] + RuleYears : 0;
            Water[i] = (int)now;
            if (old >= 0 && old != (int)now) LogEvent(i, "water", $"water.{old}.{(int)now}", t, ice, M[i] / EarthMass);
        }
    }

    void UpdateLife()
    {
        double dt = RuleYears;
        for (int i = 0; i < N; i++)
        {
            if (!Alive[i] || !IsWorld(i)) continue;
            bool fits = Water[i] == (int)WaterState.Liquid && Share(i, 2) + Share(i, 3) >= C.LifeSolidMin && Share(i, 4) >= C.LifeCarbonMin;
            double l = Life[i];
            if (l <= 0)
            {
                if (!fits || WaterYears[i] < C.LifeSparkYears) continue;
                Life[i] = C.LifeSeed;
                LogEvent(i, "life", "life.start", WaterYears[i], Temp[i], Share(i, 4));
                continue;
            }
            int stage = LifeStage(i);
            // logistic growth toward 1, written so that any dt gives the same curve
            l = fits ? 1 / (1 + (1 / l - 1) * Math.Exp(-C.LifeGrowth * dt)) : l * Math.Exp(-dt / C.LifeDecayYears);
            if (l < C.LifeSeed / 10)
            {
                Life[i] = 0; RichYears[i] = 0;
                LogEvent(i, "life", "life.end", Temp[i], Water[i], stage);
                continue;
            }
            Life[i] = l;
            RichYears[i] = l >= C.CivLifeMin ? RichYears[i] + dt : 0;
            int after = LifeStage(i);
            if (after != stage) LogEvent(i, "life", $"life.stage.{stage}.{after}", l, Temp[i], Water[i]);
        }
    }

    void UpdateCiv()
    {
        double dt = RuleYears;
        for (int i = 0; i < N; i++)
        {
            if (!Alive[i] || !IsWorld(i)) continue;
            double p = Pop[i], room = Life[i];
            if (p <= 0)
            {
                if (room < C.CivLifeMin || RichYears[i] < C.CivRiseYears) continue;
                Pop[i] = C.CivSeed; Tech[i] = 0;
                LogEvent(i, "civ", "civ.start", RichYears[i], room, Share(i, 3));
                continue;
            }
            int stage = TechStage(i);
            // the biosphere feeds the people: population follows what life there is, and starves when it fails
            p = room >= C.CivLifeMin / 5 ? room / (1 + (room / p - 1) * Math.Exp(-C.CivGrowth * dt)) : p * Math.Exp(-dt / C.CivDecayYears);
            if (p < C.CivSeed / 10)
            {
                Pop[i] = 0; Tech[i] = 0;
                LogEvent(i, "civ", "civ.end", room, Temp[i], stage);
                continue;
            }
            Pop[i] = p;
            Tech[i] += C.CivTechRate * p * Math.Min(1, Share(i, 3) / C.CivMetalRef) * dt;
            int after = TechStage(i);
            if (after != stage) LogEvent(i, "civ", $"civ.stage.{stage}.{after}", Tech[i], p, Share(i, 3));
        }
    }
    // ponytail: a civilisation does not use up the planet's metal yet, and lives on one planet only
    // (shared object over several planets = SPEC 2, not built).

    // Called from Merge before the masses are summed: `share` = mass of what hit / mass of the one that stays.
    void Impact(int k, double share)
    {
        if (Life[k] <= 0 && Pop[k] <= 0) return;
        double keep = Math.Exp(-share / C.ImpactScale), before = Life[k];
        Life[k] *= keep; Pop[k] *= keep;
        if (share >= C.ImpactScale / 10) LogEvent(k, "impact", "impact", share, before, Life[k]);
    }

    void HashLayers(Action<ulong> mix)
    {
        void number(double n) => mix(BitConverter.DoubleToUInt64Bits(n));
        for (int i = 0; i < N; i++)
        {
            if (!Alive[i]) continue;
            mix((ulong)Water[i]); number(WaterYears[i]); number(Life[i]); number(RichYears[i]); number(Pop[i]); number(Tech[i]);
        }
    }
}
