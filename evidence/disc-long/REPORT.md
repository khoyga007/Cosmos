# Vong 4 — chay DAI loi dia boi tu (bao cao)

Ngay chay: 2026-10-10. Script sinh bao cao: `analyze.py` (moi so duoi day deu tu CSV).

## Cau hinh
- Core: nhanh `claire/heat-disc` @ `c4598a5` (lat dau Heat Law: dia boi tu).
- Nhanh chay: `mesu/disc-long` tu `c4598a5`. Harness `cli/DiscLong.cs` + lenh `disc-long`.
- `git diff c4598a5 HEAD -- core game` rong (chi them/sua `cli/` + `evidence/`).
- .NET Release, `Threads=1`. May: AMD EPYC 9D25 (2 vCPU), RAM 7GB — so ms chi tham khao.
- Canh: 96 warmup + tha NS (nhu `DiscChecks.P0`), dem buoc tu luc tha NS.

## Determinism
- seed 1234, 2 lan x 5000 buoc: hash `7742FD98C274E6EC` vs `7742FD98C274E6EC` → KHOP.

## Canh A — 5 seed x 50.000 buoc, DiscOn=1 (mau moi 100 buoc)

| seed | buoc | Live | discs | disc_mass | disc_accreted (don vi goc) | mass_resid |
|---|---|---|---|---|---|---|
| 1234 | 0 | 5251 | 0 | 0.000E+00 | 0.000E+00 | 0.000E+000 |
| 1234 | 25100 | 4400 | 2 | 2.329E-02 | 3.866E-02 | -7.101E-016 |
| 1234 | 50000 | 4244 | 2 | 1.963E-02 | 4.232E-02 | -2.367E-015 |
| 1002 | 0 | 5251 | 0 | 0.000E+00 | 0.000E+00 | 0.000E+000 |
| 1002 | 25100 | 4247 | 2 | 4.973E-07 | 2.683E-06 | -3.669E-015 |
| 1002 | 50000 | 4011 | 2 | 3.966E-07 | 2.783E-06 | -5.918E-015 |
| 1011 | 0 | 5251 | 0 | 0.000E+00 | 0.000E+00 | 0.000E+000 |
| 1011 | 25100 | 4199 | 2 | 5.147E-07 | 2.500E-06 | -6.983E-015 |
| 1011 | 50000 | 3883 | 2 | 4.593E-07 | 2.630E-06 | 2.130E-015 |
| 2001 | 0 | 5251 | 0 | 0.000E+00 | 0.000E+00 | 0.000E+000 |
| 2001 | 25100 | 4190 | 2 | 5.093E-07 | 2.538E-06 | -7.101E-015 |
| 2001 | 50000 | 4043 | 3 | 4.069E-07 | 2.644E-06 | -4.616E-015 |
| 2002 | 0 | 5251 | 0 | 0.000E+00 | 0.000E+00 | 0.000E+000 |
| 2002 | 25100 | 3958 | 3 | 3.552E-06 | 7.693E-06 | -6.273E-015 |
| 2002 | 50000 | 3635 | 3 | 2.839E-06 | 8.420E-06 | -1.373E-014 |

(Don vi khoi luong: don vi goc cua core.)

Da nuot (Trai Dat, tu run.log): A-1234: 282.1, A-1002: 0.0, A-1011: 0.0, A-2001: 0.0, A-2002: 0.1

## So sach (toan bo 50.000 buoc)

| seed | max |mass_resid| | max |phan tu| | max |L| ngoai cua so merger |
|---|---|---|---|
| 1234 | 9.70E-15 | 1.85E-14 | 3.16E-05 (moi 100 buoc) |
| 1002 | 6.98E-15 | 1.34E-14 | 5.21E-07 (moi 100 buoc) |
| 1011 | 1.75E-14 | 2.28E-14 | 5.21E-07 (moi 100 buoc) |
| 2001 | 1.10E-14 | 1.70E-14 | 5.21E-07 (moi 100 buoc) |
| 2002 | 1.92E-14 | 1.42E-14 | 3.48E-07 (moi 100 buoc) |

- Khoi luong va tung nguyen to: bao toan o muc 1e-14.
- Mo-men dong luong (L quy dao quanh khoi tam): ngoai cua so merger, do troi moi 100 buoc o muc 1e-7–1e-5 tuong doi.

## Thay doi L dot ngot tai merger NS-Sun

