// Layers (SPEC 2): parameters that grow on an object when conditions fit. Water sits on temperature, life on water,
// civilisation on life. Each layer is a rule in the table; nothing here is scripted per object, and nothing knows
// "Earth": a planet gets a layer because its numbers fit, and loses it when they stop fitting.
// Every number below is a PLACEHOLDER [P] chosen by Claire (Yang 06/10: "em tu nghi"); all are Consts, so the god
// can turn them at run time. Clock rules use World.RuleYears, so one run after a jump stands for all years skipped.
using System;
using System.Collections.Generic;

namespace Cosmos.Core;

public sealed record Stage(
    string Id,
    string NameVi,
    double TechThreshold,
    bool UsesMetal = false,
    bool CanLaunchShips = false,
    bool CanDome = false
);

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
    public double[] Tech = null!;       // 0 upward; stage determined by Stages table
    public double[] Touched = null!;    // year life or population was last changed from outside the rules (seed, impact)

    public static readonly Stage[] DefaultStages = new[]
    {
        new Stage("primitive", "Chưa phát triển", 0, UsesMetal: false, CanLaunchShips: false, CanDome: false),
        new Stage("farming", "Thời kỳ Nông nghiệp", 1, UsesMetal: false, CanLaunchShips: false, CanDome: false),
        new Stage("industry", "Thời kỳ Công nghiệp", 2, UsesMetal: true, CanLaunchShips: false, CanDome: false),
        new Stage("space", "Kỷ nguyên Không gian", 3, UsesMetal: true, CanLaunchShips: true, CanDome: true)
    };

    public readonly List<Stage> Stages = new();
    public static int MaxTechStage => DefaultStages.Length - 1; // Stages
    static readonly double[] LifeStages = { 0.01, 0.1, 0.5 }; // Stages

    void InitLayers(int capacity)
    {
        Water = new int[capacity]; WaterYears = new double[capacity]; Life = new double[capacity];
        RichYears = new double[capacity]; Pop = new double[capacity]; Tech = new double[capacity]; Touched = new double[capacity];
        Stages.Clear(); Stages.AddRange(DefaultStages);
        InitCiv(capacity);
    }

    void ResetLayers(int i) { Water[i] = -1; WaterYears[i] = Life[i] = RichYears[i] = Pop[i] = Tech[i] = Touched[i] = 0; ResetCiv(i); }

    void InitLayerRules()
    {
        Rules.Add(new Rule("water", "Temp,Comp,M,WaterFreeze,WaterBoil,WaterIceMin,WaterHoldMass", "Water,WaterYears", 1, w => w.UpdateWater()));
        Rules.Add(new Rule("life", "Water,WaterYears,Comp,Life*", "Life,RichYears", 1000, w => w.UpdateLife()));
        Rules.Add(new Rule("civ", "Life,RichYears,Comp,Civ*", "Pop,Tech,Comp,Civ", 100, w => w.UpdateCiv()));
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
        for (int i = 0; i < N; i++)
        {
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
            // logistic growth toward 1, written so that any dt gives the same curve
            l = fits ? 1 / (1 + (1 / l - 1) * Math.Exp(-C.LifeGrowth * dt)) : l * Math.Exp(-dt / C.LifeDecayYears);
            if (l < C.LifeSeed / 10)
            {
                Life[i] = 0; RichYears[i] = 0;
                LogEvent(i, "life", "life.end", Temp[i], Water[i], stage);
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
                dt = Math.Min(dt, RichYears[i] - C.CivRiseYears); // it has been there since the wait was over
                CivEvent(i, Civ[i], "civ.start", RichYears[i], room, Share(i, ElementRole.Metal), Year - dt);
            }
            int stage = TechStage(i);
            double p0 = p, lived; // lived = population summed over the stretch (people * years), exact for both curves
            // the biosphere feeds the people: population follows what life there is, and starves when it fails
            double PopAt(double years) => room > 0 ? room / (1 + (room / p0 - 1) * Math.Exp(-C.CivGrowth * years)) : p0 * Math.Exp(-years / C.CivDecayYears);
            double LivedTo(double years)
            {
                double q = PopAt(years);
                return room > 0 ? (C.CivGrowth > 0 ? room * years + room / C.CivGrowth * Math.Log(p0 / q) : p0 * years) : C.CivDecayYears * (p0 - q);
            }
            p = PopAt(dt); lived = LivedTo(dt);
            if (p < C.CivSeed / 10)
            {
                Pop[i] = 0; Tech[i] = 0;
                CivEvent(i, Civ[i], "civ.end", room, Temp[i], stage);
                continue;
            }
            Pop[i] = p;
            double metal = C.CivMetalRef > 0 ? Math.Min(1, Share(i, ElementRole.Metal) / C.CivMetalRef) : 1; // 0 = metal not needed
            // nothing to learn past the last stage: tech stops one full step above its threshold (a new row in Stages moves the ceiling)
            double tech0 = Tech[i], rate = C.CivTechRate * metal, top = Stages.Count > 0 ? Stages[^1].TechThreshold + 1 : 0;
            if (tech0 < top) Tech[i] = Math.Min(top, tech0 + rate * lived);
            if (stage < Stages.Count && Stages[stage].UsesMetal) UseMetal(i, lived);
            int after = TechStage(i);
            _launchYear[i] = double.NaN;
            if (after > stage && rate > 0)
                // each stage it passed gets its own line, at the year its tech got there (found on the same curves)
                for (int k = stage + 1; k <= after; k++)
                {
                    double need = (Stages[k].TechThreshold - tech0) / rate, lo = 0, hi = dt;
                    for (int it = 0; it < 50; it++) { double mid = 0.5 * (lo + hi); if (LivedTo(mid) < need) lo = mid; else hi = mid; }
                    double when = Year - dt + hi;
                    CivEvent(i, Civ[i], $"civ.stage.{k - 1}.{k}", k == after ? Tech[i] : Stages[k].TechThreshold, PopAt(hi), Share(i, ElementRole.Metal), when);
                    if (Stages[k].CanLaunchShips && !Stages[k - 1].CanLaunchShips) _launchYear[i] = when;
                }
            else if (after != stage) CivEvent(i, Civ[i], $"civ.stage.{stage}.{after}", Tech[i], p, Share(i, ElementRole.Metal));
        }
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
        }
        HashCiv(mix);
    }
}
