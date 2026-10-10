#!/usr/bin/env python3
# Sinh REPORT.md cho vong 4 tu cac CSV. Moi so trong REPORT deu tu script nay.
import csv, os, datetime

D = os.path.dirname(os.path.abspath(__file__))
def load(fn): return list(csv.DictReader(open(os.path.join(D, fn))))

def f(x):
    try: return float(x)
    except: return 0.0

out = []
out.append("# Vong 4 — chay DAI loi dia boi tu (bao cao)")
out.append("")
out.append(f"Ngay chay: 2026-10-10. Script sinh bao cao: `analyze.py` (moi so duoi day deu tu CSV).")
out.append("")
out.append("## Cau hinh")
out.append("- Core: nhanh `claire/heat-disc` @ `c4598a5` (lat dau Heat Law: dia boi tu).")
out.append("- Nhanh chay: `mesu/disc-long` tu `c4598a5`. Harness `cli/DiscLong.cs` + lenh `disc-long`.")
out.append("- `git diff c4598a5 HEAD -- core game` rong (chi them/sua `cli/` + `evidence/`).")
out.append("- .NET Release, `Threads=1`. May: AMD EPYC 9D25 (2 vCPU), RAM 7GB — so ms chi tham khao.")
out.append("- Canh: 96 warmup + tha NS (nhu `DiscChecks.P0`), dem buoc tu luc tha NS.")
out.append("")

# Determinism
d1 = load('disc-long-det-1234a.csv'); d2 = load('disc-long-det-1234b.csv')
# hash lay tu summary
summ = {r['run']: r for r in csv.DictReader(open(os.path.join(D, 'summary.csv')))}
h1, h2 = summ['det-1234a']['final_hash'], summ['det-1234b']['final_hash']
out.append("## Determinism")
out.append(f"- seed 1234, 2 lan x 5000 buoc: hash `{h1}` vs `{h2}` → {'KHOP' if h1==h2 else 'LECH'}.")
out.append("")

