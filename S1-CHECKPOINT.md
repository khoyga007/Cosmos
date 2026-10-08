# S1 Step0 — local gravity, work counters and scale prerequisites

2026-10-08 updates: rebased onto merged master `9f34bcc`; exact master passed locked fresh fullCLI482/0 and headless selftest replay1CCDBCA414ED430D. Claire ACKed this checkpoint at08:51Z with k2/4/8 sensitivity, nonfinite reporting, quiet-oracle budgets, no-god K10 local/exact event-rate comparison, complete-rule gate and conditional radiation without threshold flips. Memory figures are reporting targets, not blocking gates. PROF and comet correction precede A+B.

At09:00Z Claire announced Yang's approved aggregate-first direction, **pending a new SPEC**: bulk belts/rings/debris become gravitating material distributions and GPU particles; real bodies are promoted when needed and demoted afterward. PROF/full-suite/comets continue; root derivation/scalar seam remain useful. Per-rock FREE500 tuning, per-rock RAIL and per-rock memory investment are paused; historical K200x500 gates will be replaced by an explicit real-body/aggregate workload. No aggregate implementation before the new SPEC. Celine sent conservation, prediction, picking/replay and natural-chaos requirements on Bridge.

Status: **PROF/comet implemented and validated at03a29a7; root/scalar prototype not started**. Branch `celine/scale-step0`, checkout `E:/Cosmos-celine-scale`, rebased onto mergedR+S0 master9f34bcc. Original checkpoint6929f34 was docs-only onf9e7a77; Claire ACKed/amended it at08:51Z. Original F a696c747 was REQUEST_CHANGES; Ariel owns repair and Celine reviews it. Selica has no remaining credit; Celine owns PROF and S0 harness. Original proposals below are retained as the root/scalar design; per-rock targets are historical after the aggregate-first heads-up.

## Evidence and gates

S0 reference: `E:/selica-work/S0-REPORT.md`, CLI-only `fe5ef43`, core unchanged. Yang's i5-10300H, 4C/8T, 16GB is the reference machine. K=200x500 measured1073.960ms/Advance; K=200x5000 has only an8.893–9.352s qualified range, no clean median-seven. Historical data predates perfbox locking and has40–75% load noise. Do not invent a clean baseline or treat R's~6ms small-N NS scene as evidence for200systems.

The fitted coefficient1.5e-9 * rocks * pullers *8 gives **seconds**, not milliseconds. Milliseconds use1.5e-6. With100krocks and8localpullers, the extrapolated gravity-only cost is9.6ms, leaving little of12ms for contact, Roche, rules, roots and scheduling. This is a model, not a measured S1 pass.

Claire's01:59Z gates: S1 A+B with rocks FREE <=12ms total atK=200x500 and local-pair work/rock independent of K; S2 quiet<=1ms/destruction<=4ms; S3<=4ms; stretch destruction2ms; window p95<=20ms. Two phase gate misses require Claire's measured-ceiling decision. No extra micro-patch chain. Prototype then clean rebuild per phase; retain scalar/reference implementations in the validation harness, not duplicate production paths without measured value.

## Ownership and delivery order

1. Rebase on merged R+S0 and record exact base. PROF-only checkpoint first, with physics identical and overhead<=1%; isolate that cost before algorithm changes.
2. Prototype physical-root/local-list derivation plus a scalar kick seam; validate root/admission/force/contact behavior on small adversarial scenes before expensive scale benchmarks.
3. Add command-boundary capacity growth and complete outcome trace; remove N-sized per-chunk/puller scratch. Integrate typed rule indexes and event accounting. Rebuild clean from the measured prototype choices, then capture changed fixtures with reasons.
4. Gate the complete S1 binary on all-rules, recurring due-rule work and exact replay. Ariel can tune the agreed isolated kernel after the scalar contract is proved; SIMD and Rust are separate measured experiments.

Files owned by Celine in this phase: `core/World.cs`, `core/Roche.cs` integration/counters, new `core/LocalGravity.cs`, `core/KickKernel.cs`, `core/Prof.cs`, `core/Capacity.cs`, `core/Commands.cs`, relevant rule/index/lifecycle hooks in Rails/Rules/Layers/Stars/Civ/Kinds, CLI scale/prof/checks, SPEC and append-only PERF-LOG. Coordinate ABI edits with Ariel; no game edits in S1. No source changes in another owner's checkout. Rendering/debug shell retains no pinned Span/array assumption across capacity growth.

