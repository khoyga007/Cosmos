# Cosmos performance evidence — append only

## 2026-10-08 R / NS-drop repair

Base `a1c832fe09352a5328797a87160bb8e620d15ccd`, delivered source checkpoint `672b81da508ab6275f0ae0fd63f4a3aba1f6fa7e`. Inputs: seed1234, Sol5000,32 warm+64 measured before Advances(.5), create70.005 mass NS at100,0,v0, progenitor10Suns snapshot atstep96,8 measured after Advances. Initial5k means5250 live incl. Saturn ring. Version comparisons always use the same inputs; physical trajectories/hashes deliberately change under the repair.

Historical original regression: seven fresh pre-C6 `e58c000` vs C6 `a1c832f` runs, alternating order, sequential. Per-pair after-cost slowdowns25.3339,47.0366,17.3014,38.0193,26.2047,34.9771,25.2518; median26.2047x. Raw `E:/Temp/cosmos-ns-study/paired-*-{before,current}.log`. Pre-C6 median after8.8713ms, C6median224.0151ms. Source/order/machine-load limits detailed in `NS-ROCHE-REPORT.md`.

Rejected/insufficient intermediate implementations (all raw retained):

| Revision/technique | Seven-run result | Decision |
|---|---|---|
| stable fragments + far-query rejection (`7c72b87`) | median after44.7175ms, paired after/before40.5599x | failed old3x gate |
| A: canonical scalar enumeration when entire bbox covered | median after44.9071ms, paired27.7400x | insufficient |
| A+B: properties cached only during atomic Advance | median after41.3628ms, paired30.6046x | insufficient |
| initial macro, old256 event budget | median after8.87155ms | failed8ms |

C was not implemented after Claire changed direction. An A exploratory run overlapping the query-test process is excluded; the sequential replacement `fixed-gate-A-sequential.log` remains the reported evidence. No measurements are deleted because a later version passes.

Macro repair: pair periapsis/current-distance acceleration admission; one global collection/Advance;8 fixed gravity slices with swept substep chords; local candidate append; stable child sizing;16-event budget. First standalone seven-run after costs5.209250,5.473225,5.279150,5.004500,5.302475,5.600775,5.257212ms; median5.279150ms, PASS intermediate8ms, FAIL new4ms architecture target. Raw `macro-ns-budget16.log`; hashC2BB44B21A0AAB7B all7. This is the result Claire explicitly accepted for an intermediate R handoff, not proof that every later run is under8ms.

Scaling, seven fresh paired before/after NS runs at each object count, Release:

| Rocks requested | Median after NS ms | Median paired after/before | Final hash |
|---:|---:|---:|---|
| 1000 | 3.440000 | 2.340524 | 4A45C998B54B1F21 |
| 5000 | 4.983875 | 2.141271 | C2BB44B21A0AAB7B |
| 20000 | 17.012625 | 2.102961 | 27A221F971CF33E6 |

All raw21pairs in `E:/Temp/cosmos-ns-study/macro-scaling.log`. First1k samples were noisier; no sample exclusion.20k does not meet8ms or4ms; S1–S3 architecture is required.

**Contrary evidence:** first full-suite process (source before redundant-housekeeping bypass) was481OK/1FAILED/exit1; sole failure was NS median7 **10.571362ms** (>8ms), paired ratio2.085267; baseline quiet means5.01..6.50ms. Same NS hashC2BB44B21A0AAB7B. Raw `macro-full-cli.log`. Physics/replay/admission checks passed; performance failure was reported immediately, not explained away as load/thermal without measurement.

Same-process collectible-AssemblyLoadContext before/after technique benchmark on final672b81d (`cli -- ns-drop-paired <a1c832f Core.dll>`), both backends warmed on0rocks,7fresh alternating pairs:

| Pair | Old a1c832f after ms | New after ms | Speedup |
|---:|---:|---:|---:|
|1|248.263275|29.708675|8.356592|
|2|243.226600|13.717025|17.731731|
|3|245.484537|6.304188|38.939917|
|4|242.626875|27.672650|8.767750|
|5|231.778450|28.101337|8.247951|
|6|243.108150|26.057450|9.329698|
|7|241.209075|17.461375|13.813865|

Median paired speedup9.329698x; new median26.057450ms, FAIL8ms. New ALC baseline quiet7.10..12.79ms differs from direct executable baselines. Possible runtime/harness/load causes are unverified; this method is not substituted for the standalone measurements. Raw `macro-final-paired.log`. Both old/new final hashes repeat7C5BFDD0F6146A50/C2BB44B21A0AAB7B. No further production micro-optimization after the stop-rule checkpoint; final validation and owner review remain required.

