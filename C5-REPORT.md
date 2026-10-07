# C5 — bàn giao sự kiện vũ trụ

Nhánh `celine/cosmic-events`, rebase theo yêu cầu từ `3ebe7cc` lên `8fef715`. Không merge/push. STEP0 và cả 5 thay đổi trong ACK đã được áp dụng. `C5-CHECKPOINT.md` chỉ lưu lịch sử đề xuất trước ACK.

## Phần thực hiện

- `core/Escape.cs:20,30,48`: shared `EscapedMass/Matter/Px/Py`, signed `NucleosynthesisDelta`, transfer từng sự kiện có year/source/generation/origin/cause. Khí/băng ghi vào sổ tổng, không sinh một record cho mỗi hạt/mỗi tick. StellarEjecta* chỉ thống kê toàn vỏ đã phóng, gồm phần bị bắt; conserve chỉ cộng shared ledger.
- `core/Kinds.cs:202,259`: mất khí và băng đi qua EscapeRole. M được cộng lại SAU khi xóa residual. Một role có nhiều element vẫn lấy tỉ lệ từng ô, có kiểm độc lập.
- `core/StarEvents.cs:43,117,156,185`: bảng 8 dòng; giữ `star.nova`, thêm HN và 2 cặp KN. HN/SN chung ChoiceGroup, Chance lấy RNG world; Nova chọn Mix cho recipe remnant chạy tiếp trong cùng transition. Không eject hai lần. Trigger/Chance/pair/burst/speed/custom table đều đọc cột và tham gia hash.
- `core/CosmicEvents.cs:66`: ejecta giải tích `R_physical²/(4 d_physical²)`, distance giải ngược squeeze AU; radius Trái Đất/stellar radius dùng vật lý, không bán kính vẽ. Bồi Comp/M và động lượng bulk+radial, remainder vào shared ledger. Không tạo packet, không dùng slot. Tổng coverage được chặn bằng normalization nếu vượt 1; không mô phỏng bóng che.
- `core/World.cs:204,224`: capture phase/mass đầu vào trước cộng; Merger dispatch sau cộng Comp/M/COM và retire/Gone victim. `core/Stars.cs:102`: giữ nhánh WD theo birth, compact remnant NS/BH theo khối lượng GIỮ LẠI và TOV.
- `core/CosmicEvents.cs:44`: NSBH gate Foucart2018, nonspin ISCO=6, NS12km. Ejecta = min(.03 solar, outside mass); **đây là cap cho recipe fiducial, không phải fit unbound ejecta**.
- `core/CosmicEvents.cs:53,103`: long GRB với HN, short với KN; unit axis 3D isotropic từ 2 draws world RNG (hai beam +/-), 7.5°, E_iso long/short riêng. Persistent record/hash có nguồn+generation+origin; Chronicle ghi với civ=-1. `DoseRadiusMetres = sqrt(E_iso/(4πF))`; `FluenceAt(d)` suy yếu 1/d². Không gây damage GRB trong hệ nhà.
- `cli/CosmicChecks.cs` + `cli/ConserveChecks.cs`: kiểm riêng chuyển sổ, phản ứng có delta, các ngưỡng, cặp va chạm, tàn dư, geometry, replay, RNG, recycled slots và extension row. Program có lệnh `cosmic` và chạy nó trong full suite.

## Nguồn, công thức, giới hạn

