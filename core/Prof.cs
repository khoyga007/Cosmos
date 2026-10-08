using System.Collections.Generic;

namespace Cosmos.Core;

/// Counts performed work, not time. Diagnostic state never participates in physics or Hash.
public readonly record struct WorkCounters(long Advances, long Substeps, long GravityPairs,
    long ContactSweeps, long RocheCollections, long RocheAdmissionPairs, long RocheCandidateChecks,
    long RuleApplications, long Merges);

public sealed partial class World
{
    // Keep cold diagnostic fields together rather than expanding the hot World's scalar layout.
    sealed class WorkState
    {
        public long Advances, Substeps, GravityPairs, ContactSweeps;
        public long RocheCollections, RocheAdmissionPairs, RocheCandidateChecks, RuleApplications, MergeOrigin;
        public readonly long[] ChunkSweeps = new long[MaxRockChunks];
        public readonly Dictionary<string, long> Rules = new();
    }
    readonly WorkState _prof = new();

    /// Read/reset only between simulation operations, under the same exclusive ownership as World.
    public WorkCounters Prof => new(_prof.Advances, _prof.Substeps, _prof.GravityPairs, _prof.ContactSweeps,
        _prof.RocheCollections, _prof.RocheAdmissionPairs, _prof.RocheCandidateChecks,
        _prof.RuleApplications, Merges - _prof.MergeOrigin);

    public long RuleApplications(string id) => _prof.Rules.TryGetValue(id, out long count) ? count : 0;

    public void ResetProf()
    {
        _prof.Advances = _prof.Substeps = _prof.GravityPairs = _prof.ContactSweeps = 0;
        _prof.RocheCollections = _prof.RocheAdmissionPairs = _prof.RocheCandidateChecks = 0;
        _prof.RuleApplications = 0; _prof.MergeOrigin = Merges;
        _prof.Rules.Clear();
    }
}
