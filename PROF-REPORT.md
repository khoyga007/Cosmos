# PROF and multi-star comet correction — 2026-10-08

Branch `celine/scale-step0`, merged base9f34bcc. PROF selected core4783f49; separate comet correction03a29a7. No A+B or aggregate implementation, no merge/push. Claire ACKed PROF/comets and the root/scalar checkpoint; at09:00Z the aggregate-first direction superseded per-rock investment pending a new SPEC. Celine owns the former Selica S0 harness/counters.

## Exact merged master validation

Under Bridge flock perfbox, freshly built exact9f34bcc in `E:/Temp/cosmos-prof-base-9f34bcc`: full ReleaseCLI482OK/0FAILED/exit0, audit4D/3R; embedded NS median7=7.456363ms (intermediate R8ms pass,4ms fail). Fresh game build0errors/0warnings; Godot headless selftest13commands exact replay1CCDBCA414ED430D/exit0. Raw logs under `E:/Temp/cosmos-prof-study/master-9f34bcc-*`. No windowed FPS claim.

## Work API / semantics

`World.Prof` returns a value snapshot; `ResetProf()` resets diagnostics exclusively between simulation operations. Neither read/reset nor values participate in physics, RNG, scheduling or Hash. Do not query raw World from the UI while its worker runs; put the snapshot into the worker's published frame/result.

| Counter | Work counted |
|---|---|
| Advances | Validated Advance invocations; a later failing invocation can have partial work. |
| Substeps | AdvanceSlice invocations, including h=0. |
| GravityPairs | Directed body/rock-source distance/force pair tests, self excluded, including current overlaps where force evaluation is skipped. At frozen membership P*(Live-1) is exact. |
| ContactSweeps | Ordinary puller-puller and near-rock swept tests actually invoked; current-overlap tests already occur with gravity pair tests. Roche sweeps are accounted in Roche work. |
| RocheCollections | Enabled collection invocations. |
| RocheAdmissionPairs | Host rows tested by AppendRocheCandidates, including early rejects. |
| RocheCandidateChecks | Candidate rows visited by quantum/entry checks and eligible rock-primary planning queries. This is not a timer or a claim to count every scalar operation. |
| RuleApplications | Actual rule application invocations; `RuleApplications(id)` gives the per-id count. No inferred cost for a rule that did not run. |
| Merges | Existing World.Merges minus reset origin; reset never alters physics merges. |

Cold diagnostic storage is a separate object. Each chunk collects swept-call count locally and writes once, then caller reduces in fixed chunk order. Gravity population is counted algebraically at caller, avoiding a per-rock increment. No shared hot-pair stores or Interlocked calls; no clock reads in core. Future raw-pointer kernel returns count data to its caller; this prototype does not implement that kernel or topology.

CLI: `prof [rocks=5000] [steps=40]` / `--prof` prints overall ms outside core plus work/per-rule calls; `prof-check` asserts meaningful counts/replay/thread equality; `prof-paired <baseline CLI.dll>` compares identical S0 command scenes. Counts cannot establish the top-three time costs needed to authorize radiation approximation; separate measured phase attribution remains required.

## Validation and measured cost

Nine targeted checks pass on v1 and selected v2: analytic2puller+1rock pair counts, real swept contact/merge, diagnostic-only read/reset, invalid Advance, identical hashes and work withThreads1/4/8, journal replay and actual due-rule/Roche calls.40-Advance reference hash9C8C9D5B8B58BB2C; work repeats exactly16,796,800gravity pairs,14,400contact sweeps,40Roche collections,2,099,600admission pairs,504candidate checks,7rule applications.

Reference hardware i5-10300H4C/8T/16GB. All timed builds/checks used advisory Bridge perfbox; own heavy jobs were sequential. Both old/new binaries loaded through the same collectible ALC route with the same private S0 scene builder, seed1234,H=.5. Two warm Advances; measured40 atK50x500 and8 atK200x500. Temperature actually runs7/1 times, respectively. Scene build/hash/output excluded from timing; fresh scene every measurement; seven interleaved pairs with alternating order.

