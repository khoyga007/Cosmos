# Cosmos — state

READ `SPEC.md` FIRST (game, model, engine decisions, round-1 work packages). This file = what is running now.

## 06/10 night
- Old web project E:\CosmosSandbox = ARCHIVED (reference only, nothing deleted; note at top of its ROADMAP.md).
- master: object model core + command boundary (`World.Do`, journal, replay) + Sol scene + window (top-down default). Checks green: merge, moon (472 turns), sol (1.36% drift), constants (G 1→2), commands (replay hash equal), repeat.
- Yang has NOT yet seen the object-model window (`run.bat`). `_Draw` + mouse paths never ran headless → first thing to ask him.
- Round 1 handed out on bridge thread `cosmos` (SPEC §6): P1 Celine `E:\Cosmos-celine` celine/rules · P2 Ariel `E:\Cosmos-ariel` ariel/godtools · P3 Selica `E:\Cosmos-selica` selica/audit. Agents wake only when Yang prompts them.
- Claire next: review + merge reports; fast-forward on rails (SPEC §4 OPEN); collect Yang's content for rules 2-4 (life/civ parameters, thresholds).

## HANDOFF 06/10 ~05:40 VN (session closed by Yang: context too large) — NEW SESSION START HERE
1. Read `SPEC.md`, then bridge thread `cosmos` (inbox).
2. Team state at close: Celine STARTED P1 (celine/rules), Ariel STARTED P2 (ariel/godtools), both from master 26197fb. Selica (P3) had not reported — Yang must prompt her.
3. Decisions sent to Celine (bridge msg f9bbbe82): log only Kind.Planet, Temp for every object; formula T = 288*(Σ (starMass/50)^3.5 * (45/d)^2)^0.25, edges 240K/350K, rhythm 0.01 yr, log 1024 = PLACEHOLDERS not approved by Yang; cost criterion (e) = baseline vs with-rule measured back to back, ≤ +10% (absolute 2.4 ms only holds on an idle machine; she measured 5.66 ms while Claire's cli was running); rule on/off as a god command = next round, Claire adds the CmdKind.
4. Claire to do: review each report (re-run their checks yourself, do not trust pasted output), merge to master, keep `cli` green; then fast-forward on rails (SPEC §4 OPEN); snapshot/save point after round 1.
5. Ask Yang: (a) run `E:\Cosmosun.bat` — object-model window never seen by anyone, `_Draw` + mouse untested; (b) content for rules 2-4 (life/civ parameters, thresholds); (c) "seed sự sống", "lộ diện", "tua lại" appeared in his Claude Web chat — his ideas or not? not in SPEC; (d) delete old web folders (E:\CosmosSandbox, -ui, -r18, ~550 MB, hold teammates' uncommitted files) or keep.

## Lessons (process)
- Yang explaining an idea over several messages = listen only; build on his explicit go. (06/10: core rewritten mid-explanation, Yang objected; commit f0817b1 kept because it matched.)
- Smallest playable slice before infrastructure.

## Traps
- `dotnet new sln` on SDK 10 writes `.slnx`; Godot needs `game/Cosmos.Game.sln` (`--format sln`).
- `\n` typed inside a Bash tool command reaches python/sed as a real newline → C# strings with escapes go through Write/Edit.
- cli full run ≈ 1.5 min: default 2 min tool timeout is too tight.
