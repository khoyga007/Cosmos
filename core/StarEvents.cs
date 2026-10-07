using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Cosmos.Core;

public enum StarTransition { Giant, Death, Merger }
public enum MassComparison { Less, LessOrEqual, Greater, GreaterOrEqual }
public enum StarEventPayload { Basic, Giant, Nova, Remnant }
public enum EjectionMode { RetainedSolarMass, Fraction, ExpelledSolarMass }

public readonly record struct StarParameter(double Value = 0, string? Constant = null)
{
    public double Read(Consts c) => Constant == null ? Value : (double)typeof(Consts).GetField(Constant)!.GetValue(c)!;
    internal void Validate()
    {
        if (!double.IsFinite(Value) || Value < 0 || Constant != null && typeof(Consts).GetField(Constant)?.FieldType != typeof(double))
            throw new ArgumentException("Star parameters require non-negative finite values or an existing scalar Consts field.");
    }
}

public readonly record struct ProgenitorCondition(MassComparison Comparison, StarParameter Threshold)
{
    internal bool Matches(Consts c, double birth) => Comparison switch
    {
        MassComparison.Less => birth < Threshold.Read(c), MassComparison.LessOrEqual => birth <= Threshold.Read(c),
        MassComparison.Greater => birth > Threshold.Read(c), MassComparison.GreaterOrEqual => birth >= Threshold.Read(c),
        _ => throw new ArgumentOutOfRangeException(nameof(Comparison))
    };
}

// A single recipe computes retained and expelled mass together at the current progenitor mass; neither is pinned separately.
public sealed record EjectionRule(EjectionMode Mode, StarParameter Intercept = default, StarParameter Slope = default,
    StarParameter Minimum = default, StarParameter Fraction = default);

public sealed record StarEvent(string Id, StarTransition Transition, IReadOnlyList<ProgenitorCondition>? Conditions = null,
    StarParameter Range = default, StarParameter Damage = default, EjectionRule? Ejection = null,
    IReadOnlyDictionary<string, double>? Mix = null, StarEventPayload Payload = StarEventPayload.Basic,
    string? ChoiceGroup = null, StarParameter? Chance = null, MergerRule? Merger = null, BurstRule? Burst = null,
    StarParameter SpeedKmS = default);

