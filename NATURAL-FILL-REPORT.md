# Sol tự đầy slot — một vòng sửa Roche

Base `dcad19a669cb481c2af87fc8985568e847e389bf`; branch `celine/roche-natural-fill`, worktree `E:/Cosmos-celine-natural`. Không push, không sửa Disc.cs, không chạy full suite. Commit chứa báo cáo này; lấy hash bằng `git log -1 --format=%H`.

## Ba câu hỏi của Claire

1. **Không phải sai đổi đơn vị.** Event A là khối lượng lõi, không phải Earth mass. Nguồn 9.928843434736446e-9 lõi = 6.619228956490964e-5 Trái Đất, bán kính vật lý 406.482 km. Con 1.5513817866775697e-10 lõi = 1.0342545244517131e-6 Trái Đất, bán kính 101.621 km. Cả hai có độ bền trung bình 2.206 MPa. Vật cỡ hàng trăm km vẫn có thể bị thủy triều xé khi tiến sâu hơn; không có miễn nhiễm chỉ vì khối lượng lõi nhỏ.
2. **Sửa no-reshred đã vào base.** `5cbfe62` là commit báo cáo; source thực là `7c72b87` (chọn con sống được tại vị trí sinh) và `672b81d` (giới hạn công việc/bước). Cả hai là ancestor của dcad19a và master eb96d34. Sửa cũ bảo đảm ổn định tại nơi sinh; stream giữ vận tốc mẹ rồi đi xuống cận điểm nhỏ hơn, lại vượt Roche của chính nó. Đây là lỗ hổng ở phạm vi quỹ đạo được kiểm, không phải chưa merge.
3. **Event gắn host là quy ước hiện hữu.** LogEvent(p,...) để đưa sự kiện vào nhật ký host. `RocheDisruptions` lưu Source/SourceGeneration/Host/HostGeneration độc lập; harness đọc record này để không nhầm Hải Vương là vật bị xé. Không đổi quy ước event.

Đối chứng: master eb96d34 cũng đầy đúng bước5163. Sau6000 bước: live/max5512,110 sự kiện, hash5103AB9EBECF0E0B. Base đĩa dcad19a: đầy5163,94 sự kiện, hash0E14BDDC9EADC3D0. Chỉ thêm harness, không sửa core bản đối chứng.

## Sửa

`core/Roche.cs:375`, BreakRoche: stream dùng cận điểm bảo thủ trên toàn bề rộng dòng thay cho bán kính entry khi đảo luật độ bền để chọn khối lượng con. Bound dưới của |h| và bound trên của cơ năng bao phủ mọi offset ngang được sinh. Con phải sống được ở cận điểm, kể cả khoảng cách bề mặt của host. Nếu không biểu diễn được con ổn định ngoài host, giữ phần bắt được trong bound reservoir hiện hữu.

`core/Roche.cs:388`: stream dùng số cặp nhỏ nhất đủ để mỗi con không vượt khối lượng ổn định tính từ độ bền. Không tự động chia thành64 khi vài mảnh lớn đã đủ ổn định. Vẫn giữ ngân sách biểu diễn64 hiện hữu; vật chất vượt khả năng biểu diễn vào reservoir. Không thêm trần mảnh/hằng vật lý mới, không đổi RocheLimit, không tăng strength, không miễn nhiễm theo ancestry. Ring và nhánh DepositDisc giữ cách xử lý cũ.

