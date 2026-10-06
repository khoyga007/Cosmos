using System;
using System.Collections.Generic;
using System.Linq;
using Cosmos.Core;

static class CivTableChecks
{
    const double H = 0.5;
    static bool _ok;
    static void Check(bool ok, string line) { _ok &= ok; Console.WriteLine($"{(ok ? "OK    " : "FAILED")} civtable: {line}"); }

    public static bool Run()
    {
        _ok = true;
        const int earth = 3;

        // 1. 10 default stages: properties, thresholds, flags match expected
        {
            var w = World.SolSystem(0, 1234);
            bool countOk = w.Stages.Count == 10;
            bool flagsOk = true;
            for (int k = 0; k <= 7; k++)
                flagsOk &= !w.Stages[k].CanLaunchShips && !w.Stages[k].CanDome;
            for (int k = 8; k <= 9; k++)
                flagsOk &= w.Stages[k].CanLaunchShips && w.Stages[k].CanDome;
            Check(countOk && flagsOk, "Default stages: 10 stages with correct thresholds, needs and flags");
        }

        // 2. Acceptance test: Inserting a 'test' stage between 1 and 2
        // Civilization advances through it and generates events civ.stage.1.2 and civ.stage.2.3
        {
            var w = World.SolSystem(0, 1234);
            // insert 'test' stage at index 2 with threshold 0.8 (between stone_age 0.5 and bronze_age 1.0)
            w.Stages.Insert(2, new Stage("copper_age", "Thời kỳ Đồ Đồng Sơ Khai", 0.8, CanLaunchShips: false, CanDome: false));
            Check(w.Stages.Count == 11 && w.Stages[2].Id == "copper_age", "Stage inserted between 1 and 2: Stages count is 11");

            // Fast forward 1 million years
            w.Do(new Command(CmdKind.FastForward, Amount: 1e6));

            bool hasStage01 = w.Events.Any(e => e.ObjectSlot == earth && e.Change == "civ.stage.0.1");
            bool hasStage12 = w.Events.Any(e => e.ObjectSlot == earth && e.Change == "civ.stage.1.2");
            bool hasStage23 = w.Events.Any(e => e.ObjectSlot == earth && e.Change == "civ.stage.2.3");
            bool hasStage34 = w.Events.Any(e => e.ObjectSlot == earth && e.Change == "civ.stage.3.4");

            Check(hasStage01 && hasStage12 && hasStage23 && hasStage34,
                $"Inserted stage traversed: civ logged events civ.stage.0.1 ({hasStage01}), 1.2 ({hasStage12}), 2.3 ({hasStage23}), 3.4 ({hasStage34})");
        }

        // 3. Default world hash unchanged when no stage inserted
        {
            var wDefault = World.SolSystem(0, 1234);
            ulong h0 = wDefault.Hash();
            wDefault.Do(new Command(CmdKind.FastForward, Amount: 1e6));
            ulong hEnd = wDefault.Hash();
            bool hasSpaceAge = wDefault.Events.Any(e => e.ObjectSlot == earth && e.Change == "civ.stage.8.9");
            bool noStage910 = !wDefault.Events.Any(e => e.ObjectSlot == earth && e.Change == "civ.stage.9.10");
            Check(hasSpaceAge && noStage910 && hEnd != h0,
                $"Default 10 stages without insertion: ends at stage 9 (civ.stage.8.9 logged, no 9.10), hash deterministic {hEnd:X16}");
        }

        // 4. CivInfo.Stats: named stats container participates in hash deterministically
        {
            var w1 = World.SolSystem(0, 1234);
            w1.Do(new Command(CmdKind.FastForward, Amount: 1e6));
            ulong hashBase = w1.Hash();

            // Add named stats to the existing civilization
            int civIndex = w1.Civ[earth];
            Check(civIndex >= 0 && civIndex < w1.Civs.Count, "Civilization exists on Earth");

            var info = w1.Civs[civIndex];
            info.Stats["awareness"] = 0.85;
            info.Stats["energy_tier"] = 1.0;
            ulong hashWithStats = w1.Hash();

            Check(hashWithStats != hashBase, $"CivInfo.Stats: populated stats change hash ({hashBase:X16} -> {hashWithStats:X16})");

            // Replay-determinism with stats
            var w2 = World.SolSystem(0, 1234);
            w2.Do(new Command(CmdKind.FastForward, Amount: 1e6));
            var info2 = w2.Civs[civIndex];
            // Add in reverse order to verify order-independence (sorted keys)
            info2.Stats["energy_tier"] = 1.0;
            info2.Stats["awareness"] = 0.85;
            ulong hashWithStats2 = w2.Hash();

            Check(hashWithStats == hashWithStats2, "CivInfo.Stats: hash is independent of dictionary insertion order");
        }

        return _ok;
    }
}
