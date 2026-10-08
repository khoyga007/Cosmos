# F sim decoupling — independent review, 2026-10-08

Reviewed exact commit `a696c74753cbbc8549c2dffa8b2571cd2e7db3f9`, parent `ba5ecce`, using `git show` for every source citation. Ariel's working tree changed to draw-bench during review; working-tree reads were discarded. Verdict: **REQUEST CHANGES**. No implementation edits, merge, push, build or benchmark. Findings below are static source proofs, not claimed runtime captures. One independent subagent audited snapshot ownership, identity and cost; Celine verified the findings against exact source.

## Blocking findings

| Priority | Exact source | Trigger / consequence | Required correction |
|---|---|---|---|
| P1 | `game/GodUi.cs:646`, `:654`, `:707`, `:731` | Matter, life, rule and constant controls call GodTools on `_w` directly while `Main`'s task can be executing Advance. They bypass the queue and expected-generation validation; World, Journal and rules get concurrent writers. F does not modify GodUi. | Route every mutation through the same serialized simulation command stream, preserve generation captured at input, and apply UI result callbacks on main thread. |
| P1 | `game/Main.cs:309`, `:562`, `:568` | A jump click starts FastForward immediately, even with an unfinished `_simTask`. A second click overwrites `_jumpTask` and leaves the earlier jump running. Checking only the latest jump's completion does not establish exclusive World ownership before CopyFrom. | One worker serializes Advance, FastForward and commands. Queue jump requests instead of creating an independent task. Publish only after that owner has finished its work. |
| P1 | `game/Main.cs:199`, `:668`, `:692`; `game/GodUi.cs:193`, `:430`, `:511`, `:522`, `:668` | Input selection, periodic panel refresh, tab/list and create-preview paths still read `_w` during the worker. PanelText combines snapshot liveness with current World arrays/collections. The apparent read is also a writer: Hill/PrimaryOf call BuildPullingHierarchy, which changes `_na` and `_att` (`core/World.cs:147`, `:155`; `core/Rails.cs:249-254`) used by physics. | Cache selected detail/panel/event/list/constant data at an exclusive sync point, or include it in a UI snapshot. All interactive UI paths read published state exclusively. No World query on UI while worker owns it. |
| P2 | `game/Main.cs:196`, `:642`, `:644`, `:1130`, `:1150`, `:1166`, `:147` | Select/follow only store a slot. Their Gen is captured after the next CopyFrom, so a click on old snapshot slot i/gen g can silently bind to replacement i/gen g+1. Drag stores only `_grab`; release recaptures the replacement Gen in QueueGodAction. | Capture Gen at selection/follow/gesture start, validate thereafter, and pass the original expected Gen to queue. Cancel a stale gesture rather than adopting a replacement. |
| P2 | `game/Main.cs:1031`, `:1045`, `:1051`, `:1056`, `:1065` | Create captures primary slot p from the old snapshot but enqueues with no target identity. If p is removed/reused before flush, CreateOrbiting uses the replacement, or launch inherits its velocity. | Capture primary Gen with p and validate at flush, or deliberately recompute the physical primary at flush and document that command semantics. Never silently adopt recycled identity. |
| P2 | `game/Main.cs:312`, `:314`, `:316`, `:566`, `:580` | ContinueWith writes camera fields from the ThreadPool while input pans/follows. It runs even when FastForward faults, does not observe the antecedent error, and validates heavy slot Alive but not Gen. Sim task faults instead escape via Result in _Process. | Return a worker result; observe exceptions at main-thread completion, enter a defined safe state, apply camera delta on main thread using the original identity and current follow policy. |
| P2 | `game/RenderSnapshot.cs:140`, `:141`; `game/Main.cs:605` | Each live puller invokes Hill, and KindOf can invoke PrimaryOf again. Each such call rebuilds the hierarchy from all N objects with no cache guard (`core/Rails.cs:238-264`). Snapshot sync on main thread therefore repeats the N scan P or more times, with worst-case repeated hierarchy work O(P^3), in addition to array copies. The 5k test does not validate 200 systems. | Build hierarchy once at sync, bulk-read derived Hill/Kind without rebuilding per body, then measure full snapshot production/publication at K=200. Trimming copies alone does not remove this cost. |

