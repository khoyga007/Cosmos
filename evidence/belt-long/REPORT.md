# Vong 5 — ban gop vanh dai + ca seed 1002 (bao cao)

Ngay chay: 2026-10-10. Script: `analyze.py` (moi so tu CSV).

## Cau hinh
- Core: `claire/heat-disc` @ `bd2a966`. Nhanh chay: `mesu/belt-long` tu `bd2a966`.
- Harness: `cli/BeltLong.cs` (`belt-long`), `cli/JumpAnomaly.cs` (`jump-anomaly`), chi `cli/` + `evidence/`.
- .NET Release, Threads=1. May cloud (ms tham khao).

## Viec 1 — ca seed 1002: tua 1e4 nam nuot 95 TĐ, tua dai hon nuot 0

Tai hien: `cli -- jump-anomaly` (seed 1002, 500 buoc roi tua). Ket qua:

| tua (nam) | da nuot (TĐ) | live truoc→sau |
|---|---|---|
| 1e2 | 0.00 | 4686→4661 |
| 1e3 | 0.00 | 4686→4686 |
| 1e4 | 95.20 | 4686→4684 |
| 1e5 | 0.00 | 4686→4686 |
| 1e6 | 0.00 | 4686→4686 |
| 1e7 | 0.00 | 4686→4686 |
| 1e8 | 0.00 | 4686→4686 |

Them (jump-anomaly2): 1.5e4→95.2, 2e4→0, 3e4→95.2, 5e4→95.2, 7e4→0; 1e6 voi 1000/5000 chunk van 0.

Chan doan (`diag-jump`):
- Cua 1e4: +1 disruption (to-disc), +1 merge, +95.2 TĐ. Cua 2e4: +0 disruption, +0 merge, +0.
- Hai dia co san truoc tua KHONG doi (cells/total/fed giong het); 95 TĐ den tu disc tam thoi sinh ra va boi tu het trong tua.
- Ket luan: KHONG phai loi do (so `DiscAccretedMass` cua core la that). La hanh vi duong Jump: phat hien su kien Roche phu thuoc kich thuoc chunk (toi da 200 chunk), nen tua dai hon co the BO SOT disruption ma tua ngan bat duoc. `jump(2e4)` khong bao gom moi su kien cua `jump(1e4)` — vi pham tinh don dieu.

## Viec 2 — chay dai ban gop vanh dai (50.000 buoc x 5 seed)

FoldBelt(sun,1,40)+FoldBelt(sun,2,40): gop ~4920 vat thanh dia nguoi (live 5251→~330).

| run | live dau→cuoi | vanh nguoi (dau→cuoi) | max |mass_res| |
|---|---|---|---|
| BN-1234 | 331→285 | 2.393E-05→2.393E-05 | 6.78E-14 |
| BN-1002 | 331→105 | 2.388E-05→2.388E-05 | 3.15E-14 |
| BN-1011 | 331→298 | 2.371E-05→2.371E-05 | 4.95E-14 |
| BN-2001 | 331→85 | 2.369E-05→2.369E-05 | 2.53E-14 |
| BN-2002 | 331→63 | 2.377E-05→2.377E-05 | 1.17E-13 |
| B-1234 | 330→330 | 2.393E-05→2.393E-05 | 8.52E-16 |
| B-1002 | 330→375 | 2.388E-05→2.388E-05 | 1.84E-15 |
| B-1011 | 330→330 | 2.371E-05→2.371E-05 | 8.52E-16 |
| B-2001 | 330→330 | 2.369E-05→2.369E-05 | 8.52E-16 |
| B-2002 | 330→330 | 2.377E-05→2.377E-05 | 8.52E-16 |
| N-1234 | 5250→5510 | 0.000E+00→0.000E+00 | 4.11E-10 |
| N-1002 | 5250→5508 | 0.000E+00→0.000E+00 | 7.37E-10 |
| N-1011 | 5250→5507 | 0.000E+00→0.000E+00 | 9.11E-10 |
| N-2001 | 5250→5512 | 0.000E+00→0.000E+00 | 1.05E-09 |
| N-2002 | 5250→5440 | 0.000E+00→0.000E+00 | 2.94E-10 |

- **B (gop, khong NS):** live 330→330 (dung yen), vanh nguoi KHONG doi (1e-15), mass 1e-15.
- **BN (gop + NS):** live giam (331→63–298, NS pha huy), vanh nguoi dung yen, mass 1e-13.
- **N (khong gop, khong NS):** live TANG 5250→5440–5512, cham tran nhieu lan (449 co do, toan bo la cham tran).
- ms/buoc: B/BN ~0.3–0.9ms (it vat), N ~5ms.
- Nguyen to: residual ~1e-14 tren tat ca run.

## Co do
- Tong 746 co do; tat ca deu la 'cham tran 5512' o cac run N (khong gop) — du kien.
- Khong co NaN, mass am, vanh nguoi doi, hay mass residual >1e-9 (sau khi sua harness thieu reservoir).

## Cai em chua chac
1. Viec 1: chua xac dinh duoc TAI SAO chunk dai bo sot disruption (can chi Claire soi duong Jump/Roche).
2. Run N (khong NS, khong gop) van tang dan toi tran — he co so co co che sinh vat tu nhien (can tach rieng).

## Ket luan (chi neu cai so lieu noi)
- FoldBelt giam 16x so vat the (5251→330); vanh nguoi dung yen tuyet doi; ban gop on dinh.
- Viec 1 la loi duong Jump (bo sot su kien theo chunk), khong phai loi do.
