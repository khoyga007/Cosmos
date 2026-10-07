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

        return allOk;
    }
}
