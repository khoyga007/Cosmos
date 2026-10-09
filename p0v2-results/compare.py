import csv,sys
EM=1.5e-4
out=[]
for s in (1234,1002,1011):
    old={int(r['step']):r for r in csv.DictReader(open(f'old/seed{s}.csv'))}
    ctl={int(r['step']):r for r in csv.DictReader(open(f'control/seed{s}-checkpoints.csv'))}
    new={int(r['step']):r for r in csv.DictReader(open(f'new/seed{s}-checkpoints.csv'))}
    mism=0;n=0
    for st,o in old.items():
        c=ctl.get(st)
        if c is None: continue
        n+=1
        ok = o['live']==c['live'] and o['N']==c['N'] and float(o['roche_dissipated'])==float(c['roche_dissipated']) and f"{float(c['ns_slot_mass'])/EM:.6G}".replace('E+0','E+')==o['compact_mass_earth'] or (o['live']==c['live'] and o['N']==c['N'] and float(o['roche_dissipated'])==float(c['roche_dissipated']) and abs(float(c['ns_slot_mass'])/EM-float(o['compact_mass_earth']))/float(o['compact_mass_earth'])<1e-5)
        if not ok: mism+=1; print('MISMATCH',s,st,o['live'],c['live'],o['N'],c['N'])
    hashes_equal = all(ctl[k]['hash']==new[k]['hash'] for k in ctl) and set(ctl)==set(new)
    oh=[l.split()[1] for l in open(f'old/seed{s}-summary.txt') if l.startswith('hash')][0]
    out.append((s,n,mism,hashes_equal,len(ctl),ctl[2000]['hash'],new[2000]['hash'],oh))
    print(s,'old-vs-control rows',n,'mismatch',mism,'| control-vs-new checkpoints',len(ctl),'all hash+counters identical',hashes_equal,'| hash@2000 ctl',ctl[2000]['hash'],'new',new[2000]['hash'],'old',oh)
