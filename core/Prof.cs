using System.Collections.Generic;

namespace Cosmos.Core;

/// Counts performed work, not time. Diagnostic state never participates in physics or Hash.
public readonly record struct WorkCounters(long Advances, long Substeps, long GravityPairs,
    long ContactSweeps, long RocheCollections, long RocheAdmissionPairs, long RocheCandidateChecks,
    long RuleApplications, long Merges);

/// A chunk/kernel returns counts; only its caller reduces them into World diagnostics.
public readonly record struct KickWork(long GravityPairs, long ContactSweeps);

public sealed partial class World
{
    long _profAdvances, _profSubsteps, _profGravityPairs, _profContactSweeps;
    long _profRocheCollections, _profRocheAdmissionPairs, _profRocheCandidateChecks;
    long _profRuleApplications, _profMergeOrigin;
    readonly KickWork[] _chunkWork = new KickWork[MaxRockChunks];
    readonly Dictionary<string, long> _profRules = new();

    /// Read/reset only between simulation operations, under the same exclusive ownership as World.
    public WorkCounters Prof => new(_profAdvances, _profSubsteps, _profGravityPairs, _profContactSweeps,
        _profRocheCollections, _profRocheAdmissionPairs, _profRocheCandidateChecks,
        _profRuleApplications, Merges - _profMergeOrigin);

    public long RuleApplications(string id) => _profRules.TryGetValue(id, out long count) ? count : 0;

    public void ResetProf()
    {
        _profAdvances = _profSubsteps = _profGravityPairs = _profContactSweeps = 0;
        _profRocheCollections = _profRocheAdmissionPairs = _profRocheCandidateChecks = 0;
        _profRuleApplications = 0; _profMergeOrigin = Merges;
        _profRules.Clear();
    }
}
