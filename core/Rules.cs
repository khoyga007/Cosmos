using System;
using System.Collections.Generic;

namespace Cosmos.Core;

public sealed partial class Consts
{
    // Reference Earth orbit at G=1; changing G never recalculates the calendar.
    public double YearTime = Math.Tau * Math.Sqrt(45 * 45 * 45 / 50.0);
    // PLACEHOLDER [P]: Yang has not approved these numbers. Toy Kelvin scale,
    // normalised to Sol's Earth; the god may tune every field through SetConst.
    public double LuminosityExponent = 3.5;
    public double TemperatureScale = 288;
    public double FrozenEdge = 240;
    public double ScorchedEdge = 350;
}

public enum TemperatureBand { Frozen, Temperate, Scorched }

/// One rule per parameter, evaluated in table order at the first Advance reaching its due year.
public sealed class Rule
{
    public string Id { get; }
    public string Reads { get; }
    public string Writes { get; }
    public bool Enabled = true;
    public bool NeedsRockPositions { get; init; }
    public Action<World> Apply { get; }
    public double NextYear { get; internal set; }
    public double LastYear { get; internal set; } // year of its last run; World.RuleYears = years since then
    double _rhythmYears;
    public double RhythmYears
    {
        get => _rhythmYears;
        set
        {
            if (!double.IsFinite(value) || value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
            _rhythmYears = value;
        }
    }

    public Rule(string id, string reads, string writes, double rhythmYears, Action<World> apply)
    {
        Id = id; Reads = reads; Writes = writes; RhythmYears = rhythmYears; Apply = apply;
    }
}

// Change is a code, never player-facing text. A/B/C hold at most three cause numbers.
public readonly record struct RuleEvent(double Year, int ObjectSlot, string RuleId, string Change,
    double A = 0, double B = 0, double C = 0);

public sealed partial class World
{
    public double Year { get; private set; }
    /// Inside a rule: years passed since that rule last ran. Clock rules (age, growth) must use this, never the rhythm:
    /// after a jump or a pause one run stands for all the years skipped.
    public double RuleYears { get; private set; }
    public readonly double[] Temp;
    readonly int[] _temperatureBands, _starSlots;
    readonly double[] _starLight;
    public readonly List<Rule> Rules = new();
    public const int EventCapacity = 1024; // PLACEHOLDER [P], not a content decision by Yang
    readonly List<RuleEvent> _events = new();
    public IReadOnlyList<RuleEvent> Events => _events;

    // PLACEHOLDER [P] rhythm: 0.01 years. No life/water/civilisation rules yet.
    void InitRules()
    {
        InitStarRule();
        Rules.Add(new Rule("temperature", "M,X,Y,StarMass,LuminosityExponent,TemperatureScale,FrozenEdge,ScorchedEdge", "Temp,temperature.band", 0.01,
            w => w.UpdateTemperature()));
        InitLayerRules();
        InitKindRules();
    }

    void ResetTemperature(int i) { Temp[i] = double.NaN; _temperatureBands[i] = -1; }

    public TemperatureBand BandOf(double temperature) => temperature < C.FrozenEdge ? TemperatureBand.Frozen
        : temperature < C.ScorchedEdge ? TemperatureBand.Temperate : TemperatureBand.Scorched;

    void UpdateTemperature()
    {
        int stars = 0;
        for (int i = 0; i < N; i++) if (Alive[i] && StarLuminosity(i) > 0)
        {
            _starSlots[stars] = i;
            _starLight[stars++] = StarLuminosity(i) * 45 * 45;
        }
        // ponytail: O(objects * stars); enough for the system tier, revisit at galaxy scale.
        for (int i = 0; i < N; i++)
        {
            if (!Alive[i] || _deferRocks && !Attracts(i)) continue;
            double flux = 0;
            int host = RailStar(i, out double meanInvD2); // in a jump: the star it orbits counts by its orbit average
            for (int s = 0; s < stars; s++)
            {
                int j = _starSlots[s];
                if (j == host) { flux += _starLight[s] * meanInvD2; continue; }
                if (i == j) continue; // incoming light, not a model of a star's internal temperature
                double dx = X[i] - X[j], dy = Y[i] - Y[j], d2 = dx * dx + dy * dy;
                if (d2 > 0) flux += _starLight[s] / d2;
            }
            double before = Temp[i], after = C.TemperatureScale * Math.Sqrt(Math.Sqrt(flux));
            int band = (int)BandOf(after), oldBand = _temperatureBands[i];
            Temp[i] = after; _temperatureBands[i] = band;
            if (oldBand >= 0 && oldBand != band && KindOf(i) == Kind.Planet)
                LogEvent(i, "temperature", $"band.{oldBand}.{band}", before, after, flux);
        }
    }

    public void LogEvent(int objectSlot, string ruleId, string change, double a = 0, double b = 0, double c = 0)
    {
        if (_events.Count == EventCapacity) _events.RemoveAt(0);
        _events.Add(new RuleEvent(Year, objectSlot, ruleId, change, a, b, c));
    }

    void RunRules()
    {
        foreach (var rule in Rules)
        {
            if (!rule.Enabled || Year < rule.NextYear) continue;
            RuleYears = Year - rule.LastYear;
            rule.Apply(this);
            rule.LastYear = Year;
            // No historical physics states exist: skipped ticks coalesce at the current year.
            rule.NextYear = (Math.Floor(Year / rule.RhythmYears) + 1) * rule.RhythmYears;
            if (ReferenceEquals(rule, _starRule)) rule.NextYear = Math.Min(rule.NextYear, NextStarBoundary());
        }
    }

    void HashRules(Action<ulong> mix)
    {
        void number(double n) => mix(BitConverter.DoubleToUInt64Bits(n));
        void code(string s) { mix((ulong)s.Length); foreach (char c in s) mix(c); }
        number(Year);
        for (int i = 0; i < N; i++) if (Alive[i]) { number(Temp[i]); mix((ulong)_temperatureBands[i]); }
        foreach (var rule in Rules) { code(rule.Id); number(rule.RhythmYears); number(rule.NextYear); number(rule.LastYear); mix(rule.Enabled ? 1UL : 0UL); }
        mix((ulong)_events.Count);
        foreach (var e in _events)
        {
            number(e.Year); mix((ulong)e.ObjectSlot); code(e.RuleId); code(e.Change); number(e.A); number(e.B); number(e.C);
        }
    }
}
