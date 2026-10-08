# F v2 independent review — REQUEST CHANGES

Exact target7e4cebd74870d59a7174b74aabdc93d1a702a6c9, base9f34bcc. Read immutable git-show sources and built an isolated exact checkout `E:/Temp/cosmos-f-v2-review-7e4cebd`; no F source edits or merge. Celine's independent probes live in `E:/Temp/cosmos-f-v2-probes`, logs in `E:/Temp/cosmos-prof-study`.

## P1: persistent hierarchy cache changes physics depending on observation

`core/Rails.cs:242` returns cached data while valid. Missing invalidation includes Push/Force (`core/Commands.cs:74`, `:136`), Ride's place writes (`core/Rails.cs:351-355`) and EscapeRole's mass update (`core/Escape.cs:43-46`). More direct mutation paths in stellar evolution, transmutation and Roche need complete audit; four example patches are not proof of a complete lifecycle.

Executed probes on exact F core:

- Warm Moon9 primary3; Push10 or targeted Force => cached primary3, explicit invalidation yields fresh primary0. Canonical hash is unchanged by invalidation itself.
- EscapeRole on Earth metal*.5 => cached Hill.45 vs fresh.42459245822848796.
- FastForward1year => cached Earth Hill.45 vs fresh.44998763618755866.
- Two Sol0/seed1234 worlds receive identical Push9Vx10 then FastForward1 commands. Only one world receives an observer PrimaryOf9 before the commands. Both hashC663C839CD517068 before jump; afterward warmed4922F247C5DFC3AE versus coldE0EEB929124B6B86, with Moon positions differing by hundreds of world units.

The same probe executable against baseline9f34bcc reports0failures; F reports5failures. Baseline warmed/cold bothE0EEB929124B6B86. Raw `f-v2-cache-probes.log` and `f-v2-cache-baseline.log`. This proves a new observation/replay-dependent physics defect, not just stale labels. Root cache is omitted from Hash under a derived-state assumption that is now false. Fix lifetime centrally or scope bulk-derived caching to one exclusive snapshot operation; retain a genuinely fresh query reference. Never “fix” it by hashing incidental UI cache history.

## Existing runtime checks, observed exits

Fresh game build0errors/0warnings. Under perfbox, exact built-in production `--workertest`:16OK/0FAILED/exit0; snapshot repeated-copy medians5k.178ms/50k1.701ms. The injected exception is deliberate and was caught. `--selftest`:13commands exact1CCDBCA414ED430D/exit0.

Exact `--uitest` independently **FAILS/exit1**:

- create-click satellite circles Sun at1.006x rather than Earth;
- push drag gives58.805x;
- circularize gives.583x.

Replay28commandsE899247F183CC29C/E899247F183CC29C is internally consistent, but does not make the suite pass. Baseline has29commandsE393FCDFD0364E2B. `RunUiTest` diff against9f34bcc adds four Snapshot.CopyFrom calls; intended gesture/command inputs are unchanged. UI action queue takes the synchronous `_uitest` bypass at `game/Main.cs:207`, so attributing the hash difference solely to production worker order is unsupported. Core cache is independently proven defective; relative contribution to these UI failures remains unisolated. Raw `f-v2-uitest1.log` / `f-v2-selftest.log` / `f-v2-workertest.log`.

## Additional static findings and test limits

These are source-proven paths, not claimed injected runtime captures:

| Priority | Source | Trigger / correction |
|---|---|---|
|P1 validation/ownership|game/Main.cs:552,207; RunUiTest initial CopyFrom|UITEST starts at frame3 without draining an active production worker; it then copies/mutates World directly while ordinary _Process also starts workers. Establish an exclusive deterministic synchronous test mode or exercise the actual worker via barriers for every action.|
|P2 identity|game/Main.cs:221-244,609-612|Select/follow still store only slot and capture Gen after the next CopyFrom. A pick on old snapshot can adopt a recycled replacement. Capture original Gen at input and validate, including deferred created-body selection callbacks.|
|P2 identity|game/Main.cs:1056,1068,1073,1078|Create primary p is captured from snapshot but queued without primary Gen guard; recycled/removed primary can be used for orbit/launch velocity. Validate original identity or explicitly recompute primary at execution.|
|P2 input ordering|game/Main.cs:496,518,618|Separate action/jump queues serialize ownership but drain all actions before a selected jump; Jump then Remove can execute as Remove then Jump. Use a single FIFO event stream or explicit stable sequence numbers to preserve intended input order. Journal must report actual accepted/rejected decisions.|
|P2 camera|game/Main.cs:331-338,524|Queued jumps store original snapshot HeavyX/Y. Two jumps queued from the same frame compute the second delta from the first origin again and double-count prior drift. Capture execution-start anchor position or return a correctly sequenced anchor result.|
|P2 error/exit coverage|game/Main.cs:495-518,471-479|Cancel is checked only in Advance loop, not before queue mutations/jump after barrier; ExitTree waits200ms, and deferred UI callbacks lack an exit/generation publication guard. The existing exit test cancels while already idle, not with blocked work. Inject pending action/jump/barrier exit and verify no later callback/mutation/publication.|

Gesture `_downGen` is now captured at press and used on release, which closes that part of the original defect. Ordinary render, panel and GodUi accesses mostly read snapshots; Toast/Select marshal to main thread. Single active worker removes the previous simultaneous Advance/jump tasks. The remaining problems above still block acceptance.

Worker test depth: it calls production StartWorker/_Process and proves a barrier does not prevent six manual process loops; it does not measure camera responsiveness or full window p95. The “300ms” label uses6x20ms delays (roughly120ms plus work). Its queued-stale-target test covers one prepared action, not stale selection/create-primary. Jump test only checks elapsed100years, not cross-type order/camera displacement. Snapshot medians repeatedly copy unchanged worlds, amortizing the first hierarchy rebuild; measure invalidated/cold sync with target puller counts after cache repair before claiming200-system scaling. No window opened.

Requested repaired snapshot: complete cache lifetime (prefer bounded scope), original identity/input ordering/camera publication and real exit/error cases; add regressions that fail exact7e4cebd and pass the repair. Report intended command sequence and why UITEST hash changed; reproduce the three current failures. Claire owns merge after independent re-review.
