# Cosmos (rebuild) — state

Rebuild of E:\CosmosSandbox (web, PAUSED, kept as physics reference). Direction = `E:\CosmosSandbox-ui\ROADMAP.md` top block "DIRECTION RESET 06/10".
Stack (Claire's call 06/10, Yang delegated): Godot 4.7.2 .NET at `E:\Godot\Godot_v4.7.2-stable_mono_win64\` (official zip, sha512 checked) + sim core = plain C# library, no Godot types. Sim 2D, view 2.5D (draw only). Visuals = code-drawn (shaders) or AI images; Yang makes no art.

## Layout
- `core/` Cosmos.Core (net8.0): `World` = bodies (double, pairwise) + grains (float, feel bodies only), fixed step, seeded stream, `Hash()`.
- `cli/` headless runner: `dotnet run -c Release --project cli` → ms/step per grain count + repeat check.
- `game/` Godot project, all scene built in `Main.cs`; grains = one MultiMesh, buffer pushed once per frame; tilt = y scaled by 0.5.
- `run.bat [grains]` builds + opens the window (Yang runs it; agents never launch windows on his machine). Bench without typing: `godot --path game -- --grains=N --bench=SECONDS` prints one BENCH line and quits.

## SPIKE status 06/10
- Core, Release, single thread, 9 bodies: 10k 1.27 ms/step · 30k 3.84 · 100k 12.5 · 300k 32.0. Repeat check OK. (Old web cap: 7k grains.)
- Godot headless run: no script errors. NOT measured: real fps in a window (needs Yang to run `run.bat`).
- Trap: `dotnet new sln` on SDK 10 writes `.slnx`; Godot needs `game/Cosmos.Game.sln` → `--format sln`.

## NEXT (needs Yang)
1. Yang runs `run.bat`, reports fps at 100000 (and 30000 if low). Decides whether the stack stays.
2. Then first playable slice: one abstract civilisation on one planet that sees a miracle and reacts. Civ state content = Yang's.
Rule: smallest playable slice before infrastructure. Do not port the web engine wholesale.
