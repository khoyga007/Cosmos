# S2 independent review

Target `ae0cc4ee09b537e93c442430373d0f51eac173c5`, detached snapshot `E:/Temp/cosmos-capnodes-review-ae0cc4e`.
Probe `E:/Temp/cosmos-capnodes-probe/Program.cs`; built against that exact snapshot. No changes to Ariel's source.

- `core/CapNodes.cs:295` — changing Human manipulation to .1 keeps `Id=human`; at Tech=2 the opened set lacks stonework/writing/smelting but the label says Trung Cổ. Restrict the legacy label to the canonical Human path; other bodies derive from their available/opened capabilities.
- `core/CapNodes.cs:198` — lifespan reference/exponent are static constants, absent from `Consts.All()` and state hash/dials. `C.Set("CivLifespanRef",160)` returns false. SPEC9 requires these through Const.
- `core/CapNodes.cs:240` — a gas-free, lifeless body with rock .5, ice .499, carbon .001 can unlock fire because ice counts as oxygen. Water/ice is not an available oxygen atmosphere; require an explicit oxygen proxy supported by the current element model.
- `core/CapNodes.cs:251` — the same unheated body (Temp NaN, no star, no geothermal source) passes `RequiresVentHeat` solely because rock share is .5. Require a modeled heat source or a documented conservative proxy that actually uses heat.

Evidence: builtin `cli -- capnodes` passes all 8 checks. Independent probe prints:

```text
id=human, manipulation=0.1, opened=fire,geothermal,solar_furnace,memory, label=Thời kỳ Trung Cổ
lifespan fields=, setRef=False
cold airless: Temp=NaN, geothermal=True
ice world fire=True, gas=0, life=0
```

This slice exposes a table/filter helper. Its effects and knowledge multiplier are not yet wired into World progression; that is stated as scope, not reported as a S2 integration defect. The builtin hash test compares repeated runs of the same build; it alone does not demonstrate cross-version equality. The separate S1 review includes a two-binary Human comparison against b08c41a.
