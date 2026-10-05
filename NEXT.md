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
- Godot headless run: no script errors.
- Window on Yang's machine 06/10 (his screenshot): 100000 grains, 38 fps, step 19.52 ms, DEBUG build, OpenGL 3.3 compatibility renderer, while a screen-share app was running. Yang: "ok, mượt" → STACK ACCEPTED. Frame is bound by the sim step (Debug 19.5 ms vs Release 12.5 ms in cli), not by drawing.
- Trap: `dotnet new sln` on SDK 10 writes `.slnx`; Godot needs `game/Cosmos.Game.sln` → `--format sln`.

## MATTER slice 06/10 (Yang: resources/elements before civ; 6 groups; element = resource, no second tier)
- Groups, fixed order: gas, ice, rock, metal, carbon, radio (`World.ElemName`; VN labels + colours in `game/Main.cs` `ElemVi`/`ElemCol`, colours = Claire's pick, Yang's eye decides).
- Grain: `Pe[i]` group byte, mass `GrainMass` 1e-6. Body: `Bcomp[k*6+e]` mass per group, `Br[k]` radius (`RadiusOf`: star fixed 8, else 2+10*cbrt(m)).
- Grain inside a body radius → `Absorb`: mass + momentum to body, table row grows, swap-remove (last grain takes the slot, so grain order changes; draw layer recolours when `Np` changes).
- Start mix: inside `FrostLine` 180 = rock/metal heavy, outside = ice/gas heavy; same table for grains and starting planets; star = gas. Numbers are Claire's placeholders, not Yang's content.
- cli matter check (100k grains, 4000 steps): total mass conserved, each table sums to body mass, 622 grains absorbed. Repeat check OK. Step cost 100k: 15.2 ms Release (was 12.5 before the radius test; one run, not isolated from noise).
- Window: grains coloured by group, body colour = its mix, left click body = matter table (percent + grain count), click empty = legend. Headless run prints the table, no script errors. NOT seen in a real window yet (click picking, Vietnamese glyphs in default font).
- Trap hit again: python heredoc turned `
` inside C# strings into real newlines → CS1039.

## NEXT (needs Yang)
0. Yang runs `run.bat`, clicks a planet, reports what he sees.
1. Civ slice (after matter): one abstract civilisation on one planet that sees a miracle and reacts. Civ state content = Yang's.
Rule: smallest playable slice before infrastructure. Do not port the web engine wholesale.
