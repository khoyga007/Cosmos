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

## Round 7 — foundation only (sent bridge da35f5cc, base 236b4db)
- A Celine `celine/elements`: element table replaces `NElem = 6`. B Ariel `ariel/civtable`: stage table + named civ stats slot. C Celine: star event table + single `Eject()`. Selica: hard-code sweep, "edits outside table = 0" audits, review.
- Old hashes must stay bit-equal. Claire merges A -> B -> C, re-runs herself. Claire writes no code this round (Yang: save quota).

## VISUAL backlog — LAST, only after the game reaches Alpha (Yang 2026-10-06). Do NOT start earlier; code-drawn placeholders stay until then.
Wiring: core event (place, year, size) -> window plays the effect; event/element/stage tables carry a visual id.
- Data already in core, not drawn / crude: temperature (glow hot, frost cold); water state (ocean, ice caps, vapour clouds); biosphere 0..1 (greening/withering); population + stage (night-side city lights); six-group composition (surface types, gas bands); star phase/class (boiling surface, corona, pulsing giant, remnants); star light on planets (day/night side); habitable zone + snow line bands moving as the star ages; Hill zone; ships (engine trail, path); domes; object classes from Ariel's kinds (comet tail away from star, longer near it); planet rings (gaps, shadow).
- Happens in core with no picture: impact/merge (flash, debris, hot scar); star -> giant / nova / remnant; planet swallowed by star; biosphere death; civ rise/fall, colony, first ship; god's hand force.
- Cosmic events (when built): supernova/hypernova (flash, shock ring, GPU-particle debris), kilonova (inspiral, flash, lensing ripple shader), gamma ray burst (two narrow jets sweeping the system), black hole (lensing shader, accretion disc); glow/bloom, shake, sound.
- Not in core yet, so not drawable: Roche breakup, ejecta clouds as real matter, everything from the civ design (wars, borders, ethics).
- OPEN for Yang: events inside a time jump — log only / stop the jump at big events / replay after. Lasting traces (nebula) want ejecta as real objects.
- Shaders + particles = keepable; surfaces/nebulae need real textures.
- Seen by Yang in play 2026-10-06, fix inside phase 4 (not now): (1) domes: any solid world, no temperature/resource/supply condition, live forever (Mercury 386 K colony at 5%); (2) long jump collapses civ.start + space age + first ship into one year (rules sampled at sparse chunks); (3) Tech grows unbounded (2081.6) over 4 stages.
- Scene debt: SolSystem has net momentum (Sun at rest, planets all prograde) -> system drifts ~0.018 units/yr; window now rides along in jumps (f0a2f8c). Zero the momentum in the stock scene after round 7.
- Yang asked 2026-10-06 whether ships use orbital transfers: they do NOT (engine steers straight at the goal, brakes near it, sidesteps stars). Option for phase 4 if Yang wants realism: pre-FTL ships on transfer orbits + launch windows + gravity assists (rails orbit maths already in core). Undecided.
- DEFECT seen by Yang in play 2026-10-06 (not fixed, he is only testing): ships get stuck at the host star when the goal's orbit carries it across the ship's line of flight. Cause (Claire's reading of SteerShips): aims at where the goal IS, straight line through the star; avoidance only holds sideways inside 1.5 R until the goal comes round, then re-aims and is pulled back. Fast inner goals (Mercury, Venus) = long loop, ship lost at ShipLifeYears. Fix direction: lead the target (aim where it will be) + route round the star on one side; transfer orbits would remove it entirely.
- Seen by Yang in play 2026-10-06 (black hole test: 31-Sun star dropped into Sol, jumped to supernova; not fixed, UNVERIFIED causes): (1) Sun + planets gone from view afterwards, panel still counts 10 attractors -> no way to find a lost object (no object list / jump-to-object in the window); likely flung out when the star lost ~3/4 of its mass at once; (2) temperature shows 0.0 K, no cosmic-background floor (~2.7 K); (3) worlds go "Thiêu đốt" -> "Đóng băng" in one step across the nova.
- Zone overlay (key Z, 0892189+): heaviest object's ring is DISPLAY ONLY (Sun's real tidal radius vs the galaxy, 230 000 AU * (M/Sun)^(1/3), through SolDist); core still gives the root an infinite zone (Rails.cs `_hill[root] = MaxValue`). Phase 5 (many systems) must give the root a real limit in core, then the window reads Hill() only.
- Seen by Yang in play 2026-10-06 (screenshot yr 9.10e9, 250 objects / 4 attractors, 5005 collisions; not fixed, causes UNVERIFIED): (1) star logged as 271 Suns swallowed Mercury, Mars, Jupiter, Saturn, Uranus, Neptune all in the same year, exploded 2 yr later -> black hole; Earth + Venus NOT swallowed (only froze) though inner — swallow order looks wrong or jump chunking stamps one year; (2) journal shows raw `[comets] comet.spent` (no Vietnamese line); (3) view centre at ~1.6e8 units = scene drift 0.018/yr x 9.1e9 yr, grid labels unreadable at that offset; (4) colony 'lụi tàn' logged for a world in the same year it was swallowed.
- R7 A MERGED (celine/elements 89b07dc -> master 89d2106, Audit bf7f070): cli 214 OK exit 0, audit 8 DEFECT / 4 RISK, selftest + uitest PASS. GuardChecks rMixShort changed to the agreed rule. NEXT: Ariel rebases B on master (drop Add edit), Celine C on celine/events.
- R7 C MERGED (celine/events 1d876e0 -> master, before B; C does not depend on B): cli 227 OK exit 0, audit 7 DEFECT / 4 RISK (table-starevent gone; table-stage + kindof + jumpsamples wait for B), selftest + uitest PASS. Selica QA of C on master still owed. LEFT: Ariel rebases ariel/civtable on master, drops Add() edit of 722946c.
- QUEUED for Celine AFTER round 7 C (Yang ok 2026-10-06): command to set a star's state (age, burnt fuel / progenitor mass) so the god can create remnants directly; then Claire adds create presets white dwarf / neutron star / black hole in GodUi. Today: only via a 30-Sun star + 1.5 Myr jump.