## A — physical hierarchy proposal requiring owner ACK

Do not use draw Par as the authority: stock belts and rings deliberately clear it. Do not use FindPrimaryInHierarchy's unconditional heaviest fallback: distant stationary roots must stay independent. Strictly heavier parent edges prevent cycles; ties use slot/Gen. Any root cache is an acceleration structure over one World, not a stored physical partition.

Proposed derivation at initialization/topology change:

- Validate an existing explicit orbit hint against live Gen, strictly heavier mass and negative two-body relative energy. A hint never fences membership; it can be invalidated/reassigned by the current physical state.
- Build finite local envelopes from these bound attachments. For unhinted objects, query nearby candidate primaries through a derived spatial index and test bound energy plus finite Hill/hold region. Root seeds without a physical parent remain roots; distant v=0 alone never establishes ownership of the entire universe.
- In a deterministic mass/slot order, re-evaluate root-parent candidates only in overlapping finite envelopes, then derive each object's physical-primary/root chain. Use existing Kepler calculations to obtain finite bound apoapsides; unbound/nearly-parabolic infinities never become a world-sized envelope. Overflow/nonfinite cases take an explicit conservative fallback and are counted, not silently clamped away.
- R_inf = proposed Const `LocalInfluenceScale=4` times max finite bound apoapsis, with contact-radius floor. This is a numerical admission dial, not a physical constant. Final envelope/Hill test and dial require Claire ACK. Existing bound binary hints remain meaningful; equal-mass binary roots can remain separate roots whose encounter lists are unioned.
- Initial membership is an O(N) pass with local spatial queries. Subsequent create/remove/merge/mass/velocity/primary changes mark deterministic dirty work; normal FREE integration flags escape/crossing while touching that object. Derived caches can be omitted from Hash only when reconstructed from the same canonical state before use. Epoch/history influencing future force or work-budget choices must be hashed.

Unbound rocks have no fictitious infinite root. In an admitted encounter region they use the union's local sources. Outside all regions, use a separately counted far-monopole fallback at the rock's actual position; S3 bounds the number of such FREE projectiles or creates moving aggregates. Do not drop their gravity or fold them into BOUND to pass a gate.

Multi-root physical hierarchy also has to be consumed safely by the current temporary Jump/Ride solver. A p=-1 root drifts as a root, not as an orbit around global slot0. Persistent Advance RAIL tier is S2; hot-interval jump integration/early stop remains S3, so S1 makes no claim that existing long jumps model all destruction.

## B — force and contact contract

Local source lists are ascending stable slot/Gen and contain own pullers plus encounter intruders. Admit current separation or a swept/predicted crossing of finite R_inf, not endpoints alone; a fast NS may traverse a whole region within one substep. Root encounter components temporarily union source lists. Membership/capture/escape has no hard boundary and is rechecked from simulation state.

Remote field: once per Advance, compute barycentric root monopoles in canonical unordered root-pair order. Members inherit their root's common acceleration. Remove root pairs already represented in an exact encounter component to avoid double counting. Never repeat K remote sums for every puller or rock. Root mass/COM uses resolved live member mass; future moving/gravitating aggregates participate in S3. This changes the old passive-rock far-field approximation and must be stated with oracle error measurements. Existing local one-way sub-threshold rock gravity is unchanged in S1; exact global positive-time P/L conservation is not claimed for that inherited model.

Within the admitted list, scalar1/r^2 and swept contact remain exact under the chosen integration law. Pullers are kicked before rock contact tests use their final substep velocities. Remove the inherited surface-acceleration near-cull; a small local list permits direct swept tests after the kick. Measure that extra contact work rather than carrying a blanket surface bound into the new kernel.

Remote gradients outside the local region are omitted. Common-field cadence initially one Advance, never a camera/wall-clock choice. Treat resulting orbital/periapsis error, flyby admission under third-body perturbations and end-substep event timing as model limits, with numerical comparison to an exact all-puller reference. Do not claim a global zero-miss theorem from the sample oracle. Larger cadence than one Advance requires a separate measured physical-error ACK.

## Kernel v1 — agreed with Ariel

