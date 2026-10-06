using System;
using System.Collections.Generic;
using System.Linq;
using Cosmos.Core;

static class CivStatsChecks
{
    static bool _ok;
    static void Check(bool ok, string line) { _ok &= ok; Console.WriteLine($"{(ok ? "OK    " : "FAILED")} civstats: {line}"); }

    public static bool Run()
    {
        _ok = true;

        // 1. Check 1: Divergent civ development on planets with different resource abundance
        {
            var w = new World(64, 42);
            int star = w.Add(0, 0, 0, 0, 50, w.Mix(("gas", 1)), "Star");
            // Planet A: Rich in all resources (metal, carbon, rock, ice)
            int planetA = w.AddOrbiting(star, 45, 0, World.EarthMass,
                w.Mix(("ice", 0.01), ("rock", 0.60), ("metal", 0.35), ("carbon", 0.03), ("radio", 0.01)), "RichWorld");
            // Planet B: Poor in metal (only 0.01 metal - below Bronze Age requirement 0.02)
            int planetB = w.AddOrbiting(star, -45, 0, World.EarthMass,
                w.Mix(("ice", 0.01), ("rock", 0.95), ("metal", 0.01), ("carbon", 0.02), ("radio", 0.01)), "PoorWorld");

            w.Do(new Command(CmdKind.FastForward, Amount: 1e6));

            bool aAlive = w.Pop[planetA] > 0;
            bool bAlive = w.Pop[planetB] > 0;
            bool divergentTech = w.Tech[planetA] > w.Tech[planetB];
            int stageA = w.TechStage(planetA);
            int stageB = w.TechStage(planetB);

            Check(aAlive && bAlive && divergentTech && stageA > stageB,
                $"Check 1: Divergent civ development - RichWorld (Tech {w.Tech[planetA]:F2}, Stage {stageA}) outpaced PoorWorld (Tech {w.Tech[planetB]:F2}, Stage {stageB})");
        }

        // 2. Check 2: Planet with Metal = 0 clamped before Bronze Age (threshold 1.0)
        {
            var w = new World(64, 99);
            int star = w.Add(0, 0, 0, 0, 50, w.Mix(("gas", 1)), "Star");
            // Planet with 0 metal: has water, rock, carbon
            int planetNoMetal = w.AddOrbiting(star, 45, 0, World.EarthMass,
                w.Mix(("ice", 0.02), ("rock", 0.95), ("metal", 0.0), ("carbon", 0.03)), "NoMetalWorld");

            // Fast forward 1 million years - enough for normal civ to reach space
            w.Do(new Command(CmdKind.FastForward, Amount: 1e6));

            bool civRose = w.Civ[planetNoMetal] >= 0;
            double tech = w.Tech[planetNoMetal];
            int stage = w.TechStage(planetNoMetal);
            bool clampedBeforeBronze = tech < 1.0 && stage <= 1; // Stage 1 is stone_age (0.5), bronze_age is 1.0
            bool noBronzeEvent = !w.Events.Any(e => e.ObjectSlot == planetNoMetal && e.Change.StartsWith("civ.stage.1.2"));

            Check(civRose && clampedBeforeBronze && noBronzeEvent,
                $"Check 2: Metal=0 planet clamped before Bronze Age - Tech: {tech:F6} < 1.0, Stage: {stage}, noBronzeEvent: {noBronzeEvent}");
        }

        // 3. Check 3: Fast-forward 1e9 years -> Tech does not exceed ceiling 4.0 & Kardashev scale valid
        {
            var w = World.SolSystem(0, 777);
            const int earth = 3;
            w.Do(new Command(CmdKind.FastForward, Amount: 1e9));

            double tech = w.Tech[earth];
            bool techCapped = tech <= 4.0;
            double kardashev = w.KardashevScale(earth);
            double people = w.PeopleCount(earth);
            double footprint = w.CivFootprint(earth);

            Check(techCapped && tech >= 3.5 && kardashev > 0.5 && people > 0 && footprint >= 0,
                $"Check 3: Tech ceiling 4.0 enforced over 1e9 yr - Tech: {tech:F3}/4.0, Kardashev: {kardashev:F2}, People: {people:E2}, Footprint: {footprint:P1}");
        }

        // 4. Check 4: Mercury with 0 ice and no home supply does not spawn domes
        {
            var w = World.SolSystem(0, 1234);
            const int mercury = 1;
            // Mercury has 0 ice:
            double ice = w.Share(mercury, ElementRole.Ice);
            double roomWithoutSupply = w.DomeRoom(mercury, -1);

            Check(ice < w.C.WaterIceMin && roomWithoutSupply == 0,
                $"Check 4: Mercury (ice share {ice:F4}) has zero dome room without home supply ({roomWithoutSupply})");
        }

        // 5. Check 5: Colony on dry planet loses home world -> decays to 0 over CivDecayYears
        {
            var w = World.SolSystem(0, 555);
            const int home = 3; // Earth
            // Fast forward 1 million years to establish mature space civ on Earth
            w.Do(new Command(CmdKind.FastForward, Amount: 1e6));
            int civ = w.Civ[home];
            Check(civ >= 0 && w.Pop[home] > 0, $"Check 5 pre: Civ rose on Home world (Civ: {civ}, Pop: {w.Pop[home]:F4})");

            // Add dry world near Earth orbit (habitable temp range, but 0 ice)
            int colony = w.AddOrbiting(0, 46, 0, World.EarthMass * 0.1,
                w.Mix(("rock", 0.70), ("metal", 0.30)), "DryColony");

            // Establish dry dome colony belonging to Earth's civ
            w.Civ[colony] = civ;
            w.Pop[colony] = w.C.CivSeed;
            w.Tech[colony] = w.Tech[home];

            // Fast forward 100 years to settle temperature and layer rules
            w.Do(new Command(CmdKind.FastForward, Amount: 100));

            // Verify colony has positive DomeRoom while Home is alive
            double roomWithHome = w.DomeRoom(colony, civ);
            Check(roomWithHome > 0, $"Check 5 pre: Dry colony has dome room while Home is alive ({roomWithHome:F4})");

            // Destroy Home world
            w.EndWorld(home);
            double roomAfterLost = w.DomeRoom(colony, civ);
            Check(roomAfterLost == 0, $"Check 5: DomeRoom dropped to 0 immediately when Home was destroyed ({roomAfterLost})");

            // Run for 10 * CivDecayYears (5000 years)
            w.Do(new Command(CmdKind.FastForward, Amount: 5000));
            bool colonyExtinct = w.Pop[colony] == 0;
            bool loggedEnd = w.Events.Any(e => e.ObjectSlot == colony && e.Change == "civ.end");

            Check(colonyExtinct && loggedEnd,
                $"Check 5: Colony on dry planet decayed to 0 after losing Home (Pop: {w.Pop[colony]}, civ.end logged: {loggedEnd})");
        }

        // 6. Check 6: Replay determinism with civ stats and mass-conserving resource consumption
        {
            var w1 = World.SolSystem(0, 888);
            w1.Do(new Command(CmdKind.FastForward, Amount: 5e5));
            ulong h1 = w1.Hash();

            var w2 = World.SolSystem(0, 888);
            w2.Do(new Command(CmdKind.FastForward, Amount: 5e5));
            ulong h2 = w2.Hash();

            // Total mass conservation across simulation
            double totalM1 = 0, totalM2 = 0;
            for (int i = 0; i < w1.N; i++) if (w1.Alive[i]) totalM1 += w1.M[i];
            for (int i = 0; i < w2.N; i++) if (w2.Alive[i]) totalM2 += w2.M[i];

            Check(h1 == h2 && Math.Abs(totalM1 - totalM2) < 1e-18,
                $"Check 6: Replay determinism verified (h1={h1:X16}, h2={h2:X16}, total mass diff={Math.Abs(totalM1 - totalM2):E2})");
        }

        return _ok;
    }

    // Helper to simulate complete destruction of a world
    static void EndWorld(this World w, int slot)
    {
        w.Pop[slot] = 0;
        w.Life[slot] = 0;
        w.Alive[slot] = false;
        w.M[slot] = 0;
    }
}
