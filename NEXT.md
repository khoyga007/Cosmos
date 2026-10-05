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

## SOL scene 06/10 (Yang: "làm hệ mặt trời để anh xem")
- `World.SolSystem(grains, seed)` = Sun + 8 planets (VN names in `Bname`, own colour `Bcol`) + asteroid belt 35% of grains (2.1-3.3 AU) + Kuiper belt 65% (32-50 AU). Window opens this scene; `World.Solar` (random system) kept for cli numbers.
- Distances squeezed `SolDist(au) = 45*AU^0.62` (Mercury 25 … Neptune 371) so all fit one screen — Claire's call; order + mass ratios real (`EarthMass` 1.5e-4). `GrainMass` now 1e-7. Planet mixes = rough real ones, placeholders.
- Planet start speed uses the softened pull, else Mercury wobbles (4.75% → 1.42% worst radius drift over 20000 steps, cli "sol:" line).
- Draw: orbit lines, names, Saturn ring = drawn ellipse only (not matter). No moons: softening (3-4 units) is wider than a moon orbit — needs per-body softening or a sub-step before moons/rings can be real.
- Headless: build clean, panel prints. `_Draw` path (orbit lines, labels, ring) NOT exercised headless → unseen until Yang runs it.

## OWN GRAVITY 06/10 (Yang: scale need not be true, but every body has its own gravity, physics exact, moons placeable)
- Cause moons were impossible: global softening 3-4 units + body radius 2-5 units, while Earth's zone of control (Hill sphere, squeezed distances + real mass ratios) is 0.45 units.
- Fix in `World`: no global softening. Each pair softened by its own radii only (`Br[i]^2+Br[j]^2`; grain-body `Br[k]^2`), so pull is true 1/r^2 outside a body. `RadiusOf` = cbrt(m) (Earth 0.053, Jupiter 0.36), star 3. Supersedes "No moons" in SOL block above.
- Bodies step `BodySub` = 8 times per grain step (bodies are few; moon orbits need it). Grains still one step.
- `CircSpeed`, `Hill(k)`, `AddMoon(parent, x, y, m, mix, name, col)`, `Bpar` (parent, draw only). Sol has the Moon (body 9, 0.12 from Earth).
- cli: Moon 409.9 turns in 20000 steps, distance 0.115..0.125; planets worst drift 1.41%; repeat OK. 100k step 11.8 ms Release.
- Window: left click = select + camera follows; right click = moon at cursor around selected body (1% of parent mass, rock — placeholder); green circle = Hill zone of selected body; wheel zoom x1.25; body drawn true size once bigger than its dot. Headless: build clean, AddMoon path runs. Draw + mouse paths unseen.
- Known: accretion now near zero (true-size bodies are tiny targets). Body-body collision/merge not written: bodies pass through each other softened. Grain orbits around planets (rings) possible now but grain step 0.5 is coarse for tight ones — unmeasured.
- Trap (3rd time): `\n` typed in a Bash tool command reaches python as a newline. C# strings with escapes → Edit tool, not a python patch.

## NEXT (needs Yang)
0. Yang runs `run.bat`, looks at Sol, reports.
1. Civ slice (after matter): one abstract civilisation on one planet that sees a miracle and reacts. Civ state content = Yang's.
Rule: smallest playable slice before infrastructure. Do not port the web engine wholesale.