public static class StarEventCatalog
{
    public static readonly IReadOnlyList<StarEvent> Events = Build();
    static StarParameter P(string name) => new(Constant: name);
    static ProgenitorCondition C(MassComparison comparison, string name) => new(comparison, P(name));
    static IReadOnlyList<StarEvent> Build()
    {
        var StarEvents = new List<StarEvent>();
        // Coarse six-group yields, not isotopic abundances; gas H/He, rock O/Si, metal Fe, radio Ni56.
        var snMix = new ReadOnlyDictionary<string, double>(new Dictionary<string, double> { ["gas"] = .45, ["rock"] = .40, ["metal"] = .10, ["carbon"] = .04, ["radio"] = .01 });
        var hnMix = new ReadOnlyDictionary<string, double>(new Dictionary<string, double> { ["gas"] = .40, ["rock"] = .38, ["metal"] = .16, ["carbon"] = .04, ["radio"] = .02 });
        var knMix = new ReadOnlyDictionary<string, double>(new Dictionary<string, double> { ["metal"] = .8, ["radio"] = .2 });
        StarEvents.Add(new("star.giant", StarTransition.Giant, Payload: StarEventPayload.Giant));
        StarEvents.Add(new("star.hypernova", StarTransition.Death, new[] { C(MassComparison.GreaterOrEqual, nameof(Consts.HypernovaMinSolar)) }, Range: P(nameof(Consts.HypernovaRange)), Damage: P(nameof(Consts.HypernovaDamage)), Mix: hnMix, Payload: StarEventPayload.Nova, ChoiceGroup: "collapse", Chance: P(nameof(Consts.HypernovaChance)), Burst: new("grb.long", P(nameof(Consts.GrbLongEnergyIsoJ)))));
        // Nova/HN selects a shell mix; the following remnant recipe expels it once, from the same transition.
        StarEvents.Add(new("star.nova", StarTransition.Death, new[] { C(MassComparison.GreaterOrEqual, nameof(Consts.StarWhiteLimit)) }, Range: P(nameof(Consts.StarNovaRange)), Damage: P(nameof(Consts.StarNovaDamage)), Mix: snMix, Payload: StarEventPayload.Nova, ChoiceGroup: "collapse"));
        StarEvents.Add(new("star.remnant.white", StarTransition.Death, Array.AsReadOnly(new[] { C(MassComparison.Less, nameof(Consts.StarWhiteLimit)) }), Ejection: new(EjectionMode.RetainedSolarMass, Intercept: P(nameof(Consts.StarWhiteIntercept)), Slope: P(nameof(Consts.StarWhiteSlope))), Payload: StarEventPayload.Remnant, SpeedKmS: P(nameof(Consts.EnvelopeSpeedKmS))));
        StarEvents.Add(new("star.remnant.neutron", StarTransition.Death, Array.AsReadOnly(new[] { C(MassComparison.GreaterOrEqual, nameof(Consts.StarWhiteLimit)), C(MassComparison.LessOrEqual, nameof(Consts.StarNeutronLimit)) }), Ejection: new(EjectionMode.RetainedSolarMass, Intercept: P(nameof(Consts.StarNeutronMass))), Payload: StarEventPayload.Remnant, SpeedKmS: P(nameof(Consts.EjectaSpeedKmS))));
        StarEvents.Add(new("star.remnant.black", StarTransition.Death, Array.AsReadOnly(new[] { C(MassComparison.GreaterOrEqual, nameof(Consts.StarWhiteLimit)), C(MassComparison.Greater, nameof(Consts.StarNeutronLimit)) }), Ejection: new(EjectionMode.RetainedSolarMass, Slope: P(nameof(Consts.StarBlackFraction)), Minimum: P(nameof(Consts.StarBlackMin))), Payload: StarEventPayload.Remnant, SpeedKmS: P(nameof(Consts.EjectaSpeedKmS))));
        StarEvents.Add(new("star.kilonova.nsns", StarTransition.Merger, Range: P(nameof(Consts.KilonovaRange)), Damage: P(nameof(Consts.KilonovaDamage)), Ejection: new(EjectionMode.ExpelledSolarMass, Intercept: P(nameof(Consts.KilonovaEjectaSolar))), Mix: knMix, Merger: new(StarPhase.NeutronStar, StarPhase.NeutronStar), Burst: new("grb.short", P(nameof(Consts.GrbShortEnergyIsoJ))), SpeedKmS: P(nameof(Consts.KilonovaSpeedKmS))));
        StarEvents.Add(new("star.kilonova.nsbh", StarTransition.Merger, Range: P(nameof(Consts.KilonovaRange)), Damage: P(nameof(Consts.KilonovaDamage)), Ejection: new(EjectionMode.ExpelledSolarMass, Intercept: P(nameof(Consts.KilonovaEjectaSolar))), Mix: knMix, Merger: new(StarPhase.NeutronStar, StarPhase.BlackHole, true), Burst: new("grb.short", P(nameof(Consts.GrbShortEnergyIsoJ))), SpeedKmS: P(nameof(Consts.KilonovaSpeedKmS))));
        return StarEvents.AsReadOnly();
    }

    internal static bool Same(StarEvent a, StarEvent b) => a.Id == b.Id && a.Transition == b.Transition
        && (a.Conditions ?? Array.Empty<ProgenitorCondition>()).SequenceEqual(b.Conditions ?? Array.Empty<ProgenitorCondition>())
        && a.Range == b.Range && a.Damage == b.Damage && a.Ejection == b.Ejection && a.Payload == b.Payload
        && a.ChoiceGroup == b.ChoiceGroup && a.Chance == b.Chance && a.Merger == b.Merger && a.Burst == b.Burst && a.SpeedKmS == b.SpeedKmS
        && (a.Mix == null && b.Mix == null || a.Mix != null && b.Mix != null
            && a.Mix.OrderBy(x => x.Key, StringComparer.Ordinal).SequenceEqual(b.Mix.OrderBy(x => x.Key, StringComparer.Ordinal)));
}

public sealed partial class World
{
    public readonly IReadOnlyList<StarEvent> StarEvents;
    readonly bool _legacyStarEvents;

