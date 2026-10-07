# Ariel batch — independent game-readiness review

## Re-review ab5411c

Replacement snapshot `ab5411cd75f3299f8e77421e4e637ad298d23f7e`, parent `78dfe2e`, changes only three CLI files. Core and game trees are byte-identical to the independently reviewed predecessor, so its Human/life/Species findings carry forward.

All eighteen corrected fixture values match the independent capture below; fresh targeted `events` and `elements` both exit 0. The benchmark implements the preregistered seven fresh pairs, both paths warmed twice, alternating order, initialization outside timers, median of paired speedups, finite state and Earth layer checks. The 5x threshold remains.

**Independent fresh Release full suite: 470 OK / 0 FAILED / exit 0.** Jump median paired speedup **5.20x**, passes 5x; finite state/layers agree. Audit remains **4 defect / 3 risk**, non-strict, unchanged accepted debt. Log: `E:/Temp/cosmos-final-review/full-cli-ab5411c.log`. Review OK: no remaining implementation or fixture blocker in this batch. Claire owns statistical-method acceptance and merge. Celine has neither merged nor pushed; C7 core remains deferred until after Yang's game test.

The predecessor results below remain evidence, including the failing single sample; the replacement does not delete that history.

## Predecessor 78dfe2e review

Reviewed exact chain A3 `67b581b2d4e2359cce8069978d78f08bfc8ee572` → S1 `82ba34e08bbd5a77c1d24af7c3e7059389622bf2` → S2 `b8a194e1b0fa29ecb314ed4f91e29530d08747dc` → life-timescale `78dfe2ed330504243ace3d823e0b41f08b642987` in detached worktrees. No Ariel source edits.

**Original three blocker groups closed. Final game-test gate still red on full CLI.**

- S2 independent probe: 20 OK, exit 0. Crossed lifespan overrides now hash distinctly; restoring both defaults restores baseline; Ref 0/negative/NaN/Infinity rejected without mutation. Constructor/init/with/WithParam validation and Extra copy/read-only checks pass. All four enum init paths reject invalid values. Canonical stage mutation is blocked; per-world override is isolated and hashed.
- Same probe against old `4e635af`: 11 failures. Also built the three NEW regression files against OLD core: civtable 1 failure, species 3 failures, capnodes 1 failure, each exit 1. Fixed S2 builtins: civtable 13/13, species 15/15, capnodes 12/12, each exit 0. Thus regressions demonstrably detect the old defects.
- Three separately built Human binaries A3/S1/S2 match accepted A3 baseline at all three checkpoints: `66B1883E599E57EE` initially, `D55A7885CD8D68E2` after 1e6 yr, `E42AD90E459EFA3A` after push + 20 advances. Final positions -41.400902146915854 / -16.774713963946162. Consumed matter 1.972219643607632e-12, remaining metal 4.799999811026832e-5, events 19.
- Life-timescale Sol year 0: all Life/Pop zero. Earth civ event at **3,945,337,738.932428 yr** after a 4e9-year jump. Single measurements: 0 rocks 32.2983 ms, 5,000 rocks 342.8413 ms (diagnostic, not a statistical performance gate). Both have rich life and population afterward. GodUi diff only imports LINQ and maps readonly stages through Select/ToArray; displayed names unchanged.
- Fresh Release full CLI on `78dfe2e`: **467 OK / 3 FAILED / exit 1**. Audit remains **4 defect / 3 risk**, non-strict. Existing accepted audit debt is unchanged.

## Remaining gate failures

1. `cli/StarEventChecks.cs:15` recaptured fixture differs from fresh exact source. Independent targeted `events` repeats failure, exit 1. Actual hashes in order: `89D797BDF91DA090 AFA16CBAD36AB034 B5B49F0C980349EA 1309AE888DF43879 FD484FE9BC27BA16 28219D4E31380FA4 77753EA5494C4736`.
2. `cli/ElementChecks.cs:13` eleven fixtures differ. Fresh `elements-probe` actuals:

| Scene | Hash |
|---|---|
| sol0 | BBC63D2996FAE88F |
| sol0-step | 4063F53F08CB21B2 |
| sol0-life | 4A51313215F96FC5 |
| sol0-edit | 32DDE80C54679D92 |
| sol2000 | 82F35AA059FE5804 |
| sol2000-step | 3AFDAA9CB3C047FC |
| sol2000-jump | 4E91B3C52D73C0CF |
| star-1 | 16BA0A372AE8C21B |
| star-2 | A2CA19CD75B36BE4 |
| star-10 | AC47DAE6B6B08CD7 |
| star-30 | 7DFF61BCBEAA658D |

Do not blindly change fixtures: owner must explain which source changes alter them and rebuild the actual checked snapshot. These tests fail independently of the performance test.

Git establishes the stale baseline: original life-timescale `e37eb975a4d69cf506d0c63ab160cabbfee4e51a` had parent old A3 `32a4d47`; rebased implementation `10497f452d1c36ad2559a7f6da86c7b592af7a5d` has parent fixed S2 `b8a194e`. `git diff e37eb97 10497f4 -- cli/ElementChecks.cs cli/StarEventChecks.cs` is empty: both entire fixture files were retained from the old pre-C6 baseline. Recapture on the new source chain is required; current literals are not evidence of a physical regression.

3. `cli/RuleChecks.cs:192` jump speed gate: original 4852.399 ms, trimmed 1003.801 ms, **4.83x vs required 5x**. Finite state and Earth layers agree. Preserve this failing sample. Additional preregistered diagnostic: 7 fresh pairs on exact 78dfe2e, same seed/50k rocks/1e6 yr, both paths warmed twice on 100 rocks, alternating order, initialization outside timer; report median of paired speedups and every raw sample. No core/gate changes by reviewer.

| Pair / order | Original ms | Trimmed ms | Paired speedup |
|---|---:|---:|---:|
| 1 slow-fast | 5178.9320 | 829.1441 | 6.2461 |
| 2 fast-slow | 4577.2886 | 837.4667 | 5.4656 |
| 3 slow-fast | 4569.6369 | 818.2211 | 5.5848 |
| 4 fast-slow | 4572.5296 | 831.4711 | 5.4993 |
| 5 slow-fast | 4580.9311 | 830.8397 | 5.5136 |
| 6 fast-slow | 4705.6091 | 825.7313 | 5.6987 |
| 7 slow-fast | 4655.3617 | 938.9444 | 4.9581 |

Median paired speedup **5.5136160441x**, passes 5x. All seven finite/layer agreement checks pass. One of seven remains below 5x; full CLI's original 4.83x failure remains recorded. This supports threshold noise; acceptance belongs to Claire. Raw `jump-paired7.log` and `JumpBench.cs` retain the fixed measurement plan.

## Evidence and next step

Independent harnesses and raw logs: `E:/Temp/cosmos-final-review/`; Human program `E:/Temp/cosmos-rebased-human/Program.cs`. Review probe uses compatible public interfaces on both old/new snapshots. Full log `full-cli.log`, fail-first logs under `fail-first/`, targeted events `events-fresh.log`.

Ariel owns fixture corrections and reports exact replacement snapshot. Claire decides performance acceptance from evidence; reviewer has not waived the 5x gate. Re-review changes and required failed checks before accepting merge. Godot hashes/DLL mtimes reported by Ariel are not independently re-run in this review. C7 STEP0 accepted, C7 core deferred until after Yang's game test.
