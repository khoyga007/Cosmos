import csv,json
res={}
for s in (1234,1002,1011):
    o=[r for r in csv.DictReader(open(f'old/seed{s}.csv')) if r['step']=='2000'][0]
    L=[r for r in csv.DictReader(open(f'new/seed{s}-ledger.csv')) if r['step']=='2000'][0]
    st=list(csv.DictReader(open(f'new/seed{s}-steps.csv')))
    q=list(csv.DictReader(open(f'new/seed{s}-q.csv')))
    B=float(L['belt_mass0']); f=lambda k: float(L[k])/B*100
    obs_steps=[int(x['step']) for x in st if int(x['obs_n_lt1000Rc'])>0]
    obs50=[x for x in obs_steps if x%50==0]
    pp=[int(x['step']) for x in st if x['peri_pass_min_q_Rc_est']!='inf' and x['peri_pass_min_q_Rc_est']!='Infinity' and float(x['peri_pass_min_q_Rc_est'])<1000]
    lt100=[(int(x['step']),x['obs_min_r_Rc_direct']) for x in st if int(x['obs_n_lt100Rc'])>0]
    q0=q[0]
    d=dict(
      old_compact=float(o['belt_frac_compact'])*100, old_other=float(o['belt_frac_other'])*100, old_esc=float(o['belt_frac_roche_escaped'])*100,
      old_res=float(o['belt_frac_reservoir'])*100, old_bound=float(o['belt_frac_alive_bound'])*100, old_unb=float(o['belt_frac_alive_unbound'])*100,
      old_sum=sum(float(o[k]) for k in ['belt_frac_compact','belt_frac_other','belt_frac_roche_escaped','belt_frac_reservoir','belt_frac_sublimated','belt_frac_alive_bound','belt_frac_alive_unbound'])*100,
      alive=f('alive_direct'), bound=f('alive_bound_heaviest_est'), unb=f('alive_unbound_heaviest_est'), esc=f('roche_escaped_direct'),
      res_unb=f('reservoir_unbound_direct'), res_unres=f('reservoir_unresolved_direct'), d_c=f('death_compact_closure'), d_o=f('death_other_closure'),
      d_u=f('death_unknown'), lost=f('lost_instep_compact_closure')+f('lost_instep_other_closure')+f('lost_instep_unknown'), partial=f('partial_loss_direct'),
      unkorig=f('unknown_origin'), tiny=f('tiny_unknown'), resid=float(L['residual_unattributed']), resid_rel=float(L['residual_unattributed'])/B,
      via_puller=f('belt_via_puller_into_compact_closure'), heur=f('death_compact_heuristic_est'), split_unk=f('belt_other_split_unknown'),
      cons_max=max(abs(float(x['global_conservation_residual'])) for x in st), cons_rel=max(abs(float(x['global_conservation_residual'])) for x in st)/B,
      obs_steps=len(obs_steps), obs50=len(obs50), pp_lt1000_steps=len(pp), lt100=lt100,
      q0_lt1000=q0['q_lt1000_bound_all_est'], q0_min=q0['q_min_bound_all_Rc_est'],
      modes={}, B=B)
    for x in st: d['modes'][x['compact_mode']]=d['modes'].get(x['compact_mode'],0)+1
    res[s]=d
json.dump(res,open('analysis.json','w'),indent=1)
for s,d in res.items(): print(s,json.dumps(d))
