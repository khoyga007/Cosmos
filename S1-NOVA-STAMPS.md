# S1 — nova, cooling, and event years

Initial base: `1e5709f`; rebased onto `132defe` (Selica S2 independent conservation probe/audit). Branch: `celine/nova-stamps`. No game edits; no merge.

## Reproduction and changes

- Base `cli -- jump-bench 1e10 0 v`: Earth life/pop become zero at `star.giant`, because the last 50 Myr are evaluated with the new heat. The old luminous era is now settled first, including clock rules whose rhythm is not due; heat/water refresh at the boundary with zero elapsed time.
- Earth now retains life/pop at the giant boundary. After +1e6 years: `life.end = giant +184206.80744`, `civ.end = giant +50681.26713`; 1/200 samples agree within 1e-3 year. Population follows the exponentially declining biosphere until its feeding threshold, then a dome/starvation curve. Its population-years integral uses bounded adaptive Simpson quadrature only for that changing carrying capacity; fixed-room curves keep their analytic integral.
- New life-curve arrays are scratch within a rule pass, cleared on every pass and slot reset. They are never future state; there are no new Consts fields or command kinds. Extinction dates are solved on their own curves; swallowed victims end at contact, with `civ.end A=-1`.
- Expansion contacts are checked at the final stellar boundary too, so Mercury is swallowed even with one chunk ending exactly at giant entry.
- Reproduced (a) on orbiting worlds heated to 400 K, at 1/10/30 solar-mass progenitors. Base remnant temperatures: 3.9224 / 10.6492 / 2.7 K. Direct transitions are possible in the instantaneous equilibrium model and remain possible (particularly a dark BH); there is no invented warm plateau.
- The actual light defect: a WD was born at only 0.01 solar luminosity (~19864 K surface in the isolated test). It now starts at 200000 K and cools by a mass-dependent Mestel law with a finite hot age origin. NS normalization is 2 MK at 330 yr rather than an immediately faint 0.02 solar luminosity.
- Cooling edges (temperature bands and water freeze/boil) split jumps, so a real warm interval cannot disappear merely because one chunk spans it. Fixed-distance WD probe: `band.2.1@2462243.435990`, `band.1.0@7561262.770518`, equal for 1/200 and matching the independently inverted age-luminosity law.

## Physical sources and limits

- WD: [Pols 2011, section 12.3](https://inpp.ohio.edu/~meisel/ASTR4201/file/StellarStructureAndEvolution_OnnoPols2011.pdf), Mestel `tau = (1.05e8/Aion) (L/M)^(-5/7)` years; C/O mean ion mass 14. Implemented `L = Lbirth (1+age/t0)^(-7/5)`, with `t0` from that law and birth luminosity from Stefan–Boltzmann/radius. [Princeton AST514](https://www.astro.princeton.edu/~burrows/classes/514/homework.2025.pdf) gives an emerging WD temperature around 200000 K. This excludes detailed neutrino losses, residual burning, crystallization and composition-dependent tracks.
- NS: [Chandra Cas A](https://chandra.cfa.harvard.edu/photo/2009/cassio/index.html), surface around 2 MK in a young remnant. The existing tunable effective exponent 1.3 remains; this is a thermal macro fit, not a universal cooling law or a fit to Cas A's measured cooling derivative. [Yakovlev & Pethick 2004](https://arxiv.org/abs/astro-ph/0402143) explains its dependence on envelope, neutrino processes and superfluidity.
- Planet thermal inertia is not added. Its long-jump temperature retains equilibrium from the existing squeezed-distance/orbit-average flux model. Continuous crossings are exact for fixed geometry; rail motion, unbound flight and other interactions retain the existing jump approximations.

## Hash migration

Four EXISTING defaults change: `StarWhiteLight`, `StarWhiteCoolYears`, `StarNeutronLight`, `StarNeutronCoolYears`. Every constant is hashed, so even unaffected Sol initial/short scenes change hash. Stellar scenarios also change because layer clocks settle at boundaries and remnant light/cooling changes. Recaptured all 11 ElementChecks and 7 StarEventChecks values; phase/ejection mass and momentum tests remain independent sentinels.

| Constant | Old | New | Basis |
| --- | ---: | ---: | --- |
| StarWhiteLight | 0.01 | 120.94993527935979 | Reference WD radius 0.00916 solar, birth surface 200000 K, Stefan–Boltzmann |
| StarWhiteCoolYears | 1000000000 | 7500000 | Mestel coefficient 1.05e8 / 14 yr (C/O core) |
| StarNeutronLight | 0.02 | 29.27999460362159 | 20 km radius and 2 MK at 330 yr, default effective exponent 1.3 |
| StarNeutronCoolYears | 1000000 | 330 | Finite hot-origin normalization at the young Cas A reference age |

## Validation

- New checks run unchanged on clean base `1e5709f`: red (11 failures before the two additional analytic-date assertions). Final focused suite: 22 OK / 0 FAILED, covering extinction dates, 1/200 comparisons, real cooling intervals, hot remnant calibration, and replay. Command: `dotnet run --project cli -c Release -- nova-stamps`.
- Final full run: `$env:COSMOS_CORE_SRC='E:/Cosmos-celine/core'; dotnet run --project cli -c Release`: **276 OK / 0 FAILED**, exit 0, audit **5D/3R** (baseline unchanged). Rule overhead at 5000 rocks: 1.51%, within the existing +10% gate. The initial full run also passed (274 before the two additional analytic-date assertions).
- `dotnet build game/Cosmos.Game.sln -c Debug`: zero errors; existing CS8600 warning in GodUi.cs:609.
- Final rebuild and headless selftest: PASS, exit 0, `C430139B76C6A372`; uitest: PASS, exit 0, `B6EC767CF72CE186`. Commands: Godot console executable `--headless --path E:/Cosmos-celine/game -- --selftest` and `--uitest`, launched hidden and waited to exit. No visible window opened.
- After rebasing onto `132defe`: core/game diff against the pre-rebase S1 is empty. `cli -- conserve`: **37 OK / 0 FAILED**, exit 0. Closed stock scene mass is exact (50.0669894548), element drift 7.105e-15, momentum drift 2.494e-8 (S2 base 3.299e-8); stock ice deficit remains 3.964e-7. No new conservation sink; no hash literals in this probe need recapturing.
- Rebased final full CLI: **315 OK / 0 FAILED**, exit 0, audit **4D/3R** (matches the updated S2 baseline). Rule overhead 4.42%, within +10%. Rebuilt game, selftest/uitest again PASS, exit 0, same hashes `C430139B76C6A372` / `B6EC767CF72CE186`.

Logs: `E:/Temp/cosmos-s1-rebased-full.log`, `E:/Temp/cosmos-s1-conserve.log`, `E:/Temp/cosmos-s1-selftest-console.log`, `E:/Temp/cosmos-s1-uitest-console.log`.
