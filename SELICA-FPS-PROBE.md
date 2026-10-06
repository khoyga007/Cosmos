# SPEC P1 — ĐO FPS (Selica) — chỉ đo, không sửa

Nhánh `selica/fps-probe`, worktree `E:\Cosmos-fps`, base `132defe` (master hiện tại = `1e5709f` + merge S2 cli/;
`game/` và `core/` y hệt `1e5709f`). Không merge, không sửa `core/`, master không bị chạm.

## Máy đo

| | |
|---|---|
| Adapter | **NVIDIA GeForce GTX 1650**, driver 32.0.15.9649 (`renderer=` do chính game in ra) |
| Adapter thứ hai | Intel UHD Graphics, driver 31.0.101.1999 — **đang giữ màn hình**: 1920×1080 @ **144 Hz** |
| Nguồn | AC, pin 100% (`Win32_Battery.BatteryStatus=2`), power plan **High performance** |
| Cửa sổ | 1920×1057 (mode=2 maximized), `vsync=0`, `Engine.MaxFps=0` — không có cap trong code (`project.godot`, không nơi nào đặt `MaxFps`) |
| Engine | Godot 4.7.2-stable mono, `E:\Godot\Godot_v4.7.2-stable_mono_win64\` |
| Build | Debug (mặc định của Godot khi chạy `--path`) |
| Lệnh bench | `Godot_..._console.exe --path E:\Cosmos-fps\game -- --bench=8` (bench có sẵn, `game/Main.cs:416-433`) |

## Phần nào đo bằng gì

| số | nguồn |
|---|---|
| `step_ms` | `Advance` — đồng hồ có sẵn `game/Main.cs:361-374` (EMA α=0.05) |
| `fill_ms` | vòng nạp buffer multimesh + `MultimeshSetBuffer` — `game/Main.cs:389-401` (có sẵn) |
| `draw_ms`, `orbit_ms`, `body_ms`, `arcs`, `segs`, `ships` | **Stopwatch em thêm tạm** quanh `_Draw` (`game/Main.cs:566`), nhánh này thôi |
| `ui_ms` | chia đều chi phí `PanelText()`+`RefreshEvents()`+`RefreshLive()` mỗi 15 khung (`game/Main.cs:403-408`); `ui_hit_ms` = một lần bấm |
| `leftover_ms` | `1000/fps − step − fill − draw − ui` — chứa GPU/present/engine **và** `_hud.Text` đặt lại mỗi khung (`game/Main.cs:402`) |

## Bảng ms mỗi khung — mặc định: Sol 5250 vật thể, chạy ×1, Debug, 1920×1057

| phần | ms | % khung 25.6 ms |
|---|---|---|
| `Advance` (`step_ms`) | **8.39 – 8.50** | 33% |
| multimesh fill (`fill_ms`) | 1.39 – 1.42 | 5% |
| `_Draw` (`draw_ms`) | 1.92 – 1.96 | 7% |
| — trong đó cung quỹ đạo (`orbit_ms`, **chỉ 8 cung**) | 0.27 | 1% |
| — trong đó `CountRings` + hành tinh + nhãn + tàu (`body_ms`) | 1.62 | 6% |
| panel/UI mỗi 15 khung (`ui_ms`) | **0.004** (một lần bấm: 0.01 – 0.04) | 0% |
| còn lại (`leftover_ms`) = chờ/GPU/present | **13.3** | 52% |
| **TỔNG** | **25.6 (fps 39.0 – 39.1)** | |

## Bảng công tắc — mỗi lần một công tắc, cùng build, 8 s

| cấu hình | fps | step | fill | draw | ui | leftover | vật thể |
|---|---|---|---|---|---|---|---|
| mặc định chạy ×1 | 39.0 | 8.39 | 1.39 | 1.92 | 0.004 | 13.32 | 5250 |
| **TẠM DỪNG (Space)** | **39.1** | **0.00** | 1.41 | 1.96 | 0.004 | 22.26 | 5250 |
| dừng, tắt quỹ đạo (`--norbit`) | 39.1 | 0.00 | 1.41 | 1.77 | 0.004 | 21.83 | 5250 (`arcs=0`) |
| dừng, ẩn HUD + bảng + GodUi (`--noui`) | 39.1 | 0.00 | 1.41 | 1.99 | 0.004 | 21.59 | 5250 |
| dừng, bật overlay vùng Hill (Z) (`--zones`) | 39.1 | 0.00 | 1.34 | 2.30 | 0.003 | 21.35 | 5250 |
| dừng, cửa sổ 640×400 (pixel ít hơn ~7×) | 39.0 | 0.00 | 2.07 | 2.88 | 0.004 | 20.05 | 5250 |
| dừng, không đá (`--rocks=0`) | 39.1 | 0.00 | 0.03 | 0.48 | 0.006 | 24.49 | 10 |
| dừng, `--rocks=200` | 39.1 | 0.00 | 0.11 | 0.58 | 0.003 | 24.30 | 310 |
| dừng, `--rocks=2000` | 39.1 | 0.00 | 0.57 | 1.09 | 0.004 | 23.34 | 2250 |
| dừng, `--norbit --rocks=0` (+ `--noui` không cần) | 39.1 | 0.00 | 0.03 | **0.24** | 0.005 | 24.73 | 10 |
| dừng, renderer ANGLE (`--rendering-driver opengl3_angle`) | 39.1 | 0.00 | 1.35 | 1.96 | 0.003 | 21.69 | 5250 |
| dừng, Vulkan (`--rendering-method forward_plus --rendering-driver vulkan`) | 39.1 | 0.00 | 1.35 | 1.89 | 0.003 | 21.75 | 5250 |
| dừng, Vulkan + `--rocks=0` | 39.1 | 0.00 | 0.03 | 0.48 | 0.006 | 24.48 | 10 |

⇒ **fps = 39.0 – 39.1 ở MỌI cấu hình.** Chênh lệch lớn nhất giữa các công tắc: **0.1 fps**.

## Kiểm chứng quyết định — bỏ hẳn cửa sổ (headless, cùng build)

| | fps | step | fill | draw | leftover |
|---|---|---|---|---|---|
| headless chạy ×1 | **83.5** | 8.41 | 1.33 | 1.79 | 0.23 |
| headless TẠM DỪNG | **140.9** | 0.00 | 1.53 | 1.96 | 3.40 |

## Đối chứng build "zin" (không có Stopwatch em thêm, `E:\Cosmos-selica` @ 50220da)

| | fps | step | fill |
|---|---|---|---|
| chạy ×1, có cửa sổ | 39.0 | 8.17 | 1.40 |
| chạy ×1, có cửa sổ (lần 2) | 39.0 | 8.13 | 1.37 |
| headless chạy ×1 | **86.1** | 8.09 | 1.38 |

⇒ Trần 39 không do đồng hồ em thêm, và 8.4 ms `Advance` là chi phí thật của code zin.

## Kết luận

1. ⚠️ **Trần ~39 fps KHÔNG nằm trong code game.** Cùng một build: có cửa sổ 39.0, headless 83.5–86.1.
2. ⚠️ **Không công tắc nào trong app dịch fps**: dừng/chạy, 10 vật thể/5250, 640×400/1920×1057, tắt UI, tắt quỹ đạo,
   GL/ANGLE/Vulkan — tất cả 39.0–39.1. Nghĩa là 25.6 ms/khung không phải chi phí tính toán.
3. ✅ **`Advance` là chi phí CPU thật duy nhất: 8.4 ms/khung (33%)** — `Sub = 8` sub-step × các vòng O(N)
   (`core/World.cs:29`, `:264-331`). Khi trần present được gỡ, đây là thứ chặn tiếp theo.
4. ✅ Danh sách nghi ngờ trong SPEC đều **nhỏ**: fill 1.4 ms, `_Draw` 1.9 ms, panel 15 khung 0.004 ms ⇒ 13% khung.
   `arcs=8`, `segs=1024` = đúng 8 hành tinh — **đá không có cha** nên không vẽ cung nào
   (`core/World.cs:243`, `:251` đặt `Par = -1`), không phải "vài chục lệnh vẽ" mà cũng không phải 5240 cung.
5. ❌ **Em KHÔNG xác định được ~14 ms chờ là gì.** Loại trừ được: pixel (640×400 y hệt), số instance (10 vật thể y hệt),
   renderer (GL/ANGLE/Vulkan y hệt), CPU advance (dừng y hệt), cap trong code (`MaxFps=0`, `vsync=0`).
   Còn lại là đường present của cửa sổ trên máy này — `[SUY ĐOÁN]`: màn hình đang do Intel UHD giữ (144 Hz) nên
   khung NVIDIA phải qua đường copy/compose của Optimus, cộng overlay GeForce Experience.
6. ⚠️ Cần anh Yang đo trên máy (headless không đo được GPU/present):
   (a) NVIDIA Control Panel → Max Frame Rate / V-Sync / Whisper Mode cho profile Godot, và tắt overlay GFE → chạy lại
   `Godot_..._console.exe --path <worktree>\game -- --bench=8`;
   (b) Windows Settings → Graphics → chọn GPU "Power saving (Intel)" cho exe Godot rồi đo lại — nếu fps nhảy thì thủ phạm là đường Optimus;
   (c) nếu TUF có MUX switch: thử chế độ "dGPU only".

## Đề xuất sửa — 1 dòng

**Đừng sửa game để lấy fps: 40 fps là trần đường present của máy này — kiểm tra cap/đường GPU ngoài app trước;
gỡ xong thì việc tiếp theo là `Advance` 8.4 ms/khung (5000 đá × `Sub=8`).**

## Lệnh chạy lại

```
$g = 'E:\Godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'
& $g --path E:\Cosmos-fps\game -- --bench=8                    # mặc định, chạy ×1
& $g --path E:\Cosmos-fps\game -- --bench=8 --paused           # TẠM DỪNG
& $g --path E:\Cosmos-fps\game -- --bench=8 --paused --norbit --rocks=0 --noui   # sàn
& $g --path E:\Cosmos-fps\game --headless -- --bench=8         # không cửa sổ (đối chứng)
```

Log thô: `E:\selica-work\fps-1-baseline.txt`, `fps-sweep.txt`, `fps-followup.txt`, `fps-final.txt`, `fps-vanilla.txt`.
Cờ tạm em thêm (nhánh này thôi, master không có): `--paused`, `--norbit`, `--noui`, `--zones`, `--size=WxH`
và dòng `BENCH2` trong `game/Main.cs`.
