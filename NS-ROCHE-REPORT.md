# Neutron-star drop — C6 regression and approved repair

Branch `celine/roche-no-reshred`, base/current `a1c832fe09352a5328797a87160bb8e620d15ccd`; pre-C6 comparison `461ce87^1 = e58c000c994056d72ded043526dc500d7cd5aa92`.

## Independent diagnosis

Probe uses SolSystem(5000,1234), 32 warm Advances(.5), 64 measured baseline Advances(.5), then Create at `(100,0)` with velocity `(0,0)` and mass `466700 EarthMass = 70.005 = 1.4001 solar masses`. SetStarState follows the GUI preset's 10-solar-mass progenitor and expired fuel; resulting phase is NeutronStar, radius `.00023709159669806613`. Then measure 8 Advances(.5), each at 8 substeps. The seed matches the game; placement and time are explicit proxy inputs, not Yang's unrecorded exact mouse position/time. x100 corresponds to ~3.625 AU on the scene's compressed distance mapping.

Seven fresh paired Release runs, alternating binary order, sequential (no own benchmark overlap). Times below are each run's mean ms/Advance; median ratio is taken over pairs, not a quotient of separate medians.

| Pair | Pre-C6 before / after NS | Current before / after NS | After-cost ratio B/A |
|---|---:|---:|---:|
| 1 | 5.3969 / 9.8414 | 3.3706 / 249.3199 | 25.3339 |
| 2 | 2.6729 / 4.3414 | 3.7025 / 204.2025 | 47.0366 |
| 3 | 6.1446 / 11.9199 | 5.8358 / 206.2302 | 17.3014 |
| 4 | 4.2914 / 6.0916 | 5.1293 / 231.5963 | 38.0193 |
| 5 | 4.8446 / 9.8216 | 8.7357 / 257.3722 | 26.2047 |
| 6 | 4.3664 / 5.4705 | 4.8296 / 191.3428 | 34.9771 |
| 7 | 4.7305 / 8.8713 | 5.2028 / 224.0151 | 25.2518 |

Median paired regression **26.2047x**. Median run means: pre-C6 4.7305→8.8713 ms, current 5.1293→224.0151 ms. Before C6 there was already extra gravity/contact work from adding an attractor; the new order-of-magnitude slowdown is C6 Roche work. All seven runs reproduce final hashes pre `E127789A9FDBF882`, current `7C5BFDD0F6146A50`.

After 8 steps: pre-C6 5251 live/11 attractors/5240 small objects, no debris/records. Current 5511 live/10 attractors/5501 small objects, 277 live Roche fragments, **2048 disruptions = 256 per step**, 1023 reservoirs (mass .04767008985916343), 2052 groups, zero ordinary merges. Object capacity is 5512; live N is bounded, while recursive breakup/record growth is pathological. Current Roche-off diagnostic after cost 4.4685 ms with no breakup; Debug single-pair diagnostics pre 11.5008 ms/current 430.9936 ms (not statistical acceptance measurements).

## Where time goes

Diagnostic copies instrument only Stopwatch counters outside the arithmetic/ordering; profile and pristine hashes match. First 8 steps, mean ms/Advance:

| Component | Pre-C6 profile | Current profile |
|---|---:|---:|
| Gravity/setup/contact candidates/parallel barriers | 5.9245 | 7.1521 |
| Drift/barrier | .3266 | .4738 |
| Ordinary Merge | 0 | 0 |
| Rules | .4106 | 2.1900 |
| Roche candidate collection | 0 | 226.9030 |
| Roche entry search | 0 | 77.1966 |
| Roche breakup | 0 | 37.4828 |
| Total measured | 6.6815 | 353.8960 |

Reservoir insertion .3134 ms and fragment creation 1.3656 ms are **nested inside** breakup, not additional totals. Roche accounts for ~96.5% of measured current time. It collects candidates 2064 times, searches entry 2120 times, breaks 2048 times. Remaining time is loop/clock overhead; diagnostic timing is not substituted for pristine seven-pair results.