| Tham số / lựa chọn | Giá trị và cơ sở |
|---|---|
| SN/HN | SN đủ ngưỡng8; HN eligible>=25 solar, damage10x/range sqrt10 theo khoảng cách thật, fallback SN/BH khi spin proxy trượt. Macro energetic model dựa [Nomoto2006](https://arxiv.org/abs/astro-ph/0605725); giữ blast attenuation toy hiện có, không nhận là hydrodynamics. |
| HN xác suất | **Suy luận cho nhóm>=25**, không tỷ lệ bin đo trực tiếp: HN/Ibc .07 từ [Guetta&DellaValle2007 §IV](https://arxiv.org/html/astro-ph/0612194); Ibc/(Ibc+II)=.14/(.14+.86), cùng hàng Sbc-Sd [Cappellaro1999 table4](https://arxiv.org/html/astro-ph/9904225). Salpeter8..100: eligible=.1879203. Chance=.07*.14/.1879203≈.05215; toàn CCSN≈.98%. Không có spin hay metallicity dynamics. Không phải mọi HN thực đều có narrow high-energy GRB: ở đây HN là channel HN+GRB theo yêu cầu C5. |
| KN | .03 solar default là macro recipe trong khoảng .01-.05; ejecta giàu r-process, v≈.2c theo [Cowperthwaite2017](https://arxiv.org/abs/1710.05840). NSNS BH nếu retained>2.2, NS nếu <=2.2; cold nonrotating proxy theo [Rezzolla2017](https://arxiv.org/abs/1711.00314). Không rotation-supported lifetime/GW binding loss. |
| NSBH | q=MBH/MNS, η=q/(1+q)², C=1.4765*(MNS/Msolar)/R_NS_km. outside/MNS=max(.406*(1-2C)/cbrt(η)-.139*6*C/η+.255,0)^1.761. [Foucart2018 eq4/6](https://arxiv.org/html/1807.00011); fit calibrated q1..7, C.13..182. Model đồng nhất baryonic/gravitational mass và cố định spin0; ngoài miền là extrapolation. |
| Mix | 6 nhóm có sẵn, không nguyên tố hư cấu. SN=(gas.45,rock.40,metal.10,carbon.04,radio.01); HN=(.40,.38,.16,.04,.02), KN=(metal.8,radio.2). Là coarse grouped recipe (rock O/Si, metal Fe, radio Ni/r-process), **không isotopic yield network**; delta có dấu công bố phép chuyển nhóm, tổng mass vẫn đóng. |
| GRB | E_iso dài5e45J, ngắn1e43J; half-angle7.5°, threshold1e5J/m². Long derived dose radius2.0442kpc, không hard range6500ly. Atmospheric dose proxy theo [Piran&Jimenez2014](https://arxiv.org/html/1409.2506), đối chiếu [Thomas2005](https://arxiv.org/abs/astro-ph/0505472). Mỗi record đóng băng tham số lúc phát. |
| Shell | Optically thin tức thời, không transit time/shielding/ablation/hydrodynamics. Speed SN10000, envelope20, KN60000 km/s; km/s→AU/year→local derivative của squeeze/time. Bảo toàn trong đơn vị world, không solver GR. |

## Số đo và nghiệm thu

- Ledger checkpoint trước bảng: full CLI **343 OK / 0 FAILED**, exit0, audit4D/3R; conserve37 + cosmic8. Sol200 mass deficit cũ≈3.964e-7 → -5.471e-13 sau ghi sổ. Checkpoint đã báo Claire qua bridge trước phần bảng.
- Bản đầy đủ trước chỉnh provenance HN: full CLI395 OK/0FAILED, exit0. Sau đó HN rate được neo cùng hàng table4 (.14/.86 thay vì trộn .16/.86), thêm tiny residual regression; kết quả cuối ghi dưới đây.
- Tiny residual đo DLL baseline3ebe7cc thật: M=5.01e-17, sumComp=5e-17, chênh=9.99999999999979e-20, ice đã bằng0. Bản mới cùng ca: M=5e-17, M-sumComp=0, Escaped=5e-17.
- Full Sol200 mới (1.06e10 năm): mass delta -1.208e-13 /50.06698945; element total delta -3.126e-13. Sink-off mass delta -9.948e-14. Stock momentum drift≈2.402e-8 còn là giới hạn rails/integration cũ; không dùng band đó cho kiểm transfer cô lập. Transfer cô lập đóng M, mỗi cell sau trừ delta, Px/Py ở rounding scale.
- NSNS1.1+1.1 -> retained2.17 NS; 1.1+1.13 ->2.2 NS;1.2+1.2 ->2.37 BH. Mỗi ca KN/shortGRB đúng1 lần, replay exact.
- NSBH1.4+3: C=.1722583, outside=.00395331451605579 solar, ejected=.00395331451605557. NSBH1.4+5: outside0, không KN/GRB/ejecta, BH còn sống. WDWD/BHBH không KN mặc định; một row WDWD tùy chỉnh vẫn dispatch được.
- Shell tại100AU, Earth vật lý6371km, full capacity2/2: captured metal1.133560e-12, khớp25*(6371/(100*149597870.7))²/4; không sinh slot, radial/bulk momentum khép.
- Journal replay exact cho volatile, mọi death boundary và merger. Advance nhỏ vs jump: outcome/axis/next RNG/ejecta exact; 1/200 cuts axis/next RNG exact, dYear=-6.054e-9 năm, origin sai số<1e-6 world unit. **Không tuyên bố full hash của hai cấu hình JumpSamples khác nhau bằng nhau**; các Consts và timeline rounding khác nhau.
- Build game: 0 lỗi (warning GodUi.cs804 tồn tại ở lần cold build, file không sửa). GUI render thật/windowed FPS chưa kiểm.

## Hash cũ → mới

Cũ = fixture trên base8fef715. Ledger checkpoint được giữ ở cột giữa. Mọi hash đổi vì state escaped+delta/records và constants mới được hash; các ca băng đổi arithmetic trimming, SN/HN thêm Mix/chance/draw, WD có transfer giải tích. Không giấu recapture.

| Cảnh | Base8fef715 | Ledger checkpoint | C5 cuối |
|---|---|---|---|
| sol0 | FFD00BC42151E6A6 | BFD6737DC7CE32A6 | 6C56216D3D2C758F |
| sol0-step | 4FBBBEAD844AB5DF | 6A8570F6B38BF3DF | 9E4C9D844F3CF2D2 |
| sol0-life | 3D72350746CF2166 | 1C558DC94CA0ED66 | 97AD3F35AE99CDAB |
| sol0-edit | FF1FE39BF4C6E9DF | 2C26DEFC7E7027DF | B672C1E7FB754212 |
| sol2000 | 0092E155298710A1 | 79B5BF7A0A12D2A1 | 68B21923CD852A6C |
| sol2000-step | 958069674F8D689D | 10753674515F229D | E847BF6F9F41D4D8 |
| sol2000-jump | 06F91580607F5E10 | 73520CD26F6C1DAE | DAE873E2BBA7FC03 |
| star-1 | A439E16F62927B5A | BE2EB55006F523DD | 5F8FEF5535B4057E |
| star-2 | 957DB3C8A7C03D49 | 564EB0E672CB81FE | 17CF3AD78EA82314 |
| star-10 | F16C0D051B35BB80 | 30D7F1412D8FC057 | EAFCA1C02E822A73 |
| star-30 | E2A3E8F272467067 | C9242F305B5DBF3E | 435329957269E91C |

| Progenitor fixture | Base8fef715 | C5 cuối |
|---|---|---|
|1.23|52EA80A55D0C831A|97C2CE571597307D|
|2.0000000000000004|E87B79406531FBA9|C7853FD92DE50040|
|7.999999999|1339DC5060019F02|870E42732CBC7754|
|8|883E846FE359D0EB|DAC46BCBA3622301|
|20|CCCF36A065EA2D69|D53856AF2ECDDF32|
|20.0000001|341E24191DE54BFC|5CD742194002C0BA|
|31.123|3F23FDCB7A13699B|59B4013AE6C9B43A|

## Kết quả cuối / cách chạy

**Full CLI396 OK / 0 FAILED, exit0; conserve52 OK, cosmic51 OK; audit4D/3R, không thêm DEBT.** Hai bảng fixture18 hash đã chụp lại và khớp. Headless selftest **B9D324ACEE1C2793** exact replay13 lệnh; uitest **3E1F690BD908F221**, replay29 lệnh, đều exit0. Không sửa game/.

Logs tại `E:/Temp/cosmos-c5-final-full.log`, `cosmos-c5-cosmic-final.log`, `cosmos-c5-final-selftest.log`, `cosmos-c5-final-uitest.log`. Selftest base8fef715 Claire/Ariel đã báo `32FE9F87D73FE807` (không chạy lại DLL base của game ở lượt này); đổi vì escaped/GRB state và Consts mới. UI hash base A2 không có số trong chỉ thị cuối, nên không nhận P4 cũ làm baseline A2.

```powershell
$env:COSMOS_CORE_SRC='E:/Cosmos-celine/core'
dotnet run --project cli -c Release
dotnet run --project cli -c Release -- cosmic
dotnet run --project cli -c Release -- conserve
dotnet build game/Cosmos.Game.sln -c Release
# Godot console executable: --headless --path E:/Cosmos-celine/game -- --selftest (hoặc --uitest)
```

Chưa làm: liên hệ sao/galaxy damage, nguyên tố hư cấu, Ia, binding-energy/GR/hydrodynamic solver, GUI art/polish. C5 không yêu cầu các phần này. Claire review nhánh rồi quyết merge.
