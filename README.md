# Cosmos

Sandbox god game: real physics + emergent stories. Star-system level first (Sol scene); galaxy / universe tiers later.
Dwarf Fortress way: write parameters + rules, objects interact by themselves, stories emerge. No scripted scenarios.
Godot 4.7.2 .NET window over a plain C# simulation core. UI text is Vietnamese.

## Features

**Physics**
- Exact 1/r² gravity, no softening. Every object feels every *pulling* object (mass >= `AttractMass`). Touch = merge, mass and matter conserved.
- Objects: stars, planets, moons, asteroids/comets (thousands of rocks), ships. One plane, top-down 2D view.
- One set of universe constants (`Consts`: G, densities, thresholds...). God can change any of them at run time.
- Six element groups: gas, ice, rock, metal, carbon, radio. Derived values (radius, kind, class) are never hand-set.
- Time: year = one Earth orbit. Fast-forward jumps up to 1e10 yr on rails (closed-form Kepler), rules still run inside the jump.

**Stars**
- Main sequence from mass alone: luminosity, colour class, radius, lifetime (Sun 1e10 yr, real scale).
- Death by mass: red giant (swallows inner worlds), white dwarf, supernova -> neutron star, black hole. Remnants cool for real.
- Cosmic background floor 2.7 K for non-star bodies.

**Layers growing on worlds** (rule table, each rule own rhythm)
- Temperature -> water state (none / ice / liquid / vapour) -> life (level, stages) -> civilisation.
- Civ: generated name, chronicle, population, tech, ten-era stage table with material needs, footprint on matter, domes on dry worlds, ships as real objects, colonies, first-ship dating.
- Impacts cut life and population by share of mass hit.

**God tools**
- Create (mass + element mix, circular orbit or launch vector), edit matter, push / pull / repel, move, remove, seed life, set star state, remnant presets (white dwarf / neutron star / black hole), change any constant, switch rules on/off.
- Time bar, jump buttons, event log, object list with jump-to, zone overlay (Hill spheres), selected-object panel.

## Architecture

```
  game/ (Godot window)            core/ (plain C#, no engine types)           cli/ (headless)
  Main.cs   input + draw  ──┐
  GodTools  mouse -> Command ├──►  World.Do(Command) ──► Journal ──► Replay     checks, audit,
  GodUi     panels          ──┘            │                                      benches, hashes
  reads arrays freely                      ▼
  never writes them          Advance(h): 8 sub-steps, exact gravity, merge
                                           │
                                           ▼
                             Rule table (id, reads, writes, rhythm, on/off)
                             temperature · water · life · civ · ships · stars
                                           │
                                           ▼
                             Events log (ids + numbers; Vietnamese text built in game/)
```

- **Core vs shell.** `core/` has no Godot types and runs headless in `cli/`. `game/` only draws, takes input, and turns it into commands.
- **Command boundary.** The outside changes a world ONLY through `World.Do(Command)` (`Commands.cs`). Commands are journaled with their step; scene + journal rebuilds the same world. Gives replay, bug repro, headless tests, later rewind. New god power = new `CmdKind` in core, then its tool in `game/`.
- **Struct of arrays.** `World` holds parallel arrays (`X Y Vx Vy M R Comp Alive Name Par Grp ...`). Slots are stable: a dead slot is reused, never shifted.
- **Rule table.** A rule is one entry (id, reads, writes, rhythm in years, switch). Rules are written per parameter type, never per object, and use `World.RuleYears`, never the rhythm. An event inside a stretch is placed at its own moment.
- **Rails fast-forward.** `Rails.cs`: each object rides its orbit by closed two-body formula around its primary; a jump is cut into <= 200 chunks and the rule table runs after each. Cost is independent of jump length.
- **Table-driven engine layer.** Elements, civ stages, star events are tables; adding one = adding a row, not code per case. `cli -- audit-tables` counts hard-coded leftovers.
- **Determinism.** Fixed step, seeded rng, sequential result. `Hash()` covers positions, velocity, matter, rng, free slots and every `Consts` field. Changing a default Const moves hashes: re-capture the checks.
- **Text.** Core events carry ids + numbers; Vietnamese sentences live in `game/`.

| Path | Content |
|---|---|
| `core/World.cs` | `Consts`, arrays, `Advance`, merge, `Hash` |
| `core/Commands.cs` | `CmdKind`, `World.Do`, journal, replay |
| `core/Rules.cs`, `Layers.cs` | rule spine, water / life / civ layers |
| `core/Civ.cs` | names, chronicle, ships, colonies, domes |
| `core/Stars.cs`, `StarEvents.cs` | stellar evolution, deaths, remnants |
| `core/Elements.cs`, `Kinds.cs` | element table, object classes |
| `core/Rails.cs` | Kepler fast-forward |
| `cli/` | checks per subsystem, `Audit.cs`, benches |
| `game/` | `Main.cs`, `GodTools.cs`, `GodUi.cs` |
| `SPEC.md` / `NEXT.md` | decisions / current state, open items |

## Run

Needs .NET SDK + Godot 4.7.2 mono (path in `run.bat`; edit if different).

```
run.bat [rocks]                     # builds core Release, opens window; default 5000 rocks
dotnet run -c Release --project cli # full check suite, ~1.5 min, exit 0 = OK
dotnet run --project cli -- rails   # one subsystem (layers, ...)
```

Headless game tests: `-- --selftest`, `-- --uitest` (real input events), `-- --bench=N`.
Keys: C create tool, V vector, M move, A pull, R repel, Z zone overlay, F follow, T tilt.

## Status and rules

- Alpha not reached. Visuals are code-drawn placeholders; real art and effects wait until after Alpha.
- Physics forks = real-world physics.
- Not built yet: ethics / awareness / empire relations, first contact, cosmic events beyond star death, several star systems, FTL, galaxy tier. See `NEXT.md`.
