# Cosmos

Sandbox god game: real physics + emergent stories. Star-system level first (Sol scene), galaxy tier later.
Dwarf Fortress way: write parameters + rules, objects interact, stories emerge. No scripted scenarios.

## Idea
- Everything = object with own physics (mass, pos, vel, matter mix). One set of universe constants; god can change them live.
- Layers grow on worlds: water -> life -> civilisation (names, chronicle, domes, ships, colonies).
- Six element groups: gas, ice, rock, metal, carbon, radio. Kind (star / planet / moon / rock) derived, never picked.
- Stars age for real (billions of yr): giant, nova, white dwarf / neutron star / black hole. Time jumps fast-forward on rails.
- God tools: create, push, edit matter, seed life, set star state, change constants. UI text Vietnamese.

## Layout
- `core/` — C# sim, no engine dependency. World, commands (journal + replay), rails, rules, layers, civ, stars.
- `cli/` — headless checks, audits, benches. `dotnet run --project cli` = full check suite (exit 0 = OK).
- `game/` — Godot 4.7.2 .NET window (`Main.cs` draw, `GodUi.cs` panel). Look is placeholder, code-drawn.
- `SPEC.md` — game + model decisions. `NEXT.md` — current state, open items. Read SPEC first.

## Run
Needs .NET SDK + Godot 4.7.2 mono (path in `run.bat`; edit if different).

```
run.bat [rocks]      # builds core Release, opens window; default 5000 rocks
dotnet run --project cli
dotnet run --project cli -- --selftest
```

Keys: C create tool, Z zone overlay, F follow, T tilt, Del remove selected.

## Rules of the repo
- Determinism: state hash covers velocity, matter, rng, all Consts. Changing a default Const moves hashes; re-capture checks.
- Physics forks = real-world physics.
- Visual polish waits until Alpha.