`KickBatchDesc`, `[StructLayout(LayoutKind.Sequential, Pack=8)]`, Version1 and SizeBytes validation. Caller pins/reserves buffers for the entire invocation; no topology change while pointers are live. AlignmentBytes0 accepts ordinary pinned heap buffers via unaligned-safe loads;64 is only promised for aligned native allocation.

Inputs: dense double pointers X/Y/Vx/Vy/R, RockSlotMap, RockCount/range; frozen PullerX/Y/GM/R/FinalVx/FinalVy and PullerSlotMap/count; dt; inherited RootAccX/Y (one root per batch, split batches by derived list). Output velocities can be separate buffers and are committed once. `ContactPair` uses explicit dense rock/puller indices; caller translates to stable slot/Gen, deduplicates and sorts by canonical pair/slot order before merge. Gen is caller-owned since topology is frozen during the kernel.

OutContacts, ContactCapacity, OutContactCount, OutOverflow protocol: count means required total, even beyond capacity; count/overflow do not stop evaluating remaining pairs. Caller pre-reserves a checked upper bound for fixed-size chunks (initial512rocks, benchmark512vs1024 later) based on local list size. Overflow is an error or a count/reserve/rerun from original inputs before committing outputs; never truncate contacts or kick twice. Buffers are chunk-local/reused. Allocation failure cannot silently continue a partially committed step.

Kernel returns a small `KickWork` structure (gravity pair tests, swept contact tests/overlaps). No shared Prof mutation, allocation, World reference, delegate, RNG, topology or rule work inside. Fixed chunks, deterministic caller reduction/merge independent of thread count. Scalar reference -> C# Vector256 SIMD -> separately measured Rust C ABI experiment with>=20% keep gate; no native rewrite now.

## S0 blockers included in S1

| Blocker | Planned correction / validation |
|---|---|
| Flat rock x all-puller loop | Local lists/root common field; count local/far work separately and verify no hidden K loop per rock. |
| Fixed World(rocks+512) capacity | Grow every slot-indexed array atomically at the exclusive command boundary; stable slots/Gen and correct new-tail sentinels. Reserve before N changes, with deterministic configured slot ceiling and recorded rejection; existing array references may change. Inventory World/Rules/Layers/Stars/Civ/Rails/Roche caches, not just XYZ. Test deliberately tiny initial capacity through many growths, initialized subsystem arrays, reuse and exact replay. |
| Missing rejected attempts | Record every attempt, Step, immutable command payload and outcome/reason; replay verifies accepted/rejected outcomes and ordering. Keep return semantics. Missing failed attempts proves loss of intent trace; actual same-capacity state divergence is not established merely by inspecting Do. Test state replay and attempted-command trace separately. |
| 511B/object S0 heap | Hot kick/index target<=96B/FREE object; total managed target<=440B/live object including Journal atK200x500, reporting snapshot separately. These are proposed budgets, not measurements. Puller gather arrays scale with P; scratch scales with chunk/local-list size instead of16xN. Keep composition/layer data cold; first avoid scratch amplification before a speculative public data API rewrite. Report actual allocation categories, peak/resident memory and GC. |
| EventCapacity1024 evicts silently | Configurable retention sized for200systems, lifetime monotonic event sequence plus dropped count; keep view-log retention separate from physical pending-event queue. Count every evicted visual record. Hash canonical retained events/drop metadata as appropriate; changing retention never alters physical event execution. RemoveAt(0) O(capacity) becomes a bounded ring/deque. |
| Rules scan N / first-eight stars | Derived sorted stars/worlds/ships/volatile-body indexes updated on all lifecycle and threshold changes; all eligible stars remain represented, including natural remnants. Remove UpdateComets stackalloc8/starCount<8 truncation. Keep RuleYears/calendar/chronicle behavior and material ledgers. Temperature's N x stars remains a potential next bottleneck: list changes alone do not erase it. |

Temperature proposal for separate ACK if profiling requires it: near irradiating stars exact, distant luminosity as a root-common incident flux at rule cadence, with measured temperature/threshold error and a conservative bright-star gradient admission. This extends the gravity approximation to radiation and is **not yet approved**. Do not skip rocks' Temp, drop distant bright stars or alter hash via observational lazy reads just to meet12ms.

## PROF — always-on work, no wall-clock in core