Final diagnostic phase attribution (`macro-final-profile.log`), source672b81d with counter-only instrumentation and matching hashC2BB44B21A0AAB7B: total20.5371ms/Advance; gravity+contact candidate work8.7210, drift.6098, merge.3444, rules.2114, Roche global collect2.5177, quantum check8.0461. Thus Roche10.5638ms (~51.4%) in THIS run. Break5.1222/fragments.1058/reservoir.0394ms are nested, not additive. Exactly64 slices/8Advances and8 global collects;10450 accumulated candidates. Do not extrapolate this timing split onto the5.28ms run under different load/instrumentation.

### Final full suite — exact672b81d

Fresh final full Release executable: **482OK/0FAILED/exit0**, audit4D/3R. NS seven after costs6.226662,6.014138,5.794725,5.932300,5.816775,6.188900,5.743163ms; median5.932300ms, intermediate8ms PASS/new4ms FAIL. Seven own baseline means2.721342,2.702187,2.606767,2.631061,2.629334,2.611633,2.735284ms; median paired ratio2.225655. HashC2BB44B21A0AAB7B throughout. Raw `macro-full-final-672b81d.log`. This later pass does not delete or resolve the earlier absolute failures; runtime/load variance remains a material limitation.

## 2026-10-08 Draw-Bench spike: Godot 4.7.2 vs Bevy 0.19.1 (@a0d4e59)

Hardware: NVIDIA GeForce GTX 1650 (Laptop dGPU, `--gpu-index 0` enforced, in-process active adapter assertion PASS). 24 runs sequential, 0 timeouts, 0 watchdog triggers. Raw JSONs in `spikes/draw-bench/results/*.json`.

### Telemetry Table

| Engine | Scene | Mode | Elements | Cap | FPS Avg | Frame ms (avg / p50 / p95) | CPU push (ms) | RAM (MB) | Active Adapter |
|---|:---:|:---:|:---:|:---:|---:|:---:|:---:|:---:|---|
| Godot | A | SHADER | 100k pts | 60 | 60.0 | 16.67 / 16.67 / 16.70 | 0.04 | 577.1 | NVIDIA GeForce GTX 1650 |
| Godot | A | SHADER | 100k pts | uncapped | 462.5 | 2.16 / 1.78 / 4.06 | 0.03 | 473.1 | NVIDIA GeForce GTX 1650 |
| Godot | B | SHADER | 1M pts | 60 | 60.0 | 16.67 / 16.67 / 16.72 | 0.04 | 781.9 | NVIDIA GeForce GTX 1650 |
| Godot | B | SHADER | 1M pts | uncapped | 319.0 | 3.13 / 3.10 / 3.39 | 0.02 | 785.0 | NVIDIA GeForce GTX 1650 |
| Godot | C | SHADER | 1M pts + bloom + 50k particles | 60 | 60.0 | 16.67 / 16.67 / 16.73 | 0.04 | 845.3 | NVIDIA GeForce GTX 1650 |
| Godot | C | SHADER | 1M pts + bloom + 50k particles | uncapped | 249.3 | 4.01 / 3.92 / 4.79 | 0.02 | 818.0 | NVIDIA GeForce GTX 1650 |
| Godot | D1 | SHADER | 100k pts + 1k bodies | 60 | 60.0 | 16.67 / 16.67 / 16.71 | 0.29 | 498.7 | NVIDIA GeForce GTX 1650 |
| Godot | D1 | SHADER | 100k pts + 1k bodies | uncapped | 435.5 | 2.30 / 2.23 / 4.39 | 0.14 | 475.3 | NVIDIA GeForce GTX 1650 |
| Godot | D2 | SHADER | 300k pts + 1k bodies | 60 | 60.0 | 16.67 / 16.67 / 16.96 | 0.31 | 529.8 | NVIDIA GeForce GTX 1650 |
| Godot | D2 | SHADER | 300k pts + 1k bodies | uncapped | 437.5 | 2.29 / 1.91 / 4.23 | 0.15 | 530.2 | NVIDIA GeForce GTX 1650 |
| Godot | D3 | SHADER | 1M pts + 1k bodies | 60 | 60.0 | 16.67 / 16.67 / 16.72 | 0.32 | 785.0 | NVIDIA GeForce GTX 1650 |
| Godot | D3 | SHADER | 1M pts + 1k bodies | uncapped | 307.9 | 3.25 / 3.05 / 3.33 | 0.13 | 810.3 | NVIDIA GeForce GTX 1650 |
| Bevy | A | SHADER | 100k pts | 60 | 44.9 | 22.29 / 20.18 / 26.64 | 0.00 | 348.2 | NVIDIA GeForce GTX 1650 |
| Bevy | A | SHADER | 100k pts | uncapped | 60.0 | 16.67 / 16.65 / 17.61 | 0.00 | 303.7 | NVIDIA GeForce GTX 1650 |
| Bevy | B | SHADER | 1M pts | 60 | 42.2 | 23.69 / 25.03 / 26.61 | 0.00 | 337.0 | NVIDIA GeForce GTX 1650 |
| Bevy | B | SHADER | 1M pts | uncapped | 74.6 | 13.41 / 16.45 / 17.46 | 0.00 | 337.9 | NVIDIA GeForce GTX 1650 |
| Bevy | C | SHADER | 1M pts | 60 | 40.1 | 24.94 / 25.44 / 26.66 | 0.00 | 377.3 | NVIDIA GeForce GTX 1650 |
| Bevy | C | SHADER | 1M pts | uncapped | 60.0 | 16.67 / 16.68 / 17.50 | 0.00 | 345.8 | NVIDIA GeForce GTX 1650 |
| Bevy | D1 | SHADER | 100k pts + 1k bodies | 60 | 39.9 | 25.08 / 25.67 / 26.93 | 0.10 | 303.8 | NVIDIA GeForce GTX 1650 |
| Bevy | D1 | SHADER | 100k pts + 1k bodies | uncapped | 60.0 | 16.66 / 16.65 / 17.43 | 0.02 | 303.7 | NVIDIA GeForce GTX 1650 |
| Bevy | D2 | SHADER | 300k pts + 1k bodies | 60 | 39.8 | 25.12 / 25.68 / 26.88 | 0.10 | 310.1 | NVIDIA GeForce GTX 1650 |
| Bevy | D2 | SHADER | 300k pts + 1k bodies | uncapped | 60.0 | 16.67 / 16.67 / 17.61 | 0.04 | 311.8 | NVIDIA GeForce GTX 1650 |
| Bevy | D3 | SHADER | 1M pts + 1k bodies | 60 | 39.7 | 25.16 / 25.63 / 26.98 | 0.10 | 337.5 | NVIDIA GeForce GTX 1650 |
| Bevy | D3 | SHADER | 1M pts + 1k bodies | uncapped | 60.0 | 16.67 / 16.65 / 17.66 | 0.03 | 337.1 | NVIDIA GeForce GTX 1650 |

