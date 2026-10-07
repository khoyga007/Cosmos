# Cosmos — state

READ `SPEC.md` FIRST (game, model, engine decisions, round-1 work packages). This file = what is running now.

## C6 Roche performance debt — Claire decision 2026-10-08
- Selected core `8e2b2f5`; delivery waits Claire's diff review/merge. The experimental limit cache `67194c8` is discarded.
- Roche candidate collection is about0.22ms at5250 stock objects. It normally runs once per Advance, plus topology/housekeeping rebuilds; this is not0.22ms for each of the8 substeps. Entry queries cost far less.
- Gate chosen by Claire: median7 component cost stock≤0.25ms and10-body collection≤0.02ms. Paired-ratio median7 cost remains a reported metric; the10% relative-cost experiment failed10.02%/15.22% and is retained in C6-REPORT.md. Existing element5% and jump5× gates stay separate.
- Debt accepted for now: integrate Roche rejection into the existing gravity pass to avoid a separate scan. Do this after species work; no further micro-optimization now.
- Bound unresolved reservoirs, ring spreading/dust collisions and detailed three-body tidal timing remain the explicit model limits documented in C6-REPORT.md.

## Scale design — Yang 2026-10-06, IDEA ONLY, no code until he says go (phase 5)
- galaxy map = one continuous real-time space, no per-system scene; inter-system distance = REAL (45 u ≈ 1 AU → nearest star ≈ 1.2e7 u). Systems gravitationally isolated (neighbour pull ~1e-11) → per-system chunks/threads, per-system local coords, camera-relative draw.
- system count = symbolic, player picks at map gen (Stellaris slider); core takes it as parameter, cap from measurement.
- universe = procedural from seed, galaxies drawn as dots (only cells in view); a galaxy generates on click, at current universe age.
- opened galaxy lives in the save: state + last-computed year, catch-up by rails on return. Untouched rocks regen from seed.
- OPEN: catch-up for civs; snapshot save format (unchecked whether one exists); rock budget (200 systems × 5240 rocks ≈ 460 ms/step extrapolated); stars orbit galactic centre (real physics, Claire decides).

## Out on bridge 2026-10-06 night (base 3ebe7cc) — all need Yang's prompt, all have step-0 checkpoint
- C5 Celine `celine/cosmic-events` (claimed): mass ledger (sublimation debt) + SN ejecta Mix + hypernova + kilonova (merger trigger) + GRB record only. Yang: no fictional elements yet. GRB = random axis, harms OTHER systems only (phase 5), home system gets the blast; falloff 1/d², dose-based.
- A2 Ariel `ariel/civ-stats` c8b7f96 MERGED -> master 8fef715 (Claire read diff, did NOT re-run; Ariel: cli 335 OK, selftest 32FE9F87D73FE807, uitest 29). 10-era Stage table with Needs, tech clamps below threshold when a Need is unmet, 5 stats (PeopleCount, Kardashev, footprint = ConsumedMatter[] hashed), dry dome scales with home Pop. Ethics/awareness/relations still no code. Lessons: Claire's spec said "consumption -> waste rock for all groups" and "supply moves real mass" — both unphysical, retracted; first hand-in had new Consts as get-only properties = outside Hash, "hash unchanged" was fake.
- A3 Ariel `ariel/civ-stats-2` (sent, not started): waste column instead of Metal/Radio named in core; ceiling = last row + margin (TechMaxCeiling 4.0 absolute blocks new rows); dead Const CivMetalUse; ConsumeRate 1e5x too high (real ~5e-13 planet mass/yr metal); Earth mix hand-typed in CalcEarthRadiusRef.
- C5 Celine step 0 ACKed with changes: no ejecta packets (analytic deposit by solid angle, rest to Escaped ledger); hypernova = RNG fraction of >=25 Msun (spin proxy); NSBH Foucart2018 with real 12 km NS radius; GRB stores E_iso + half-angle, range derived from dose. Celine + Selica must rebase on 8fef715.
- P5 Selica `selica/civ-dates` ef21d38 MERGED (Claire read core diff only, no re-run; Selica: cli 336 OK exit 0). Root: Layers.cs dt = min(RuleYears, Year-Touched) used a run WINDOW as a milestone AGE; same in Civ.cs _launchYear. civ.start spread 562 yr -> 0; residual 24-150 yr on ~692000 (0.02%, < 1/200) = coarser rule sampling in long chunks, ACCEPTED (exact match would need ~1e4 chunks per 1e6 yr). UNVERIFIED: Selica reports selftest C430139B76C6A372 "unchanged" on a base where Ariel reports 32FE9F87D73FE807 -> one of the two game runs used a stale core DLL; asked Selica to rebuild + re-run on master. Unchecked by her: Touched != 0, civ.colony dates, 5250-object scene.
- Candidates Yang has not picked: SN Ia, superflare, Roche breakup → rings.

