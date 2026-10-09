# Vòng 3 — Đối chứng nguyên nhân P0 bằng bật/tắt Roche

Nhánh: `mesu/p0-roche-ab` (chỉ `cli/` + tài liệu/evidence; không đổi core/vật lý).
Commit core: `eb96d34`. .NET 8.0.425 Release, `Threads=1`.
Harness: `cli/P0RocheAB.cs` (lệnh `p0rocheab`), đấu nối ở `cli/Program.cs`.

## Thiết kế

- Seed 1234, 1002, 1011. Mỗi seed dựng 3 bản từ cùng `SolSystem(5000, seed)` + 96×`Advance(.5)`:
  - **A**: không thả NS, Roche bật — đối chứng nền.
  - **B**: thả NS đúng `cli/NsDropChecks.cs:Drop`, Roche bật.
  - **C**: thả NS y hệt B, rồi gửi `Command(CmdKind.SetRule, Name:"roche", Amount:0)`
    trước Advance kế tiếp; đã xác minh nhánh C không thêm disruption nào
    (đếm disruptions trước/sau 2000 bước bằng nhau ở cả 3 seed).
- Mỗi bản chạy 2000×`Advance(.5)`. Ghi mọi bước trong 300 bước đầu, sau đó mỗi
  50 bước; kiểm tra chạm trần (định nghĩa: `Live == X.Length`, không còn slot
  trống) ở **mọi** bước.
- Đối chứng phụ: seed1234/B chạy lại 2000 bước không thu thập diagnostic trong
  loop — Hash và counters cuối (Live, N, Merges, disruptions, reservoirs,
  EscapedMass) khớp bản B có đo. Không lệch.

## Bảng A/B/C từng seed

| seed | bản | chạm trần (bước) | Live max/cuối | Merges | disruptions | mảnh Roche sinh ra* | reservoir cuối |
|---|---|---|---|---|---|---|---|
| 1234 | A | không | 5250 / 5250 | 0 | 0 | 0 | 0 |
| 1234 | B | **bước 108** | 5512 / 5506 | 471 | 1520 | **2246** | 1040 |
| 1234 | C | không | 5251 / 4812 | 439 | 0 | 0 | 0 |
| 1002 | A | không | 5250 / 5250 | 0 | 0 | 0 | 0 |
| 1002 | B | **bước 108** | 5512 / 5319 | 595 | 1535 | **2198** | 1065 |
| 1002 | C | không | 5251 / 4838 | 413 | 0 | 0 | 0 |
| 1011 | A | không | 5250 / 5250 | 0 | 0 | 0 | 0 |
| 1011 | B | **bước 107** | 5512 / 5077 | 382 | 1573 | **2210** | 1101 |
| 1011 | C | không | 5251 / 5077 | 174 | 0 | 0 | 0 |

\* Số mảnh sinh ra tính chính xác từ sổ: `mảnh = ΔLive + merges + disruptions`
(vì trong 2000 bước, sinh chỉ có mảnh Roche, tử chỉ có merge và nạn nhân
disruption). Đếm từ event log chỉ là cận dưới (đã kiểm chứng thiếu do bộ đệm
1024 sự kiện tràn ở các bản B) nên không dùng.

Sức chứa: `X.Length` = 5512 = 5000 đá + 512 dự phòng. Bản A đứng yên 5250.
Bản B chạm 5512 ở bước ~107–108 trên cả 3 seed rồi dao động quanh trần
(merge giải phóng slot, Roche lấp lại ngay).

## Trả lời cho Yang

**Roche đóng góp bao nhiêu?** Hầu hết toàn bộ. Sau khi thả sao neutron,
mỗi bản B có ~1520–1573 disruption Roche sinh ra ~2200 mảnh, lấp đầy 512 slot
dự phòng chỉ sau ~107 bước. Tắt Roche (bản C) thì disruptions = 0,
mảnh sinh = 0.

**Tắt riêng Roche có tránh được chạm trần không?** Có, dứt khoát trên cả 3
seed trong cùng 2000 bước: bản C không chạm trần lần nào (Live max 5251 =
5250 + 1 NS). Ngược lại, không có Roche thì số vật thể còn **giảm** dần
(5251 → ~4800–5080) vì merge giết vật mà không có mảnh mới bù vào.

**Vật thể đặc có hút vật chất không?** Không ghi nhận vụ nào: số merge có vật
sống sót là NS/BH bằng 0 ở cả 9 bản (chỉ đếm các vụ merge đáng kể được ghi
log; hạt bụi merge thầm lặng không ghi — nhưng khối lượng NS cuối ở B và C
giống hệt nhau, Gen không đổi, nên không có hút đáng kể). Các merge ở bản C
(174–439 vụ) diễn ra giữa các vật thể khác, không liên quan NS — đúng triệu
chứng P0 thứ nhất (vật đặc không nuốt vật chất). Khối lượng NS 70.005 →
120.005 kèm chuyển pha NeutronStar → BlackHole diễn ra giống hệt ở B và C,
là tiến hóa sao nội tại, không liên quan bật/tắt Roche.

## Giải thích chênh B/C (đọc code)

`core/Roche.cs`: mỗi disruption giết vật nguồn (`Kill(i)`) rồi sinh `count`
mảnh bằng `Add(...)`, với

```
count = min(RocheFragments, X.Length - Live + 1, RocheRockBudget - small)  // chẵn hóa
```

`+1` là slot vừa giải phóng của chính vật bị xé. Khi slot gần đầy, `count`
co lại nhưng vật nguồn vẫn chết — đây chính là "nhánh sinh mảnh khi slot gần
đầy". Tắt rule `roche` (`SetRule` Amount 0 → `rule.Enabled = false` →
`RocheEnabled` false ở `core/Roche.cs:55`) thì không có disruption nào, không
có mảnh nào — đã xác minh bằng đếm.

## Sổ khối lượng

`sum(M vật sống) + sum(M reservoir) + EscapedMass` trước/sau 2000 bước:
residual tuyệt đối ~1e-13, tương đối ~1e-15 ở cả 9 bản — bảo toàn tốt,
không phải sổ năng lượng, không suy ra phần vành bị BH nuốt.

## Ghi chú

- Thời gian chạy trên cloud chỉ tham khảo (mỗi bản 10–21 giây); không nới gate.
- Không chạy triệu bước hay full suite như yêu cầu. Không tự mở đợt chạy mới.
- Dữ liệu thiếu: không có — đủ 9 bản + đối chứng phụ.

## File

- `p0-roche-ab-<seed>-<variant>.csv` — mẫu đo từng bước (9 file).
- `summary.csv` — tóm tắt mỗi bản.
- `run.log` — log console 9 bản + đối chứng phụ.
- `cli/P0RocheAB.cs` — harness (đã ở nhánh này).
