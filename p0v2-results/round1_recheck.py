# Re-reads round-1 raw CSVs (branch hark/p0-results, raw/seedN.csv; sampled every 50 steps to 2000, then every 1000).
# Usage: python3 round1_recheck.py <dir with round-1 raw/seed*.csv>
import csv,glob,sys
d=sys.argv[1] if len(sys.argv)>1 else 'raw'
print("seed live_end qmin_over_run(step) max_q_lt1000(step) last_sample_with_q_lt1000 samples_live>=5511_after_step2000 live_min_after2000 live_max_after2000 qmin_end lt1000_end")
for f in sorted(glob.glob(d+'/seed*[0-9].csv')):
    s=f.split('seed')[-1][:-4]; r=list(csv.DictReader(open(f)))
    mn=min((float(x['peri_min_Rc']),int(x['step'])) for x in r if x['peri_min_Rc']!='NaN')
    mx=max((int(x['peri_lt1000Rc']),int(x['step'])) for x in r)
    late=[x for x in r if int(x['step'])>=2000]; lv=[int(x['live']) for x in late]
    lastlt=[int(x['step']) for x in r if int(x['peri_lt1000Rc'])>0]
    print(s,r[-1]['live'],f"{mn[0]:.3g}({mn[1]})",f"{mx[0]}({mx[1]})",max(lastlt) if lastlt else None,
          f"{sum(1 for v in lv if v>=5511)}/{len(lv)}",min(lv),max(lv),r[-1]['peri_min_Rc'],r[-1]['peri_lt1000Rc'])
