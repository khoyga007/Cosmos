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
