# S1 followup — source caching and table boundary policies

Base `58e1c79`, branch `celine/s1-followup`. No physics constants or formulas changed; no game edits.

## Changes

- `NextCoolingBoundary` collects luminous sources once per call into the existing scratch arrays, in original slot order. Each Flux query visits only those sources; constant light is cached, remnant light still evaluates its requested cooling age. No cache survives the call.
- `Rule.Boundary` declares `BeforeStar`, `BeforeCooling`, `After`. Four table rows declare the existing policy; `RunRules` contains no string literals or rule-id selection. An extension joins either boundary by adding one row.
- Canonical built-in boundary metadata is implicit in the legacy hash. Overrides, replacement rows with the same id, and new flagged rows are hashed explicitly. The canonical policy snapshot comes from the initial table, not a hardcoded list of names. All 18 legacy hashes remain unchanged; no re-capture.
- Added a two-sided `table-rule-boundary` audit. Against the merged S1 core it reports DEFECT (11 literals, zero flagged rows); against this core it reports OK (zero literals, four flagged rows). Comments cannot mask a match; missing dispatcher/policy rows also fail. A newly introduced rule-name literal is caught, not just one of the four old names.

## Performance

Same-process paired median of seven runs, rotating three backend DLLs; each gets an untimed full-jump warmup. Setup and final Hash are outside the stopwatch. Every backend receives the same public command sequence: SolSystem(5000,1234), remove 19.4 mass units from the Sun, snapshot a 0.612-solar-mass WD with progenitor 2 solar masses and cooling age 1e8 years, prime 1000 years, then time a 1e8-year jump. Scene: 5250 slots; standalone ends with 5248 live objects.

| Backend | Median ms |
| --- | ---: |
| Before S1, `132defe` | 75.489 |
| Merged S1, `58e1c79` | 544.129 |
| Followup | 100.019 |

**5.44x faster / 81.6% less time than merged S1.** Merged S1/followup hashes match in all seven rounds: `5BC1550EE29FFCA0`. The pre-S1 backend has different remnant physics/defaults, so its output hash is not an equivalence test. Warm/cold process timings vary; a standalone CLI run measured 181 ms.

This measures an already cool WD without thermal-edge crossings in the timed interval. It does not measure newborn/crossing worst-case performance. An initial newborn pilot did not produce a completed sample before it was stopped; its cause was not profiled or changed here.

Reproduce:

```powershell
# Build Core in detached worktrees at 132defe and 58e1c79, then:
dotnet run --project cli -c Release -- cooling-paired <132defe-Core.dll> <58e1c79-Core.dll>
dotnet run --project cli -c Release -- jump-bench 1e8 5000 remnant
```

## Validation

- Full CLI with `COSMOS_CORE_SRC=E:/Cosmos-celine/core`: **322 OK / 0 FAILED**, exit 0, audit **4D/3R**, unchanged baseline. Rule overhead 0.94%, within +10%.
- Six new checks: policy override affects Hash, canonical restoration is exact, same-id replacement is visible, extension runs before/after star boundary, its journal replays, cooling extension runs at all four thermal/water edges. `cli -- boundaries`: 6/6.
- `cli -- elements` and `cli -- events`: all 11+7 baseline snapshots unchanged. `cli -- nova-stamps`: 22/22, same extinction/cooling dates and replay hashes.
- Game build: zero errors (existing CS8600 warning at GodUi.cs:609). Headless selftest PASS/0 `C430139B76C6A372`; uitest PASS/0 `B6EC767CF72CE186`, unchanged from master.
- Audit-only command: `cli -- audit-tables`, with `COSMOS_CORE_SRC` set to either source tree for the red/green comparison.

Logs: `E:/Temp/cosmos-followup-paired.log`, `cosmos-followup-full.log`, `cosmos-followup-selftest.log`, `cosmos-followup-uitest.log`.
