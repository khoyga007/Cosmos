# C6 bước 0 — Roche → rings

Base `b08c41a` (master có C5/P5, SPEC §10), nhánh `celine/roche-rings`. Chưa sửa core. Gửi Claire ACK + amendments trước implementation.

## Luồng hiện có và hook dự kiến

- `core/World.cs:124`: R solid từ Comp/Density. `:272-277`: Saturn ring hiện là object thật trên AddOrbiting, d≈1.4..2.4 R_Saturn; chưa có fragmentation. `:294-354`: Advance có kick/drift/contact mỗi substep, không thể chỉ check Roche sau một Advance dài vì flyby có thể đã xuyên qua rồi.
- `core/Rails.cs:172`: PrimaryContactTime đã có first-entry solver cho orbit kín và straight fallback unbound. `:294`: Ride kiểm primary surface crossings; `:148`: contacts của sibling chỉ ở endpoints. `:135`: deferred rock contacts có source/primary generation guard. C6 cần first-entry radius=Roche, cắt trước surface merge, place rocks/rebuild origins sau đổi topology; không check mỗi chunk bằng endpoint.
- `core/Commands.cs:150-169`: Kill/Gone giải phóng slot, xóa parent/ship/civ-home references. Capture đủ Comp/M/P trước Kill; retire parent trước Add để có một slot, nhưng Gen của debris phải mới. Nếu parent có satellites/civ/life, dùng đường end/swallowed theo nguyên nhân tidal, không im lặng để pointer bám slot tái dùng.
- `core/Escape.cs:20`: ledger chung ghi mass/cell/P + year/source generation/origin/cause; C6 không dùng NucleosynthesisDelta vì chỉ phân chia vật chất.

Đề xuất file mới `core/Roche.cs`, `cli/RocheChecks.cs`; hook nhỏ `World.Advance`, `Rails.Jump/Ride` + Rule init/hash/Program. Không game/. Branch Ariel có World/Layers đang đổi, sẽ giữ hook hẹp và báo khi rebase.

## Vật lý và nguồn

Roche cohesionless: k_rigid=1.26, k_fluid=2.44. Interpolation theo tổng share gas+ice (macro vật liệu theo §10), density từ M/sum(Comp/Density); ngưỡng tính trong cùng local length scale của R và orbit (như Saturn rings hiện có), không trộn SolDist(AU) với local satellite distances. Material strength thật khiến mảnh nhỏ sống được trong Roche; giới hạn cohesionless không phải luật xé mọi hạt. Nguồn: [Holsapple & Michel2008, Icarus](https://doi.org/10.1016/j.icarus.2007.09.005), [Aggarwal/Oberbeck1974, NTRS](https://ntrs.nasa.gov/citations/19740055720).

Đề xuất Consts[P]: RocheRigid=1.26, RocheFluid=2.44; fragment target64/budget tổng10000 (resolution budget, không số thật); ring radial width~.02 (macro, sẽ kiểm không tăng energy). Mảnh nhỏ cần strength/resolution gate để tránh xé lại vô hạn. Giá trị gate phải được ghi là proxy hoặc có strength formula; không gắn “loại ring” tùy ý vào Kind.

Hyperbolic capture KHÔNG đồng nghĩa bắt toàn bộ parent. Dones' energy-spread proxy (nonspin): Δε=G Mp Rbody/q², ε∞=.5 v∞²; captured fraction clamp((.9Δε+ε_stable-ε∞)/(1.8Δε),0,1), ε_stable=-G Mp/Hill. Primary root vô hạn -> ε_stable=0. [Hyodo2016 §II.1 eq1/2](https://arxiv.org/html/1609.02396) cho đây là proxy, không phổ quát; spin/self-gravity thay hiệu suất. Bound/circular parent được biểu diễn đủ khối lượng khi đủ slots. Hyperbolic phần còn lại cần stream/unbound debris; không tạo vòng tròn từ vật hoàn toàn không đủ điều kiện capture.

Circularization là tiêu tán năng lượng, không được cộng kinetic vô hạn. Giữ specific angular momentum h: r_c=h²/μ (có reduced-mass correction khi gom thành full ring ở COM). Eccentric/flyby có thể r_c>Roche dù q<Roche; ép r_c vào Roche vừa giữ toàn mass vừa giữ L là không thể nếu không có nơi nhận torque. Source tham khảo mô hình bound debris/collisional eccentricity damping [Hyodo2016 §V](https://arxiv.org/html/1609.02396). Sẽ ghi ΔE_dissipated>=0 và check P; đề nghị kiểm thêm L/COM cho ring, không chỉ mass.

## Ba điểm cần ACK/amendment trước khi code

1. **Ring mass=parent chỉ cho ca bound, đủ budget.** Hyperbolic: ring+debris/escaped=parent; captured fraction có thể0. Không gọi toàn phần “escaped khỏi hệ” nếu nó chỉ thoát primary mà còn bound với star.
2. **Budget thiếu không được xóa vật chất còn bound vào Escaped.** Đề nghị giữ unresolved bound debris trong một record/reservoir có host+generation, Comp/M/P và hash, chỉ materialize phần đủ slots; hoặc Claire chọn cap N nhưng toàn M trên N object thật (và chấp nhận mảnh có thể còn hút/tiếp tục fragmentation). Không giả vờ unresolved mass đã bay khỏi hệ. Reservoir cần exception rõ cho nguyên tắc mọi vật thể là object.
3. **r_c ngoài Roche:** giữ eccentric debris stream rồi circularize ở r_c (có thể ngoài Roche), không ép toàn ring vào Roche. Ca acceptance ring-inside dùng near-circular input. Với finite-size fragment survival: ưu tiên strength gate; budget/cooldown chỉ là numerical guard, không lý do vật lý để miễn thủy triều.

Nếu Claire muốn bản macro gọn hơn, các simplification phải chốt công khai ở ACK. Không hỏi Yang về đáp án vật lý; các điểm này là mâu thuẫn giữa requirement/budget và mô hình trạng thái hiện có.

## Ma trận kiểm

- Same density rigid/fluid: limit ratio2.44/1.26; composition interpolation; reordered element IDs/roles, custom material table.
- Bound circular moon: parent retired/gen reused safely; ringM=parent, từng cell/Px/Py/COM/L khép; E_after<=E_before; mọi ring radius nằm giữa surface và Roche trong ca được chọn.
- Hyperbolic: near-parabolic captured fraction>0, high-speed capture0, no forced binding/free energy; survivor/debris/system ledger phân biệt thoát primary và thoát hệ.
- Capacity0/1/full, min/max budgets: không mất/nhân đôi mass, không reuse stale references. Tiny fragments không recursive explosion.
- Advance swept path vs rails first-entry: breakup trước contact, timestamp/source identity đúng;1/200 cuts không bỏ lỡ. Primary chết/Gen đổi không dùng kế hoạch cũ.
- Replay cùng seed/Journal exactHash; 1e6-year ring persistence, drift/merge count/energy/angular momentum được báo; Sol default và5000 rocks overhead đo trước/sau.
- FullCLI exit0, ^OK/^FAILED + auditD/R, game build và headless replay; hash cũ→mới giải thích từng fixture.

Chưa code/đo C6. Sau ACK mới implementation; không merge. Trong lúc chờ, review chéo A3 của Ariel ở bản rebase đã xác nhận.
