# Review of thermal/aggregate draft — 2026-10-08

Response to Claire09:40Z, not an implemented law or an approved SPEC. No energy/aggregate code written. Existing source inspected at03a29a7; implementation below needs owner-defined units/closures before hard invariants.

## Replace the proposed monotone usable-energy invariant

Gravitational contraction lowers negative potential energy while kinetic/thermal energy can rise. An isolated gravitating system can lose total energy and become hotter; therefore heat/ordered kinetic energy is not a universally one-way ladder. Negative heat capacity is an established feature of self-gravitating systems. [Lynden-Bell1998](https://arxiv.org/abs/cond-mat/9812172).

Proposed game audit, a model choice: `E_accounted = K + U_pair + U_self + E_internal/dispersion + E_fuel + E_material_escaped + E_radiated`. Compare its change to **signed** God work/injection plus a declared numerical residual. Fuel/internal terms must not double-count rest-mass energy or orbital kinetic energy. Only genuine cumulative radiated escape and specified dissipative production are required nonnegative/monotone. A “remaining useful energy” gauge can be an estimate; it is not a hard each-step physics invariant. Fixed kick/drift integration already produces energy oscillation/error without God input.

God Push/Remove/Create/Move/SetConst all need signed M/P/L/E effects, including changes to interaction/self potential. Increasing G is not just positive “fuel injection.” Unit conversion is mandatory: engine distances are squeezed and GRB metadata is SI joules. Use one declared model convention instead of adding incomparable numbers.

Heat/dispersion also does pressure work during expansion. Cooling dense material is not universally proportional to density*dispersion: opacity and dynamical response matter. Disk self-gravity can balance cooling by heating or fragment under a sufficiently rapid cooling prescription; the published threshold belongs to that model, not every cloud. [Gammie2001](https://arxiv.org/abs/astro-ph/0101501).

## Promotion, grouping and real formation

Demotion based only on velocity relative to dispersion destroys coherent clumps, correlations and resonances. Require eligible debris, no pending encounter/Roche/command interest, quiet time and hysteresis. Carry internal kinetic energy/covariance and angular momentum; preserve significant clumps in cells or real bodies. Grouping is a representation operation with no invented dissipation; collapse/coagulation is a physical process with progress/time/energy bookkeeping.

Physical decisions must use binding/pressure/spin/geometry and cooling instability, not theNth body or a renderer's radius. A collisionless stellar swarm is different from collisional gas. A grouped stellar population needs mass/age/fuel distributions so its nonlinear lifetime/death/SN rates survive. Yang accepted individual rock-history loss; this does not authorize suppressing natural stellar deaths.

Quasistar means an accreting black hole embedded inside a massive gas envelope, not merely a giant star above a mass threshold. Formation has to retain the central-BH/envelope distinction even if represented by a macro object. [Begelman,Rossi,Armitage2008](https://arxiv.org/abs/0711.4078).

If timescales are compressed, use an explicit numerical/content dial and dated progress; do not silently turn a long physical process into immediate grouping to satisfy cost. Jump/warp can traverse long quiet intervals while respecting predicted hot events and stopping at work-budget exhaustion.

Promotion/demotion and monopole↔exact switching can change the approximate potential at unchanged physical state. Quantify that as representation/numerical residual; do not label it physical radiated heat. Preserve M/Comp/P/L/internal energy by extracting the promoted parcel from its source, not duplicating its mass. No aggregate code before SPEC.

## Limits found in the current code

| Source | Reuse / missing closure |
|---|---|
|core/World.cs:209|Merge's mass-weighted COM/velocity and composition summation are useful. Missing internal spin/angular momentum and a ΔK/ΔU/heat transfer. Relative orbital L must become spin or other resolved motion; relative kinetic loss alone is not the whole gravitational energy change.|
|core/World.cs:393,432|Pullers kick only other pullers; sub-threshold rocks receive their force without reaction. Thus current model does not conserve global positive-time P/E. With few real bodies, define reciprocal body/aggregate forces before asserting full closure; never hide the imbalance in an invented escaped ledger.|
|core/Escape.cs:7,12,31|Useful M/P/Comp transfer and residual trimming. Missing E/L. Existing matter “escaped from the simulated system” cannot be equated with heat lost from a whole universe: decide whether it remains diffuse/interstellar matter or crosses an explicit domain boundary.|
|core/Roche.cs:30-40,296,327|Reservoir has M/P/L/Comp and formation coordinates, but is inert snapshot matter. Symmetric partition/retirement logic is useful; it is not an advecting/gravitating distribution or thermal model.|
|core/Stars.cs:10,123,322|Burnt-fraction/lifetime/boundary clocks are reusable. Fuel is not joules; luminosity and stellar mass loss are not yet debited into an energy ledger.|
|core/CosmicEvents.cs:30,56|GRB has directional SI-energy metadata; no corresponding fuel/thermal debit or recoil/radiation momentum closure.|

A single escaped-heat scalar cannot carry directional radiation P/L. Either specify an isotropic Newtonian approximation and its omitted terms, or add radiation/recoil ledgers. Starlight absorbed by another object comes from radiation not yet escaped; never recover energy from the irreversible escape bucket. Newtonian conserved material mass plus fuel energy is a declared approximation; exact rest-mass-energy conversion is a different contract.

The first energy milestone should be diagnostic balances for explicitly modelled terms, with unmodelled/numerical residuals shown. It should not impose usable-energy monotonicity on current code or claim full M/P/L/E closure merely because existing event mass checks pass. Use small isolated orbits, infall, grazing merge, aggregate promotion/demotion, radiative cooling, stellar mass loss and signed God changes as distinct tests. God-unbounded intent still needs a finite representable numeric domain and deterministic rejection/early-stop trace before NaN/Inf enters state.
