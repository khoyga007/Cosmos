# C6 — Roche breakup → rings

Base `b08c41a`, branch `celine/roche-rings`. STEP0: `C6-CHECKPOINT.md`, three amendments ACKed by Claire before implementation. Ownership of the narrow Audit/Commands changes ACKed by Selica on bridge message `127809d5-e645-4194-ac29-5f5515cc8b27` (response 18:44:22Z). No merge, no game source changes. C7 waits for Claire's diff review.

## Behavior

`core/Roche.cs` adds a switchable `roche` rule, composition-based Roche coefficients, a finite-strength survival correction, stellar physical mean density, first-entry planning, breakup, conserved bound reservoirs and records/hash. `World.Advance` cuts its existing integrator at the two-body predicted entry; `Rails.Jump` cuts Kepler motion at primary entry dates and places deferred rocks before breakup. Generation checks invalidate recycled source/host slots. A positive interval rounded to the current Gyr year advances to `Math.BitIncrement(Year)`.

Near-circular bound matter becomes opposite pairs of real fragments around the pair COM. Angular momentum fixes the circularization radius, and circularization can only remove energy. If that radius is outside Roche, below the host surface, or energetically inaccessible, matter remains an eccentric stream. Stream width is bounded by actual host binding energy. Capture uses host mass alone when evaluating individual debris binding, not the mass of the destroyed body.

The nonspinning radial-slice capture proxy is evaluated at disruption entry. Near-parabolic example captures `4.436582191845402e-7` of `1e-6`; a faster hyperbolic pass captures zero. Leaving a planet while still bound to the system's heaviest body goes into a bound root reservoir; only genuinely system-unbound outgoing material enters the common Escaped ledger. No nucleosynthesis occurs.

Source life/civ ends and references are retired before the slot is reused. A queued contact involving the old source cannot merge its replacement. The impact audit now compares the original generation because Moon debris can reuse the impactor's slot after a successful collision.

## Parameters and model limits

