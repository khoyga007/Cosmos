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

### 2b. Layers — content [C][P] (Yang 06/10: "em tự nghĩ" = handed to Claire; every number a `Consts` field, his to change)
Only planets + moons carry layers (`IsWorld`: pulls others, not a star). Code `core/Layers.cs`, checks `cli -- layers`.
- WATER (rule `water`, 1 yr): no ice (< 0.1% of mass) → None; Temp < 273 K → Ice; > 373 K or mass < 0.3 Earths → Vapour; else Liquid. Counts years of liquid water in a row.
- LIFE (rule `life`, 1000 yr): starts by itself after 200 000 yr of liquid water in a row, if carbon ≥ 0.1% and rock+metal ≥ 50% (a surface). Level 0..1, logistic growth 2e-5/yr while conditions fit (0.001 → 0.5 in ~350 000 yr); when they fail it falls by e every 20 000 yr and ends below 1e-4. Stages at 0.01 / 0.1 / 0.5 (microbes / complex / rich biosphere).
- CIVILISATION (rule `civ`, 100 yr): rises after life ≥ 0.5 held 100 000 yr. Population 0..1, logistic 1e-3/yr toward the life level (the biosphere feeds the people); life < 0.1 → falls by e every 500 yr, ends below 1e-5. Tech += 1e-4/yr × population × min(1, metal share / 0.3); stages 1 farming / 2 industry / 3 space.
- IMPACT: a merge cuts life and population by exp(−share / 1e-3), share = mass that hit / planet mass. Logged from 1e-4 up.
- GOD POWER `SeedLife` (from Yang's Claude Web chat, he allowed it 06/10): put life at a level on a planet/moon; whether it lasts is up to the rules.
- Sol untouched for 1e6 yr: life on Earth at 200 000, civilisation at 645 000, space age at 685 000; nowhere else. Push Earth outward → water freezes, civ ends ~50 000 yr later, life ~185 000 yr later.
- Events: `water.<old>.<new>` (0 none 1 ice 2 liquid 3 vapour) · `life.start` `life.stage.<a>.<b>` `life.end` · `civ.start` `civ.stage.<a>.<b>` `civ.end` · `impact`.
- NOT built, next slices [C]: civ uses up the planet's metal; civ over several planets (shared object, §2); "lộ diện" = the god shows itself, the civ reacts (belief, fear) — Yang's 05/10 requirement that civs know the god exists, to design after he has seen this slice; "tua lại" = rewind = snapshot (§3 LATER).

### 2c. Civilisation acts — `core/Civ.cs` [C][P] (Yang 06/10: "làm hướng 2 đi, kèm hướng 1"; "lộ diện" NOT now)
- Identity: `Civs[]` (Name from world rng out of invented syllables, Home slot, BornYear); `Civ[slot]` = people on a world (kept after `civ.end`). `Chronicle` = per-civ event list (not hashed, same events as `Events`).
- Metal: from stage 2 a world turns metal into rock, `CivMetalUse` * population-years * mass; mass conserved.
- Domes: stage-3 people without a biosphere hold `CivDome` (0.05) instead of dying. `CivDome` 0 = old behaviour.
- Rule `ships` (2 yr): stage-3 world with Pop >= `ShipPop` sends a ship to the nearest empty solid world (rock+metal >= LifeSolidMin); none left -> supply runs to own worlds (raise Tech to home's). Landing on empty world = colony (Pop = CivSeed, ship's Tech), event `civ.colony`; first ship of a people = `civ.ship.first`.
- Ship = ordinary object (mass `ShipMass`, no pull) + `ShipCiv/ShipTo/ShipFrom/ShipTech/ShipBorn`. Engine `SteerShips(h)` at top of `Advance`: wanted velocity = goal's velocity + closing speed min(`ShipSpeed`, sqrt(0.5*`ShipThrust`*d)), turn limited by thrust*h; inside 1.5 R of a star it holds off sideways until the goal comes round. Touching any pulling body = `Merge` -> `ShipArrives` (lands if solid, else lost; no mass added, no impact). Lost after `ShipLifeYears`.
- Fast-forward: `Jump` lands all flying ships first; rule then founds colonies directly (one per world per run), no objects.
- NOT built: contact between two peoples, war, trade, colony independence, moving a planet with its ships.

## 3. Engine decisions (Claire's, delegated)
- PRINCIPLE (Yang 2026-10-06): the game is built so the ENGINE LAYER can be edited freely — adding/removing powers, constants, physics, element groups, rules, civ stats must stay a small local change. Hard-coded counts and per-case code are debt. Weigh every design against this.
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
  - Rules inside a jump DONE: a jump is cut into ≤ `JumpSamples` (200) chunks, never shorter than the fastest enabled rule; rule table runs after each; a rule reads `World.RuleYears` (years since ITS last run) — clock rules must use it, never the rhythm. In a jump the star an object orbits counts by its orbit-average light (`RailStar`), else a stretched orbit reads hot/cold by chance. Cost 1e6 yr: ~0.45 s at 5000 rocks, ~4.3 s at 50000 (old path). Celine round 2: with the built-in rule table, rocks keep the primary they start with and are solved once at the end (~0.16 s at 50000); they do not weigh on their body between chunks, so worlds WITH rocks differ slightly from the old path; no rocks / one chunk / any added rule = old path, bit-exact.
  - Primary must also HOLD the object (bound), else a fly-by caught inside a Hill zone rode off on a straight line. Kepler solver bracketed (plain Newton threw co-orbital planets to infinity).
  - OPEN: (3) orbit whose near point is inside its primary should merge at jump time; close encounters in a jump are crude (two co-orbital planets end as a pair, not a horseshoe); (5) galaxy tier not considered; (6) stepping on a stretched orbit still logs real freeze/thaw every turn (seasons) — may flood the 1024-event log.

## 5. Code now (master)
- `core/World.cs`: `Consts` (G, AttractMass 1e-7, StarMass 4, RadiusScale 1.5, Density[6]) · `Kind` · `World`: SoA `X Y Vx Vy M R Comp Alive Name Col Par Grp`, `Groups`, `Add`, `AddOrbiting`, `Attracts`, `KindOf`, `Hill`, `RecalcRadii`, `SolSystem(rocks, seed)`, `Advance(h)`, `Hash()`, `Step`, `Merges`.
- `core/Commands.cs`: `CmdKind` (Create, CreateOrbiting, AddMatter, Push, SetConst, Remove, SetRule, SeedLife, FastForward), `Command`, `World.Do`, `World.Journal`, `World.Replay`, `Consts.All()` / `Consts.Set(name, value)` (every constant by name, e.g. "G", "Density[2]"). `World` and `Consts` are `partial`.
- `core/Rules.cs` (Celine, P1 merged f613d7a): `World.Year`, `Consts.YearTime` 268.233883, `World.Rules` (Id/Reads/Writes/RhythmYears/Enabled/NextYear/Apply), `Events` (bounded 1024, `RuleEvent{Year, ObjectSlot, RuleId, Change, A, B, C}`), `Temp[]`, `BandOf`, rule `temperature` (event `band.<old>.<new>`, 0 frozen / 1 temperate / 2 scorched; Kind.Planet only). All numbers [P]. Switch a rule from outside ONLY by `CmdKind.SetRule`; `Rule.Enabled` / `LogEvent` are public for tests, the window must not write them.
- `core/Layers.cs` (Claire): arrays `Water` `WaterYears` `Life` `RichYears` `Pop` `Tech`, `IsWorld`, `LifeStage`, `TechStage`, rules `water` `life` `civ`, `Impact` (called from `Merge`). `World.RuleYears`, `Rule.LastYear` added to Rules.cs.
- `core/Rails.cs`: `Jump(t)` (private, reached through `CmdKind.FastForward`), `Kepler`, `OffRails`.
- `cli/Program.cs`: timing + checks merge / moon / sol / constants / commands / repeat, then `RailChecks.Run()` (cli/RailChecks.cs; alone: `cli -- rails`), `LayerChecks.Run()` (alone: `cli -- layers`), `RuleChecks.Run()` (cli/RuleChecks.cs) and `Audit.Run()` (cli/Audit.cs). `dotnet run -c Release --project cli` ≈ 1.5 min → use a long timeout.
- `game/` (Ariel P2 merged): `Main.cs` window + input, `GodTools.cs` (Create / AddMatter / Push / SetConst as plain methods → `World.Do`), `GodUi.cs` panels; `--selftest`, `--bench=N` headless. `run.bat [rocks]` = Yang's launcher. Headless: `<godot>_console.exe --headless --path game -- --rocks=N --bench=SECONDS`.
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

## 7. Round 2 (06/10) — same rules as §6; rebase on master first
### Ariel — the window shows the story · `game/` only
1. HUD: year. Selected planet/moon panel: temperature + band, water state, life level + stage, population, tech + stage (Vietnamese names in `game/`).
2. Event log panel: newest `World.Events` as Vietnamese sentences built in `game/` from the codes (§2b), with year + object name.
3. Jump: buttons 1 / 100 / 1e4 / 1e6 years → `CmdKind.FastForward`. Seed life on the selected object (level slider) → `SeedLife`. Rule switches → `SetRule`. A mark on bodies that carry life / a civilisation.
4. `--selftest` covers FastForward, SeedLife, SetRule + replay. Fix: TimeControl selftest line must call `SetTimeWarp` and the throttle path, not a bare loop. Read arrays freely, write nothing.
### Celine — cross-vendor review + jump cost · report on thread
1. Review `core/Layers.cs` + `core/Rails.cs` + the `RuleYears` change in her Rules.cs: defects as `file:line — what breaks — smallest input`. Look hard at: dt-independence of the three rules, merge of two living planets (who keeps the layer), slot reuse, NaN paths, hash coverage.
2. Trim the jump: rocks are moved every chunk though no rule needs them between (only `Temp` of rocks) → propose + build on branch `celine/jumpcost`; gate = all checks green, same hashes for worlds without rocks, 50000-rock 1e6-yr jump ≥ 5× faster.
### Selica — P3 audit unchanged, plus Rails/Layers in scope (`cli/Audit.cs` only).
### Round 2 result: Ariel + Celine merged 06/10; Celine's 7 defects fixed by Claire (NEXT.md). Rule for clock rules, added: an event inside a stretch (start, threshold, seed, impact) is placed at its own moment — only the years after it count (`Touched`, crossing time on the curve).
### Round 3/4 (06/10, Claire, after Yang's verdict on the window): `game/` interaction rewritten; god tools by mouse = select, create (C), vector (V: arrow = new velocity), move (M), pull (A), push away (R). Core commands added: `Move` (Target, X, Y, Vx, Vy), `Force` (X, Y, radius in Vx, Amount toward centre, linear fade, mass-blind; one command per frame while held). Mouse scale everywhere: 120 px = one circular-orbit speed at that spot. `-- --uitest` = headless gesture test. Not built: a moved planet does not take its moons along.

## 8. Round 5 (06/10) — what a body IS, by physics · Yang: "đúng vật lý thì như nào" -> "giao cho team đi"
Law of the round: NO type is chosen or stored. A type is read off numbers the body already has (mass, 6-element mix, temperature, age). Push a body somewhere else / change its mass -> it becomes something else by itself. Every threshold a `Consts` field marked [P] with the real-world value in the comment. Units: star mass 50 = 1 Sun; Earth = 1.5e-4; Jupiter = 317.8 Earths = 0.0477; StarMass 4 = 0.08 Sun.
Rules of §6 hold: own worktree, rebase on master 26a56f5+ first, headless only, never open a window, outside changes only via `World.Do`, clock rules stretch-exact (§7 note: an event inside a stretch is placed at its own moment), new state goes into `Hash()`, replay check, full `cli` exit 0 before reporting. Report on thread `cosmos` with numbers. Do NOT touch `game/` (Claire).

### Celine — stars live and die · `core/Stars.cs` + `cli/StarChecks.cs` · branch `celine/stars`
1. Per-star state: age, fuel burnt (or one of them derived). Reset in `Add`, merged sensibly in `Merge` (two stars -> mass sum, fuel mixed), hashed.
2. Main sequence from mass alone: luminosity (replace the single `LuminosityExponent` use in Rules.cs:86 with a function; Sun stays exactly 1 so Sol checks hold), surface temperature -> colour class (red dwarf .. blue giant), radius, lifetime ~ 10 Gyr * M^-2.5 (Sun 1e10 yr; 10 Suns ~2e7 yr; red dwarf > age of universe).
3. Below StarMass: brown dwarf band 13-80 Jupiters (0.62-4 units): glows faintly, cools with age, never a star. Decide + document whether it counts as `IsWorld`.
4. Death, by initial mass: fuel gone -> red giant (radius x100, luminosity up: inner planets scorched or swallowed through the normal collision path) -> < 8 Suns: sheds envelope (mass loss: orbits widen — do it through state, momentum conserved), white dwarf (Earth-size, cooling); 8-20 Suns: supernova (event; mass thrown off; `Impact`-like blow to biospheres within a range) -> neutron star; > 20: black hole (radius = Schwarzschild-like tiny, no light).
5. Rule `stars` in the rule table (rhythm your call; must survive `Jump` chunks of 5e6 yr: stretch-exact, phase changes placed at their own year, logged as events `star.giant`, `star.nova`, `star.remnant.*`).
6. Const `StarLifeScale` (1 = real years). Yang decides the dial; default 1.
7. Checks: Sun lifetime within 5% of 1e10 at scale 1; lifetimes ordered by mass; Sol at +1e6 yr unchanged vs master (Earth temperature equal to 1e-9); a 10-Sun star dies inside 3e7 yr and leaves a neutron star; cut 1/200 equality of phase years; replay hash.
Not in scope: binaries exchanging mass, novae, accretion discs.

### Ariel — worlds and small bodies name themselves · `core/Kinds.cs` + `cli/KindChecks.cs` · branch `ariel/kinds`
1. `World.ClassOf(i)` -> enum (pure function of M, Comp shares, Temp, Water, R, primary; no stored field): rocky / lava (Temp above rock melt) / ocean (liquid water + ice share) / ice ball / gas giant (gas share + mass) / ice giant / dwarf planet (pulls, round, below a clearing mass) / asteroid / comet (ice-rich, no pull) / ship. Keep `Kind` (Star/Planet/Moon/Rock) as is — it is about orbits, yours is about matter.
2. Snow line: `World.SnowLine(star)` = distance where Temp = WaterFreeze. Check against Sol (between Mars and Jupiter).
3. Comets act: rule `comets` — an ice-rich small body inside the snow line loses ice (mass) per year at a rate rising with temperature; `Comp`/`M`/radius updated; gone when ice is gone or mass ~0 (event `comet.spent`). Expose `TailStrength(i)` (0..1) for the window. Must cost ~nothing at 50000 rocks: touch only bodies inside the snow line, stretch-exact loss.
4. Atmosphere keeping (number only, no new state unless needed): `HoldsGas(i)` from escape speed vs thermal speed; a hot light world loses its `gas` share over time (rule or part of 3 — your call, say why).
5. Checks: each Sol body gets the class a textbook gives it (Mercury..Neptune, Moon); a Kuiper rock moved to 1 AU (`CmdKind.Move`) becomes a comet with tail > 0 and loses ice year by year, same result in 1 or 200 cuts; Earth moved to 0.2 AU reads lava; classes of Sol unchanged after +1e6 yr; replay hash; rule overhead at 5000 rocks under +10% (same criterion as RuleChecks).
Not in scope: Roche breakup (Claire), drawing.

### Selica — audit · `cli/Audit.cs` only · branch `selica/audit`
1. Finish the re-measure on master (rebase to 26a56f5): which of the 18 old defects still fail. One table: id · file:line · still fails? · smallest input.
2. New in scope: `core/Civ.cs` — ships. Hunt: ship whose goal slot is reused by another object; ship alive across `Jump`; `SetConst` ShipThrust/ShipSpeed/ShipMass to 0, negative, huge (ShipMass >= AttractMass makes a ship pull: what then?); two peoples, one goal; `Remove`/`Move`/`Force` on a ship then replay; capacity full at launch; `_civLaunches`/`Chronicle` vs replay; SteerShips with star == goal.
3. Policy: default run stays exit 0 + prints the count (as you chose). Good.
4. After Celine/Ariel report: cross-vendor review of their diffs before merge.

### Claire
`game/`: class names, star colours, comet tails, remnants; jump buttons past 1e6 yr; Roche breakup; fixes from Selica's table; merge.

## ROADMAP (Yang approved 2026-10-06)
| # | Phase | Contents | State |
|---|---|---|---|
| 1 | Physics base | gravity, contact, rails jump, temperature, water, life, star evolution, god tools | DONE |
| 2 | Extensible base | three tables: matter groups, civ stages + named stats, star events | round 7, in progress |
| 3 | Cosmic events | supernova, hypernova, kilonova, gamma ray burst; ejecta = real matter; fictional elements born there | waits Yang: element list |
| 4 | Civ on one planet | stats, eras, uses all six groups, ethics 4 axes + Gestalt, abstract history + wars, awareness | design in progress (NEXT.md) |
| 5 | Many star systems + FTL | several systems in one world, FTL, empires meet, first contact | not started |
| 6 | Empire relations | opinion, wars, alliances, revolt, culture/religion spread, god tools on relations, reactions to god | not started |
| — | ALPHA | = end of phase 6: every system runs end to end on placeholder visuals | |
| 7 | Visual | whole VISUAL backlog (NEXT.md), textures, effects, sound | after Alpha only |

3 and 4 may run in parallel (different owners); 5 needs both.

### After Alpha (Yang 2026-10-06)
Audience: Yang plays it himself; if it turns out well -> itch.io. Mods: design for free modding throughout, but NOT opened to outsiders until after 1.0. English: required if shared, deferred until then (UI stays Vietnamese).
| # | Phase | Contents | Done when |
|---|---|---|---|
| 7 | Visual | textures, shaders, event effects, lighting, sound, music | no placeholder left on screen |
| 8 | UI / experience | control panels redone, readable journal + chronicle, narrator summarising history, shortcuts, settings. Onboarding kept light (player = Yang) | Yang plays without asking what a control does |
| 9 | Save + data | save, load, rewind to a save point; stock start scenes; data files so rules/numbers/tables change without a rebuild | a world saved today opens in a later build |
| — | BETA | = end of phase 9: feature complete, no new systems after this | |
| 10 | Balance + content | tune numbers so histories come out interesting; fill the tables (elements, events, eras, ethics); long playtests | many random worlds in a row without stalls or sameness |
| 11 | Performance + stability | big worlds, many systems; no crash over hours | target frame rate at the world size Yang picks |
| 12 | Packaging | release build Yang can install and update on his own machine | clean install runs |
| — | 1.0 | = end of phase 12 | |
| later | Sharing | itch.io page, English, mod support opened to outsiders (docs, stable data format) | only if Yang decides to share |

## 9. Species + capability graph (Yang "làm đi" 2026-10-08) [Y]=Yang [C]=Claire default; Yang CONFIRMED 08/10 the 3 [C] defaults: lifespan real effect, escape-velocity space cost, generic era names
- [Y] Not every species has one stage ladder. Human 10-era table = default path for land/tool-using body only. Human = first template + default; every other species = free composition from parameters (no fixed species list). Params = set at birth (god may override) AND drawn from home planet (both).
- Species record (table-driven, one row per param, adding a param = adding a row): Habitat {land, ocean, atmosphere, ice, subsurface}; Manipulation 0..1; EnergyBasis {photo, chem, thermal}; Senses {vision, sonar, electro, chemical}; Lifespan yr; Social {solitary, band, hive, gestalt}; TempMin/TempMax K. Human = land, 1.0, chem, vision, 80, band, 250-320.
- [C] Birth draw weighted by planet: ocean -> habitat ocean, dim star -> thermal, high g -> small body. Seeded rng, hashed. Species fixed after birth. Habitat mismatch later (moved world) = fit penalty, survives only via dome (existing CivDome path).
- Capability graph replaces ladder. Node = {id, domain, Needs (element shares, habitat flags, min Manipulation, prereq nodes), tech threshold, effect (WattsPerCapita, MaxPop, Kardashev term)}. Tech stays one accumulated scalar; node opens when Tech >= threshold AND Needs met. Era LABEL = derived from Kardashev + opened set (bottom of the pipe, never read by rules).
- Start nodes: heat {fire (needs O2 gas + carbon fuel + not underwater), geothermal (needs vent heat), solar furnace}; material {stone work (rock), smelting (needs a heat node + metal), alloys}; knowledge {memory, writing, printing, computing}; power {electricity, fission (radio), fusion (gas)}; space {orbit, interplanetary}.
- [C] Spaceflight node cost from physics: delta-v = escape velocity of home planet from its mass/radius (gas giant habitat = huge cost). No special-case rule.
- [C] Lifespan has REAL effect on knowledge accumulation rate (not description only). Define via Const, real-world fork, no asking.
- [C] Era labels of non-human species: generic table names ("era 1.."), Yang names later.
- Human path must equal today's 10-era table BIT-EQUAL (hash unchanged for human civs). Existing Needs (StageNeed) = the seed of node Needs.
- Slices, all Ariel, own branch each, no merge: S1 `ariel/species` Species record + human template, hash unchanged. S2 `ariel/capnodes` node table + availability filter, human path bit-equal. S3 `ariel/species-samples` 3 sample species (ocean, atmosphere, ice) each with a check taking a different path + escape-velocity check. S4 `ariel/species-birth` birth draw from planet. S5 `ariel/species-ui` window panel + god override command (`CmdKind` new, via World.Do).
- Not decided by Yang: full param list beyond above, node list beyond start nodes, planet/species coupling strength. (Era names for other species: generic, Yang names later.)

## 10. Cosmic events round 2 — Celine, Yang picked ALL 3 on 2026-10-08 (order = Claire's)
Same rules as C5: own branch, step-0 checkpoint on bridge BEFORE code (Claire ACKs with amendments), real physics (no asking Yang), every number a Const field with source, matter/momentum through the Escaped ledger, hash covers new state, replay check, full cli exit 0, report file + hashes, NO merge.
- C6 `celine/roche-rings` — Roche breakup -> rings. Body inside Roche limit of a heavier primary breaks up: d_R = k R_prim (rho_prim/rho_body)^(1/3), k 2.44 fluid (gas/ice dominated) .. 1.26 rigid (rock/metal dominated); Const k per composition, interpolate by mix. Debris = N rocks (cap by rock budget, `Rocks` already thousands) on near-circular orbits around the primary, mass + momentum conserved, matter split by mix. Parent slot freed. Must survive Rails jump (rocks keep primary) and ring spreading stays plausible (no explosion of energy). Also: tidal-capture path when a pass is hyperbolic. Checks: conservation, ring mass = parent, ring inside Roche, rigid vs fluid differ, replay, jump 1e6 yr stable.
- C7 `celine/superflare` — star flares. Rate + energy from star mass/age/phase (M dwarfs frequent+young; Kepler superflares 1e33-1e36 erg, solar-type ~1 per 1e2-1e3 yr at 1e34); power law energy distribution. Effect on a world: fluence 1/d^2 (same dose model as GRB), cuts life/pop by dose (`Impact`-like), strips atmosphere gas share of close worlds around active stars through the Escaped ledger (EscapeRole gas). Stretch-exact in Jump: do NOT roll per-yr per-star; sample count per chunk by Poisson (seeded), place each event at own year. Checks: M dwarf flares more than Sun, energy distribution slope, atmosphere loss conserved in ledger, Earth at Sun not wiped by a 1e34 flare, replay.
- C8 `celine/snia` — Type Ia. Degenerate path only: two white dwarfs merge (existing Merge path) with total mass >= Chandrasekhar (~1.4 Sun, Const) -> thermonuclear total disruption, NO remnant, ejecta ~total mass as Fe-peak (mostly metal share through element table + some rock), energy ~1e51 erg, reuse DistributeEjecta + ledger + NucleosynthesisDelta. Single-degenerate (WD accreting from companion, needs Roche overflow + mass transfer) = NOT now, note as debt. Merger below Chandrasekhar = no Ia (stays heavier WD). Checks: 0.7+0.8 Sun Ia, 0.5+0.6 not, ledger closes, nothing left in slot, replay.
- Order: C6, C7, C8, each reported + Claire diff review before the next. Rebase on master first (head has C5 + A3/species work landing; ask on thread if a file you need is moving).

### §9 amendment (Claire 2026-10-08): human baseline after A3
A3 changes consumption/duration physics + Consts on purpose => Human path is NOT bit-equal to master 461ce87 from A3 on. New rule: Human baseline = A3 5275f03 family. S1/S2/S3.. must be bit-equal to the A3 baseline (Celine table seed1234/Sol0: initial 66B1883E599E57EE, +1e6yr D55A7885CD8D68E2, push20 E42AD90E459EFA3A; S1 matches). Defaults of new Consts are elided from Hash only when ALL fields of the block are default; any override hashes the block. Dead fields (e.g. CivMetalUse removed by A3) must NOT be resurrected.

## §11 Collision breakup (DRAFT, Yang 2026-10-08, NOT scheduled; after Yang's playtest)
Status: notes only. No code until Yang says go. Likely owner: Celine (reuses C6 Roche.cs fragment/reservoir machinery).
Today: `World.Merge` = perfect merge (heavier keeps identity; M, p, Comp summed; no ejecta, no fragments). Only Roche tidal breakup (C6) makes debris.
Decisions (Yang):
- Debris = ordinary objects with Comp[] over element groups (meteors/asteroids/fragments are just small objects; no class "asteroid"). Planet debris scope ONLY; star ejecta (C5), belts/rings, gas stay as they are.
- Break rule: collision specific energy Q = KE_rel / M_target vs threshold Q*_D(M) [P: Benz&Asphaug 1999 style]; below threshold = merge (or crater, later); above = breakup. Both Q* and the largest-remnant fraction f_LR = Consts FIELDS (hashed), tunable.
- Fragment count N = f(mass), one continuous law capped by budget (no planet/asteroid branches): small body -> 2..4 pieces, planet -> tens..hundreds up to cap. Pieces conserve M, p, L(spin proxy), Comp exactly. Excess below resolution = bound reservoir (C6), never silent loss; mass that truly leaves system -> Escaped ledger.
- Life/pop on a shattered world: ends (Impact/EndSwallowedWorld style); fragments are inert (no biosphere). Colonies later by domes (existing rule).
- Display: "draw more than simulated". Dust/cloud sprites derived from real fragments + reservoir density, RENDER-ONLY: own RNG (never sim RNG), no mass, no collisions, not in Hash. Ordering: visuals LAST (after Alpha), per NEXT.md. Old CosmosSandbox dust layer is NOT revived; only its look idea (gas = cloud, rocks = real small bodies).
- LOD idea (open): fine detail only where observed (layer frames); needs deterministic, conserving split-on-zoom. Hard; not promised.
Open Qs for Yang: threshold for what counts as a body that can break (AttractMass?), crater vs merge below Q*, do pieces inherit spin.

## §12 Cloud collapse / star+planet formation (DRAFT, Yang 2026-10-08, NOT scheduled; backlog)
Yang wish: drop a cloud of matter, it gathers by itself into star(s)/planets.
Code today (read, not run): objects with M >= Consts.AttractMass (1e-7 ~ 0.0007 Earth) pull each other; contact -> Merge (heavier keeps identity; M, p, Comp summed). So a cloud of >=AttractMass bodies already accretes N-body style (billiard-ball accretion). Kind by mass (Consts.StarMass); NOT verified: a merged-up body ignites / starts star life cycle correctly.
Gaps (all real physics, none modeled):
1. Sub-AttractMass particles do not pull each other and do not merge among themselves; dust-only cloud never collapses.
2. No gas: no pressure, no thermal support, no Jeans criterion M_J (collapse only if cloud mass > M_J(T, rho)).
3. No accretion disc (SPEC §6 non-goal): angular momentum is not redistributed, so no flat protoplanetary disc; spin of merged bodies is not tracked at all.
Candidate slices (order): (a) check/test: ignition + life cycle for a body merged past StarMass, with composition from Comp; (b) god tool "create cloud" (N bodies, mass dist, velocity dispersion + net rotation) via Command; (c) gas parcels as macro-particles with Jeans-gated collapse -> real star birth; (d) disc: angular momentum bookkeeping (spin L per object), merge conserves L (C6 already conserves L in breakup) -> discs/orbits emerge from rotation; (e) planetesimal growth threshold (gravitational focusing) so cloud of small bodies can bootstrap.
Constraints: Hash discipline (new Consts are FIELDS), replay via Command, mass/p/L/Comp conserved, budget on N objects (same as C6 budget/reservoir idea, compress below resolution), render-only visuals last.
Open Qs for Yang: realism target (billiard accretion enough vs real gas collapse)? does the cloud tool live in god window (needs UI, after Alpha)? star birth visible live or only after time jump?

§12 DECISIONS (Yang 2026-10-08): realism = REAL gas collapse (Jeans, pressure), billiard accretion NOT enough. Cloud tool = after Alpha OK. Star birth = watch LIVE (not only after time jump) -> needs cost budget so collapse runs at playable speed; plan slice (c)+(d) accordingly, rails/jump must also handle cloud state.

## §13 Perf invariant for god powers (Yang 2026-10-08)
Rule: NO god action may collapse fps. Yang: "nobody wants to drop a black hole in a system and watch fps die". Any command (create NS/BH, SetConst G x100, huge mass, mass object spawn, star inside planet) must have BOUNDED per-step cost: work caps + deferral (time-slice across steps, physics may lag/simplify, fps must not).
Origin: C6 Roche passed stock-scene gates (typical collect <=0.25ms) but NS drop = 26.2x slower (paired median, Celine, a1c832f): fragments re-shred cascade + broad-phase scan. Gate tested typical scenes only = process gap.
Todo: (1) "god torture" cli suite: NS drop, BH drop, G x100, mass 1e6, create 10k rocks, star inside planet, many NS; per-Advance ms bound as PAIRED MEDIAN, Release, vs own baseline; run on every merge touching core. (2) per-step hard budget (ms or work units) in Roche/gravity entry, excess deferred. (3) scale curve 1k/5k/20k objects. (4) design check: every new Command declares worst-case cost.
Owner: Celine (core, after roche-no-reshred), Selica QA runs suite. Related: Roche broad-phase debt (see C6).
Amend §13 (Yang 2026-10-08): "what the player wants in this game is DESTRUCTION". So destructive god actions (BH/NS drop, collisions, breakups, G abuse) are the MAIN PLAY LOOP, not edge cases. Perf budget + torture suite must treat them as primary scenes; destruction must also look good (fragments/debris/rings readable) and cost bounded. Design note only; concept details = Yang.
Amend §13 (Yang 2026-10-08): universe is NOT static; with no god input stars die over time (red giant, white dwarf, supernova -> NS, BH). So NS/BH inside a system also arise NATURALLY, incl. inside long FastForward (1e9..1e10 yr). Perf invariant + torture suite cover natural events and long jumps, not only god actions. World must tell stories without god (DF principle).

## §14 Scale architecture — 100-200 systems, heavy destruction, smooth (Yang 2026-10-08: "anh không cần biết, em làm thế nào để có"; design = Claire's, no asking him)
TARGET: 200 star systems in ONE World, destruction in several at once, window never stalls. Sim budget <=8 ms/Advance Release on Yang's machine; window frame p95 <=20 ms whatever the sim does. Rocks per system = `Consts` field (start 500 -> 100k rocks; 5000 -> 1M is stretch, needs S4).
WHY NOW FAILS: (1) Advance inside `_Process` (game/Main.cs:383) -> slow step freezes window. (2) no cost model: worst-case bounds from SURFACE gravity (Roche.cs:149, World.cs:346) + per-event slicing/rescans, unbounded. (3) gravity flat rocks x ALL pullers x 8: 200 systems = 2000 pullers x 100k rocks x 8 = 1.6e9 pair evals/Advance.
INVARIANTS (keep): one World, one slot array, no stored per-system partition (Yang); command boundary; same binary + same journal = same run. DROPPED: "an optimisation may not move hashes". Hashes move, recapture, say why.
DETERMINISM RULE: tier/budget decisions read sim state ONLY. Never camera, never wall-clock ms. Core budgets are WORK UNITS (counts). ms budgets live in game/ only (how many Advances per frame).

### Mechanism (written out; intricate)
A. SYSTEM = derived, not stored. Root = pulling object that no heavier object holds (existing `Par`/`Hill` chain). A system = a root + everything whose Par chain ends at it. Recomputed on topology change only (create/remove/merge/capture/escape), not per step. An object leaving one root's hold and entering another's is re-parented by the existing Hill logic: nothing is fenced.
B. LOCAL GRAVITY. Each system keeps a puller list = its own pullers + INTRUDERS. Intruder = any pulling object from outside whose distance to the root is inside the system's influence radius R_inf (= k x largest apoapsis among its members; k Const). A pulling object feels: its own list exactly (1/r^2, as now) + every OTHER system as one point mass at its barycentre (200 monopoles). A rock feels its own system's list only. Two roots inside each other's R_inf -> lists unioned (stellar encounter costs more only there). Dropped NS = intruder of the system it lands in -> pulls everything there exactly, costs nothing elsewhere.
C. ROCK TIERS (pullers are ALWAYS integrated; they are few: ~10/system). A rock is either
   - RAIL: rides closed two-body orbit round its primary (Rails.cs math; position = primary position + Kepler offset). Stores elements + epoch. Costs ZERO per Advance. Position materialised lazily: only when something reads it (draw buffer, wake, a rule at its rhythm; star-light rules use orbit-average `RailStar`).
   - FREE: integrated every substep as now.
   FREE -> RAIL: bound to one primary, no wake condition true for T years (Const).
   RAIL -> FREE (wake), all from sim state: (1) an intruder enters the system's R_inf: wake that system's rocks whose radial band [peri, apo] meets the intruder's perturbation zone (tidal accel of intruder > eps x primary's pull at the rock; eps Const). (2) primary changes abruptly (supernova, mass loss, removal, merge) -> wake its rocks; slow mass change -> re-derive elements, stay RAIL. (3) radial band overlaps a sibling puller's band [a - k*Hill, a + k*Hill]: such rocks get a scheduled check at next conjunction (time wheel keyed by Year), woken only if the encounter is real; bands that never overlap = never checked. (4) a Command touches the rock. (5) a Roche/contact candidate names it.
   Loss, accepted (same as FastForward §4): sibling perturbation on RAIL rocks outside (3).