    StarEvent SnapshotStarEvent(StarEvent row)
    {
        if (string.IsNullOrWhiteSpace(row.Id) || !Enum.IsDefined(row.Transition) || !Enum.IsDefined(row.Payload))
            throw new ArgumentException("A stellar event needs an id and a supported physical transition/payload.");
        var conditions = (row.Conditions ?? Array.Empty<ProgenitorCondition>()).ToArray();
        foreach (var condition in conditions)
        {
            condition.Threshold.Validate();
            if (!Enum.IsDefined(condition.Comparison)) throw new ArgumentException("Unknown progenitor comparison.");
        }
        row.Range.Validate(); row.Damage.Validate();
        row.SpeedKmS.Validate(); row.Chance?.Validate(); row.Burst?.EnergyIso.Validate();
        if (row.Chance is { } chance && chance.Read(C) > 1 || row.Merger != null && (row.Transition != StarTransition.Merger || !Enum.IsDefined(row.Merger.First) || !Enum.IsDefined(row.Merger.Second))
            || row.Transition == StarTransition.Merger && row.Merger == null || row.Burst != null && string.IsNullOrWhiteSpace(row.Burst.Id)) throw new ArgumentException("Invalid event chance, pair or burst.");
        if (row.Ejection != null)
        {
            if (!Enum.IsDefined(row.Ejection.Mode)) throw new ArgumentException("Unknown ejection rule.");
            row.Ejection.Intercept.Validate(); row.Ejection.Slope.Validate(); row.Ejection.Minimum.Validate(); row.Ejection.Fraction.Validate();
            if (row.Ejection.Mode == EjectionMode.Fraction && row.Ejection.Fraction.Read(C) > 1)
                throw new ArgumentException("An ejected fraction must be within 0..1.");
        }
        IReadOnlyDictionary<string, double>? mix = null;
        if (row.Mix != null)
        {
            if (row.Ejection == null && row.Payload != StarEventPayload.Nova) throw new ArgumentException("An ejecta mix requires an ejection rule or shell selector.");
            var copy = new Dictionary<string, double>(StringComparer.Ordinal); double sum = 0;
            foreach (var (id, share) in row.Mix)
            {
                if (Elem(id) < 0 || !double.IsFinite(share) || share < 0) throw new ArgumentException("Unknown ejecta element or invalid share.");
                copy.Add(id, share); sum += share;
            }
            if (Math.Abs(sum - 1) > 1e-11) throw new ArgumentException("Ejecta shares must sum to one.");
            mix = new ReadOnlyDictionary<string, double>(copy);
        }
        return row with { Conditions = Array.AsReadOnly(conditions), Mix = mix };
    }

    void ApplyStarEvents(int i, StarTransition transition, StarPhase first = StarPhase.None, double firstMass = 0, StarPhase second = StarPhase.None, double secondMass = 0)
    {
        double birth = StarInitialMass[i] / C.StarSolarMass;
        var chosen = new HashSet<string>(StringComparer.Ordinal);
        IReadOnlyDictionary<string, double>? shellMix = null;
        string? shellCause = null;
        foreach (StarEvent row in StarEvents)
        {
            if (!Alive[i]) break;
            if (row.Transition != transition || !row.Conditions!.All(c => c.Matches(C, birth))) continue;
            double available = double.PositiveInfinity;
            if (row.Merger is { } pair)
            {
                if (!(first == pair.First && second == pair.Second || second == pair.First && first == pair.Second)) continue;
                if (pair.RequiresDisruption) { available = DisruptedMass(first, firstMass, second, secondMass); if (!(available > 0)) continue; }
            }
            if (row.ChoiceGroup != null && chosen.Contains(row.ChoiceGroup)) continue;
            if (row.Chance is { } chance && Next() >= Math.Clamp(chance.Read(C), 0, 1)) continue;
            if (row.ChoiceGroup != null) chosen.Add(row.ChoiceGroup);
            if (row.Payload == StarEventPayload.Nova) { shellMix = row.Mix; shellCause = row.Id; }
            double range = row.Range.Read(C), damage = row.Damage.Read(C);
            // Nova's original record precedes its blast; the table makes that payload ordering explicit.
            if (row.Payload == StarEventPayload.Nova) LogEvent(i, "stars", row.Id, birth, M[i], range);
            if (range > 0)
            {
                for (int j = 0; j < N; j++) if (j != i && Alive[j] && IsWorld(j))
                {
                    double dx = X[j] - X[i], dy = Y[j] - Y[i], distance = Math.Sqrt(dx * dx + dy * dy);
                    if (distance < range) Impact(j, C.ImpactScale * damage * Math.Pow(1 - distance / range, 2));
                }
            }
            if (row.Ejection != null) { Eject(i, row.Ejection, row.Mix ?? shellMix, row.Mix != null ? row.Id : shellCause ?? row.Id, row.SpeedKmS.Read(C), available); if (Alive[i]) SetRadius(i); }
            if (row.Burst != null) EmitBurst(i, row.Burst);
            if (row.Payload == StarEventPayload.Giant) LogEvent(i, "stars", row.Id, birth, R[i], StarLuminosity(i));
            else if (row.Payload == StarEventPayload.Remnant || row.Payload == StarEventPayload.Basic)
                LogEvent(i, "stars", row.Id, birth, M[i] / C.StarSolarMass, StellarEjectaMass);
        }
    }