## Perf state (master 3ebe7cc)
- 363b81e run.bat builds core Release; 3ebe7cc selica/rocks-parallel (P4): rock pass + drift in fixed chunks clamp(N/512,1,16), bit-exact at any thread count (Selica: 4 scenes × 6 thread levels, cli 322 OK). `World.Threads` is a FIELD.
- Yang's window: 40 → 58 (NVIDIA WhisperMode/Max Frame Rate off) → 146-150 fps at ×1, 5250 objects (before P4). Ceiling at 5250 = draw, not Advance (0.96 ms). SIMD not worth it now.
- Not re-run by Claire after 3ebe7cc merge. OPEN: Parallel.For overhead T_s ~0.76 ms unmeasured split; tab row overflows with 5 tabs ("Nhật ký" pushed off) → Ariel.

## Team pass 2026-10-06 late (master 58e1c79) — RESUME HERE
- Merged by diff review only (Claire ran nothing, quota): S2 selica/audit-gaps (132defe, cli only, `cli -- conserve` 37 checks) + S1 celine/nova-stamps (a928acb). Celine's numbers on rebased tree: cli 315 OK exit 0, audit 4D/3R, selftest C430139B76C6A372, uitest B6EC767CF72CE186. Report + sources: S1-NOVA-STAMPS.md.
- S1: RunRules settles old luminous era before each star boundary; life.end/civ.end dated analytically, cut 1/200 equal; WD born 200 kK, Mestel cooling, NS 2 MK @ 330 yr (macro fit); jump cut at cooling edges -> real warm interval scorched->temperate->frozen. 4 Const defaults changed (StarWhiteLight, StarWhiteCoolYears, StarNeutronLight, StarNeutronCoolYears), all hashes re-captured.
- Rule (Yang 06/10): physics forks = real-world physics, no asking him.
- 2nd wave merged, master 4cfa75c, diff review only, COMBINED TREE NOT YET RUN (Selica asked to verify): celine/s1-followup 6153a9c (RuleBoundary flags BeforeStar/BeforeCooling/After on rule rows, no rule ids in RunRules; cooling scan gathers light sources once: 544 -> 100 ms on 5250 slots WD age 1e8 +1e8 jump, pre-S1 75; newborn-WD worst case unmeasured), selica/advance-perf 2bafa6c (KickBody/KickRock, _attX/_attGM gathered per sub-step, bit-exact, Release 2.55 -> 2.34 ms/Advance), ariel/object-list db464c2 (tab "Thiên thể", JumpTo, uitest PushInput only, root size 1280x800 in uitest). Expected: selftest C430139B76C6A372, uitest C233FAEA280FBE6B, cli = union of 322 + AdvanceBench.
- FPS (Selica P1/P2): 39 fps cap was NVIDIA App WhisperMode/Max Frame Rate on Yang's machine, off -> 58 fps. Now Advance-bound: Debug 8.4 ms vs Release 2.5 ms per Advance at 5250 objects; 98.7% = rocks kicked by 10 pulling bodies (5240x10x8). run.bat builds -c Debug; game sln maps Release->Debug for game but Release for core, so `-c Release` in run.bat = free 3.3x. Asked Selica to change run.bat.
- (superseded, kept for history) A1 ariel/object-list returned (uitest had _GuiInput + EmitSignal fallbacks = proves nothing; move list to own tab "Thiên thể"; rebase, hashes moved). P1 selica/fps-probe: window 40 fps paused with 5250 objects, Debug build, measure only. S1 follow-ups sent to Celine: NextCoolingBoundary scans all N per world/edge (cost with 5250 rocks unmeasured), RunRules hard-codes rule ids "temperature/water/life/civ" (table debt). After those: counter for sublimated ice/gas mass (Kinds.cs:257-274, :203), spec not written.

