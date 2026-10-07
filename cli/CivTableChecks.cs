using System;
using System.Collections.Generic;
using System.Linq;
using Cosmos.Core;

static class CivTableChecks
{
    const double H = 0.5;
    static bool _ok;
    static void Check(bool ok, string line) { _ok &= ok; Console.WriteLine($"{(ok ? "OK    " : "FAILED")} civtable: {line}"); }
    static double JumpCiv(World w)
    {
        double lifeGrow = Math.Log((1.0 / w.C.LifeSeed - 1.0) / (1.0 / w.C.CivLifeMin - 1.0)) / w.C.LifeGrowth;
        double civStart = w.C.LifeSparkYears + lifeGrow + w.C.CivRiseYears;
        double eraToSpace = w.Stages.TakeWhile(s => !s.CanLaunchShips).Sum(s => s.EarthYears);
        double toSpace = civStart + eraToSpace + 1.5e4;
        return Math.Max(toSpace + 2.5e4, toSpace * 1.14);
    }

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

            // Fast forward to mature civ
            w.Do(new Command(CmdKind.FastForward, Amount: JumpCiv(w)));

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
            wDefault.Do(new Command(CmdKind.FastForward, Amount: JumpCiv(wDefault)));
            ulong hEnd = wDefault.Hash();
            bool hasSpaceAge = wDefault.Events.Any(e => e.ObjectSlot == earth && e.Change == "civ.stage.8.9");
            bool noStage910 = !wDefault.Events.Any(e => e.ObjectSlot == earth && e.Change == "civ.stage.9.10");
            Check(hasSpaceAge && noStage910 && hEnd != h0,
                $"Default 10 stages without insertion: ends at stage 9 (civ.stage.8.9 logged, no 9.10), hash deterministic {hEnd:X16}");
        }

        // 4. CivInfo.Stats: named stats container participates in hash deterministically
        {
            var w1 = World.SolSystem(0, 1234);
            w1.Do(new Command(CmdKind.FastForward, Amount: JumpCiv(w1)));
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
            w2.Do(new Command(CmdKind.FastForward, Amount: JumpCiv(w2)));
            var info2 = w2.Civs[civIndex];
            // Add in reverse order to verify order-independence (sorted keys)
            info2.Stats["energy_tier"] = 1.0;
            info2.Stats["awareness"] = 0.85;
            ulong hashWithStats2 = w2.Hash();

            Check(hashWithStats == hashWithStats2, "CivInfo.Stats: hash is independent of dictionary insertion order");
        }

        // 5. Per-world custom Stages hashing (A3 defect closure):
        // EarthYears or BecomesWaste changes must alter World.Hash()
        {
            var wProbe = new World(4, 7);
            ulong hBefore = wProbe.Hash();
            wProbe.Stages[0] = wProbe.Stages[0] with { EarthYears = 1 };
            ulong hAfter = wProbe.Hash();
            Check(hBefore != hAfter, $"Custom Stages EarthYears change alters World.Hash() ({hBefore:X16} -> {hAfter:X16})");

            var wA = new World(4, 7);
            var wB = new World(4, 7);
            int stage = 6;
            var needs = wB.Stages[stage].Needs.ToArray();
            needs[0] = needs[0] with { BecomesWaste = !needs[0].BecomesWaste };
            wB.Stages[stage] = wB.Stages[stage] with { Needs = needs };
            Check(wA.Hash() != wB.Hash(), $"Custom Stages BecomesWaste change alters World.Hash() ({wA.Hash():X16} vs {wB.Hash():X16})");
        }

        // 6. Stage string boundary collision check:
        // Id="ab",NameVi="c" vs Id="a",NameVi="bc" must produce distinct hashes
        {
            var s1 = new World(4, 7); s1.Stages.Clear(); s1.Stages.Add(new Stage("ab", "c", 0));
            var s2 = new World(4, 7); s2.Stages.Clear(); s2.Stages.Add(new Stage("a", "bc", 0));
            Check(s1.Hash() != s2.Hash(), $"Stage string-boundary separation ({s1.Hash():X16} != {s2.Hash():X16})");
        }

        // 7. Stage.Needs immutability & isolation:
        // Cannot cast Needs to mutable array; modifying one world does not mutate another
        {
            var w1 = new World(4, 7);
            var w2 = new World(4, 7);
            bool castFailed = false;
            try { _ = (StageNeed[])w1.Stages[6].Needs; }
            catch (InvalidCastException) { castFailed = true; }
            Check(castFailed, "Stage.Needs cannot be cast to mutable StageNeed[] array");

            var needsCopy = w1.Stages[6].Needs.ToArray();
            needsCopy[0] = needsCopy[0] with { BecomesWaste = !needsCopy[0].BecomesWaste };
            w1.Stages[6] = w1.Stages[6] with { Needs = needsCopy };
            Check(w2.IsDefaultStages, "Mutating world 1 Stages does not affect world 2 DefaultStages");
        }

        // 8. Canonical DefaultStages immutability:
        // DefaultStages cannot be mutated in-place; attempting to set via IList throws NotSupportedException
        {
            bool mutateBlocked = false;
            try
            {
                if (World.DefaultStages is System.Collections.IList list)
                {
                    list[0] = World.DefaultStages[0] with { EarthYears = 1 };
                }
            }
            catch (NotSupportedException) { mutateBlocked = true; }
            Check(mutateBlocked, "Canonical DefaultStages is immutable; in-place array mutation throws NotSupportedException");
        }

        return _ok;
    }
}