### Caveats & Architecture Breakdown

1. **Bevy measurement invalidated**:
   - `AutoNoVsync` did not override driver/Winit sync. Uncapped locked at 60.0 FPS / 16.67 ms across 5/6 scenes.
   - Capped 60 used `std::thread::sleep` in update loop (`main.rs:553-559`), beating against vsync cadence -> dropped frames to ~40 FPS / 25 ms.
   - Excluded from all performance comparisons until re-measured with true `PresentMode::Immediate` / `Mailbox` and proper frame limiter.

2. **Godot Binary Status**:
   - Godot ran `DrawBenchGodot.dll` built in **Debug** configuration (`spikes/draw-bench/godot/.godot/mono/temp/bin/Debug/`).
   - Despite Debug DLL, CPU push for 1,000 dynamic bodies per frame was only 0.13 - 0.32 ms (`Main.cs:433-451`).

3. **Trajectory Math Origin (Points vs Bodies)**:
   - **Points (100k, 300k, 1M)**: Rendered via `MultiMeshInstance2D` (`Main.cs:193-200`). Stored with custom parameters `INSTANCE_CUSTOM = (r, theta0, omega, 0)` (`Main.cs:238-242`). Trajectories calculated **entirely in vertex shader** `orbit_rail.gdshader:7-14` (`theta = theta0 + omega * time; vec2 pos = center + vec2(r * cos(theta), r * sin(theta)); VERTEX += pos;`). CPU does ZERO trajectory work per point; CPU only pushes single `uniform float time` per frame (`Main.cs:408`).
   - **Bodies (1,000 in D1, D2, D3)**: Trajectories calculated **on CPU every frame** (`Main.cs:439-449`), uploaded to GPU via `RenderingServer.MultimeshSetBuffer` (`Main.cs:450`). CPU cost: 0.13 - 0.32 ms.

4. **GPUParticles2D vs Static Points**:
   - **D1, D2, D3 DO NOT use `GPUParticles2D`**. They use static `MultiMesh` instances displaced by analytical closed-form orbits in the vertex shader. No GPU compute buffer, no particle lifecycle, no inter-particle interaction, no dynamic temperature state.
   - **Only Scene C** uses actual `GPUParticles2D`: exactly 50,000 particles with `ParticleProcessMaterial` (`Main.cs:373-393`) + 200 MultiMesh bloom quads (`Main.cs:333-370`).
   - **Implication for SPEC §15**: D1-D3 demonstrates pure draw throughput of 1M quads + 1k CPU-updated bodies (~3.25 ms GPU). It does NOT measure dynamic GPU particle simulation (Euler integration of velocity/position + scalar temperature color updates). A dedicated Scene E is required to measure 1M stateful GPU particles across 200 emitters with frustum culling.