    void Eject(int i, EjectionRule mass, IReadOnlyDictionary<string, double>? mix, string cause, double speed, double available)
    {
        double birth = StarInitialMass[i] / C.StarSolarMass;
        double target = mass.Mode == EjectionMode.RetainedSolarMass
            ? Math.Max(mass.Minimum.Read(C), mass.Intercept.Read(C) + mass.Slope.Read(C) * birth) * C.StarSolarMass
            : mass.Mode == EjectionMode.ExpelledSolarMass ? M[i] - Math.Min(available, mass.Intercept.Read(C) * C.StarSolarMass)
            : M[i] * (1 - Math.Clamp(mass.Fraction.Read(C), 0, 1));
        double keptMass = Math.Clamp(target, 0, M[i]), expelled = M[i] - keptMass, fraction = keptMass / M[i];
        StellarEjectaMass += expelled; StellarEjectaPx += expelled * Vx[i]; StellarEjectaPy += expelled * Vy[i];
        var matter = new double[ElementCount];
        for (int e = 0; e < ElementCount; e++)
        {
            int offset = i * ElementCount + e; double kept = Comp[offset] * fraction;
            matter[e] = Comp[offset] - kept;
            if (mix != null) NucleosynthesisDelta[e] -= matter[e];
            Comp[offset] = kept;
        }
        // Explicit rows describe transmutation of expelled material, not a withdrawal from a possibly absent reservoir.
        if (mix != null)
        {
            Array.Clear(matter);
            foreach (var (id, share) in mix) { int e = Elem(id); matter[e] = expelled * share; NucleosynthesisDelta[e] += matter[e]; }
        }
        for (int e = 0; e < ElementCount; e++) StellarEjectaMatter[e] += matter[e];
        M[i] = keptMass;
        DistributeEjecta(i, cause, expelled, matter, speed);
        if (keptMass == 0) Kill(i);
    }

    void HashStarEvents(Action<ulong> mix)
    {
        if (_legacyStarEvents) return;
        void Text(string? value) { mix((ulong)(value?.Length ?? 0)); if (value != null) foreach (char c in value) mix(c); }
        void Parameter(StarParameter p) { mix(BitConverter.DoubleToUInt64Bits(p.Value)); Text(p.Constant); }
        mix((ulong)StarEvents.Count);
        foreach (var row in StarEvents)
        {
            Text(row.Id); mix((ulong)row.Transition); mix((ulong)row.Payload); Parameter(row.Range); Parameter(row.Damage);
            Text(row.ChoiceGroup); mix(row.Chance == null ? 0UL : 1UL); if (row.Chance is { } chance) Parameter(chance);
            mix(row.Merger == null ? 0UL : 1UL); if (row.Merger is { } pair) { mix((ulong)pair.First); mix((ulong)pair.Second); mix(pair.RequiresDisruption ? 1UL : 0UL); }
            mix(row.Burst == null ? 0UL : 1UL); if (row.Burst is { } burst) { Text(burst.Id); Parameter(burst.EnergyIso); }
            Parameter(row.SpeedKmS);
            mix((ulong)row.Conditions!.Count);
            foreach (var condition in row.Conditions) { mix((ulong)condition.Comparison); Parameter(condition.Threshold); }
            mix(row.Ejection == null ? 0UL : 1UL);
            if (row.Ejection != null) { mix((ulong)row.Ejection.Mode); Parameter(row.Ejection.Intercept); Parameter(row.Ejection.Slope); Parameter(row.Ejection.Minimum); Parameter(row.Ejection.Fraction); }
            mix((ulong)(row.Mix?.Count ?? 0));
            if (row.Mix != null) foreach (var (id, share) in row.Mix.OrderBy(x => x.Key, StringComparer.Ordinal)) { Text(id); mix(BitConverter.DoubleToUInt64Bits(share)); }
        }
    }
}