World.Prof is a readonly snapshot; ResetProf is an exclusive diagnostic operation. Counts: Advances, Substeps, gravity pair tests (including separate remote root pairs), swept/current contact tests, Roche admission/sweep tests, actual rule applications (with per-rule visit counts where needed); Merges reuses existing World.Merges. Definitions are stable and documented, not “ticks” inferred from time.

Chunk counters accumulate locally; derive counts algebraically when all pairs are tested, and return actual branch-specific counts for swept checks. Reduce at the caller in fixed chunk order. No Interlocked/shared store per hot pair. Rule/Roche counters sit at caller/admission boundaries. Reset/read cannot influence core work budgets, cadence, tier, RNG or Hash; physics work budgets are separate canonical state. Same input journal and fixed chunk scheme must yield identical counters withThreads1/4/8 as well as identical simulation hashes.

`cli prof` prints per-Advance counters plus measured ms outside core. PROF-only before/after uses seven interleaved sequential pairs against separately built exact baseline DLLs under perfbox. Require exact state/hash equality and median overhead<=1%; record individual raw values, not ratio-of-unrelated-medians. If noise/negative cost overwhelms1%, report inconclusive rather than assert free instrumentation. S0 harness reuse keeps scenario seed1234, H0.5, 4000spacing and per-run fresh scenes.

## Validation and reporting

- Before force changes: exact scalar seam vs old kick/contact outputs on the same admitted lists; Threads1/4/8 equality; full contact-buffer/overflow paths and unchanged ledger closure.
- Root/admission oracle: equal/unequal stationary distant stars, hinted binaries, stock Par=-1 belts/rings, isolated root with no members, unbound infinity, near-parabolic finite orbit, root-slot reuse, mass loss/NS birth, moving/swept intruder crossing between endpoints, encounters chaining A-B-C and separation/capture. Report root counts and no doubled/missing force terms.
- Compare one-step/long-step positions, velocities, orbital energy/periapsis against exact all-puller reference with agreed error bounds; distinguish inherited passive-rock momentum drift from instantaneous event M/P/L/Comp closure. No new arbitrary tolerance is declared a pass without owner agreement.
- Gate matrix K1/10/50/200,500rocks/system;5000 is a separately qualified stretch. Rules ON, include both JIT-warm initial Advance where rules are due and a recurring window>=40Advances. Assert/print actual rule applications; add due-rule cases not represented by that short window. Publish median7, max/due-step samples, local tests/rock, remote tests/root, memory and exact replay hashes. An average cheap window with zero rule fires is not enough.
- NS drop into one/ten regions is descriptive S1 measurement, not a4ms pass claim. S2/S3 also require no-god200-system runs and1e9..1e10-year jumps with actual star death/collision/ejection/breakup counts, fairness/lag and early-stop checks. Natural chaos cannot disappear under RAIL or indexes.
- Acquire Bridge flock perfbox before any gate build/bench/test batch, tell other owners, renew while measuring and release in finally. No own overlaps. Append all raw pass/fail/inconclusive data and exact source/base/runtime/machine to PERF-LOG. Headless checks only; windowed p95 belongs to Yang's render measurement, not a simulated Pick loop.

Proposed numerical acceptance for the small quiet reference samples: normalized acceleration RMS<=1e-3 and max<=1e-2; position error after64Advances<=1e-2 of the member's bound apoapsis; periapsis error<=1e-2 in stable two-body samples. Use an explicit tiny absolute floor when the exact force cancels, print both absolute/relative errors, and do not disguise a large error with a vanishing denominator. These are representation error budgets requiring owner ACK, not empirical results or guarantees for chaotic trajectories. All admitted local force/contact terms must match the scalar reference exactly; no tolerance permits a dropped contact. Event ledger checks reuse existing meaningful conservation tests, and full replay remains bit exact within each binary.

Claire ACKed the requested finite-envelope/k4 law, live-mass monopoles and numerical budgets at08:51Z with the added sensitivity/no-god checks stated at the top. Memory targets are advisory; radiation requires measured top-three cost and no physical threshold flips. The12ms/FREE500 gate is historical pending the new aggregate workload. After PROF/comet, current priority is cross-review F then §15 diagnostic balance ledger; root/scalar remains authorized afterward. New aggregate formation awaits its explicit phase GO.
