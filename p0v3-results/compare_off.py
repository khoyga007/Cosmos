# Vòng 3 mục 3: so bản DiscOn=0 (c4598a5) với control v2 (8f5ccbb, p0v2-results/control) ở mọi mốc.
import csv,sys,os
here=os.path.dirname(os.path.abspath(__file__))
v2=os.path.join(here,'v2-control-ref') if len(sys.argv)<2 else sys.argv[1]
cols=['live','N','merges','roche_events','reservoir_count','reservoir_mass','escaped_mass','roche_dissipated','ns_slot_mass','ns_slot_gen','ns_slot_alive']
for seed in (1234,1002,1011):
    a=list(csv.DictReader(open(os.path.join(v2,f'seed{seed}-checkpoints.csv'))))
    b=list(csv.DictReader(open(os.path.join(here,'off-control',f'seed{seed}-checkpoints.csv'))))
    assert len(a)==len(b),(len(a),len(b))
    raw=sum(x['hash']==y['hash'] for x,y in zip(a,b)); neu=sum(x['hash']==y['hash_dial_neutral'] for x,y in zip(a,b))
    cnt=sum(all(x[c]==y[c] for c in cols) for x,y in zip(a,b))
    first=[(x['step'],[c for c in ['hash_dial_neutral']+cols if x.get(c if c!='hash_dial_neutral' else 'hash')!=y[c]]) for x,y in zip(a,b) if x['hash']!=y['hash_dial_neutral'] or any(x[c]!=y[c] for c in cols)][:1]
    print(f'seed {seed}: mốc {len(a)}; Hash thô trùng {raw}/{len(a)}; Hash trung tính trùng {neu}/{len(a)}; 11 counter trùng (chuỗi R) {cnt}/{len(a)}; lệch đầu tiên {first or "không"}; hash bước 2000 v2 {a[-1]["hash"]} / off thô {b[-1]["hash"]} / off trung tính {b[-1]["hash_dial_neutral"]}')