The initial Bridge message cited Select as line 211; the correct reference is **199**. No finding depends on the temporary old working-tree source.

## Existing validation does not certify the worker

`Main.cs:483` selects the synchronous branch for selftest, uitest and bench. The added decouple test (`:1508-1540`) starts a separate local task, measures only a camera-field increment plus Screen/Pick, excludes its Task.Delay cadence, and does not execute the production async _Process, draw upload, panels, queued command/jump path or snapshot publication. `_uitest` remains true, so Screen/Pick read World, not RenderSnapshot (`:331`, `:421`). Its printed “frames rendered” / p95 wording overstates what was measured.

Required regression coverage on the repaired production path, headless only:

- Hold the actual worker at a deterministic barrier for ~300 ms; exercise complete process/draw/UI updates and camera input while it is busy. Report the scope of measured p95 precisely; a headless result cannot certify windowed GPU p95.
- Click matter/seed/constant/rule controls while blocked, then unblock; assert no direct write, preserved FIFO journal/Step order, and exact replay.
- Queue jump while Advance is blocked, then two jumps plus commands; assert maximum one World owner and correct chronological journal/replay.
- Select/follow/begin Aim/Carry on gen g, reuse its slot before sync/release, and assert no action or follow attaches to gen g+1. Include a stale primary for CreateOrbiting/Launch.
- Inject Advance and FastForward exceptions and exit with work pending; observe failures, prohibit UI callback/publication after node exit, and stop scheduling safely. Exact a696c747 has no ExitTree/close/cancellation lifecycle hook. This is a coverage gap; no exit crash was reproduced.
- With bridge `flock perfbox`, measure snapshot production plus main-thread publication at 5k, 100k and target puller count, after warmup, with seven runs. The current 1.056 ms result is Ariel's report, not independently remeasured here.

## Positive scope / limits

CopyFrom owns its arrays and normally runs after the tracked task completes; the single published snapshot is sufficient if exclusive ownership is enforced. Drawing/picking/tool overlays mostly use snapshot state. ExpectedGen at queue flush protects the captured target when the original generation was captured correctly. Worker warp batching reads wall clock only in game; individual core Advances still use fixed sim input. The faults above break those intended ownership guarantees.

No F perf gate is passed by this review. No tests were rerun: the proven interactive ownership blockers must be fixed before repeating synchronous checks. Celine has not edited Ariel's branch. Claire owns merge after the repaired exact snapshot is independently reviewed.

## S1 handoff facts read alongside this review

- Selica S0 `fe5ef43`: K=200x500 measured 1073.960 ms/Advance; K=200x5000 only a qualified 8.893–9.352 s range, not clean median-seven. Historical measurements lacked the newly required perfbox lock and remain baselines with noise caveats.
- The fitted coefficient `1.5e-9 x rocks x pullers x 8` has units **seconds**; milliseconds use `1.5e-6`. Flat all-puller kicks are the first bottleneck. R's small-N NS improvement is not evidence for the 200-system target.
- Claire's 01:59Z decision: S1 A+B <=12 ms at 200x500 while rocks remain FREE, local gravity work per rock independent of K; S2 quiet <=1 ms and destruction <=4 ms; S3 <=4 ms; stretch destruction 2 ms; window p95 <=20 ms. Phase lines still mention 8 ms in f9e7a77 and must be corrected in S1 Step0.
- S1 Step0 must address flat kicks, dynamic capacity, deterministic rejected-command records, hot/cold bytes per object, event-drop accounting/capacity, and rule index lists; include the previously ACKed §14I rail identity/lazy-read/wake/aggregate/queue corrections.
- Kernel v1: raw-pointer descriptor, caller owns topology and deterministic contacts, scalar -> C# SIMD -> measured Rust. Pinning managed arrays does not guarantee 64-byte alignment; use unaligned-safe reference loads or explicitly flagged aligned native storage. Contact identity/overflow protocol and slot maps must be explicit. Ariel's final amendment ACK is pending.

S1 core has not started. R branch `5cbfe62` remains unchanged; Claire/Selica's review and merge plus the kernel agreement precede S1 implementation.