## Star-system defect pass 2026-10-06 night (Claire, master d8fbe31)
- Yang: "quay lại fix mấy lỗi ở tầng hệ sao". Done on master, cli 254 OK exit 0, audit 5D/3R, selftest 011B7BC90A48FFC7, uitest D5B228338AAFCA83.
- HASHES CHANGED for everyone: Consts +3 fields (CosmicBackground, CivDomeTemp, CivDomeTempRange), all hashed by reflection. Branches must rebase; Legacy tables in ElementChecks/StarEventChecks re-captured. Proof: with the 3 fields skipped in Hash() only sol0-life/sol0-edit/sol2000-jump moved.
- Fixed: events in a jump dated at their own year (life.start, life.stage per mark, civ.start, civ.stage per stage via bisection on lived-years curve, civ.ship.first via `_launchYear`); tech ceiling = last threshold + 1; DomeRoom (heat factor, needs own ice or fed civ `FindFed`); ShipGoal skips dead-end worlds; civ.end A=-1 = swallowed (window wording); floor 2.7 K = `max(star, floor)` for non-star bodies only (T^4 sum tried, moved Earth 5.5e-7 K and broke 6 exact checks -> dropped); comet.spent only named/pulling; ghost label names remnant preset.
- Ship-stuck NOT reproduced: `cli -- ships` stock Sol 279 and 1395 ships, 0 lost, longest flight 1.1 yr, nearest 1.33 Sun R; g at 1.5R 0.33 < thrust 2. Asked Yang what conditions he saw.
- Red giant: repro now Sun R 28 (not 826), swallows Mercury only. Still: everything in that jump stamped 1e10 (jump ends on the giant moment); colony years land on chunk ends (5e7, 1e8).
- Not done: scorched->frozen in one step across nova; object list / jump-to-object for lost objects; Selica's 2 audit gaps (LifeStages literal, conservation probe).

## ROUND 7 CLOSED 2026-10-06 evening (Claire, resume list done)
- master: starstate merged (406729f) -> remnant presets (1aa0d7c) -> R7 B civtable merged -> Sol scene momentum zeroed. Claire re-ran after each: final cli 252 OK exit 0, audit 5D/3R, selftest PASS, uitest PASS (25 lines).
- Remnant presets: Create tab buttons Sao lùn trắng 0.6 Sun (birth 2) / Sao neutron 1.4 (birth 10) / Lỗ đen 7.5 (birth 30); `GodTools.MakeRemnant` = Create then SetStarState at end of burning, cooling age 0. Mix = gas 100% (placeholder, real remnants are not gas). Ghost while placing still draws/labels an ordinary star. NOT seen by eye.
- B hash question: after merge selftest 32AA0B3F10E7016A / uitest F2BD752070D6B108 = identical to master before merge -> default stages bit-exact. Where Ariel's first-report hashes (983C.. / 9FE5..) came from = not found, not reproduced.
- B note: audit `table-stage` went to 0 partly by wording: `LifeStageAt` renamed `LifeStages` + trailing `// Stages` comments on Layers.cs:68-69 (audit regex skips lines naming Stages). `MaxTechStage` now derives from DefaultStages (static) while `Stages` is per-world: a world with an inserted stage has MaxTechStage out of step (only LayerChecks.cs:29 reads it). Life stages still a literal array. For Selica QA.
- Momentum: SolSystem subtracts centre-of-mass velocity from every object -> Sun no longer at rest at origin velocity 0. ALL Sol hashes changed (ElementChecks Legacy first 7 re-captured; selftest now 83572EF31AC27154, uitest 5ED67DDEB6EC20DA). Any check placing things by absolute velocity must add the Sun's (KindChecks comet fixed).
- Selica QA DONE (bridge 12:30Z + 12:38Z, record E:\selica-work\R7-BC-QA.md): reproduced master 304bdc1 from clean archive, same numbers. `selica/audit-fix` cd08d07 MERGED (table checks two-sided, star-event rows = Add() only); Claire re-ran: cli 252 OK exit 0, audit 5D/3R, starevent 5 rows, stage 4 rows / 8 read sites. Ariel's odd hashes = report for pre-rebase HEAD f34146f (Selica; cause of the numbers unproven). STILL OPEN: table-stage text claims no life threshold is a literal, but `LifeStages = { 0.01, 0.1, 0.5 }` (Layers.cs:69) is one (regex names only the old `LifeStageAt`); no independent conservation probe for C (only Celine's own checks).
- WAITING ON YANG: verdict on window (create-at-rest, Z zones, remnant presets); civ design go; cosmic events scope; first contact.
- Window added today by Claire: create-at-rest checkbox (ba09c5a), zone overlay key Z (0892189, 9addf74). Neither seen by eye by Claire.

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
- (R now 28, see top section) Was OPEN defect (Celine, bridge 741111ba): red giant R=826 swallows whole system to Neptune (Sun R inflated ~40x vs orbit scale); bodies swallowed in jump leave no events. Repro `cli -- jump-bench 1e10 0 v`.

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

