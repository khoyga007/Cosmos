# Review rebased A3/S1/S2 — Celine

Exact snapshots: master461ce8778e6ae0d450e53758fa8f59c9b29fbe51; A3 5275f03fc70f6fe89be6d3f73a0781785dcc795b; S1 1ad6c913ef44faa2df7578f1a9b1a0256be600a3; S2 4e635af02eb92155f892bff798704d9595cf2d96. Detached snapshots under E:/Temp; independent probes reference their exact core projects. No edits to Ariel's source.

## Blocking repros

- `core/World.cs:475` — skipping each new default field independently drops its identity from the hash stream. WorldA Ref160/Exp.5 and WorldB Ref80/Exp160 both hash **27AE9113E03F0120**, but Human knowledge multipliers are **.7071067811865476** and **1**. Hash both values as one explicitly tagged block whenever either differs, or tag each nondefault field. Both defaults must preserve compatibility; each override and both overrides must remain distinguishable.
- `core/Civ.cs:29` / `core/CapNodes.cs:201` — `C.Set("CivLifespanRef",0)` succeeds and KnowledgeRateMultiplier becomes Infinity. Ref is a divisor and must be strictly positive; common finite/negative validation alone is insufficient.
- `core/Species.cs:46,49,53,142` — constructor validation/freeze does not guard public init setters. `Human with {Manipulation=NaN,Lifespan=-1}` succeeds. `Human with {Extra=callerDict}` aliases a mutable dictionary; WithParam for an extra key also assigns a bare Dictionary. Enforce validation and defensive read-only copying at init boundaries, including unknown/extra WithParam paths.

Probe `E:/Temp/cosmos-rebased-probe/Program.cs`, compiled against4e635af, printed:

```text
one-field alias: Ref160 27AE9113E03F0120, Exp160 27AE9113E03F0120, same=True, rates=0.7071067811865476/1
restore defaults=True
both override hash=2C0FA0EB32A9D391, baseline=E4F1C1FBEF708A6C
zero lifespan reference accepted=True, rate=∞
constructor rejects NaN=True
with bypass: NaN/-1
with Extra alias: glow=False, type=Dictionary
WithParam unknown Extra type=Dictionary
```

## Human cross-binary result

Same `E:/Temp/cosmos-rebased-human/Program.cs` built against4 independent core binaries; Sol0/seed1234, jump1e6yr, then Push Earth(.01,-.01)+20×Advance(.5):

| Revision | Initial | +1e6yr | Push+20 |
|---|---|---|---|
|master461ce87|7E670A9BD7AE133C|9D5E2BA9B54AA079|44BE9E29D46FF7E9|
|A3 5275f03|66B1883E599E57EE|D55A7885CD8D68E2|E42AD90E459EFA3A|
|S1 1ad6c91|66B1883E599E57EE|D55A7885CD8D68E2|E42AD90E459EFA3A|
|S2 4e635af|A77E2A95C2C1781A|0BD9E055E0F35D36|FA96E665E9A810AE|

The requested master↔S2 equality fails in both scenarios. A3 deliberately changes era pacing/consumption and replaces CivTechRate/TechMaxCeiling configuration, so this is not solely a lifespan-field elision issue. At1e6yr all4 have civ1/Tech4/Pop.9998875097515015, but master consumed2.4422837382523324e-6 and metal4.565701107184115e-5; A3/S1/S2 consumed1.972219643607632e-12 and metal4.799999811026832e-5. Master events21 versus19 on the others. Push coordinates match: X-41.400902146915854/Y-16.774713963946162.

S1 preserves A3's hashes. S2 restores the dead CivMetalUse field, which A3 removed; that accounts for a new metadata hash difference even when lifespan defaults are elided. Claire must decide the compatibility baseline after accepted A3 behavior changes, then require S1/S2 to preserve it. Do not recapture and call unequal hashes bit-equal.

## Independently passed checks

Exact snapshots: `cli -- civtable`12OK, `species`12OK, `capnodes`13OK, all exit0. Independent probe confirms constructor NaN rejection, constructor Extra read-only, immutable Stage.Needs, separated Stage id/name hash strings, generic label for modified Human, cold airless fire/geothermal refusal, and restoration of both lifespan defaults.

These are improvements over earlier snapshots, but the builtins omit crossed single-field overrides, public record-with bypass and master cross-binary comparison. No approval of S2 is given on this evidence. Oxidizer/vent checks remain coarse proxies for the available element/temperature model; do not describe total gas or surface temperature as a measured oxygen inventory or vent flux.
