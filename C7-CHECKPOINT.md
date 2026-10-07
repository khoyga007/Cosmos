# C7 STEP0 — superflare

Branch `celine/superflare`, base master2a61296 (physical core461ce87 with merged C6; §9 baseline amendment included). Only review/checkpoint documents changed so far; **no C7 core code**. Consolidated A3/S1/S2 review is `REBASED-SPECIES-REVIEW.md`; Ariel owns those fixes.

Bridge update: Claire ACKed all three STEP0 points at 2026-10-07 20:48:47Z, with her summary taking precedence if this file conflicts: integrated hazard calendar; explicit deterministic, hashed high-count aggregation with an approximation flag and conservation accounting (preflight rejection only when bounded operation cannot be ensured); system-binding energy deducted before true Escaped, retained matter in bound reservoir. Performance uses absolute component gates with preregistered statistics. At 20:50:02Z Claire prioritized Yang's game test: **defer C7 core until after that test**; Celine first re-reviews Ariel's consolidated batch. ACK remains valid.

## Existing flow and proposed hooks

- World.Advance already cuts at Roche entry; add flare event cuts to that same calendar, not a second integrator. Rates use mass/phase and age at the physical event year.
- Rails.Jump already cuts at star/cooling/Roche boundaries; add flare boundaries, settle old biology before damage and rebuild orbital origins after gas mass loss. World positions must be evaluated at a resolved flare's own year. Deferred tiny rocks need not be placed for a world-atmosphere rule.
- Rules use generic Boundary flags and ApplyRule/RuleYears. Propose a BeforeRadiation flag on life/civ rows to settle growth/decay before each exposure; no string-id policy in RunRules. Changes to Ariel's Layers/Civ rows require ownership ACK.
- New `core/Superflares.cs` + `cli/FlareChecks.cs`; narrow init/hash/Advance/Rails hooks. Reuse Escaped ledger, EscapeRole and the existing dose scale; no game source changes.
- New source generation must get a new flare calendar; old plans cannot attach to a recycled slot. Rule off integrates zero hazard; re-enable must not retroactively emit flares from the disabled interval. Runtime changes of flare constants update future rates without rewriting past events.

## Physical model and sources

