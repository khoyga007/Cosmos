using System;
using System.Collections.Generic;
using System.Linq;
using Cosmos.Core;

static class CapNodeChecks
{
    static void Check(bool ok, string line, ref bool allOk)
    {
        allOk &= ok;
        Console.WriteLine($"{(ok ? "OK    " : "FAILED")} {line}");
    }

    public static bool Run()
    {
        bool allOk = true;

        // Check 1: Catalog completeness and domain coverage (SPEC §9)
        var domains = Enum.GetValues<CapDomain>();
        bool coversAllDomains = domains.All(d => CapNodeCatalog.Nodes.Any(n => n.Domain == d));
        Check(coversAllDomains && CapNodeCatalog.Nodes.Count >= 14,
            $"capnodes-catalog: {CapNodeCatalog.Nodes.Count} nodes defined, covers all 5 domains (Heat, Material, Knowledge, Power, Space)", ref allOk);

        // Check 2: Human progression bit-equal mapping on Earth
        var w = World.SolSystem(0, 1234);
        int earth = 3;
        var human = SpeciesCatalog.Human;

        var opened0 = CapGraph.EvaluateOpened(w, earth, human, 0.0);
        bool p0 = opened0.Contains("stonework") && opened0.Contains("memory") && !opened0.Contains("fire");

        var opened05 = CapGraph.EvaluateOpened(w, earth, human, 0.5);
        bool p05 = opened05.Contains("fire") && opened05.Contains("writing");

        var opened10 = CapGraph.EvaluateOpened(w, earth, human, 1.0);
        bool p10 = opened10.Contains("smelting") && opened10.Contains("geothermal");

        var opened20 = CapGraph.EvaluateOpened(w, earth, human, 2.0);
        bool p20 = opened20.Contains("alloys") && opened20.Contains("solar_furnace");

        var opened30 = CapGraph.EvaluateOpened(w, earth, human, 3.0);
        bool p30 = opened30.Contains("orbit") && CapNodeCatalog.ById["orbit"].Effect.CanLaunchShips;

        var opened35 = CapGraph.EvaluateOpened(w, earth, human, 3.5);
        bool p35 = opened35.Contains("interplanetary") && opened35.Contains("fusion");

        bool humanLadder = p0 && p05 && p10 && p20 && p30 && p35;
        Check(humanLadder,
            "capnodes-human-progression: start nodes unlock progressively matching traditional 10-era ladder", ref allOk);

        // Check 3: Era label derivation
        string era0 = CapGraph.DeriveEraLabel(human, opened0, 0.0, w, earth);
        string eraSpace = CapGraph.DeriveEraLabel(human, opened30, 3.0, w, earth);
        Check(era0 == "Thời kỳ Tiền sử" && eraSpace == "Kỷ nguyên Tiền Vũ trụ",
            $"capnodes-era-labels: human era labels preserve 10-era names ('{era0}', '{eraSpace}')", ref allOk);

        // Check 4: Habitat filter (Ocean cannot unlock fire, but can unlock geothermal -> smelting)
        var oceanSpecies = human with { Habitat = Habitat.Ocean };
        var oceanOpened10 = CapGraph.EvaluateOpened(w, earth, oceanSpecies, 1.0);
        bool oceanNoFire = !oceanOpened10.Contains("fire");
        bool oceanHasGeothermal = oceanOpened10.Contains("geothermal");
        bool oceanHasSmelting = oceanOpened10.Contains("smelting");
        Check(oceanNoFire && oceanHasGeothermal && oceanHasSmelting,
            "capnodes-habitat-filter: ocean species cannot ignite fire, but reaches smelting via geothermal heat", ref allOk);

        // Check 5: Manipulation filter (Low manipulation blocks toolmaking and writing)
        var lowManipSpecies = human with { Manipulation = 0.1 };
        var lowManipOpened = CapGraph.EvaluateOpened(w, earth, lowManipSpecies, 1.0);
        bool lowManipBlocked = !lowManipOpened.Contains("stonework") && !lowManipOpened.Contains("writing") && !lowManipOpened.Contains("smelting");
        Check(lowManipBlocked,
            "capnodes-manipulation-filter: low manipulation (0.1) prevents stonework, writing, and smelting", ref allOk);

        // Check 6: Escape velocity delta-v cost on heavy planets
        int jupiter = 5;
        double vEarth = CapGraph.EscapeVelocity(w, earth);
        double vJup = CapGraph.EscapeVelocity(w, jupiter);
        double ratio = CapGraph.EscapeVelocityRatio(w, jupiter);
        double reqEarthOrbit = CapGraph.EffectiveThreshold(CapNodeCatalog.ById["orbit"], w, earth);
        double reqJupOrbit = CapGraph.EffectiveThreshold(CapNodeCatalog.ById["orbit"], w, jupiter);

        bool escapeCostHigher = vJup > vEarth && ratio > 4.0 && reqJupOrbit > reqEarthOrbit * 4.0;
        Check(escapeCostHigher,
            $"capnodes-escape-velocity: v_esc Earth={vEarth:F3}, Jupiter={vJup:F3} ({ratio:F2}x); orbit tech threshold scales {reqEarthOrbit:F1} -> {reqJupOrbit:F1}", ref allOk);

        // Check 7: Lifespan knowledge rate scaling (Const field reflection)
        double rate80 = CapGraph.KnowledgeRateMultiplier(w, human);
        var longLived = human with { Lifespan = 320.0 };
        double rate320 = CapGraph.KnowledgeRateMultiplier(w, longLived);
        Check(Math.Abs(rate80 - 1.0) < 1e-9 && Math.Abs(rate320 - 2.0) < 1e-9,
            $"capnodes-lifespan-rate: knowledge multiplier lifespan 80yr={rate80:F2}x, 320yr={rate320:F2}x", ref allOk);

        // Check 8: Hash invariant (bit-equal simulation)
        var wA = World.SolSystem(500, 777);
        wA.Advance(10.0);
        ulong hashA = wA.Hash();

        var wB = World.SolSystem(500, 777);
        wB.Advance(10.0);
        ulong hashB = wB.Hash();

        Check(hashA == hashB,
            $"capnodes-hash-invariant: simulation hash bit-equal ({hashA:X16})", ref allOk);

        return allOk;
    }
}
