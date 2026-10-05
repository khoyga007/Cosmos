# Cosmos (rebuild) — state

Rebuild of E:\CosmosSandbox (web, PAUSED, kept as physics reference). Direction = `E:\CosmosSandbox-ui\ROADMAP.md` top block "DIRECTION RESET 06/10".
Stack (Claire's call 06/10, Yang delegated): Godot 4.7.2 .NET at `E:\Godot\Godot_v4.7.2-stable_mono_win64\` (official zip, sha512 checked) + sim core = plain C# library, no Godot types. Sim 2D, view 2.5D (draw only). Visuals = code-drawn (shaders) or AI images; Yang makes no art.

## MODEL (Yang 06/10, final for now — overrides every grain/softening note in git history)
Yang verbatim: "mọi thứ đều phải là object, có physic riêng, tham số riêng" · "mình sẽ bỏ cơ chế hạt đi" · belts/asteroids = objects too · "vẫn phải có hằng số" · "thần vẫn thao túng hằng số các kiểu được".
- NO grains. World = objects + one `Consts` (G, AttractMass, StarMass, RadiusScale, Density[6]) the god can change at run time.
- Object params: pos, vel, mass, matter table (6 groups: gas ice rock metal carbon radio; element = resource, no second tier), name, colour, parent, group.
- Derived, never hand-set: radius (matter / densities), "pulls others" (mass >= AttractMass), kind (Star/Planet/Moon/Rock). Claire's earlier per-object "pulls" toggle = dropped, threshold constant instead.
- Belt = group (name); its numbers (count, total mass) computed from members. Each asteroid = full object, clickable.
- Pull = exact 1/r^2, no softening. Touch (d < r1+r2) = merge: heavier keeps identity; mass, momentum, matter summed. Slots stable (dead slot reused, never shifted).
- Cost rule: every object feels every PULLING object; 8 small steps per Advance for all.

## Layout
- `core/World.cs` (net8.0, no Godot types): `Consts`, `Kind`, `World` (`Add`, `AddOrbiting`, `Hill`, `KindOf`, `Attracts`, `RecalcRadii`, `SolSystem(rocks, seed)`, `Advance`, `Hash`).
- `cli/` headless: `dotnet run -c Release --project cli` → ms/step + checks merge / moon / sol / constants / repeat. Takes ~1.5 min (run with a long timeout).
- `game/` Godot project, scene built in `Main.cs`: pulling objects in `_Draw` (dot, name, orbit line), non-pulling in one MultiMesh; left click select + camera follow, right click = moon around selection (1% parent mass, rock: placeholder), wheel zoom, T tilt, Space pause, `[` `]` = G down/up.
- `run.bat [rocks]` (default 5000) builds + opens the window (Yang runs it; agents never launch windows on his machine). `godot --headless --path game -- --rocks=N --bench=SECONDS` prints two panels + BENCH line.

## Status 06/10 (commit after this file)
- cli Release: 2000 rocks 0.89 ms/step · 5000 2.38 · 20000 9.4 · 50000 20.5. Merge OK (mass, momentum, matter kept). Moon 472 turns in 20000 steps, 0.115..0.123. Planets worst drift 1.36%; 4 of 5000 rocks merged. G 1→2: Earth dips 45→14.8. Repeat OK.
- Sol = squeezed distances `45*AU^0.62`, real order + mass ratios, Moon at 0.12 from Earth. Planet/rock mixes + densities + thresholds = Claire's placeholders.
- Headless Godot (Debug): 5011 objects, step 6.5 ms, no script errors. `_Draw` and mouse paths NOT exercised headless → unseen until Yang runs it.
- Not written: rock-rock collision (non-pulling pairs pass through; needs grid), fragmentation, swept test for fast small bodies, Saturn ring as objects (still a drawn ellipse), god tools beyond G and moon placing, temperature/spin params (add when a rule reads them).
- Traps: `dotnet new sln` on SDK 10 writes `.slnx`, Godot needs `.sln` (`--format sln`). `\n` typed in a Bash tool command reaches python as a newline → C# strings with escapes go through Write/Edit.
- History (superseded, read git log only if needed): grain spike 6824ef4 (100k grains 38 fps in Yang's window, stack accepted), matter-on-grains d25837c, Sol 61984d9, own gravity 35ffd95.

## NEXT (needs Yang)
0. Yang runs `run.bat`, looks at Sol, reports.
1. Civ slice (after matter): one abstract civilisation on one planet that sees a miracle and reacts. Civ state content = Yang's.
Rule: smallest playable slice before infrastructure. Do not port the web engine wholesale.