Steps 9..16 worsen: 361 integration slices vs ordinary 64, candidate collection 939 calls/808.1130 ms per Advance, entry 999 calls/458.5005 ms, breakup 927 calls/11.0436 ms. Accumulated candidates ~11.6M. Compact-primary surface acceleration `GM/R²` makes the conservative drift bound enormous; rebuilding and searching after recursive topology changes multiplies that debt. Actual fragment allocation remains a small fraction.

## Cause and conservation

`core/Roche.cs:324` chooses represented mass from the object budget before checking the **new fragment's own** Roche limit at its planned ring radius. The original body's radius defines breakup, then much smaller ring radius can place children inside their own limit. On Advance(0), first ancestry is source5 Gen1 Jupiter (.04767) → Gen2 (4e-8) → Gen3 (6.25e-10) → Gen4 (9.765625e-12), all same year/NS host. Full slots reduce count to 2 and keep halving children, rebuilding the global list each time. After 256 zero-time breakups, **189 of 265 live debris objects remain inside their own Roche limits**; the numerical cap delays processing but cannot repair the representation.

Zero-time ledger includes live objects + bound reservoirs + Escaped ledger, minus explicit nucleosynthesis delta per material. Differences: ΔM=-2.4158453e-13 (~2e-15 relative to system mass), ΔPx=-4.8577538e-18, ΔPy=8.6826978e-17, max |ΔComp|=3.1263880e-13. No mass-loss/false-escape finding. Positive-time momentum drift is not attributed to breakup: non-attracting rocks do not exert reciprocal force, so the existing gravity approximation does not conserve total momentum exactly.

The NS's physical mean density is ~8.3081e13 g/cm³. Existing macro law yields Earth's K=1.2718, density3.6482g/cm³, strength38.71MPa, cohesionless limit8.5469342 and strength-corrected8.5438777 simulation units, agreeing with independent arithmetic. Jupiter's limit35.7998 exceeds its distance28.1293: its first breakup is warranted by the accepted macro model; Earth at140.0576 is not inside its NS limit. Radii are local simulation distances, not exact astronomical AU: the accepted squeezed coordinate map is not a linear physical distance model. Size/strength dependence is physically motivated ([Aggarwal/Oberbeck NASA abstract](https://ntrs.nasa.gov/citations/19740055720)); the implemented strength law remains a macro proxy, not their elastic yield solver.

Rendering is a secondary unmeasured cost: `game/Main.cs:606` draws 128-point orbit arcs for all alive objects with Par, radius≥6px. Roche gives every fragment a parent. At default zoom .75, the step112 snapshot has 40 eligible arcs; not all 301 fragments cross the pixel threshold. Small multimesh instances grow only ~5%. No GPU/windowed FPS claim is made. Ariel owns game draw/log changes.

## Approved repair specification

Claire approved on bridge 2026-10-07 23:46:38Z: select fragment mass/size that survives Roche at the planned radius before spawning; retain nonrepresentable captured matter in the existing hashed BOUND reservoir. Do not generate unstable children and let the cascade chew through them. No arbitrary same-host immunity or cooldown as a physical substitute.

Invert the existing law using source composition (same density, K, strength) and planned minimum host separation. For cohesionless limit D0 and target distance d below D0, a cohesive fragment survives when `Rf² ≤ sigma/[Gphys*G*rho²*((D0/d)^3-1)]`; mass follows radius cubed at fixed composition. Zero-strength material inside its cohesionless limit has no stable resolved size and stays in the bound cloud. Also respect host-surface contact radius and the existing fragment/slot/mass budget, with roundoff clearance from the entry tolerance. Opposite pairs retain COM/P/L and all leftover material stays accounted for. Avoid unnecessary persistent state or new physical constants.

Acceptance: finish predecessor's 200-step settle trace, then implement on this branch. Seven paired Release runs after-NS cost ≤3× own before cost; bounded fragment/reservoir growth and no cap hits in steady state; zero-time M/P/Comp closure, Escaped ledger, all existing Roche/C6 checks, deterministic replay; explain any changed hashes and fresh recapture; new independent NS CLI regression plus median performance gate; fixed scaling at 1k/5k/20k objects. C7 is deferred. Raw harness/source/profiles/logs live at `E:/Temp/cosmos-ns-study/` with binary paths and DLL UTC mtimes recorded.
