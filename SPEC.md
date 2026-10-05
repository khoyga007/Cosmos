# Cosmos — SPEC (06/10/2026)

Source = Yang's words 06/10, restated and confirmed by him point by point. Tags: **[Y]** Yang decided · **[C]** Claire proposal, Yang heard it and did not object (not explicitly approved) · **[P]** placeholder value/content, Yang's to change.
Supersedes: web project E:\CosmosSandbox (archived), every grain/softening note in git history. State of work = `NEXT.md`.

## 1. Game
- [Y] Sandbox god play at UNIVERSE scale: physics simulation AND god game at once.
- [Y] Build bottom-up: star-system + planet level first. Galaxy later = its own realtime physics tier (not a static map). Keep the door open, build nothing for it now.
- [Y] Dwarf Fortress way: we write PARAMETERS and RULES, objects interact by themselves, stories emerge. No scripted scenarios.
- [Y] Minimal + macro first: physics, scale, distance simplified; big laws must stay real (heavy pulls light, real orbits, near star = hot, collisions conserve). Numbers need not match reality.
- [Y] View: 2D straight from above. (T key tilt stays as a draw-only extra.)
- [Y] Yang makes no art: visuals = code-drawn or AI images.
- Scale honesty (said to Yang, accepted): studio-size idea; plan = small playable in weeks, thickens over months; never "complete".

## 2. Model
- [Y] EVERYTHING is an object with its own physics and own parameters. No grain mechanism.
- [Y] Asteroids = objects. A belt = an object that contains them (group).
- [Y] One set of universe CONSTANTS every object obeys; the god can change them at run time.
- [Y] Parameters grow in LAYERS on the same object: planet with right conditions gets a life layer; life long enough gets a civilisation layer.
- [Y] Elements: 6 groups — gas, ice, rock, metal, carbon, radio. Element = resource (no second tier). Also a toy: god mixes ratios to make different planets/stars.
- [C] Kind (star / gas planet / rock planet / ice planet / moon / rock) is DERIVED by rule from mass + matter; the god never picks a kind.
- [C] Derived values are never hand-set: radius, "pulls others" (mass >= threshold constant), kind.
- [C] Matter moves between objects by collision only (merge sums the matter tables) or by direct god edit of a table.
- [C] Civilisation over several planets/systems: shared part (identity, tech, belief, member list) = one abstract object, no position/mass, same pattern as a belt; local part (population, development, state) stays as a layer on each planet. One-planet civ = member list of one.
- [Y] Travel between planets/systems = abstract flow of numbers. No ship objects.
- [C] Role of each group in rules [P]: gas → atmosphere / gas planet; ice → liquid water when temperature fits (needed for life); rock → solid surface; metal → density, industry/tech; carbon → stuff of life; radio → inner heat, late-civ energy. Civ consumes from the table of the planet it lives on.

