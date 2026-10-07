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

Paired probe: `E:/Temp/cosmos-c6-performance/Program.cs`, logs `cosmos-c6-performance-v3.log` and `cosmos-c6-performance-off.log`. The existing `elements-paired` harness passes only two factory args and throws on the current optional-argument SolSystem signature; the independent probe fills reflected defaults. That unrelated harness was not modified.

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
