# Re-review fixed A3/S1 and rebased S2

Exact heads: A3 `729ee6ff7c3736a5bc46bdac7788b98cf3ba2dd8`, S1 `a3155fe0f530d4112f0a3345f539e13dd733a41f`, S2 `67a0c96206832132eb74cdf8d54b24e2a71d6c91`. Independent detached snapshots under `E:/Temp/cosmos-{a3,s1,s2}-fixed-*`; probe `E:/Temp/cosmos-fixed-review/Program.cs` references exact67a0c96, which contains both fixes. No changes to Ariel's code.

Six original checks now pass: replacing EarthYears changes hash; replacing Needs with changed BecomesWaste changes hash; nondefault per-Civ Species changes hash; WithParam rejects manipulationNaN and lifespan-1; constructor copies the caller Extra dictionary. These are closed for the original minimal inputs.

Independent two-binary Human comparison (A3 baseline729ee6f versus S1a3155fe) also matches: after1e6yr both **03AB169D8DAB2DB2** (civ1, Tech4, Pop.9998875097515015); Push Earth(.01,-.01)+20×Advance(.5) both **65BB338A5DBA4593**. Probe `E:/Temp/cosmos-fixed-human/{a3,s1}/Human.csproj` shares the same Program.cs. This supports two specific default-path scenes, not a claim of100% coverage.

Remaining/new failures:

- `core/Layers.cs:158` / `core/Layers.cs:48` — Stages.AddRange(DefaultStages) shares mutable Needs arrays. `((StageNeed[])w.Stages[6].Needs)[0]=old with {BecomesWaste=!old.BecomesWaste}` changes a second world's row, while `w.IsDefaultStages=true` and its hash stays unchanged. Freeze the default catalog and each Needs collection; compare against an immutable canonical baseline.
- `core/Species.cs:58` — defensive input copying still exposes a mutable Dictionary through the Extra getter. `((IDictionary<string,object>)species.Extra)["glow"]=false` changes the supposedly fixed birth record without WithParam. Return an actually read-only collection; copying alone does not freeze it.
- `core/Species.cs:40` — constructor and record `with` setters bypass WithParam validation. `new Species("bad","bad",Manipulation:double.NaN,Lifespan:-1)` succeeds. Enforce bounds at supported ingress/property initialization too; the graph's `< MinManipulation` test can otherwise accept NaN.
- `core/Layers.cs:537` — hash strings have no lengths: one custom Stage with Id`ab`/NameVi`c` hashes exactly like Id`a`/NameVi`bc`, **6EB405EB57C4D23A** for both. Add lengths/separators consistently to new metadata hashing, including Species id/name.

S2 has only been rebased; the four findings from `S2-CROSS-REVIEW.md` remain in67a0c96. The probe still returns the legacy Human era label for manipulation.1, refuses the absent lifespan Const dial, and grants fire/geothermal to an unheated gas-free lifeless ice/rock body. Passing8 builtin checks does not cover these cases. S2 acceptance remains open.

Each alias probe restores the shared stage need before continuing, so later results are not artifacts of a deliberately mutated global catalog. The independent probe prints six `OK` lines followed by the demonstrated residual failures; its exit0 is execution success, not an approval of all findings.