## 3. Engine decisions (Claire's, delegated)
- Godot 4.7.2 .NET (`E:\Godot\Godot_v4.7.2-stable_mono_win64\`) = shell. Sim core = plain C# library `core/`, NO Godot types, runs headless in `cli/`.
- Sim 2D, one plane per world. Fixed step, seeded stream, sequential → a run repeats exactly (cli "repeat" check must stay green).
- Pull = exact 1/r^2, no softening. Touch = merge (heavier keeps identity). Every object feels every PULLING object. 8 small steps per `Advance`. Slots stable (dead slot reused, never shifted).
- Distances squeezed `45*AU^0.62`, real order + mass ratios [P].
- Text shown to the player (Vietnamese) lives in `game/`. Core events carry ids + numbers, not sentences. (Scene names in `SolSystem` are data, allowed.)
- BOUNDARY (Yang approved 06/10, idea from his Claude Web chat): the outside changes a world ONLY through `World.Do(Command)` (`core/Commands.cs`). Commands are recorded in `World.Journal` with their step; scene + journal rebuilds the same world (cli "commands" check). Reading world arrays for drawing is free; writing them from `game/` is forbidden. A new god power = a new `CmdKind` in core (ask Claire), then its tool in `game/`. Gives replay, bug reproduction, headless tests, later rewind.
- LATER, agreed in principle, not started: snapshot of the whole state = save point / rewind (after round 1); rule numbers + thresholds + kinds in a data file Yang can edit (when rules 2-3 exist); narrator that reads the event log. Language stays C# (web/TypeScript and Rust were weighed and dropped: stack already measured and accepted; C# core is already engine-free).
- Agents NEVER open a window on Yang's machine (no Godot editor, no non-headless run). Only `--headless`. Anything a mouse does must also be callable from code so a headless self-test can run it.

## 4. Rules, time, log [C]
- RULE TABLE: each rule = one entry: id, what it reads, what it writes, rhythm (years between runs), on/off switch. Written per parameter type, never per object. Adding a rule = adding an entry.
- TIME unit = year. 1 year = one Earth orbit in `SolSystem` (2π·sqrt(45³/(G·50)) ≈ 267.7 time units at G=1 → keep as a constant `Consts.YearTime`, NOT recomputed when the god changes G). Life/civ rules are written in years, never in steps.
- EVENT LOG: when a rule moves a state across a threshold it appends {year, object, rule id, what changed, cause numbers}. Automatic, no hand-written messages in core.
- First chain, one link per slice: (1) star light + distance → planet temperature; (2) temperature + ice → liquid water; (3) water long enough → life; (4) life long enough → civilisation. Life/civ parameters and thresholds = Yang's content, NOT to be invented by the team; ask on the thread.
- FAST-FORWARD, spike on master (`core/Rails.cs`, `CmdKind.FastForward`, Amount = years; checks `cli -- rails`): one jump, every object rides its present orbit by closed two-body formula; cost independent of length (1e6 years: 0.05 ms bodies, 4.5 ms with 5000 rocks, 28 ms with 50000). Primary = lightest pulling object whose Hill zone holds it, else heaviest. Body + satellites ride as one lump (centre of mass orbits the primary). No rails mode: state after a jump is ordinary, next `Advance` pulls for real.
  - Lost in a jump, on purpose: pull between siblings, collisions, crossings. Measured vs stepping: 3.7 years 1.3% distance / 0.024 rad; 37 years 1.1% / 0.20 rad (phase is meaningless over long jumps, shape holds). Not bound = straight line, counted in `World.OffRails`.
  - OPEN: (1) rules inside a jump — a jump of T years must run rules with rhythm << T. Proposal [C]: jump in K chunks (K <= ~1000), run the rule table after each chunk, each rule gets the years passed since its last run; state rules (temperature) just sample, clock rules (life age) add the years. Eccentric orbit sampled at random phase can flicker a band edge -> orbit-mean light for such rules. (2) DONE: jump advances `World.Year`, rule table runs once at the landing year. (3) orbit whose near point is inside its primary should merge at jump time; crossing orbits unchecked. (4) Newton solver only: e > ~0.97 needs a bracketed one. (5) galaxy tier not considered.

## 5. Code now (master)
- `core/World.cs`: `Consts` (G, AttractMass 1e-7, StarMass 4, RadiusScale 1.5, Density[6]) · `Kind` · `World`: SoA `X Y Vx Vy M R Comp Alive Name Col Par Grp`, `Groups`, `Add`, `AddOrbiting`, `Attracts`, `KindOf`, `Hill`, `RecalcRadii`, `SolSystem(rocks, seed)`, `Advance(h)`, `Hash()`, `Step`, `Merges`.
- `core/Commands.cs`: `CmdKind` (Create, CreateOrbiting, AddMatter, Push, SetConst, Remove, SetRule, FastForward), `Command`, `World.Do`, `World.Journal`, `World.Replay`, `Consts.All()` / `Consts.Set(name, value)` (every constant by name, e.g. "G", "Density[2]"). `World` and `Consts` are `partial`.
- `core/Rules.cs` (Celine, P1 merged f613d7a): `World.Year`, `Consts.YearTime` 268.233883, `World.Rules` (Id/Reads/Writes/RhythmYears/Enabled/NextYear/Apply), `Events` (bounded 1024, `RuleEvent{Year, ObjectSlot, RuleId, Change, A, B, C}`), `Temp[]`, `BandOf`, rule `temperature` (event `band.<old>.<new>`, 0 frozen / 1 temperate / 2 scorched; Kind.Planet only). All numbers [P]. Switch a rule from outside ONLY by `CmdKind.SetRule`; `Rule.Enabled` / `LogEvent` are public for tests, the window must not write them.
- `core/Rails.cs`: `Jump(t)` (private, reached through `CmdKind.FastForward`), `Kepler`, `OffRails`.
- `cli/Program.cs`: timing + checks merge / moon / sol / constants / commands / repeat, then `RailChecks.Run()` (cli/RailChecks.cs; alone: `cli -- rails`), `RuleChecks.Run()` (cli/RuleChecks.cs) and `Audit.Run()` (cli/Audit.cs). `dotnet run -c Release --project cli` ≈ 1.5 min → use a long timeout.
- `game/Main.cs`: whole window in code. `run.bat [rocks]` = Yang's launcher. Headless: `<godot>_console.exe --headless --path game -- --rocks=N --bench=SECONDS`.
- Measured (Release, Yang's machine): 5000 rocks 2.4 ms/step · 20000 9.4 · 50000 20.5.
- Not written: rock-rock collision, fragmentation, swept collision for fast small bodies, layers, rules, log, years, god tools beyond G keys + moon placing.

## 6. Work packages — round 1
Each agent: own worktree + branch (already created), claim on bridge thread `cosmos` before coding, report there with commit hash + the exact check output. Stay inside your files. Need a change in someone else's file → ask on the thread, do not edit. Unclear or two readings → ask before building. Do not invent game content (life/civ parameters, thresholds, god powers beyond the listed ones). Commit often; no force-push; no `git add -A`. Claire reviews and merges to master.

### P1 — Celine — rule spine + first rule · worktree `E:\Cosmos-celine` branch `celine/rules`
Files: `core/Rules.cs` (new), `core/World.cs` (hooks only, smallest diff), `cli/RuleChecks.cs`.
1. `World.Year` (double), `Consts.YearTime`. `Advance` moves the year.
2. Rule table: entry = id, rhythm in years, on/off, the function. Runs inside `Advance`, deterministic order, each rule only when its rhythm is due.
3. Event log: bounded list of {year, object slot, rule id, change code, up to 3 numbers}.
4. Rule 1 `temperature`: per object, from light of every star (mass ≥ StarMass; luminosity from mass, simple power law [P], exponent a `Consts` field) and distance. Writes `World.Temp[i]`. Logs when a planet crosses a band edge (bands [P]: frozen / temperate / scorched, edges = `Consts` fields).
Done when, in `RuleChecks`: (a) Sol at start: Temp falls with distance, Earth in the temperate band; (b) push Mars outward (edit its velocity) → Temp drops → exactly one log event naming the rule and the cause; (c) rule switched off → Temp stops changing; (d) "repeat" check still equal; (e) 5000-rock step cost not more than 10% above 2.4 ms. Paste the cli output.
STOP after rule 1. Rule 2+ need Yang's content.

### P2 — Ariel — god tools in the window · worktree `E:\Cosmos-ariel` branch `ariel/godtools`
Files: `game/` only (split `Main.cs` into more files if it helps). No edits in `core/`.
1. Create tool: a panel with mass + six ratio sliders (sum shown, normalised on create) → new object at the cursor: on a circular orbit around the selected object, or at rest when nothing is selected. Shows the derived kind + radius before placing.
2. Edit tool: on the selected object, add or remove an amount of one group (table changes, mass follows, `RecalcRadii`).
3. Push tool: drag from the selected object = add velocity along the drag; show the arrow while dragging.
4. Time control: keys for 1× 2× 4× … 64× = that many `Advance` per frame, shown in the HUD with real ms; drop back automatically if a frame goes over 50 ms.
5. Constants panel: every `Consts` field shown, editable (G keys stay).
Every tool builds a `Command` and sends it through `World.Do` — no direct writes to world arrays, no direct `Add`/`AddOrbiting`/`C.x =` (the two existing spots in `Main.cs` already use `Do`). Tools 1, 2, 3, 5 map to Create/CreateOrbiting, AddMatter, Push, SetConst; the constants panel lists `Consts.All()`. Time control is window state, not a command. Missing command kind → ask Claire on the thread. Each tool = a plain method the mouse handler calls. Add `--selftest`: headless, calls each method with fixed inputs, then replays `World.Journal` on a fresh world and compares `Hash()`, prints one line per tool with before/after numbers, exits 1 on any mismatch. Done when `--selftest` and `--bench=3` both run clean headless; paste output. Look (colours, layout) is judged by Yang's eyes later: keep it plain, do not polish.

### P3 — Selica — audit of the core + measurements · worktree `E:\Cosmos-selica` branch `selica/audit`
Files: `cli/Audit.cs` only. Read `core/World.cs` (f0817b1 + later), change nothing in it.
1. Review `World.cs` line by line. Report each defect as `file:line — what breaks — smallest input that shows it`. Look hard at: slot reuse after merge (`Par`, `Grp`, selection held by the window), `_hits` when one object touches two in one small step, `Hill` when the parent died, capacity full, G or densities changed mid-run, mass 0.
2. Measure in `Audit.Run()` and print: total energy + angular momentum drift of Sol with 0 rocks over 20000 steps; the same with G changed mid-run; how fast a small object can go before it skips across Earth without merging (the swept-test question); cost of `Advance` per object count split bodies vs rocks.
3. Every defect you can show with a failing check → add that check (it should FAIL now); list them. Do not fix the core.
Done when the report is on the thread with numbers and the list of failing checks.

### Claire
Fast-forward on rails (design + spike), review + merge P1–P3, Yang's content questions, next round.