Energy is **bolometric**, truncated1e33..1e36erg. Proposal differential slopeα=2, inverse-CDF sampling; reference rate defined for E≥1e34erg and normalized correctly to the lower cutoff. [Shibayama2013](https://arxiv.org/abs/1308.1480) finds approximately this slope and800..5000yr recurrence for a selected slowly rotating solar-like sample. [Vasilyev2024](https://arxiv.org/abs/2412.12265) reports approximately100yr in a different Sun-like sample. Default reference1e-3/yr is a conservative tunable macro choice within SPEC's range, not a universal measured solar clock.

Mass/age activity: cool main-sequence stars only in this slice; M dwarfs get an increased rate and young stars a larger rate. [Davenport2019](https://arxiv.org/abs/1901.00890) supports declining activity with age but cautions its sample overpredicts the Sun; [Günther2020](https://arxiv.org/abs/1901.00443) links active M-dwarf flaring to rotation. Proposed age proxy: saturation at100Myr, decline proportional age^-0.5, normalized at4.6Gyr. Proposed M-rate enhancement up to100× below0.6Msun. These coefficients are explicit[P] approximations, not claims of an exact fit. Giants, brown dwarfs and remnants have no modeled flare process here; their magnetic/binary bursts need their own calibrated model.

Do not treat all bolometric energy as gamma/XUV. [Osten/Wolk2015 Table2](https://arxiv.org/html/1506.04994) separates solar/active-star coronal fractions from optical light (representative .2/.3 SXR fractions; GOES-band fractions are smaller). Proposal uses a configurable coronal fraction with its wavelength range stated. Fluence is E_band/(4πd_SI²), converting squeezed orbital distance by the same inverse SolDist relation used by C5.

Life/pop attenuation uses the existing GRB dose law as a conservative macro proxy, not a photochemical prediction. [Tilley et al.](https://arxiv.org/abs/1711.08484) distinguishes repeated electromagnetic flares from proton events; EM-only ozone effects can be small. CME/proton/magnetosphere/ozone transport are outside this slice and must not be claimed as solved. A1e34erg flare at1AU should cause a small instantaneous loss in the chosen proxy, not erase Earth's biosphere.

Gas escape uses an absorbed-energy budget: ΔM≤η E_abs/Φ, bounded by available Gas-role mass. Physical planetary mass/radius use solar/Earth references; [Erkaev2007](https://arxiv.org/abs/astro-ph/0612729) gives the Roche-potential correction, while [Salz2016](https://doi.org/10.1051/0004-6361/201527042) shows that efficiency is not universal and energy-limited estimates can fail for compact planets. Proposal η=.15 as an explicit macro placeholder, with exclusions/invalid-regime handling documented. EscapeRole records removed mass, each material and bulk momentum; no invented matter.

Every implemented physical/numerical dial will be an instance Consts field with range validation, source or explicit[P] origin. History/clock state and counters enter Hash; transient plans do not. New constants and stochastic state necessarily change the C7 default fixtures, which will be recaptured only after independent conservation/replay tests.

## Three decisions needing Claire ACK before code

1. **Poisson calendar:** propose integrating λ(t) over each stellar/command epoch and using seeded exponential hazard thresholds, mathematically the same nonhomogeneous Poisson process as chunk counts. This preserves a realization across1/200 sampling cuts and avoids per-year rolls; each resolved event retains its own year. If literal count-per-chunk is required instead, draw N~Poisson(∫λdt) and sample ordered event years from the normalized hazard CDF, accepting that different chunking changes the seeded realization even though distributions match.
2. **Gyr cost versus exact event dates:** explicit enumeration can reach tens of millions of flares in a Gyr even for a solar source. Do not truncate counts/energy, silently skip effects or label a cap as physics. Preferred exception: resolve low-count windows exactly, but expose high-count windows as flagged aggregate records with Poisson count and documented compound-energy/temporal approximation; report count/energy and convergence with finer windows. Individual event years cannot be honestly claimed in that aggregate regime. Alternative is an explicit preflight rejection before state mutation for unsupported huge jumps. Claire must choose/limit this exception to the literal stretch-exact/own-year requirement.
3. **Atmospheric loss and ledger semantics:** gas escaping a planet may remain star-bound, as C6 established. EscapeRole is currently a sink outside the resolved world, not a kinetic gas-cloud solver. Preferred use for C7: require enough absorbed energy to pay both planetary escape and the remaining system-binding energy before booking true Escaped; retained wind would need a bound reservoir. Alternatively ACK a clearly named unresolved-atmosphere sink exception. Do not call all planet escape true system escape without that decision. Ionizing dose and energy-limited escape remain explicit macro proxies, not full ozone/hydrodynamics.

## Validation planned

- Rate normalization, M dwarf vs Sun at the same age, younger vs older at the same mass; phase/unsupported-source exclusions.
- Seeded Poisson mean/variance and truncated-energy distribution quantiles/slope; no per-year work.1/200 cut equality if hazard-calendar proposal is accepted.
- Resolved event timing inside Advance/Jump and correct biology settlement; source/target generation reuse, source death, switch off/on, changed constants.
- Gas loss ≤available Gas and ≤energy budget, every material/M/P ledger closure; distinguish planet-bound/system-bound/unbound material as ACKed.
- Earth at1AU,1e34erg survives; closer identical worlds receive1/d² fluence.
- Same Journal/seed exact replay and persistent-state hash coverage, independent of history eviction.
- Long jumps: explicitly test the ACKed high-count policy; no silent loss or fabricated exact dates. Measure cost on stock5250 objects and event-heavy systems.
- FullCLI, fresh game build plus headless selftest/uitest with DLLmtime; report all old→new fixture hashes, limits and failed runs. No merge; C8 waits Claire's C7 review.
