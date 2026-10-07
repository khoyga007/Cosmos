# C5 checkpoint — chưa sửa code

> Tài liệu lịch sử STEP0. ACK của Claire lúc 17:36:10Z đã thay thế đề xuất packet, mass-only HN, NS radius20km và hard GRB range bên dưới. Bản thực hiện và số đo cuối ở `C5-REPORT.md`.

Base `3ebe7cc`, branch `celine/cosmic-events`. Gửi Claire duyệt trước implementation.

## A. Dòng vật chất hiện tại

- `core/StarEvents.cs:50`: `star.nova` chỉ blast + log, không có Ejection/Mix.
- `core/StarEvents.cs:51-53`: các remnant row gọi Eject qua `:121`.
- `core/StarEvents.cs:135-143`: toàn bộ expelled mass, momentum và từng cell đi vào `StellarEjecta*`; không tạo vật thể. Mix là chuyển hóa phần expelled, không trừ một reservoir khác.
- `core/Stars.cs:70-73,360-361`: ledger được lưu/hash. `cli/ConserveChecks.cs` cộng ledger này với live bodies.
- `core/Kinds.cs:202-203`: gas bị ScaleMatter rồi M giảm, không ledger.
- `core/Kinds.cs:260-274`: ice bị LoseMatter, phần rất nhỏ còn lại có thể bị xóa sau khi đã tính newM; không ledger.

Đề xuất: giữ StellarEjecta* cho thống kê tương thích; thêm một shared escaped ledger cho tất cả volatile và phần ejecta không materialize, conserve chỉ cộng shared ledger (không cộng hai lần stellar statistics). Mỗi transfer ghi đúng từng cell và bulk momentum tại thời điểm rời body. Mix tạo signed nucleosynthesis delta: từng nguyên tố không bất biến qua phản ứng, nhưng tổng khối lượng đóng. CLI kiểm per-element với delta được công bố, và kiểm trực tiếp từng cell trên transfer không chuyển hóa.

Ejecta materialized theo các cặp đối xứng, vận tốc bulk +/- radial, thứ tự cố định. Số packet và mass cap là Consts[P]; cap dưới brown/stellar threshold. Phần vượt budget/cap hoặc hết slot vào shared ledger. Đây là vật chất thật được biểu diễn một phần, không tracer vô khối lượng. Lý do cap: `World.Add/Stars.ResetStars/StarPhaseOf` hiện nhận bất kỳ packet nặng nào thành sao, kể cả metal/radio; tránh thêm stored StarKind hoặc hàng nghìn object chỉ để chia hết ejecta. Báo cả represented/unrepresented mass trong kiểm thử.

## B. Các ngưỡng và giới hạn vật lý

Tất cả tuning mới là Consts[P]; số dưới đây là đề xuất mô hình, không coi là ngưỡng phổ quát.