| Source | K50x500 median overhead | K200x500 median overhead | Gate<=1% |
|---|---:|---:|---|
| v1 232ae53 | +2.660589% | -0.082635% | FAIL, exit1 |
| v2 4783f49 | -1.974807% | -0.270523% | PASS, exit0 |

All28 paired hashes match baseline within their scene across both prototypes: K50=14EE5EF1A27629AD, K200=6D69AFA88197064D. Raw `prof-v1-paired.log` / `prof-v2-paired.log` in `E:/Temp/cosmos-prof-study`. Every failure remains in append-only PERF-LOG. Negative median estimates do not prove negative instrumentation cost; no null-control confidence interval was measured. Even locked K200 baseline spans2.530–3.041s in v2 and2.986–4.032s in v1, so runtime/load/thermal variation remains material, cause unverified. ALC times are not direct CLI absolute gates, and these historical FREE-rock scenes do not certify the upcoming aggregate workload.

No further PROF micro-patch after the second prototype. Cost gate is for PROF-only core4783f49; the later comet list change and future architecture have not been isolated in a new overhead experiment.

Final combined source03a29a7: fresh fullCLI **499OK/0FAILED/exit0**, audit4D/3R; NS median7 **6.918538ms**, R8ms pass / architecture4ms fail. Full log ended09:41:23Z while perfbox remained valid to09:46:49Z. Fresh game build0errors/0warnings; headless selftest13commands exact1CCDBCA414ED430D/exit0, UITEST29commands exactE393FCDFD0364E2B/exit0. Those game checks ended09:51:09Z/09:51:25Z **after the advisory lock expired** and are functional results only. Built-in game UI is the synchronous master path, not certification of Ariel's future worker production path. All results observed from completed process/log; an earlier Bridge UITEST claim based on the expected hash was retracted and corrected. Raw `prof-comet-final-*.log` under `E:/Temp/cosmos-prof-study`.

## First-eight-star comet defect

Old UpdateComets used stackalloc8 and stopped its star scan at8. A hot comet orbiting the9th or200th star had291.323K temperature yet lost no ice, because its source star was absent from the sublimation pass. New reusable slot/snow-line list gathers every eligible star in stable slot order. No new thermal law, material law, calendar or constant; derived scratch is not hashed. Existing<=8star arithmetic/source order is preserved.

Regression captured BEFORE correction: two physical failures, six other checks pass, exit1 (`comet-scale-before.log`). AFTER correction:8/8 pass; hot ice8e-9 becomes0 with8e-9 accounted in Escaped; cold ice8e-9 stays unchanged; resolved+escaped+reservoir composition closes; exact replay. Rate=.1 is an explicit test command used to make sublimation observable, not a changed default.

| Scene | Before hash | Corrected exact replay |
|---|---|---|
|9stars|531CA97DBB63D738|D969B32505914E3D|
|200stars|8ADCDAC50FB60485|525A746026EA44C1|

Hashes change because previously ignored stars now remove physical volatile mass. Separate commit03a29a7 includes the reproducible `comet-scale` checks. Source changes only core/Kinds.cs plus CLI tests/dispatch.

Combined validation has now completed as recorded above; no<=8star fixture recapture was required. No source change followed that full validation.

## Next

Report exact combined validation; Claire/Ariel review isolated PROF/comet commits. Coordinate any game HUD use through worker snapshots. Root derivation/scalar seam can proceed under the ACKed quiet-error/k sensitivity/natural-event requirements; don't invest in per-rock FREE/RAIL paths. Aggregate M/P/L/Comp/distribution/promotion semantics wait for Claire's new SPEC. Celine's conservation/prediction/picking/replay/no-god critique was sent on Bridge.
