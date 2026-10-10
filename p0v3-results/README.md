# P0 vòng 3 — đo P0 trên lõi đĩa, luật bật/tắt (Hark, issue #2)

Chỉ đo. Gốc: `c4598a5` (`claire/heat-disc`). `git diff c4598a5 HEAD -- core game` rỗng.

## Mã

- `cli/P0ProbeV2.cs` — harness v2 (từ `2681c8f`), port sang `c4598a5`. Bản gốc build được nguyên trạng; chỉ đổi:
  1. `Scene`: đọc bắt buộc `P0_DISC` (0/1) và đặt `w.C.DiscOn` **trước** 96 bước khởi động, đúng như `cli/DiscChecks.cs` `P0`. Lý do: core mặc định `DiscOn = 1` (`core/Disc.cs:33`), chạy harness cũ không đổi sẽ lặng lẽ thành bản BẬT.
  2. Thêm cột cuối `hash_dial_neutral` vào checkpoints. `HashDiscs` trộn các nút đĩa vào `Hash()` khi `DiscOn != 1` (`core/Disc.cs:271,273,276`), kể cả khi chưa có đĩa nào, nên bản `DiscOn=0` không bao giờ cho lại hash trước đĩa. Cột này đặt tạm `DiscOn = 1` chỉ trong lúc đọc `Hash()` (đọc thuần) khi chưa có đĩa và `DiscAccretedMass == 0`; `World.Hash` bỏ qua các hằng `Disc*` (`core/World.cs:495`), nên ra đúng công thức hash cũ. Ghi `NA` khi luật đĩa đã có trạng thái.
  3. `Scene`, `CheckpointHeader`, `Checkpoint` chuyển `internal` để harness v3 dùng chung.
- `cli/P0DiscV3.cs` — harness đo vòng 3 (`p0-disc-v3`): đọc trạng thái công khai giữa các `Advance`.
- `cli/Program.cs` — 3 dòng đăng ký lệnh.

## Máy và lệnh

Sandbox Hark, 2 vCPU ARM64, .NET SDK 8.0.425, `dotnet build cli -c Release`. `P0_THREADS=1` mọi lần chạy (trừ `claire-disc-p0/`, xem dưới).

```
P0_THREADS=1 P0_DISC=0 dotnet Cosmos.Cli.dll p0-control  <seed> 2000 50 off-control   # chạy TRƯỚC
P0_THREADS=1 P0_DISC=0 dotnet Cosmos.Cli.dll p0-probe-v2 <seed> 2000 50 off-v2harness
python3 compare_off.py                                                                   # -> compare_off.txt
P0_THREADS=1 P0_DISC=0 dotnet Cosmos.Cli.dll p0-disc-v3  <seed> 2000 off
P0_THREADS=1 P0_DISC=1 dotnet Cosmos.Cli.dll p0-disc-v3  <seed> 2000 on
P0_THREADS=1 P0_DISC=1 dotnet Cosmos.Cli.dll p0-control  <seed> 2000 50 on-control
dotnet Cosmos.Cli.dll disc-p0 1000 > claire-disc-p0/disc-p0-1000.txt                   # lệnh của chị Claire, Threads mặc định
python3 analyze_v3.py                                                                    # -> analysis_v3.json
```
Seed 1234, 1002, 1011.

## Tệp

- `v2-control-ref/` — checkpoints control v2 (chép từ `2681c8f:p0v2-results/control/`) để so.
- `off-control/`, `on-control/` — control (chỉ Advance + checkpoint).
- `off-v2harness/` — harness v2 chạy bản tắt; `steps/ledger/q.csv` so byte-với-byte với `2681c8f:p0v2-results/new/`.
- `off/`, `on/` — harness v3, mỗi seed:
  - `seedN-steps.csv` — mỗi bước 1..300, rồi mỗi 50 bước.
  - `seedN-records.csv` — mọi `RocheDisruption` (formation, host, nguồn gốc vật bị xé, khối lượng, năng lượng xả).
  - `seedN-near.csv` — danh sách vật trong bán kính Roche lớn nhất quanh sao đặc, mỗi 50 bước, kèm lý do.
  - `seedN-ledger.csv` — sổ khối lượng/nguyên tố/động lượng, residual tương đối.
  - `seedN-checkpoints.csv` — cùng cột control; trùng byte với `*-control/`.
  - `seedN-summary.txt`.

## Lớp số liệu (hậu tố cột)

- `_direct`: đọc thẳng từ core (`Live`, `Merges`, `RocheDisruptions`, `Discs`, `DiscAccretedMass`, `RocheReservoirs`, `EscapedMass`, `Events`) hoặc tổng chính xác.
- `_closure`: suy bằng khép kín khối lượng trong một bước (chỉ khi lời giải duy nhất):
  - khối lượng nạn nhân vào sao đặc = dM(sao đặc) − phần bồi tụ từ đĩa của nó (ΔFed − ΔTotal) − phần rơi thẳng;
  - số merge vào sao đặc = số vật chết trong bước (không phải nguồn Roche, không escape) khi tổng khối lượng của chúng khớp khối lượng nạn nhân;
  - rơi thẳng = khối lượng bản ghi `disc` − ΔFed của các đĩa (quỹ đạo tròn nằm trong sao chủ, `core/Disc.cs:143-148`);
  - nguồn gốc của vật bị xé mà vừa sinh vừa vỡ trong cùng bước: gán nếu mọi nguồn trước đó trong bước cùng một loại.
- `_est`: công thức trên trạng thái ở **ranh giới bước** (core xé ở substep, API không cho thấy): năng lượng riêng so với sao đặc (tính cả khối lượng đĩa), năng lượng thừa so với quỹ đạo tròn cùng mô-men (cùng biểu thức `core/Roche.cs:367-370`), chia cho `C.DiscVaporEnergy × m`.
- `unknown`: không quan sát được.

Nguồn gốc theo `Grp`/tên trong `World.SolSystem` (`core/World.cs:250-296`): `belt_rock` = Grp 1 (vành đai tiểu hành tinh), `kuiper_ice` = Grp 2 (Kuiper), `saturn_ring_ice` = Grp 3, `moon` = "Mặt Trăng", `planet` = Grp 0 có hút, `sun`, `compact`. Mảnh Roche thừa hưởng nguồn gốc vật mẹ qua tên nhóm `roche.<slot>.<gen>` (`core/Roche.cs:395`).

Sổ khối lượng: sống + đĩa + reservoir + escaped. `DiscAccretedMass` đã nằm trong `M` của sao chủ (`core/Disc.cs:116-122` `AccreteDisc`), nên không cộng thêm (cộng sẽ đếm hai lần). Đĩa đóng băng (Host = −1) ghi riêng; không gặp trong các lần chạy này.

## Sửa harness trong lúc làm (ghi rõ)

Lần chạy đầu, cột `compact_merges_events_direct` lọc sự kiện theo `Year > năm đầu bước`, nhưng merge ghi sự kiện ở `Year` = đầu bước (`core/World.cs:323,328`), nên đếm ra 0. Đã sửa (định vị sự kiện cuối trước bước theo giá trị, vì log có trần 1024 ở `core/Rules.cs:69,124`) và chạy lại cả 6 lần `p0-disc-v3`. Hash bước 2000 trước và sau khi sửa giống nhau ở cả 6 lần (harness chỉ đọc); checkpoint của harness trùng byte với control ở cả 6 lần.
