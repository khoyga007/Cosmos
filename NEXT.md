# Cosmos — state

READ `SPEC.md` FIRST (game, model, engine decisions, round-1 work packages). This file = what is running now.

## 06/10 night
- Old web project E:\CosmosSandbox = ARCHIVED (reference only, nothing deleted; note at top of its ROADMAP.md).
- master: object model core + command boundary (`World.Do`, journal, replay) + Sol scene + window (top-down default). Checks green: merge, moon (472 turns), sol (1.36% drift), constants (G 1→2), commands (replay hash equal), repeat.
- Yang has NOT yet seen the object-model window (`run.bat`). `_Draw` + mouse paths never ran headless → first thing to ask him.
- Round 1 handed out on bridge thread `cosmos` (SPEC §6): P1 Celine `E:\Cosmos-celine` celine/rules · P2 Ariel `E:\Cosmos-ariel` ariel/godtools · P3 Selica `E:\Cosmos-selica` selica/audit. Agents wake only when Yang prompts them.
- Claire next: review + merge reports; fast-forward on rails (SPEC §4 OPEN); collect Yang's content for rules 2-4 (life/civ parameters, thresholds).

## 06/10 session 3 — START HERE
- master = see `git log`. Round 2 MERGED: Celine `celine/jumpcost` (rocks solved once per jump, 1e6 yr @50000 rocks ~4.4 s -> ~0.16 s) + Ariel `ariel/godtools` (window: year, layer panel, life/civ marks, Vietnamese event log, jump buttons, SeedLife slider, rule switches). Re-run by Claire: full `cli` 49 OK exit 0; `--selftest` PASS; `--uitest` PASS; `--bench=3` ~97 fps @5010.
- Celine's 7 review defects (CELINE-REVIEW.md) FIXED by Claire, each has a check in `cli/LayerChecks.cs`: threshold crossings / life + civ start placed at their own moment inside a stretch; tech = exact population*years; `Touched[i]` = year of last outside change (seed, impact) caps a rule's dt; CivMetalRef 0 = metal not needed; r0 = 0 in Kepler -> straight line; Hash now covers velocity, Comp, Par, rng, free slots, all Consts (ALL old hash values changed); impact / low seed clears RichYears at once.
- State of a layer = as of that rule's last run (life every 1000 yr): a seed 2 yr old may show 1 yr of growth. By design.
- Selica P3 audit: accepted, no commit yet (branch `selica/audit`). Needs Yang's prompt.
- 06/10 Yang ran the window (round 2 UI by Ariel): verdict "UI khó chịu, tạo vật thể chả ra đâu vào đâu, không tương tác được gì ra hồn". Causes found in code: selecting snapped the camera, no pan, zoom at centre only, right-click create fell into the Sun unless something was selected (then orbited THAT), rocks stole clicks, buttons kept keyboard focus (Space re-pressed them), fixed-pixel panel, vi-VN number parsing ("1.5" = 15), push scale tied to zoom.
- Round 3 = Claire rewrote `game/` interaction (Main.cs head, GodUi.cs, GodTools helpers): free camera (pan, zoom at pointer, follow only on double click / F), create tool (C) with ghost + auto primary (`GodTools.PrimaryAt`) + click = circular orbit / drag = launch with predicted path, push scaled to local orbit speed, time bar always visible, anchored side panel, toasts, no focus on buttons, invariant culture. `-- --uitest` = headless gesture test feeding real input events (15 OK). STILL not seen by anyone: wait for Yang's verdict before more UI work.
- NOBODY has seen the window (agents headless only). Yang to run `E:/Cosmos/run.bat`; his eyes judge the look.
- Claire next, AFTER Yang has looked: "lộ diện" slice (civ reacts to the god), snapshot/rewind, civ uses metal. SPEC §4 OPEN list still open.
- Yang's standing answers 06/10: layer content = Claire decides; Web-chat ideas allowed if good; old web folders = keep.

## 06/10 session 4 — civilisation acts
- SPEC §2c built by Claire: names + chronicle, metal use, domes, ships as objects, colonies. Checks: `cli` 56 OK; `--selftest` PASS; `--uitest` 22 OK (click takes a ship, Delete removes it, Earth panel shows people + chronicle); bench ~113 fps @5010.
- Seen in numbers, NOT by eye: Sol +1e6 yr -> people on Earth + domes on Mercury/Venus/Mars/Moon; stepping: ship Earth->Mars 0.1-0.7 yr, constant supply traffic (1 launch / 2 yr / world).
- Changed behaviour: Earth thrown out no longer ends a space-age people (domes 5%); farming/industry people still die.
- Yang to judge: ship speed/traffic, dome size, panel length (chronicle up to 9 lines).
- Selica P3 report on bridge (18 defect on OLD base fab4bc2, re-measuring on master): setconst-density-0, setconst-yeartime-0, swept-phase... wait for her rebased run before fixing.

