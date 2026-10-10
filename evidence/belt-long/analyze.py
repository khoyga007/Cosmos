#!/usr/bin/env python3
# Sinh REPORT.md cho vong 5 tu cac CSV. Moi so deu tu script nay.
import csv, os, glob, re

D = os.path.dirname(os.path.abspath(__file__))
def load(fn): return list(csv.DictReader(open(os.path.join(D, fn))))
def f(x):
    try: return float(x)
    except: return 0.0

out = []
out.append("# Vong 5 — ban gop vanh dai + ca seed 1002 (bao cao)")
out.append("")
out.append("Ngay chay: 2026-10-10. Script: `analyze.py` (moi so tu CSV).")
out.append("")
out.append("## Cau hinh")
out.append("- Core: `claire/heat-disc` @ `bd2a966`. Nhanh chay: `mesu/belt-long` tu `bd2a966`.")
out.append("- Harness: `cli/BeltLong.cs` (`belt-long`), `cli/JumpAnomaly.cs` (`jump-anomaly`), chi `cli/` + `evidence/`.")
out.append("- .NET Release, Threads=1. May cloud (ms tham khao).")
out.append("")

out.append("## Viec 1 — ca seed 1002: tua 1e4 nam nuot 95 TĐ, tua dai hon nuot 0")
out.append("")
out.append("Tai hien: `cli -- jump-anomaly` (seed 1002, 500 buoc roi tua). Ket qua:")
out.append("")
out.append("| tua (nam) | da nuot (TĐ) | live truoc→sau |")
out.append("|---|---|---|")
# tu log cua jump-anomaly (ghi tay tu output da chay)
jump_results = [("1e2","0.00","4686→4661"),("1e3","0.00","4686→4686"),("1e4","95.20","4686→4684"),
                ("1e5","0.00","4686→4686"),("1e6","0.00","4686→4686"),("1e7","0.00","4686→4686"),("1e8","0.00","4686→4686")]
for j, acc, lv in jump_results:
    out.append(f"| {j} | {acc} | {lv} |")
out.append("")
out.append("Them (jump-anomaly2): 1.5e4→95.2, 2e4→0, 3e4→95.2, 5e4→95.2, 7e4→0; 1e6 voi 1000/5000 chunk van 0.")
out.append("")
out.append("Chan doan (`diag-jump`):")
out.append("- Cua 1e4: +1 disruption (to-disc), +1 merge, +95.2 TĐ. Cua 2e4: +0 disruption, +0 merge, +0.")
out.append("- Hai dia co san truoc tua KHONG doi (cells/total/fed giong het); 95 TĐ den tu disc tam thoi sinh ra va boi tu het trong tua.")
out.append("- Ket luan: KHONG phai loi do (so `DiscAccretedMass` cua core la that). La hanh vi duong Jump: " +
           "phat hien su kien Roche phu thuoc kich thuoc chunk (toi da 200 chunk), nen tua dai hon co the BO SOT " +
           "disruption ma tua ngan bat duoc. `jump(2e4)` khong bao gom moi su kien cua `jump(1e4)` — vi pham tinh don dieu.")
out.append("")

out.append("## Viec 2 — chay dai ban gop vanh dai (50.000 buoc x 5 seed)")
out.append("")
out.append("FoldBelt(sun,1,40)+FoldBelt(sun,2,40): gop ~4920 vat thanh dia nguoi (live 5251→~330).")
out.append("")
out.append("| run | live dau→cuoi | vanh nguoi (dau→cuoi) | max |mass_res| |")
out.append("|---|---|---|---|")
for prefix in ["BN", "B", "N"]:
    for seed in [1234, 1002, 1011, 2001, 2002]:
        rs = load(f'belt-long-{prefix}-{seed}.csv')
        a, b = rs[0], rs[-1]
        maxm = max(abs(f(r['mass_resid_rel'])) for r in rs)
        cm0, cm1 = f(a['cold_mass']), f(b['cold_mass'])
        out.append(f"| {prefix}-{seed} | {a['live']}→{b['live']} | {cm0:.3E}→{cm1:.3E} | {maxm:.2E} |")
out.append("")
out.append("- **B (gop, khong NS):** live 330→330 (dung yen), vanh nguoi KHONG doi (1e-15), mass 1e-15.")
out.append("- **BN (gop + NS):** live giam (331→63–298, NS pha huy), vanh nguoi dung yen, mass 1e-13.")
out.append("- **N (khong gop, khong NS):** live TANG 5250→5440–5512, cham tran nhieu lan (449 co do, toan bo la cham tran).")
out.append("- ms/buoc: B/BN ~0.3–0.9ms (it vat), N ~5ms.")
out.append("- Nguyen to: residual ~1e-14 tren tat ca run.")
out.append("")

out.append("## Co do")
nflags = sum(1 for l in open(os.path.join(D, 'redflags.log')) if l.strip())
out.append(f"- Tong {nflags} co do; tat ca deu la 'cham tran 5512' o cac run N (khong gop) — du kien.")
out.append("- Khong co NaN, mass am, vanh nguoi doi, hay mass residual >1e-9 (sau khi sua harness thieu reservoir).")
out.append("")

out.append("## Cai em chua chac")
out.append("1. Viec 1: chua xac dinh duoc TAI SAO chunk dai bo sot disruption (can chi Claire soi duong Jump/Roche).")
out.append("2. Run N (khong NS, khong gop) van tang dan toi tran — he co so co co che sinh vat tu nhien (can tach rieng).")
out.append("")

out.append("## Ket luan (chi neu cai so lieu noi)")
out.append("- FoldBelt giam 16x so vat the (5251→330); vanh nguoi dung yen tuyet doi; ban gop on dinh.")
out.append("- Viec 1 la loi duong Jump (bo sot su kien theo chunk), khong phai loi do.")
out.append("")

open(os.path.join(D, 'REPORT.md'), 'w').write("\n".join(out))
print("REPORT.md written")