D. BOUNDED DESTRUCTION. `HotCap` (Const) = max FREE rocks integrated per Advance, `EventCap` = max breakups/merges per Advance. Over cap: lowest-priority debris (smallest mass, then highest slot) is folded into the existing BOUND RESERVOIR / ring aggregate of its host (mass, composition, momentum into the ledger; nothing vanishes); excess events queue to next Advance. All bounds from quantities at CURRENT distance or two-body periapsis; surface-gravity bounds are banned.
E. LOOK of destruction != sim objects. Core emits event records (where, mass, velocity spread, composition); game/ spawns GPU particles from them. Few real fragments + reservoir carry the physics; thousands of particles carry the picture.
F. SHELL. Sim on a worker task, window never waits (Ariel msg 551973b6). Draw: cull per system; far system = one glow sprite; RAIL rocks drawn from elements (CPU parallel first; Kepler in vertex shader only if measured need).

### Phases — each ends with numbers; next phase starts only on a pass
- S0 MEASURE (Selica, cli only, no core edit): `cli -- scale`: build K systems far apart through Commands (K=1,10,50,200; rocksPer=500,5000), report ms/Advance, memory, per-rule cost, capacity blockers, on master as-is. This is the baseline every later phase is paid against.
- R (Celine, running): Roche no-reshred + swept entry + caps (msgs a78135ee, f60794d2).
- F (Ariel, running): sim off the window thread.
- S1 (Celine after R): A + B. Gate: K=200 x 500 rocks, all quiet, <=8 ms.
- S2 (Celine): C. Gate: K=200 quiet <=1 ms; NS dropped into 1 of 200 systems <=8 ms; wake/sleep replay-deterministic.
- S3 (Celine + Selica): D + god-torture suite (§13) run at K=200: NS/BH drops in 10 systems at once, G x100, 10k rocks spawned. Gate <=8 ms, ledger closure.
- S4 (Ariel): E + draw LOD. Gate: window p95 <=20 ms at K=200 with 10 systems breaking up.
- STOP RULE: a phase that misses its gate twice -> Claire reports the measured ceiling (systems x rocks x simultaneous destruction that DOES fit) to Yang instead of patching on.
Prototype-then-clean-rebuild decree applies per phase.
G. JUMPS UNDER §14 (added 2026-10-08, Yang asked "nhảy 100/1000/1e6 năm thì có hết không"). Today a jump puts EVERYTHING on rails: no collision, no Roche, no sibling pull; an unbound intruder flies a straight line through a system and wrecks nothing. Under §14 a jump is the same tier logic over a long span: systems with no wake condition ride rails for the whole jump (cost independent of length, as now); a system that is HOT at jump start, or that a wake condition (C1-C5, evaluated at chunk edges + predicted intruder entry time into R_inf) fires in, is integrated for real through its hot interval, under HotCap/EventCap, then put back on rails when quiet. Long hot intervals are bounded by a work budget per jump: over budget -> the jump STOPS EARLY at that year and says why (event record), never silently skips the destruction. Phase: after S2, with S3. Gate: NS dropped into a system then 1000-yr jump -> breakups/merges happen and are dated; 1e6-yr jump over 200 quiet systems costs no more than per-chunk rules.
H. PERF MANDATE (Yang 2026-10-08, verbatim sense: "mục tiêu tối quan trọng hiện tại là tối ưu hiệu năng hết mức có thể; được phép áp dụng tất cả phương pháp tối ưu mà ngành từng nghĩ ra; không cần sợ thừa"). Perf = THE priority; over-engineering for speed is ALLOWED on this project (overrides YAGNI/ponytail for perf work). Hard gate 8 ms/Advance, stretch 2 ms (quiet AND during destruction). Still required: same binary + same journal = same run; ledger closure; every technique lands with 7-run paired before/after, kept if it pays, numbers in PERF-LOG.md (append-only).
   Menu, by expected payoff — all approved, order = owner's call after measuring:
   1 algorithmic: B local gravity, C rail tier, D caps (above). 2 per-object block timesteps (power-of-two substeps by dynamical time; far/slow rocks 1 kick, close/fast 8+). 3 SIMD kick kernel (Vector256/AVX2+FMA over SoA; float accumulation on relative coords allowed if error measured). 4 layout: hot/cold split (X Y Vx Vy R dense; Comp/layers cold), FREE rocks reached through a dense index list (slots stay stable), per-system contiguous ranges. 5 threads: persistent worker pool instead of Parallel.For per substep (old note: ~0.76 ms overhead), chunk by system, no false sharing. 6 broad phase: uniform grid / spatial hash for contact + Roche; quadtree/Barnes-Hut once pullers per list is large. 7 zero allocation per Advance (Roche.cs:133 `new List`, lambdas, closures -> pooled/struct), Span/unsafe to kill bounds checks, AggressiveInlining, SkipLocalsInit. 8 runtime: TieredPGO, ReadyToRun or NativeAOT for core, GC SustainedLowLatency, measure Server/Concurrent GC. 9 rules: index lists per kind (planet-only rule never scans N), dirty flags, rhythms staggered across steps, amortised rebuilds. 10 draw (game/): direct MultiMesh buffer writes, per-system cull, LOD, cached orbit arcs, GPU particles, Kepler in vertex shader for RAIL rocks. 11 GPU compute for FREE rock kicks = LAST resort (breaks engine-free core + cross-GPU repeatability; needs Claire OK with numbers). 12 time-slice any O(N) rebuild across Advances.
   Infrastructure FIRST: always-on per-phase tick counters in core (gravity/contact/roche/rules/merge), `cli -- prof`, shown in HUD; perf regression gate on every core merge.
   Seam: Celine's S1 step-0 defines the kick KERNEL boundary (inputs: puller list arrays + dense rock index range; no topology inside) in its own file so a second owner can optimise it (2,3,4,5,7) without colliding with S1/S2 edits.
GATES (Yang OK 2026-10-08, supersede every "<=8 ms" above in §14): HARD quiet <=1 ms/Advance at K=200 · HARD during destruction <=4 ms/Advance (HotCap sized by measurement to fit; expected ~8-9k real FREE rocks at today's kernel speed, more after kernel work) · STRETCH destruction <=2 ms. Window frame p95 <=20 ms unchanged. Reason: 60 fps frame = 16.7 ms -> 1 ms = x16 warp quiet, 4 ms = x4 warp while things break; 4 ms keeps at least today's whole-system fragment count as real objects.