## Lessons (process)
- Yang explaining an idea over several messages = listen only; build on his explicit go. (06/10: core rewritten mid-explanation, Yang objected; commit f0817b1 kept because it matched.)
- Smallest playable slice before infrastructure.

## Traps
- `dotnet new sln` on SDK 10 writes `.slnx`; Godot needs `game/Cosmos.Game.sln` (`--format sln`).
- `\n` typed inside a Bash tool command reaches python/sed as a real newline → C# strings with escapes go through Write/Edit.
- cli full run ≈ 1.5-3 min: default 2 min tool timeout is too tight. `cli -- rails` / `cli -- layers` = those checks alone (~20 s).
- `in Command c` cannot be captured by a lambda (CS1628): copy the field to a local first.
- A check that reads a rule's output right after `SetConst` must run past that rule's rhythm (water = 1 yr ≈ 536 steps).
- Reflection on private fields (`_prim`, `_hill`) from a throwaway cli file = fast way to see what a jump decided; delete the file after.
- Long markdown with quotes/backticks inside a bash heredoc fails to parse → Write a .py to the scratchpad, run it.

## Yang decisions 2026-10-06 (late)
- Star lifetimes: REAL, billions of years. `StarLifeScale` stays 1. No compression.
- Move: toggle. `Command.Index == 1` = alone; default carries what target holds. Window checkbox "kéo theo những gì nó đang giữ" (fb58b89).
- First contact (two peoples meet): Yang will design it "tý nữa". WAIT, his concept. No build.
- Cosmic events Yang listed: supernova, hypernova, kilonova, gamma ray burst, "..." (list open). Core has only `star.giant`, `star.nova` today. WAIT for his go + scope.
- Code-drawn visuals = PLACEHOLDER; textures later. Look lives in `Main.BodyCol` + `Main.DrawBody` only (199464e).
- OPEN defect (Celine, bridge 741111ba): red giant R=826 swallows whole system to Neptune (Sun R inflated ~40x vs orbit scale); bodies swallowed in jump leave no events. Repro `cli -- jump-bench 1e10 0 v`.

## Civ design — Yang dictating 2026-10-06, IN PROGRESS, NO CODE until he says go
- Stats per civ: interaction with its world, world population, energy-harnessing level, tech level, development era (prehistoric, stone, bronze, iron, ... Renaissance, industrial, pre-space age, ...; list open).
- Awareness = how much a civ senses the god. Drives different reactions. Depends on ethic + tech base: faith/spiritual civs high, pure physical-tech civs low.
- Reference: Stellaris. Take ALL 4 ethic axes (Materialist-Spiritualist, Militarist-Pacifist, Xenophile-Xenophobe, Authoritarian-Egalitarian) + Gestalt.
- Levels: normal + fanatic, 3-point budget, as Stellaris. YES.
- Ethic at birth: random + shaped by circumstances (both).
- Ethic drifts over time; each ethic type has its own stubbornness (resistance to drift).
- Gestalt awareness: treats the god as part of determinism (a given of the universe; not worship, not denial).
- Reactions per awareness level: LATER (Yang).
- Undecided by Yang: civics, which circumstances push which ethic, stubbornness values, what each reaction is, first contact content, "lộ diện".
- Core today: Pop = 0..1 fill, one Tech number, 4 stages, all civs identical except name; only interaction = industry eats metal.
- Empire-to-empire interaction: take ALL of WorldBox's kingdom layer, combined with the Stellaris ethics: self-running (god never commands), pairwise opinion with named reasons, wars of several kinds with winners/losers and territory changing hands, alliances, internal revolt/secession, succession, ruler traits, culture/religion/language spreading independent of borders, god tools on relations (force friendship/spite, madness, bless, curse), world chronicle.
- Unit, Yang answered: a pre-FTL civ stays within ONE planet; it has an Earth-like history incl. wars before FTL (many nations on one planet), but modelled ABSTRACTLY first (no nation objects / map; numbers + chronicle events).
- Yang: KEEP in-system colonisation by slow ships for pre-FTL civs. FTL = the step that reaches OTHER star systems. Implies core needs several star systems + FTL travel (not built; one system only today).
- Resources (Yang): civs must plausibly use ALL six element groups + water/biosphere/starlight (table given to Yang in chat: rock tools/building; carbon fuel pre-electric + greenhouse; metal from bronze on; ice = water, in space O2 + rocket fuel; radio = fission; gas = fusion, late). Six groups coarse = OK for the abstract version.
- Yang: FTL cannot be explained by the six -> ADD fictional element(s). Name/number/source undecided. NElem is a const 6 with literal 6-long mixes everywhere (scenes, checks, UI): adding one = wide mechanical edit.
- Fictional elements (Yang): SEVERAL, each with its own role later; they are BORN IN COSMIC EVENTS (supernova, hypernova, kilonova, gamma ray burst, ... — the events the team builds next). Names, count, which event makes which, roles: undecided. Design consequence: element count must become easy to extend (not a hard 6), and cosmic events must leave real matter behind (ejecta/remnant composition).
