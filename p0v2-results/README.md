# P0 vòng 2 — kiểm định phép đo (Hark, issue #2)

Chỉ đo. Không sửa `core/`, không thêm hook core. Gốc: `8f5ccbb` (nhánh `hark/p0-probe`). Mã đo mới: `cli/P0ProbeV2.cs`, đăng ký lệnh ở `cli/Program.cs` (2 dòng). Harness cũ `cli/P0Probe.cs` giữ nguyên để làm đối chứng.

## Cấu hình máy và lệnh

- Máy: sandbox Hark, 2 vCPU ARM64 (Neoverse-V2), 7 GB RAM, .NET SDK 8.0.425, `dotnet build cli -c Release`.
- `P0_THREADS=1` cho mọi lần chạy. Cảnh giống hệt vòng 1: `SolSystem(5000, seed)`, 96×`Advance(.5)`, NS 1.4 Msun tại X=100 (cli/P0Probe.cs:28-36 @8f5ccbb, chép nguyên vào `P0ProbeV2.Scene`).
- Seed 1234, 1002, 1011; 2000 bước sau khi thả NS; mốc so sánh mỗi 50 bước.

```
# harness cũ (build từ 8f5ccbb, không sửa)
P0_THREADS=1 dotnet Cosmos.Cli.dll p0-probe    <seed> 2000 1000 old 50000
# đối chứng: cùng cảnh, chỉ Advance + đọc mốc (Hash, Live, N, Merges, số Roche, reservoir, EscapedMass, RocheDissipatedEnergy, M/Gen của slot NS)
P0_THREADS=1 dotnet Cosmos.Cli.dll p0-control  <seed> 2000 50 control
# harness mới
P0_THREADS=1 dotnet Cosmos.Cli.dll p0-probe-v2 <seed> 2000 50 new
python3 compare.py      # -> compare.txt
python3 analyze_v2.py   # -> analysis.json
python3 round1_recheck.py <thư mục raw vòng 1>   # -> round1_recheck.txt
```

## Tệp

- `old/` — đầu ra harness cũ (seedN.csv, seedN-peri.csv, seedN-summary.txt).
- `control/seedN-checkpoints.csv` — đối chứng không đọc diagnostic.
- `new/seedN-checkpoints.csv` — cùng cột với control, so được byte-với-byte.
- `new/seedN-steps.csv` — mỗi bước: khối lượng/đếm trực tiếp, khép kín khối lượng theo bước, chế độ quy về vật đặc, tiếp cận quan sát.
- `new/seedN-ledger.csv` — sổ khối lượng vành mỗi 50 bước, từng hạng mục có nhãn lớp số liệu.
- `new/seedN-q.csv` — q suy ra (ước lượng hai vật) tách khỏi tiếp cận quan sát.
- `new/seedN-summary.txt` — tóm tắt + danh sách hook còn thiếu.

## Lớp số liệu (hậu tố cột)

- `_direct`: đọc thẳng từ trạng thái/bản ghi core (mảng World, `Merges`, `RocheDisruptions`, `RocheReservoirs`, `EscapedMass`, `Events`) hoặc tổng chính xác của chúng.
- `_closure`: quy bằng khép kín khối lượng của các số trực tiếp trong một bước, chỉ khi lời giải duy nhất và sai số trong ngưỡng làm tròn (64·(n+2)·ulp của khối lượng nhận).
- `_est`: ước lượng (q hai vật, heuristic bề mặt gần nhất của vòng 1, q tại lần qua cận điểm giữa hai mốc).
- `unknown`: không quan sát được qua API công khai ở độ phân giải bước; summary ghi hook cần có.

Vật chất được theo dõi theo (slot, Gen), không theo slot.

## Hook còn thiếu (không thêm ở vòng này)

1. Đích merge từng đá: `Merge` (core/World.cs:209-232) tăng `Merges` nhưng chỉ ghi Events cho nạn nhân ≥ AttractMass (core/World.cs:225).
2. Vị trí trong 8 substep (core/World.cs:30, :307): tiếp cận gần nhất trong một bước không quan sát được.
3. Mảnh sinh và chết trong cùng một bước: `Add` trong BreakRoche (core/Roche.cs:401) rồi Merge/Kill cùng Advance không để lại bản ghi; `RocheDisruption` không có số/khối lượng mảnh (core/Roche.cs:33-34).
4. Kill vs merge của đá: `Kill` (core/Commands.cs:155-159) sau `EscapeRole` (core/Escape.cs:45) không ghi nguồn vào EscapedTransfers.
5. Reservoir không có id sự kiện (core/Roche.cs:31-32, :296-300): ghép với RocheDisruption theo thứ tự thêm vào (0 lần ghép lỗi trong 3 seed).
