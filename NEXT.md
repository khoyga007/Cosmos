# Cosmos — state

READ `SPEC.md` FIRST (game, model, engine decisions, round-1 work packages). This file = what is running now.

## 06/10 night
- Old web project E:\CosmosSandbox = ARCHIVED (reference only, nothing deleted; note at top of its ROADMAP.md).
- master: object model core + command boundary (`World.Do`, journal, replay) + Sol scene + window (top-down default). Checks green: merge, moon (472 turns), sol (1.36% drift), constants (G 1→2), commands (replay hash equal), repeat.
- Yang has NOT yet seen the object-model window (`run.bat`). `_Draw` + mouse paths never ran headless → first thing to ask him.
- Round 1 handed out on bridge thread `cosmos` (SPEC §6): P1 Celine `E:\Cosmos-celine` celine/rules · P2 Ariel `E:\Cosmos-ariel` ariel/godtools · P3 Selica `E:\Cosmos-selica` selica/audit. Agents wake only when Yang prompts them.
- Claire next: review + merge reports; fast-forward on rails (SPEC §4 OPEN); collect Yang's content for rules 2-4 (life/civ parameters, thresholds).

## 06/10 session 2 — START HERE
- master = see `git log`: rails fast-forward · Celine P1 merged (f613d7a) · Ariel P2 merged (god tools; selftest + bench re-run by Claire, pass) · LAYERS water/life/civ + SeedLife + impact (content Claire's, Yang: "em tự nghĩ"; SPEC §2b) · jumps run rules in chunks. Full `cli` green (34 OK), `--selftest` pass.
- Round 2 handed out (SPEC §7, thread `cosmos`): Ariel = window shows year/layers/event log + jump/seed/rule buttons; Celine = review Layers+Rails + trim jump cost; Selica = P3 audit (never started). All need Yang's prompt.
- Yang's answers 06/10: rules 2-4 content = Claire decides; run.bat = later; temperature placeholders = not looked at; Web-chat ideas (seed life / lộ diện / tua lại) not his, allowed if Claire finds them good → SeedLife built, the other two = next slices; old web folders = keep for now.
- Until Ariel's round 2 lands the window shows NOTHING of water/life/civ (core only, proven by cli). Do not tell Yang it is visible.
- Claire next: review round 2; then "lộ diện" slice (civ reacts to the god) — design AFTER Yang has seen life/civ in the window; snapshot/rewind; civ uses metal.
- Still open with Yang: run `E:/Cosmos/run.bat` (window never seen by anyone: `_Draw`, mouse, Ariel's panels).

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
