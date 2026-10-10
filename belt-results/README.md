# Vòng 4 — belt-probe (Hark, issue #2)

Nhánh `hark/belt-probe`, gốc `claire/heat-disc` @ `bd2a966`. `core/` và `game/` không đổi (`git diff bd2a966 HEAD -- core game` rỗng).
Harness: `cli/BeltProbe.cs` + 1 dòng trong `cli/Program.cs`. Chỉ đọc state công khai giữa các lần gọi.

Tái hiện (sau `dotnet build -c Release cli`):

    BELT_THREADS=1 dotnet cli/bin/Release/net8.0/Cosmos.Cli.dll belt-probe belt-results

Máy ghi kết quả: sandbox 2 vCPU ARM64, .NET 8.0.425, Release, `BELT_THREADS=1`. Thời gian chạy chỉ so tương đối.
Cảnh: `World.SolSystem(5000, seed)`, seed 1234 / 1002 / 1011 (capacity 5512), `FoldBelt(sun,1,40)` rồi `FoldBelt(sun,2,40)` như `game/Main.cs:145`.
Sổ = khối lượng (vật + đĩa + reservoir + escaped), từng nguyên tố, động lượng (đĩa đi cùng vận tốc host), mô-men động lượng quanh gốc tọa độ
(đĩa: `DiscMassOn` ở vị trí host + `DiscAngular`), và mô-men khối lượng (CoM). Residual = sau − trước.

Files
- `console.txt` — toàn bộ log chạy.
- `item1-fold-ledger.csv` — sổ trước/sau từng `FoldBelt`, và gọi lặp lại.
- `item2-withdraw-cases.csv` — 8 ca rút x 3 seed, sổ + kiểm tra vị trí/vận tốc/chiều quay từng vật mới. Ca g1..g5 (tham số xấu) chỉ in trong `console.txt`.
- `item3-repeat100.csv` — 100 lần gọi liên tiếp cùng chỗ (2 chỗ x 3 seed).
- `item4-fate.csv`, `item4-samples.csv` — 2000 x `Advance(.5)` (= 3,728 năm trong thế giới) sau khi rút; `baseline` = 5000 đá vành đai chưa gộp; `control` = chỉ gộp, không rút.
