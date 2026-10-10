# Vòng 3: tổng hợp từ on/ off/ (p0-disc-v3) -> analysis_v3.json + in bảng. Chỉ đọc CSV, không chạy lại.
import csv,json,math,collections,os
here=os.path.dirname(os.path.abspath(__file__)); out={}
def rows(p): return list(csv.DictReader(open(os.path.join(here,p))))
for mode in ('off','on'):
  for s in (1234,1002,1011):
    st={int(r['step']):r for r in rows(f'{mode}/seed{s}-steps.csv')}
    led={int(r['step']):r for r in rows(f'{mode}/seed{s}-ledger.csv')}
    rec=rows(f'{mode}/seed{s}-records.csv')
    ecols=[c for c in led[2000] if c.startswith('elem_')]
    d={}
    # bước Mặt Trời bị nuốt: compact_victim_mass nhảy > 1000 E
    prev=0; sunstep=None
    for k in sorted(st):
      v=float(st[k]['compact_victim_mass_E_closure'])
      if v-prev>1000 and sunstep is None: sunstep=(k,v-prev)
      prev=v
    d['sun_into_compact_step_closure']=sunstep
    for k in (300,1000,2000):
      r=st[k]; l=led[k]
      d[k]=dict(live=int(r['live_direct']),merges=int(r['merges_direct']),disc=int(r['breaks_disc_direct']),ring=int(r['breaks_ring_direct']),stream=int(r['breaks_stream_direct']),
        disc_mass_E=float(r['disc_mass_all_E_direct']),disc_on_compact_E=float(r['disc_mass_on_compact_E_direct']),accreted_E=float(r['disc_accreted_E_direct']),
        compact_phase=r['compact_phase'],compact_mass_E=float(r['compact_mass_E_direct']),compact_merges_closure=int(r['compact_merges_closure']),
        compact_merges_events=int(r['compact_merges_events_direct']),ambiguous=int(r['compact_merges_ambiguous_steps']),compact_victim_E=float(r['compact_victim_mass_E_closure']),
        reservoir_E=float(r['reservoir_mass_E_direct']),escaped_E=float(r['escaped_mass_E_direct']),
        to_disc_by_origin={o.replace('to_disc_from_','').replace('_E_direct',''):float(r[o]) for o in r if o.startswith('to_disc_from_') and float(r[o])!=0},
        straight_fall_E=float(r['to_disc_straight_fall_E_closure']),
        near=dict(total=int(r['near_total_est']),unbound=int(r['near_unbound_est']),out_hot=int(r['near_outside_own_roche_hot_est']),out_cold=int(r['near_outside_own_roche_cold_est']),in_hot=int(r['near_inside_own_roche_hot_est']),in_cold=int(r['near_inside_own_roche_cold_est']),rmax=r['near_rmax_est'],rmax_body=r['near_rmax_body']),
        mass_resid_rel=float(l['mass_resid_rel']),px=float(l['px_resid_over_sum_m_v']),py=float(l['py_resid_over_sum_m_v']),
        elem_resid_max_abs=max(abs(float(l[c])) for c in ecols if l[c] not in ('inf',)),elem=dict((c,l[c]) for c in ecols))
    # max |resid| khối lượng suốt run (mẫu)
    d['mass_resid_rel_maxabs_sampled']=max(abs(float(x['mass_resid_rel'])) for x in led.values())
    d['elem_resid_rel_maxabs_sampled']=max(abs(float(x[c])) for x in led.values() for c in ecols if x[c]!='inf')
    # breakups theo formation x origin x host
    agg=collections.Counter(); m=collections.defaultdict(float)
    for r in rec: agg[(r['formation_direct'],r['source_origin'],r['host_kind'])]+=1; m[(r['formation_direct'],r['source_origin'],r['host_kind'])]+=float(r['captured_mass_E_direct'])
    d['breaks_by_form_origin_host']={'/'.join(k):[v,m[k]] for k,v in sorted(agg.items())}
    out[f'{mode}{s}']=d
# mục 6: stream ở seed 1234 bản ON
st=[r for r in rows('on/seed1234-records.csv') if r['formation_direct']=='stream']
def f(x):
  try: return float(x)
  except: return math.nan
s6=dict(count_total=len(st),count_le1000=sum(int(r['step'])<=1000 for r in st),
  steps=sorted(collections.Counter(int(r['step']) for r in st).items()),
  origin=collections.Counter(r['source_origin'] for r in st), host=collections.Counter(r['host_kind'] for r in st),
  origin_class=collections.Counter(r['origin_class'] for r in st),
  fully_unbound=sum(f(r['captured_mass_E_direct'])==0 for r in st), partly_bound=sum(0<f(r['bound_fraction_direct'])<1 for r in st), fully_bound=sum(f(r['bound_fraction_direct'])==1 for r in st),
  source_mass_E_sum=sum(f(r['source_mass_E_direct']) for r in st), captured_E_sum=sum(f(r['captured_mass_E_direct']) for r in st), escaped_E_sum=sum(f(r['escaped_mass_E_direct']) for r in st),
  source_mass_E_min=min(f(r['source_mass_E_direct']) for r in st), source_mass_E_max=max(f(r['source_mass_E_direct']) for r in st),
  est_excess_over_vapor=[r['excess_over_vapor_est'] for r in st], est_spec=[r['specific_energy_est'] for r in st])
out['item6_seed1234_on']=s6
json.dump(out,open(os.path.join(here,'analysis_v3.json'),'w'),indent=1,default=str,ensure_ascii=False)
for k,v in out.items():
  if k.startswith('item6'): continue
  print(k,'sun->compact',v['sun_into_compact_step_closure'],'massresid max',v['mass_resid_rel_maxabs_sampled'],'elem max',v['elem_resid_rel_maxabs_sampled'])
  for st_ in (1000,2000): x=v[st_]; print('  ',st_,{a:x[a] for a in x if a not in('elem','near')},x['near'])
  print('   breaks',v['breaks_by_form_origin_host'])
print(json.dumps({k:v for k,v in s6.items() if k not in ('est_excess_over_vapor','est_spec')},default=str))
print(s6['est_excess_over_vapor']); print(s6['est_spec'])
