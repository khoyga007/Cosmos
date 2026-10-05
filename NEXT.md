# Cosmos — state

READ `SPEC.md` FIRST (game, model, engine decisions, round-1 work packages). This file = what is running now.

## 06/10 night
- Old web project E:\CosmosSandbox = ARCHIVED (reference only, nothing deleted; note at top of its ROADMAP.md).
- master: object model core + command boundary (`World.Do`, journal, replay) + Sol scene + window (top-down default). Checks green: merge, moon (472 turns), sol (1.36% drift), constants (G 1→2), commands (replay hash equal), repeat.
- Yang has NOT yet seen the object-model window (`run.bat`). `_Draw` + mouse paths never ran headless → first thing to ask him.
- Round 1 handed out on bridge thread `cosmos` (SPEC §6): P1 Celine `E:\Cosmos-celine` celine/rules · P2 Ariel `E:\Cosmos-ariel` ariel/godtools · P3 Selica `E:\Cosmos-selica` selica/audit. Agents wake only when Yang prompts them.
- Claire next: review + merge reports; fast-forward on rails (SPEC §4 OPEN); collect Yang's content for rules 2-4 (life/civ parameters, thresholds).

## 06/10 session 2 — START HERE
- master: + fast-forward on rails (`core/Rails.cs`, `CmdKind.FastForward` Amount = years) + Celine P1 MERGED (f613d7a: year clock, rule table, event log, rule `temperature`) + `CmdKind.SetRule`. Full `cli` green, re-run by Claire (Celine's numbers reproduced; rule cost 2.24 on / 2.37 off ms). Rails numbers + what a jump gives up: SPEC §4.
- Round 1: P1 Celine DONE + merged. P2 Ariel: nothing committed on ariel/godtools (still 26197fb). P3 Selica: nothing on selica/audit. Both need Yang's prompt. Told on thread `cosmos`: rebase on master, new CmdKinds SetRule + FastForward (Ariel: time panel may offer a jump), new Consts appear in `Consts.All()`.
- Claire next: (1) rules inside a jump (SPEC §4 OPEN 1) — needs rule 2-4 shape, i.e. Yang's content; (2) snapshot/save point; (3) review P2/P3 when they land.
- Ask Yang (still open): (a) run `E:/Cosmos/run.bat` — object-model window never seen, `_Draw` + mouse untested; (b) content for rules 2-4 (water / life / civilisation: parameters, thresholds, how many years); (c) temperature placeholders ok? 288 K at Earth, light ~ mass^3.5, bands 240 K / 350 K; (d) "seed sự sống", "lộ diện", "tua lại" from his Claude Web chat — his ideas or not?; (e) old web folders E:\CosmosSandbox, -ui, -r18 (~550 MB): delete or keep.

## Lessons (process)
- Yang explaining an idea over several messages = listen only; build on his explicit go. (06/10: core rewritten mid-explanation, Yang objected; commit f0817b1 kept because it matched.)
- Smallest playable slice before infrastructure.

## Traps
- `dotnet new sln` on SDK 10 writes `.slnx`; Godot needs `game/Cosmos.Game.sln` (`--format sln`).
- `\n` typed inside a Bash tool command reaches python/sed as a real newline → C# strings with escapes go through Write/Edit.
- cli full run ≈ 1.5-3 min: default 2 min tool timeout is too tight. `cli -- rails` = rail checks alone (~20 s).
- `in Command c` cannot be captured by a lambda (CS1628): copy the field to a local first.