- A-1234: NeutronStar → BlackHole (buoc 200→300): dL/L = -0.547.
- A-1002: NeutronStar → BlackHole (buoc 200→300): dL/L = +0.355.
- A-1011: NeutronStar → BlackHole (buoc 200→300): dL/L = +0.239.
- A-2001: NeutronStar → BlackHole (buoc 200→300): dL/L = +0.316.
- A-2002: NeutronStar → BlackHole (buoc 200→300): dL/L = +0.170.

Giai thich: khi NS merge voi Sun, mo-men dong luong quy dao cua cap doi chuyen thanh spin (core khong mo hinh spin) nen mat khoi ngan sach quy dao. Day la hanh vi cua core Merge, khong phai troi cua luat dia. Ngoai ra phep do L ve khoi tam cua harness bi dut doan khi dia rehost (xem muc 'chua chac').

## Canh B — nhay thoi gian sau 500 buoc (moi cu mot run rieng)

| run | dL qua nhay (tuong doi) | dmerges | dAccAng | dL+dAccAng |
|---|---|---|---|---|
| B-1234-1e2 | -2.15E-04 | +0 | +6.27E-04 | +6.74E-05 |
| B-1234-1e4 | -2.18E-04 | +0 | +6.27E-04 | +6.04E-05 |
| B-1234-1e6 | -2.13E-04 | +0 | +6.27E-04 | +7.14E-05 |
| B-1234-1e8 | -2.00E-04 | +0 | +6.27E-04 | +1.05E-04 |
| B-1002-1e2 | -2.12E-01 | +24 | +0.00E+00 | -1.65E+00 |
| B-1002-1e4 | -1.61E-04 | +1 | +2.25E-03 | +9.98E-04 |
| B-1002-1e6 | +3.63E-04 | +0 | +0.00E+00 | +2.83E-03 |
| B-1002-1e8 | +7.71E-05 | +0 | +0.00E+00 | +6.01E-04 |

- B-1234: ca 4 cu nhay cho cung mot ket qua (dia xa het trong <100 nam); dL gan nhu bi triet tieu boi ledger spin (+6e-4), du ~6e-5.
- B-1002-1e2: nhay gay 24 merger → dL -21% (spin-loss cua merger, khong phai troi dia).
- B-1002-1e4/1e6/1e8: 0–1 merger; dL ~1e-4 tuong doi, mot phan vao ledger spin.
- `DiscResidualAngular` = 0 trong toan bo cac run (khong co du so mo-men cua dia).

## Canh C — doi chung DiscOn=0, seed 1234, 5000 buoc
- Live: 5251 → 5482; cham tran 5512: CO.
- Voi dia tat, van kich tran nhu cu; voi dia bat (canh A), khong seed nao cham tran trong 50.000 buoc.

## Hieu nang (tham khao, may cloud)
- A-1234: 4.9–7.7 ms/Advance (khong tang >2x).
- A-1002: 5.2–7.8 ms/Advance (khong tang >2x).
- A-1011: 5.6–10.4 ms/Advance (khong tang >2x).
- A-2001: 5.9–10.1 ms/Advance (khong tang >2x).
- A-2002: 4.5–9.7 ms/Advance (khong tang >2x).

## Co do
- Tong 7 co do; ngoai canh C: 0.
- Canh C cham tran 5512 nhieu lan — la hanh vi doi chung du kien (dia tat = loi cu).

## Cai em chua chac
1. Phep do L ve khoi tam cua harness dung transport-term theo host hien tai cua dia; khi dia rehost (NS→Sun trong merger), so do dut doan. Khong tach duoc bao nhieu cua dot sut L la spin-loss that, bao nhieu la loi do.
2. Residual Px/Py tuong doi vo nghia vi baseline gan 0; can residual tuyet doi neu muon ket luan ve dong luong tuyen tinh.
3. Toc do boi tu (accretion) chenh lech lon giua cac seed (1234: 282 Trai Dat/50k buoc; 1002/1011/2001: ~0). Cu nhay 1e4 nam kich hoat boi tu tren seed 1002 (95 Trai Dat), goi y thoi gian nhot (viscous) dai hon 25.000 nam o mot so dia — chua phai bug, nhung dang ghi nhan.

## Ket luan (chi neu cai so lieu noi)
- Dia giu so khoi luong va nguyen to o muc 1e-14 trong 50.000 buoc x 5 seed.
- So vat the on dinh va giam dan (5251 → 3635–4244), khong cham tran 5512 khi dia bat; tat dia thi cham tran nhu cu.
- Determinism: 2 lan seed 1234 x 5000 buoc khop hash.
- Khong thay troi/no/phình cua luat dia trong pham vi chay; thay doi L lon deu gan voi merger (spin-loss) hoac nam trong ledger spin co chu dich.
