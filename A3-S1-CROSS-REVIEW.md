# Cross-review A3 / Species S1 — Celine

Targets cố định: A3 `32a4d47` (parent implementation `69fb5ed`), S1 `ffdfa9a` (base `b08c41a`). Snapshot/work/probes ở E:/Temp, không sửa checkout của Ariel. S2 `ae0cc4e` được review riêng trong `S2-CROSS-REVIEW.md`.

## Defect đã tái hiện

1. `core/Layers.cs:24,38,499` / `core/Civ.cs:315` (A3) — `EarthYears`/`BecomesWaste` per-world không vào hash, nên cùng hash nhưng khác trạng thái tương lai — smallest input: `World(4,7)`, hash `5E1E3536BABB892E`; `w.Stages[0] = w.Stages[0] with { EarthYears=1 }` vẫn cùng hash. Hai world đổi duy nhất industrial `Needs[0].BecomesWaste` cũng cùng hash; gọi ConsumeResources(earth,1e8,industrial) cho metal `4.799249999999999e-5` vs `4.7999999999999994e-5`. **Stages là List per-world**, InitLayers copy DefaultStages; không phải chỉ static catalog. Default elision hợp lệ nếu hash custom tables như HashElements/HashStarEvents.
2. `core/Civ.cs:316` (S1 HashCiv) — new per-Civ Species không được hash — smallest input: `World(4,7); Civs.Add(new CivInfo("probe",-1,0))`; hash `F07346EB8E5B70F7`; đổi Species từ Human sang Human.WithParam("lifespan",300) vẫn y hệt. Giữ elision cho exact default Human, hash phần khác trước khi S2 đọc traits.
3. `core/Species.cs:71` (S1 WithParam) — catalog ValueType/Min/Max không được kiểm — smallest input: `Human.WithParam("manipulation",double.NaN).WithParam("lifespan",-1)` được nhận, dù rows khai báo0..1 và .1..100000. Dữ liệu không hợp lệ sẽ đi vào rate/fit S2. Validate hữu hạn/range/enum theo row.
4. `core/Species.cs:54` (S1 Extra) — alias caller dictionary phá tính cố định của dữ liệu birth — smallest input: construct Species với `Extra={bioluminescence:true}`, sửa dictionary ban đầu thànhfalse; GetParam trảfalse dù không override Species. Snapshot/immutable Extra; policy cho with/constructor cũng cần cùng bảo đảm.

## Kiểm đã qua / không quy kết nhầm

- `TechCeilingMargin` là public instance Consts FIELD, phản chiếu vào hash và SetConst hoạt động. Không phải lỗi Const hash mới.
- BecomesWaste giữ M và sumComp ở rounding scale. Metal→rock đổi **phân nhóm vật chất**, không thay đổi Escaped/NucleosynthesisDelta. Đây là mô hình waste cũ từ A2, không quy kết A3 làm mất mass. Nếu cần per-group invariant trên toàn simulation, phải công bố waste delta riêng; không gọi mọi biến phân nhóm là stellar nucleosynthesis.
- `CivTechRate` 1e-4→1 chuyển từ tech/year thành multiplier cho `(nextThreshold-currentThreshold)/EarthYears`; default pace vì vậy thay đổi theo duration từng row. Tech ceiling từ last threshold+margin đúng ý mở rộng. Chưa coi timescale đổi là defect khi đã có spec/approval của Claire.
- Human path S1 đối chiếu **hai binary khác nhau**: b08c41a vs ffdfa9a, cùng code probe. Sau1e6yr: cả hai hash `73F2981182041A5C`, civ1, Tech4, Pop0.9998875097515015; sau Push+.5×20 Advance: cả hai `99B2C234A99912F9`. Không chỉ so hai lượt cùng build như SpeciesChecks5.
- Source S1 thêm default Human metadata mà không thêm draw RNG/numeric write. Bit-equality đã đo ở hai ca trên; không tuyên bố đã chạy toàn bộ GUI/graph paths.

Repros giữ nguyên: `E:/Temp/cosmos-a3-probe/Program.cs` + snapshot detached32a4d47; `E:/Temp/cosmos-species-probe/Program.cs` + detachedffdfa9a; `E:/Temp/cosmos-human-compare/{base,species}/*.csproj`. Finding và phản biện static/per-world đã gửi thread cosmos từng lát. Chưa fix code Ariel; đợi exact fixed commits để re-review.