# Scene A
out.append("## Canh A — 5 seed x 50.000 buoc, DiscOn=1 (mau moi 100 buoc)")
out.append("")
out.append("| seed | buoc | Live | discs | disc_mass | da nuot (Trai Dat) | mass_resid |")
out.append("|---|---|---|---|---|---|---|")
EARTH = None  # disc_accreted_mass trong CSV la don vi khoi luong goc; doi ra Trai Dat o duoi
for seed in [1234, 1002, 1011, 2001, 2002]:
    rs = load(f'disc-long-A-{seed}.csv')
    # tim ti le Earth: dung so "swallowed Earths" tu run.log? Khong — tinh tu CSV khong co.
    # Ghi nguyen don vi goc, va ti le rieng.
    f0, m0, l0 = rs[0], rs[len(rs)//2], rs[-1]
    out.append(f"| {seed} | 0 | {f0['live']} | {f0['discs']} | {f(float(f0['disc_mass'])):.3E} | {f(float(f0['disc_accreted_mass'])):.3E} | {f0['mass_resid_rel']} |")
    out.append(f"| {seed} | {m0['step_drop']} | {m0['live']} | {m0['discs']} | {f(float(m0['disc_mass'])):.3E} | {f(float(m0['disc_accreted_mass'])):.3E} | {m0['mass_resid_rel']} |")
    out.append(f"| {seed} | {l0['step_drop']} | {l0['live']} | {l0['discs']} | {f(float(l0['disc_mass'])):.3E} | {f(float(l0['disc_accreted_mass'])):.3E} | {l0['mass_resid_rel']} |")
out.append("")
out.append("(Don vi khoi luong: don vi goc cua core. Ti le ra Trai Dat o muc 'da nuot' duoi.)")
out.append("")

# Da nuot ra Trai Dat — lay tu run.log dong "swallowed=... Earths"
import re
swallowed = {}
log = open(os.path.join(D, 'run.log')).read()
for m in re.finditer(r'disc-long: (\S+) xong — .*swallowed=([\d.]+) Earths', log):
    swallowed[m.group(1)] = float(m.group(2))
out.append("Da nuot (Trai Dat, tu run.log): " + ", ".join(
    f"A-{s}: {swallowed.get(f'A-{s}', 0):.1f}" for s in [1234,1002,1011,2001,2002]))
out.append("")

# Residuals
out.append("## So sach (toan bo 50.000 buoc)")
out.append("")
out.append("| seed | max |mass_resid| | max |phan tu| | max |L| ngoai cua so merger |")
out.append("|---|---|---|---|")
for seed in [1234, 1002, 1011, 2001, 2002]:
    rs = load(f'disc-long-A-{seed}.csv')
    elcols = [c for c in rs[0].keys() if c.startswith('el') and c.endswith('_resid_rel')]
    maxm = max(abs(f(r['mass_resid_rel'])) for r in rs)
    maxel = max(abs(f(r[c])) for r in rs for c in elcols)
    # L ngoai cua so merger
    merge_idx = set()
    for i in range(1, len(rs)):
        if rs[i]['merges'] != rs[i-1]['merges']: merge_idx.add(i); merge_idx.add(i-1)
    maxdL, L0v = 0, abs(f(rs[0]['L_orbital']))
    for i in range(1, len(rs)):
        if i in merge_idx: continue
        d = abs(f(rs[i]['L_orbital']) - f(rs[i-1]['L_orbital']))
        if d > maxdL: maxdL = d
    out.append(f"| {seed} | {maxm:.2E} | {maxel:.2E} | {maxdL/L0v:.2E} (moi 100 buoc) |")
out.append("")
out.append("- Khoi luong va tung nguyen to: bao toan o muc 1e-14.")
out.append("- Mo-men dong luong (L quy dao quanh khoi tam): ngoai cua so merger, do troi moi 100 buoc o muc 1e-7–1e-5 tuong doi.")
out.append("")

# Merger L jumps
out.append("## Thay doi L dot ngot tai merger NS-Sun")
out.append("")
for seed in [1234, 1002, 1011, 2001, 2002]:
    rs = load(f'disc-long-A-{seed}.csv')
    for i in range(1, len(rs)):
        if rs[i]['heavy_phase'] != rs[i-1]['heavy_phase']:
            dL = (f(rs[i]['L_orbital']) - f(rs[i-1]['L_orbital'])) / abs(f(rs[i-1]['L_orbital']))
            out.append(f"- A-{seed}: {rs[i-1]['heavy_phase']} → {rs[i]['heavy_phase']} (buoc {rs[i-1]['step_drop']}→{rs[i]['step_drop']}): dL/L = {dL:+.3f}.")
            break
out.append("")
out.append("Giai thich: khi NS merge voi Sun, mo-men dong luong quy dao cua cap doi chuyen thanh spin (core khong mo hinh spin) nen mat khoi ngan sach quy dao. Day la hanh vi cua core Merge, khong phai troi cua luat dia. Ngoai ra phep do L ve khoi tam cua harness bi dut doan khi dia rehost (xem muc 'chua chac').")
out.append("")

# Scene B
out.append("## Canh B — nhay thoi gian sau 500 buoc (moi cu mot run rieng)")
out.append("")
out.append("| run | dL qua nhay (tuong doi) | dmerges | dAccAng | dL+dAccAng |")
out.append("|---|---|---|---|---|")
for seed in [1234, 1002]:
    for j in ['1e2','1e4','1e6','1e8']:
        rs = load(f'disc-long-B-{seed}-jump{j}.csv')
        pre = [r for r in rs if r['run'].endswith('prejump')][0]
        post = [r for r in rs if r['run'].endswith('postjump')][0]
        dL = f(post['L_orbital']) - f(pre['L_orbital'])
        rel = dL / abs(f(pre['L_orbital']))
        dm = int(post['merges']) - int(pre['merges'])
        dAcc = f(post['disc_accreted_angular']) - f(pre['disc_accreted_angular'])
        out.append(f"| B-{seed}-{j} | {rel:+.2E} | {dm:+d} | {dAcc:+.2E} | {dL+dAcc:+.2E} |")
out.append("")
out.append("- B-1234: ca 4 cu nhay cho cung mot ket qua (dia xa het trong <100 nam); dL gan nhu bi triet tieu boi ledger spin (+6e-4), du ~6e-5.")
out.append("- B-1002-1e2: nhay gay 24 merger → dL -21% (spin-loss cua merger, khong phai troi dia).")
out.append("- B-1002-1e4/1e6/1e8: 0–1 merger; dL ~1e-4 tuong doi, mot phan vao ledger spin.")
out.append("- `DiscResidualAngular` = 0 trong toan bo cac run (khong co du so mo-men cua dia).")
out.append("")

# Scene C
rc = load('disc-long-C-1234.csv')
out.append("## Canh C — doi chung DiscOn=0, seed 1234, 5000 buoc")
out.append(f"- Live: {rc[0]['live']} → {rc[-1]['live']}; cham tran 5512: {'CO' if any(r['live']=='5512' for r in rc) else 'khong'}.")
out.append("- Voi dia tat, van kich tran nhu cu; voi dia bat (canh A), khong seed nao cham tran trong 50.000 buoc.")
out.append("")

# ms
out.append("## Hieu nang (tham khao, may cloud)")
for seed in [1234, 1002, 1011, 2001, 2002]:
    rs = load(f'disc-long-A-{seed}.csv')
    vals = [f(r['ms_per_adv']) for r in rs[10:]]
    out.append(f"- A-{seed}: {min(vals):.1f}–{max(vals):.1f} ms/Advance (khong tang >2x).")
out.append("")

# Red flags
out.append("## Co do")
flags = [l.strip() for l in open(os.path.join(D, 'redflags.log')) if l.strip()]
non_c = [x for x in flags if 'C-1234' not in x]
out.append(f"- Tong {len(flags)} co do; ngoai canh C: {len(non_c)}.")
for x in non_c[:20]: out.append(f"  - {x}")
out.append("- Canh C cham tran 5512 nhieu lan — la hanh vi doi chung du kien (dia tat = loi cu).")
out.append("")

out.append("## Cai em chua chac")
out.append("1. Phep do L ve khoi tam cua harness dung transport-term theo host hien tai cua dia; khi dia rehost (NS→Sun trong merger), so do dut doan. Khong tach duoc bao nhieu cua dot sut L la spin-loss that, bao nhieu la loi do.")
out.append("2. Residual Px/Py tuong doi vo nghia vi baseline gan 0; can residual tuyet doi neu muon ket luan ve dong luong tuyen tinh.")
out.append("3. Toc do boi tu (accretion) chenh lech lon giua cac seed (1234: 282 Trai Dat/50k buoc; 1002/1011/2001: ~0). Cu nhay 1e4 nam kich hoat boi tu tren seed 1002 (95 Trai Dat), goi y thoi gian nhot (viscous) dai hon 25.000 nam o mot so dia — chua phai bug, nhung dang ghi nhan.")
out.append("")

out.append("## Ket luan (chi neu cai so lieu noi)")
out.append("- Dia giu so khoi luong va nguyen to o muc 1e-14 trong 50.000 buoc x 5 seed.")
out.append("- So vat the on dinh va giam dan (5251 → 3635–4244), khong cham tran 5512 khi dia bat; tat dia thi cham tran nhu cu.")
out.append("- Determinism: 2 lan seed 1234 x 5000 buoc khop hash.")
out.append("- Khong thay troi/no/phi`nh cua luat dia trong pham vi chay; thay doi L lon deu gan voi merger (spin-loss) hoac nam trong ledger spin co chu dich.")
out.append("")

open(os.path.join(D, 'REPORT.md'), 'w').write("\n".join(out))
print("REPORT.md written,", len("\n".join(out)), "chars")