## Scale design — Yang dictating 2026-10-06 night, IN PROGRESS, NO CODE until he says go
- Recursive object: every object = body with physics seen from outside (mass, pos, vel, pulls/pulled) AND a folder holding a world like today's. Universe > cluster > galaxy > star system > planet/moons. Same law every layer.
- Each layer = own frame: own length unit, time step, look (a star at galaxy layer = a dust speck). Too-small things do not exist as objects at that layer (asteroid invisible from outside galaxy), only add to the dot's mass.
- Real-time: all layers live on one clock. Watched path (root -> where Yang stands) simulated in detail, each layer at its own grain; everything else closed, cheap. Yang accepted: this costs some realism.
- Content, three kinds: (1) SCRIPTED clusters/galaxies: authored layout + storyline + event chain; (2) UNSCRIPTED: truly random, alive, part of the game, not scenery; (3) player-made (player can create galaxies etc).
- Events: scripted chain lives in scripted galaxies; other events spread as PROBABILITY over unscripted galaxies; CONDITIONAL events fire in ANY galaxy (scripted or not, incl. player-made) whose physical conditions are met.
- Claire's technical notes given to Yang (not decisions): door between layers = tide as zone radius down, objects crossing in/out, flux (radiation/blast) down, summary (mass, momentum, light, big events) up; closed folder = calendar for known-time events + closed-form stretch rules + seeded dice for chaotic ones; an event must be computed only when something outside can observe it; announced events are journaled so the inside matches later; storing for real feasible to ~1e5-1e6 systems (Claire's rough estimate, unmeasured), beyond = seed formula as birth certificate only; rocks collapse to numbers when closed.
- Constraint for conditional events: the condition must be readable from what a closed folder still holds (summary / calendar), else it can only fire in open or warm folders.
- Undecided by Yang: counts, which events, story content, how player creates at upper layers, warm-folder budget.

## Civ design — Yang 2026-10-08, species body/environment (IN PROGRESS, NO CODE until he says go)
- Not every species has one stage ladder. Human ladder (10-era table: prehistoric..interplanetary) is valid ONLY for land-living tool-using body plan. Examples Claire gave: ocean (no fire -> chem/bio/geothermal path), gas-giant atmosphere (no rock -> no stone age), ice world (metal scarce), no-hands body, hive/gestalt (skips nations), lifespan changes accumulation speed.
- Claire proposal (heard, not approved): capability graph. Node = capability (heat control, metalwork, memory/writing, electricity, nuclear, spaceflight..), each with Needs (element shares, prereq nodes) like StageNeed today. Civ unlocks nodes its environment+body allow; era LABEL = derived from Kardashev + unlocked set. Human 10-era table = default path inside the graph.
- Yang answers: (1) body + environment BOTH species parameters at birth AND derived from the home planet (both). (2) Human = first, default template; all other species = free composition from parameters (no fixed species list).
- Open (Yang): parameter list (body plan, senses, habitat, lifespan, social structure...), how birth params and planet interact (planet constrains/biases, species picks within), node list.

## Reference: Macht: Cosmic Engine (Yang played 2026-10-08)
Yang verdict: messy, vibe-coded, worse than Cosmos. Specific complaints (traps to avoid in Cosmos):
- civ meaningless (no visible why/cause; stages/events with no player-readable meaning)
- many control panels with no meaning (panel count != depth; each panel must answer a player question)
- tutorial incomprehensible
Macht pitch for comparison: 1:1 galaxy ~1e11 stars, zoom intergalactic->planet surface, VR, Kepler orbits, DNA editing, god-mode wars. Unverified (search summary only; dev = Kaan solo vs Kestro Games listing conflict).
Cosmos TODO from this: before adding any panel, state the question it answers; civ events must surface a readable cause (log line "why"); tutorial = Yang test, not agents. Yang's eyes judge.

## Idea (Yang test 2026-10-08): change G keeps orbits?
God sets G x100 in window -> rocks keep old v (circular v ~ sqrt(G M/r) now 10x higher) -> plunge to tight eccentric orbits, collide (1687), Roche around white dwarf. Physics-correct, but surprising. Candidate god option: "SetConst G + rescale velocities by sqrt(Gnew/Gold)" (keeps orbit shapes). NOT built; Yang decides. Setting G back does NOT undo (merged/destroyed rocks stay); no rewind yet (journal replay only).