Nguồn của luật độ bền đang dùng: [Aggarwal & Oberbeck 1974, NASA](https://ntrs.nasa.gov/citations/19740055720). Luật hiện tại là macro proxy có phụ thuộc kích thước/độ bền, không phải nghiệm đàn hồi đầy đủ của bài báo. Cận điểm là quỹ đạo hai vật tại thời điểm sinh; lực kéo thứ ba về sau có thể đổi quỹ đạo, nên không tuyên bố con được miễn xé vĩnh viễn.

## Nghiệm thu

Đã chạy đủ60000 và hai seed còn lại. Log mỗi1000 bước và mọi sự kiện của seed1234 nằm trong `evidence/natural/final60000-seed1234.txt`; prefix20000 của cùng trajectory là kết quả seed1234 cho bảng3seed. Các tiến trình chạy tuần tự trên laptop, có perfbox lock; không chạy benchmark/fuzz song song.

| Mục | Kết quả |
|---|---|
| a, 6000/60000 seed1234 | 6000: live/max5312≤5314,2 sự kiện, PASS mốc ngắn. 60000: live5507/max5512,18 sự kiện, đầy27814 (~51.85 năm), hashCE444EB00568A1FF. KHÔNG chứng minh hết lỗi đầy về lâu dài |
| b, seed1234/1002/1011 ×20000 | 1234 max5374/live5373/4events/hashDDA9EBAA3FB6F34E;1002 max5444/live5444/11events/hash29895614FB6692B1;1011 max5322/live5317/7events/hash70040D390D69F5CE. Không seed nào đầy trong20000 |
| c, disc-p0 1000 | Before ON live4604, swallowed118E; OFF live5512. After ON vẫn4604/swallowed118E; OFF vẫn5512, stream897→646, merge460→398; KHÔNG tuyên bố hết đầy khi đĩa tắt |
| d, disc/elements/conserve/ns-drop-check | Tất cả PASS; disc16/0, stock hash3EC8933C94BF05EB;11elements hash giữ nguyên;NSdrop837217C5716FA034 và natural97950F09996305B3 giữ nguyên/replayexact;conserve caveat momentum cũ giữ nguyên |
| e, vật lớn | Earth-mass quanh NS vẫn bị xé: RocheLimit8.54367426510364, separation8.458237522452603,1 sự kiện |

`natural-check`: stream fixture sinh64 con; tất cả sống được tại cận điểm riêng tính từ vị trí/vận tốc thực sau sinh;1000 bước nhỏ không sinh thêm sự kiện. Đây là kiểm tra vật lý độc lập với log dài. Release CLI/core build0warnings/errors.

Thêm kiểm tra focused `cli -- roche`: không FAILED. Không full suite, không sửa expected hash. Hash mới khác ở trajectory bị tác động là đúng: base seed1234 bước6000 `0E14BDDC9EADC3D0`→`94F6150176A46C4E` vì tránh các lần phá vỡ con sau entry, số sự kiện94→2 và live5512→5312. Bước1000/2000/3000/4000/5000 vẫn hash cũ tương ứng, nên không có đổi trạng thái trước stream cần sửa.

Live seed1234 mỗi1000 bước đến6000: **5250,5250,5250,5249,5249,5312**. Nhật ký có đủ18 sự kiện của60000, không chỉ trích các sự kiện thuận lợi. Năm60000=111.84269356539613.

**Kết quả xấu giữ nguyên:** bản trung gian chỉ sửa cận điểm, vẫn luôn sinh64:6000max5312 nhưng60000 vẫn đầy27814, cuối5507/max5512/18events. Log `fix60000-seed1234.txt` được giữ. Kết quả này là lý do bổ sung cách chọn số mảnh bằng khối lượng ổn định, không chạy lại cùng code để tìm số đẹp.

**Bổ sung số mảnh không làm ca seed1234 tốt hơn:** tại các entry đó cần hơn64 mảnh nhỏ để sống ở cận điểm, nên ngân sách hiện hữu vẫn đầy. Cả bản cuối và bản trung gian có cùng hashCE444EB00568A1FF tại60000. Giữ nguyên sự thật này; không nhận là đã khắc phục vĩnh viễn hết capacity. Sau khoảng100 năm có con generation2 bị xé lại; chưa tách lực kéo thứ ba khỏi sai số tích phân ở ca muộn đó. Không quy toàn bộ tăng trưởng muộn là lỗi con bất ổn ngay khi sinh, cũng không khẳng định toàn bộ ca muộn là vật lý đúng.

## Giới hạn

- Dung lượng object vẫn hữu hạn5512; sửa vật lý không chứng minh vũ trụ sẽ không bao giờ đầy. Nghiệm thu chỉ cho thời gian/seed đã đo.
- Bound reservoir là biểu diễn vật chất chưa resolve đã có từ trước; chưa mô phỏng đầy đủ va chạm/hạ cánh của đám mây đó. Không biến reservoir thành escaped mass để xóa vật chất.
- Không chỉnh Disc.cs hoặc hiệu ứng/game UI. GW vẫn treo; Hark round2 chưa review trong lượt P0 ưu tiên này.

## Lệnh và đường dẫn

`dotnet build cli/Cosmos.Cli.csproj -c Release --nologo`; `dotnet cli/bin/Release/net8.0/Cosmos.Cli.dll natural-check`; `... natural-fill 1234 60000 detail`; `... natural-fill 1002 20000`; `... natural-fill 1011 20000`; `... disc-p0 1000`; `... disc`; `... elements`; `... elements-probe`; `... conserve`; `... ns-drop-check`; `... roche`.

Harness `cli/NaturalFillChecks.cs:7` kiểm vật lý, `:47` chạy dài, in source/host từ record. Log `evidence/natural/base*`, `master6000.txt`, `physics.txt`, `final*`, `after*`, và thất bại trung gian `fix60000-seed1234.txt` đều giữ trong commit. Bản master độc lập nằm ở `E:/Temp/cosmos-natural-master` với core pristine eb96d34, chỉ thêm harness. Không đụng checkout chính đang có công việc Claire.
