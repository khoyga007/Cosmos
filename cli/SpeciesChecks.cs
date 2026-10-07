using System;
using System.Collections.Generic;
using System.Linq;
using Cosmos.Core;

static class SpeciesChecks
{
    static void Check(bool ok, string line, ref bool allOk)
    {
        allOk &= ok;
        Console.WriteLine($"{(ok ? "OK    " : "FAILED")} {line}");
    }

    public static bool Run()
    {
        bool allOk = true;

        // Check 1: SpeciesParamCatalog completeness (table-driven, 1 row per param)
        var requiredParams = new[] { "habitat", "manipulation", "energy_basis", "senses", "lifespan", "social", "temp_min", "temp_max" };
        bool hasAll = requiredParams.All(p => SpeciesParamCatalog.ById.ContainsKey(p));
        Check(hasAll && SpeciesParamCatalog.Params.Count >= 8,
            $"species-catalog-params: {SpeciesParamCatalog.Params.Count} params defined, covers all 8 required parameters", ref allOk);

        // Check 2: Human template specs match SPEC §9 bit-exact
        var h = SpeciesCatalog.Human;
        bool humanMatches = h.Id == "human"
            && h.Habitat == Habitat.Land
            && Math.Abs(h.Manipulation - 1.0) < 1e-9
            && h.EnergyBasis == EnergyBasis.Chem
            && h.Senses == SenseKind.Vision
            && Math.Abs(h.Lifespan - 80.0) < 1e-9
            && h.Social == SocialStructure.Band
            && Math.Abs(h.TempMin - 250.0) < 1e-9
            && Math.Abs(h.TempMax - 320.0) < 1e-9;
        Check(humanMatches,
            $"species-human-template: Habitat={h.Habitat}, Manip={h.Manipulation:F1}, Energy={h.EnergyBasis}, Senses={h.Senses}, Lifespan={h.Lifespan:F0}yr, Social={h.Social}, Temp={h.TempMin:F0}..{h.TempMax:F0}K", ref allOk);

        // Check 3: Table-driven parameter access and free composition (adding extra row without breaking)
        var alien = h.WithParam("habitat", Habitat.Ocean)
                     .WithParam("manipulation", 0.7)
                     .WithParam("energy_basis", EnergyBasis.Thermal)
                     .WithParam("senses", SenseKind.Sonar)
                     .WithParam("lifespan", 300.0)
                     .WithParam("social", SocialStructure.Hive)
                     .WithParam("temp_min", 270.0)
                     .WithParam("temp_max", 370.0)
                     .WithParam("bioluminescence", true);

        bool alienMatches = (Habitat)alien.GetParam("habitat") == Habitat.Ocean
            && Math.Abs((double)alien.GetParam("manipulation") - 0.7) < 1e-9
            && (EnergyBasis)alien.GetParam("energy_basis") == EnergyBasis.Thermal
            && (SenseKind)alien.GetParam("senses") == SenseKind.Sonar
            && Math.Abs((double)alien.GetParam("lifespan") - 300.0) < 1e-9
            && (SocialStructure)alien.GetParam("social") == SocialStructure.Hive
            && (bool)alien.GetParam("bioluminescence") == true;
        Check(alienMatches,
            $"species-free-composition: custom alien with ocean/sonar/hive/bioluminescence composed seamlessly", ref allOk);

        // Check 4: CivInfo integration & default human assignment
        var w = World.SolSystem(0, 1234);
        int earth = 3;
        // Force spawn civ on Earth
        w.Life[earth] = 1.0;
        w.Pop[earth] = 0.01;
        w.Advance(10.0); // runs civ rule and creates civ if not already created
        if (w.Civs.Count == 0)
        {
            // If advance didn't create, simulate NewCiv via reflection or check default structure
            var civInfo = new CivInfo("TestCiv", earth, w.Year);
            Check(civInfo.Species == SpeciesCatalog.Human,
                "species-civinfo-default: unassigned CivInfo.Species defaults to SpeciesCatalog.Human", ref allOk);
        }
        else
        {
            Check(w.Civs[0].Species == SpeciesCatalog.Human,
                $"species-civ-created: spawned civ '{w.Civs[0].Name}' carries human species template", ref allOk);
        }

        // Check 5: Hash invariant (bit-equal with baseline)
        var wA = World.SolSystem(500, 4321);
        wA.Advance(10.0);
        ulong hashA = wA.Hash();

        var wB = World.SolSystem(500, 4321);
        wB.Advance(10.0);
        ulong hashB = wB.Hash();

        Check(hashA == hashB,
            $"species-hash-invariant: hash deterministic and bit-equal ({hashA:X16})", ref allOk);

        // Check 6: Non-default species alters World.Hash() (A3/S1 defect closure)
        {
            var wProbe = new World(4, 7);
            wProbe.Civs.Add(new CivInfo("probe", -1, 0));
            ulong hBefore = wProbe.Hash();
            wProbe.Civs[0] = wProbe.Civs[0] with { Species = SpeciesCatalog.Human.WithParam("lifespan", 300.0) };
            ulong hAfter = wProbe.Hash();
            Check(hBefore != hAfter,
                $"species-hash-nondefault: nondefault species alters World.Hash() ({hBefore:X16} -> {hAfter:X16})", ref allOk);
        }

        // Check 7: WithParam rejects NaN/Infinity and out of range values
        {
            bool threwNan = false;
            try { SpeciesCatalog.Human.WithParam("manipulation", double.NaN); }
            catch (ArgumentOutOfRangeException) { threwNan = true; }
            Check(threwNan, "species-validate-nan: manipulation=NaN rejected with ArgumentOutOfRangeException", ref allOk);

            bool threwNegativeLifespan = false;
            try { SpeciesCatalog.Human.WithParam("lifespan", -1.0); }
            catch (ArgumentOutOfRangeException) { threwNegativeLifespan = true; }
            Check(threwNegativeLifespan, "species-validate-bounds: lifespan=-1 rejected with ArgumentOutOfRangeException", ref allOk);
        }

        // Check 8: Defensive copy of Extra dictionary against external mutation
        {
            var extraDict = new Dictionary<string, object> { { "bioluminescence", true } };
            var spAlien = new Species("probe", "probe", Extra: extraDict);
            extraDict["bioluminescence"] = false;
            Check((bool)spAlien.GetParam("bioluminescence") == true,
                "species-defensive-extra: external mutation of caller dict does not alter Species.Extra", ref allOk);
        }

        // Check 9: Extra dictionary getter returns read-only collection (mutation cast throws)
        {
            var extraDict = new Dictionary<string, object> { { "glow", true } };
            var sp = new Species("probe", "probe", Extra: extraDict);
            bool castMutateRefused = false;
            try { ((IDictionary<string, object>)sp.Extra)["glow"] = false; }
            catch (NotSupportedException) { castMutateRefused = true; }
            Check(castMutateRefused, "species-extra-readonly: ((IDictionary)species.Extra) mutation throws NotSupportedException", ref allOk);
        }

        // Check 10: Constructor validates parameters and rejects NaN / out of range values
        {
            bool ctorRefused = false;
            try { _ = new Species("bad", "bad", Manipulation: double.NaN, Lifespan: -1.0); }
            catch (ArgumentOutOfRangeException) { ctorRefused = true; }
            Check(ctorRefused, "species-constructor-validate: constructor rejects NaN/out-of-range via ArgumentOutOfRangeException", ref allOk);
        }

        // Check 11: Species string boundary separation in HashCiv
        {
            var wS1 = new World(4, 7); wS1.Civs.Add(new CivInfo("p", -1, 0, Species: new Species("ab", "c")));
            var wS2 = new World(4, 7); wS2.Civs.Add(new CivInfo("p", -1, 0, Species: new Species("a", "bc")));
            Check(wS1.Hash() != wS2.Hash(), $"species-string-boundary: string boundary separation ({wS1.Hash():X16} != {wS2.Hash():X16})", ref allOk);
        }

        return allOk;
    }
}