All numerical dials are public instance `Consts` fields in Roche.cs, included by the existing reflection hash/SetConst interface. Rigid/fluid coefficients 1.26/2.44 follow the cohesionless limits described by [Aggarwal/Oberbeck](https://ntrs.nasa.gov/citations/19740055720) and [Holsapple/Michel](https://doi.org/10.1016/j.icarus.2007.09.005). The strength factor `cbrt(1+sigma/(G*rho²*R²))` is a dimensional macro correction, not their ellipsoidal yield solver. Ice/rock/metal defaults 1/10/100 MPa are representative placeholders; they are not measurements of every material. Ice tensile strength varies with temperature, as illustrated by [field tensile tests](https://www.iahr.org/library/infor?pid=18435); [USGS rock tests](https://pubs.usgs.gov/publication/ds1126) and [NIST metal tables](https://www.nist.gov/publications/circular-bureau-standards-no-447mechanical-properties-metals-and-alloys) show why a single material-wide strength cannot be exact.

Physical G uses [CODATA](https://physics.nist.gov/cuu/pdf/all_2018.pdf). Solar mass is the [NASA reference](https://nssdc.gsfc.nasa.gov/planetary/factsheet/sunfact.html); 696340 km keeps the existing C5 stellar-radius conversion. Densities convert g/cm³ to kg/m³. `EarthRadiusRef` maps solid radii into physical km while all orbital/contact/Roche distances remain in the existing local simulation scale.

Energy spread .9 and the finite-Hill binding threshold are adapted from the nonspinning approximation in [Hyodo et al.](https://arxiv.org/html/1609.02396). This is a macro capture model, without hydrodynamics, self-gravity of the cloud, spin torques, or collisional viscosity. Circular rings have zero initial width; a .01 body-radius transverse width represents eccentric streams. Neither is a physical grain-size or ring-spreading solver.

- Defaults: 64 fragments; 10000 total small-body budget; each new fragment ≤4e-8 and ≤half AttractMass; 256 topology changes per public step/jump. These are numerical budgets, not physical laws. Remaining unprocessed objects persist physically.
- Budget/fragments=0 is valid: captured mass stays in the **hashed unresolved bound reservoir**. A reservoir records its formation frame, mass, each material, momentum, intrinsic angular momentum, host and host generation. It has no forces/collisions and no materialization API in this slice. This explicit resolution exception was accepted in STEP0; material is never silently deleted or called Escaped because capacity is full.
- Housekeeping defaults to 1 yr. SetConst updates the actual rule cadence; shortening advances the next tick, while increasing does not postpone an already scheduled tick, matching the existing scheduling idiom.
- Exact first-entry planning uses the existing two-body primary path. Advance still uses the engine's kick/drift approximation and cannot promise exact three-body event times. Tiny sibling flybys during a jump retain the preexisting sampled-contact limit. Stellar/substellar phase bodies are excluded as disruption sources in this slice.
- The event/reservoir history grows with disruptions. Reservoir state affects accounting/hash but does not enter the live gravity solver. Long-term physical ring spreading, dust collisions, and reservoir resolution remain debt.

## Validation

Before the final candidate-filter optimization: full CLI **437 OK / 0 FAILED, exit0**; Roche **40 OK**, conservation **47 OK**, audit **4 defects / 3 risks** (same baseline count; audit intentionally non-strict). Logs: `E:/Temp/cosmos-c6-final-cli.log`. Final optimized rerun results are recorded below after completion.

Final optimized full CLI: **437 OK / 0 FAILED, exit0**, all 40 Roche checks and both fixture tables pass; conservation47, audit4D/3R. `E:/Temp/cosmos-c6-final-v3-cli.log`. Temperature-rule overhead2.49% (1.693/1.652 ms). Final 50000-rock jump original5588.039/trimmed945.682 ms = **5.91×**, passes the 5× gate. Replay and fixture hashes are unchanged by candidate-filter optimization.

Roche checks include same-density 2.44/1.26 ratio and mix interpolation; tiny cohesive survival; capacity2/4/128 and budget0; each material, M/P/COM/L closure; retired identity/replay; stable circular ring after 1e6 yr (65 live, one breakup, total mass `0.05000100000000014`); partial/zero hyperbolic capture and negative energies of represented captured fragments; first-entry 1/200 cuts; positive-Gyr clock; WD density; impact slot reuse; invalid dials/cadence; planet-unbound/system-bound retention; reordered element roles. Conservation errors are at floating-point rounding scale (ring ΔL≈3.5e-18).

Game build: 0 errors, one preexisting nullable warning GodUi.cs:804. Headless selftest: exact 13-command replay `99B0905D9C0B42AE`, exit0. Headless uitest: exact 29-command replay `039C93B5A9C7C87C`, exit0. Logs `E:/Temp/cosmos-c6-{selftest,uitest}.log`.

After the final optimization the game was rebuilt and both headless tests rerun, exit0 with the same hashes. Logs `E:/Temp/cosmos-c6-final-v3-{selftest,uitest}.log`. Claire ACKed both shared-file hunks at 18:50:07Z, but has not accepted the +34.73% Advance regression: next gate is ≤10% or an explicit cost breakdown and proposed ring-specific gate.

Initial full run jump-cost check: 50000 rocks / 1e6 yr original `7789.349` ms, trimmed `1344.092` ms, **5.80×**, passes 5× gate. Temperature-rule overhead on identical C6 worlds is -5.40% in that run; it does not measure Roche cost.

Separate same-process baseline comparison against the b08c41a core binary alternates before/after order across seven freshly seeded 5000-rock scenes, 100 warmup+500 measured advances. Initial C6 paired median overhead **+63.55%**. Filtering strong debris before substeps, removing per-body role-array allocations, and precomputing conservative host/body drift bounds reduced it to **+34.73%** (raw medians baseline `2.417873` / C6 `3.245350` ms). Per-pair variation -5.66%..+59.00%; these are noisy wall-clock measurements, not confidence bounds. With Roche disabled, the diagnostic paired median is -19.91%, also noisy. The ~35% measured cost increase remains a review concern; full CLI passing does not prove the new rule meets a +10% cost limit.

Paired probe: `E:/Temp/cosmos-c6-performance/Program.cs`, logs `cosmos-c6-performance-v3.log` and `cosmos-c6-performance-off.log`. These two medians were measured in separate runs; dividing them cannot measure Roche-only cost. A direct same-build on/off measurement before SIMD gave paired-ratio median+23.09%, while ratio of raw medians1.642502/1.391222 gives+18.06%; both are stated and neither is a confidence interval.

### Performance followup (after c5197d0)

`CollectRocheCandidates` was the bottleneck: at5250 live objects with no disruptions, its isolated cost was .423290ms, versus .001621ms for8 entry queries and .000176ms for CheckHere. Geometric rejection alone did not reach10%. Standard-library SIMD now scans the existing position arrays and rejects distant lanes before reading material. Candidate sorting preserves original Body/Host ordering. Material lookup reads each element's Roles once and skips zero cells. Orbital planning retains its scalar path; hardware without SIMD uses the scalar fallback.

Benchmark is now in the repo: `dotnet run --project cli -c Release -- roche-bench`. Before measurement, it specifies the gate as **median of7 paired ratios**, null off/off within±3%, on/off overhead≤10%. Each fresh seeded pair warms100steps, then interleaves60blocks of16steps; ordering is randomized with seed42. All worlds in the measured control/feature pairs had zero disruptions. Claire requested this10% review gate after the initial report; SPEC C6 itself does not yet state a performance ceiling, and the old element5% gate remains separate.

After the material-lookup fix: **null paired median+1.15%; Roche on/off paired median+7.33%; gate passes, exit0**. Raw medians on2.217865/off2.003933ms have ratio+10.68%, a separate statistic not used for the preselected gate. Collect fell to .214147ms;8 entry queries .003655ms. Full raw output is `E:/Temp/cosmos-c6-simd-material-bench.log`:

| Pair | Null off/off delta% | Roche on/off ms | Paired delta% |
|---|---:|---|---:|
|0|14.31|2.217865/1.948922|13.80|
|1|-5.84|2.151914/1.952906|10.19|
|2|-5.29|1.883752/1.847734|1.95|
|3|-2.03|3.108488/3.024121|2.79|
|4|1.15|2.580574/2.336738|10.43|
|5|2.92|2.150798/2.003933|7.33|
|6|9.14|3.630749/3.452751|5.16|

These results validate the chosen median gate, not a guarantee of≤10% in every round or every scene. The original5000-rock stock scene contains240 preexisting ring particles but no disruption during this benchmark. Isolated10-body worlds had a larger relative cost on the initial scalar build (.010962/.006865ms, +59.89%); that absolute microsecond cost and event-heavy scenes are not covered by the5000-rock gate. Cost scales with the number of pullers, scanned objects and nearby material. No zero-cost guarantee is claimed for worlds without a disruption.

Revision8e2b2f5 was committed before remeasurement. Its fullCLI passes437OK/0FAILED, audit4D/3R; jump5768.893/1094.472ms=5.27×, temperature overhead.64%; rebuilt headless selftest/uitest retain99B0905D9C0B42AE/039C93B5A9C7C87C, exit0. However its fresh benchmark **fails the exact10% gate**: control median0.00%, feature median10.02% (raw log `E:/Temp/cosmos-c6-8e2b2f5-bench.log`). The earlier7.33% pass is retained as history;10.02% is not rounded down or discarded.

Experimental revision67194c8 caches the composition/strength-derived Roche limit in each candidate, guarded by body/host mass, radius and generation; a merge forces recomputation. Selected Roche40 and both fixture suites pass unchanged. Its fullCLI also passes437/0, audit4D/3R, jump7457.967/1238.751ms=6.02×. However its immutable-revision benchmark **fails**: null median2.98%, paired cost15.22%, exit1. Raw log `E:/Temp/cosmos-c6-limit-cache-bench.log`; that cache is not selected for delivery.

### Claire's final selection — 19:21:11Z

Claire chose the **core from8e2b2f5**, declined67194c8 because the extra invalidation state has not demonstrated an end-to-end benefit, and stopped further micro-optimization. Core/Roche.cs is restored byte-for-byte to8e2b2f5; history retains the experiment and its results. Paired median7 remains the reporting statistic; ratio-of-medians is also reported.

Claire explicitly changed the C6 review gate to **stock5250-object broad phase≤.25ms and10-body collection≤.02ms**. Relative paired cost is a reported metric, not pass/fail. The previous10% failures are retained; this is a documented owner decision, not an implementer silently relaxing a test. The benchmark now preselects median7 for the component measurements and prints all component samples. Existing element5% and jump5× gates remain separate. Debt in NEXT.md: further reduction should integrate Roche rejection into the gravity loop after species work.

Failure provenance:

| Measurement | Source revision/state | Result |
|---|---|---|
| first SIMD full jump | dirty work afterc5197d0, before max-speed orbital guard; not committed separately |4.52×, full436/1 |
| SIMD/material paired pass | dirty precursor of8e2b2f5, before guard/comment changes |7.33%; historical result, not a committed-revision acceptance |
| selected core paired rerun |8e2b2f5308ba060a83a0dc494752f7170f8cac7a |10.02%, old10% gate fails |
| limit-cache paired rerun |67194c80b60be87168df73c829b99c7a3a9fa0a1 |15.22%, old10% gate fails |
| selected core full suite |8e2b2f5 |437/0; jump5.27×; rebuilt Godot exact replay |

Updated-gate measurements will be appended with their immutable benchmark revision.

The broken `elements-paired` reflection call is also repaired by supplying the optional third argument (`null`), as already done by the other repo benches. The scalar fallback was tested with `DOTNET_EnableHWIntrinsic=0`:40 Roche checks pass, all recorded replay hashes remain exact.

First fullSIMD run:436OK/1FAILED, unique failure is jump-cost4.52× (5842.942/1293.921ms); all physics/replay/fixtures and audit4D/3R pass. The maximum-speed scan introduced for SIMD was also running in orbital/deferred collections where it was unused. That scan now runs only on the vector path. Focused `cli -- rules` passes again: original8168.157/trimmed1428.546ms, **5.72×**, exit0. FullCLI and fresh game checks on this followup are pending in this snapshot.

Selica reviewed the evidence and pointed out that the old temperature benchmark uses ratio-of-medians. The requested new C6 review ceiling has no statistic yet in SPEC; **Claire's final acceptance of the statistic remains pending**. The7.33% value is a pass of the explicitly preselected diagnostic benchmark, while10.68% would fail a gate using ratio-of-medians. Three feature pairs exceed10% (13.80,10.19,10.43); control pair0 reaches14.31%. No causally exact cross-run speedup is inferred from12.42%→7.33%; the separate component timings and changes identify work removed, while scheduling noise limits the total-cost comparison.

## Fixture recapture

Adding Roche constants, the rule, empty/new persistent Roche state necessarily changes hashes even in scenes without a breakup. Capture occurs after meaningful physics, conservation and replay checks; fixtures are not used as proof of conservation. Element and progenitor scenes preserve their existing definitions. Old C5→C6 captures:

| Scene | C5 | C6 |
|---|---|---|
| sol0 | 6C56216D3D2C758F | 7E670A9BD7AE133C |
| sol0-step | 9E4C9D844F3CF2D2 | D588C1BD6D4DCC4D |
| sol0-life | 97AD3F35AE99CDAB | 9EAA1C5766BDD984 |
| sol0-edit | B672C1E7FB754212 | 3C0DB003B8DBBA3C |
| sol2000 | 68B21923CD852A6C | DBA8488A4FDFF0EF |
| sol2000-step | E847BF6F9F41D4D8 | 2B38413528BA23F3 |
| sol2000-jump | DAE873E2BBA7FC03 | EDA695957A058E08 |
| star-1 | 5F8FEF5535B4057E | 4E981380251BCB3C |
| star-2 | 17CF3AD78EA82314 | BA1041AD9CA14257 |
| star-10 | EAFCA1C02E822A73 | AD49FB56004C4D70 |
| star-30 | 435329957269E91C | 86EE5961B16EC2B6 |
| progenitor1.23 | 97C2CE571597307D | 3111EC43BE1F8CC3 |
| progenitor2.0000000000000004 | C7853FD92DE50040 | 86CA1273DE11B663 |
| progenitor7.999999999 | 870E42732CBC7754 | 74CF37E143ED8D4D |
| progenitor8 | DAC46BCBA3622301 | 78816E46EAD729B2 |
| progenitor20 | D53856AF2ECDDF32 | 6ABE1F6A11F178F1 |
| progenitor20.0000001 | 5CD742194002C0BA | 72E2504BE76F0DC3 |
| progenitor31.123 | 59B4013AE6C9B43A | 27C9F85B6BA0D221 |

Cross-review reports `A3-S1-CROSS-REVIEW.md` and `S2-CROSS-REVIEW.md` contain independent exact-snapshot findings and passed checks. No edits to Ariel's source; fixed commits are still awaited for re-review.