- **SN**: giữ `star.nova`; birth >=8 và <25 solar. Giữ remnant recipes hiện tại. Ejection + Mix ở một row thực thi duy nhất để tránh eject hai lần. Sáu nhóm có sẵn; rock đại diện O/Si, metal Fe-group, radio Ni56. Yield fractions là coarse grouped model, không isotopic network. [Nomoto et al. 2006](https://arxiv.org/abs/astro-ph/0605725) mô tả yields phụ thuộc mass/metallicity/energy và Ni56; không có một mix đúng cho mọi SN.
- **HN**: birth >=25 solar, BH; energy proxy 1e52 erg so với SN 1e51. Không có spin nên mass-only collapsar eligibility là simplification rõ ràng. Damage x10; range tại fluence cố định tăng sqrt(10) trong physical distance rồi áp dụng phép squeeze của world. Không gọi toy blast là energy-conserving hydro. [Nomoto et al. 2002](https://arxiv.org/abs/astro-ph/0209064), [2006](https://arxiv.org/abs/astro-ph/0605725).
- **KN NS+NS**: total ejecta default .03 solar, tuning .01-.05 gồm dynamical ejecta và wind. Remnant current mass >2.2 solar => BH, <= => NS, áp dụng sau ejecta. Đây là cold/nonrotating TOV proxy, bỏ qua rotation-supported lifetime/binding/GW mass loss. [Cowperthwaite et al. 2017](https://www.darkenergysurvey.org/wp-content/uploads/2017/10/FINAL_Cowperthwaite.pdf) inferred total ~.05 solar cho GW170817; [Rezzolla et al. 2017](https://arxiv.org/abs/1711.00314) cold TOV estimate ~2.16 với uncertainties. Mix r-process gom metal/radio.
- **KN NS+BH**: không khẳng định mọi merger eject .03 solar. Chọn tidal-disruption condition trước khi phát KN, dùng nonspinning ISCO approximation; nondisrupting swallow vẫn merge/BH, ejecta=0 và không short GRB. Không có BH spin/NS compactness dynamics nên ghi giới hạn này. [Foucart et al. 2018](https://arxiv.org/abs/1807.00011) cho thấy vật chất ngoài BH phụ thuộc mass ratio, spin, NS radius; [Foucart et al. 2012](https://arxiv.org/abs/1212.4810) .01-.05 solar là kết quả có điều kiện. Cần Claire chốt conservative filter này thay vì unconditional KN cho mọi NS+BH.
- **GRB**: long sau HN, short sau KN; world RNG lấy axis, hai beam đối nhau; half-angle default 7.5 deg[P], range 6500 ly[P] (~2 kpc). Range là metadata galaxy-stage, không lấy SolDist(AU) để gây damage local. [Meszaros/Rees review](https://ned.ipac.caltech.edu/level5/Sept13/Meszaros/Meszaros3.html) mô tả collimated jets; [Thomas et al. 2005](https://arxiv.org/abs/astro-ph/0505472) nghiên cứu atmospheric damage phụ thuộc fluence/distance. Không suy ra hard lethal cutoff cho mọi hành tinh. Persistent beam record lưu year/origin/source-generation/event-id/axis/half-angle/range; hash bao gồm record và RNG. Chronicle nhận cosmic entry; chưa cross-system damage.
- **Ia optional**: đề nghị defer để ưu tiên ledger/SN/HN/KN/GRB và bộ kiểm. 1.44 solar là tuning cho near-Chandrasekhar CO WD, không đồng nghĩa mọi WD accretion/merger đều Ia; ONe collapse và sub-Chandra channels khác. [Hillebrandt/Niemeyer 2000](https://arxiv.org/abs/astro-ph/0006305).

## C. Merger trigger

`core/World.cs:203-219`: MergeStars hiện chạy ở :207 trước khi cộng Comp/M/COM; victim retire ở :218. Capture premerge phases/masses sau SyncStar, rồi gọi transition mới `StarTransition.Merger` sau cộng vật chất + retire victim/Gone. Table condition nhận pair phases, current merged mass và disruption scalar; core dispatch theo enum/payload/conditions, không ID literals.

`core/Stars.cs:105-108` hiện suy compact type từ tổng progenitor mass. NS+NS tổng progenitor >20 sẽ sai nếu dùng trực tiếp. Đề xuất exhausted massive remnant phân loại bằng retained current mass và TOV proxy; WD giữ birth branch. Canonical NS1.4/BH>=3 giữ type cũ. Kiểm riêng compact mergers dưới/trên TOV, recycled slot generation và immediate event timing.

Acceptance: transfer ledger closed (gas/ice + zero-slot ejecta); SN/HN/NSNS/disrupting+nondisrupting NSBH conditions/remnant/represented+escaped matter+momentum; RNG/beam/Chronicle replay; Jump vs rails event timestamps; old->new hash từng case; full CLI exit0 + anchored OK/FAILED count; audit không thêm DEBT. Chưa tuyên bố kết quả đo nào cho C5.

## Ma trận kiểm chuẩn bị trong lúc chờ ACK

| Probe | Điều phải chứng minh |
|---|---|
| Gas loss, single body, nonzero V | live+escaped M và từng cell/Px/Py khép kín; escape không lặp khi tiếp tục |
| Ice sublimation, hai Ice-role cells | proportional withdrawal; tiny residual được tính đúng; M bằng tổng Comp |
| SN 7.999/8/24.999 solar | đúng ngưỡng row, chỉ eject một lần, remnant recipe, metal/radio xuất hiện; birth trước ngưỡng không nova |
| HN 25/30 solar | HN thay SN, BH, eject mass khép kín, long GRB duy nhất; energy/range metadata/constants đúng |
| Ejecta slot budget 0/1/full | chỉ spawn trọn symmetric pair; remainder vào escaped; không mất momentum hoặc biến packet thành sao |
| NSNS retained dưới/bằng/trên TOV | output phase dựa current retained mass; short GRB/KN chỉ một lần; capture input phases trước MergeStars |
| NSBH tidal disruption / direct swallow | KN+radio khi disrupted; direct swallow không KN/GRB/ejecta, BH vẫn giữ phase |
| NS+planet, WD+WD, BH+BH | không phát KN do filter pair; ordinary merge giữ conservation |
| Slot recycled sau merger | beam record/source generation không đổi identity khi slot tái sử dụng |
| Same journal / different seed | seed giống: tất cả cells, ledger, beam metadata/hash khớp bit; seed khác: axis đổi, scalar recipe không đổi |
| GRB record observer | đọc Chronicle/Events và clear bounded Events không sửa state/hash; record persistence riêng có đủ hai-beam axis |
| Boundary | no-overstep death timestamp; Jump sample settings/observer không thay event semantics; merger nằm tại actual contact time |
| Group remap | Mix tra Element ID của world; reordered six roles vẫn đúng, missing catalog IDs bị từ chối như contract hiện tại |

Không dùng stock system momentum drift cũ làm epsilon cho transfer test: transfer cô lập phải khép ở rounding scale. Toàn stock vẫn báo drift integration/ships riêng, không gọi per-element transmutation là invariant.

## Baseline đã đo

`dotnet run --project cli -c Release` tại base `3ebe7cc`: exit 0, **322 ^OK, 0 ^FAILED**; audit **4 defect / 3 risk** cũ. Log `E:/Temp/cosmos-c5-baseline.log`; core DLL preserved `E:/Temp/cosmos-c5-baseline/Cosmos.Core.dll`.

`elements-probe` trước sửa:

```text
sol0 FF93E515461EB21B
sol0-step 1691CEFD928A00F2
sol0-life 6E538E7F8250AC54
sol0-edit 8E43B74DB17CEB6A
sol2000 06F4793A7DF1D9C8
sol2000-step D0AB98DA2959D1B4
sol2000-jump 6C45D249D8FD6A1A
star-1 4149B47870F8D8B7
star-2 16D4AD80223BC008
star-10 64F7A7B90834F061
star-30 0535F96C63617AAE
```

NSBH gate refinement được gửi Claire riêng: [Foucart 2018 eq4/6](https://arxiv.org/html/1807.00011) có fit GR cho mass còn ngoài BH, với coefficients .406/.139/.255/1.761. Nonspin Risco=6 GM/c². Gate này tốt hơn pure Newtonian test ở q gần1; mass ngoài BH không đồng nhất unbound ejecta, cần cap và chú thích. Radius NS20km hiện có cho C~.103 nằm ngoài calibrated C=.13-.182, phải ghi extrapolation nếu dùng. Newtonian alternative có factor3: d_dis=(3q)^(1/3)R_NS. Chưa chọn/implement trước ACK.
