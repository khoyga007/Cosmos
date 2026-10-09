# Bằng chứng soak test 50 lượt — vòng 2 (chị Celine giao)

Nhánh: `mesu/soak-evidence` (chỉ tài liệu/dữ liệu, không chạm master).
Commit chạy: `eb96d34` — "NEXT: P0 second symptom, world full at fixed capacity blocks creation".
Ngày chạy: 2026-10-08 18:56 UTC → 2026-10-09 01:54 UTC (~7.5h).

## File trong nhánh này

- `soak-50runs.csv` — một hàng một lượt: run id, commit, host/session, số OK/FAILED,
  exit code, thời gian suite (s), kết quả gate NS (OK/FAILED), median7 (ms),
  tỉ số paired after/before, 7 giá trị after từng cặp (ms), hash từng cặp,
  kết quả + hash bài ns-drop 200-step, kết quả + overhead bài rules 5000-rock,
  kết quả bài elements.
- `logs/run-01.log` … `logs/run-50.log` — log thô từng lượt (3.6 MB).
- `EVIDENCE.md` — file này.

Số liệu trong CSV trích bằng script từ log thô, không gõ tay.

## Host/session

Log không ghi hostname. Ranh giới duy nhất xác minh được: khoảng trống ~40 phút
giữa run-06 (xong 19:44 UTC) và run-07 (xong 20:24 UTC), kèm chế độ thời gian
đổi hẳn (suite ~540s → ~390s, NS median ~11.9ms → ~8.5ms).

- `session-1` = runs 01–06 (suy luận từ chế độ thời gian, không phải hostname đã ghi)
- `session-2` = runs 07–50 (suy luận tương tự)
- Run 26 là outlier đơn lẻ trong session-2 (11.137ms giữa các lượt ~8.5ms) —
  giống nhiễu hàng xóm trên cloud, không phải đổi host.

## Thống kê (ms, %)

Công thức p95: nearest-rank — sắp xếp tăng dần, lấy giá trị ở hạng
ceil(0.95 × n) (đánh số từ 1).

| Phạm vi | ns median7: min / median / p95 / max | gate đạt | rules overhead: min / median / p95 / max | rules đạt |
|---|---|---|---|---|
| Toàn bộ 50 lượt | 7.255 / 8.592 / 12.045 / 13.688 | 6/50 | −10.47 / 0.38 / 5.59 / 15.80 | 48/50 |
| session-1 (01–06) | 11.022 / 11.869 / 13.688 / 13.688 | 0/6 | −4.87 / 1.61 / 15.80 / 15.80 | 5/6 |
| session-2 (07–50) | 7.255 / 8.527 / 9.107 / 11.137 | 6/44 | −10.47 / 0.34 / 5.07 / 11.29 | 43/44 |

Thêm: thời gian suite min 387 / median 391 / max 561 s; tỉ số paired
after/before min 1.914 / median 2.360 / max 2.592.

## Tách lỗi cố định khỏi timing

- **Cố định, fail 50/50 (không phải timing):**
  - `elements`: eleven C6 default-table regression hashes match the recorded capture
  - `ns-drop` 200-step counts: live5387, disruptions1312, reservoirs855,
    hash `3A398B489488C0F9` — giống hệt cả 50 lượt
- **Flaky, chỉ ở số đo thời gian:**
  - gate ns-drop rocks5000 median7 (≤8ms): đạt 6/50
  - trần overhead rules 5000-rock (+10%): vượt 2/50 (run 05: 15.80%, run 41: 11.29%)

Tính đơn định của vật lý: hash từng cặp NS (`A7CD37BE2B01CE25`) và hash
200-step (`3A398B489488C0F9`) giống hệt nhau ở cả 50 lượt — phương sai hoàn
toàn nằm ở đo thời gian.

## "7 cặp đo trong một lượt" và median7 (theo code)

`cli/NsDropChecks.cs`, hàm `Bench(5000)`: mỗi lượt chạy 7 cặp đo. Mỗi cặp:
đo `before` = thời gian trung bình mỗi bước trên 64 bước advance, thả sao
neutron (`Drop(w)`), rồi đo `after` = thời gian trung bình mỗi bước trên
**8 bước** advance; ratio = after/before. Sau 7 cặp, sắp xếp 7 giá trị after,
median7 = `costs[3]` (giá trị thứ 4). Gate: `costs[3] <= 8` ms → đạt.

Điểm đáng chú ý: `after` chỉ đo trên 8 bước, `before` đo trên 64 bước —
phép đo after nhiễu hơn ngay từ thiết kế. Ví dụ run-25, 7 giá trị after:
8.681 / 11.947 / 7.038 / 7.058 / 7.744 / 8.810 / 7.063 ms → median7 = 7.744ms
(đạt). Cùng một lượt mà các cặp đã lệch nhau gần gấp đôi, nên median7 giữa
các lượt dao động là điều tất yếu khi ngưỡng 8ms nằm giữa dải đo.

## Giới hạn kết luận (theo yêu cầu vòng 2)

- Hash lặp lại trong đúng 50 lượt này; chưa chứng minh đơn định trên mọi nền tảng.
- Không gộp việc đổi host thành nhiễu cùng máy: session-1 và session-2 là hai
  chế độ đo khác nhau, thống kê tách riêng ở bảng trên.
- Số đo trên cloud không phải căn cứ để nới ngân sách hiệu năng của laptop Yang.

## Dữ liệu còn thiếu

- Hostname/session thực tế của từng lượt: không được ghi trong log → cột
  `host_session` là suy luận từ chế độ thời gian, đã ghi rõ.
- Ngoài ra không thiếu gì: đủ 50/50 log thô, đủ mọi trường trong CSV.
