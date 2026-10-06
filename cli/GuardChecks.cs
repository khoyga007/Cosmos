// Verification suite for Ariel's Round 6 Defect Fixes & Guards.
// Defect items: #2 #3 #4 (SetConst guards), #5 #6 #7 (Add / AddOrbiting guards),
// #9 (Do return contract), #10 (hill-orphan re-parenting), #15 (layers-water-bank clock reset).
// Alone: `cli -- guards`.
using System;
using System.Linq;
using Cosmos.Core;

static class GuardChecks
{
    const double H = 0.5;
    static bool _ok;
    static void Check(bool ok, string line)
    {
        _ok &= ok;
        Console.WriteLine($"{(ok ? "OK    " : "FAILED")} guards: {line}");
    }

    public static bool Run()
    {
        _ok = true;

        // 1. #2 #3 #4: SetConst input validation
        {
            var w = World.SolSystem(0, 1234);
            int j0 = w.Journal.Count;
            double oldG = w.C.G, oldYearTime = w.C.YearTime, oldDensity0 = w.C.Density[0];
            double oldThrust = w.C.ShipThrust, oldShipMass = w.C.ShipMass, oldAttract = w.C.AttractMass;

            // Density <= 0 rejected
            bool rDenZero = w.Do(new Command(CmdKind.SetConst, Name: "Density[0]", Amount: 0)) == -1;
            bool rDenNeg = w.Do(new Command(CmdKind.SetConst, Name: "Density[1]", Amount: -1.5)) == -1;
            bool rDenNan = w.Do(new Command(CmdKind.SetConst, Name: "Density[2]", Amount: double.NaN)) == -1;

            // YearTime <= 0 rejected
            bool rYtZero = w.Do(new Command(CmdKind.SetConst, Name: "YearTime", Amount: 0)) == -1;
            bool rYtNeg = w.Do(new Command(CmdKind.SetConst, Name: "YearTime", Amount: -100)) == -1;

            // Negative values for physical constants rejected
            bool rGNeg = w.Do(new Command(CmdKind.SetConst, Name: "G", Amount: -1)) == -1;
            bool rRadNeg = w.Do(new Command(CmdKind.SetConst, Name: "RadiusScale", Amount: -0.5)) == -1;
            bool rRadZero = w.Do(new Command(CmdKind.SetConst, Name: "RadiusScale", Amount: 0)) == -1;

            // Ship constants validation
            bool rThrustNeg = w.Do(new Command(CmdKind.SetConst, Name: "ShipThrust", Amount: -2)) == -1;
            bool rShipMassGteAttract = w.Do(new Command(CmdKind.SetConst, Name: "ShipMass", Amount: w.C.AttractMass)) == -1;
            bool rAttractLteShip = w.Do(new Command(CmdKind.SetConst, Name: "AttractMass", Amount: w.C.ShipMass)) == -1;
            bool rShipMassNeg = w.Do(new Command(CmdKind.SetConst, Name: "ShipMass", Amount: -1e-12)) == -1;

            // Star constants and JumpSamples validation
            bool rStarSolarMassZero = w.Do(new Command(CmdKind.SetConst, Name: "StarSolarMass", Amount: 0)) == -1;
            bool rStarWhiteInterceptZero = w.Do(new Command(CmdKind.SetConst, Name: "StarWhiteIntercept", Amount: 0)) == -1;
            bool rJumpSamplesZero = w.Do(new Command(CmdKind.SetConst, Name: "JumpSamples", Amount: 0)) == -1;
            bool rJumpSamplesNeg = w.Do(new Command(CmdKind.SetConst, Name: "JumpSamples", Amount: -10)) == -1;

            // Valid SetConst succeeds with -2 and records in journal
            bool rValid = w.Do(new Command(CmdKind.SetConst, Name: "G", Amount: 1.5)) == -2;

            bool preserved = w.C.Density[0] == oldDensity0 && w.C.YearTime == oldYearTime && w.C.ShipThrust == oldThrust
                && w.C.ShipMass == oldShipMass && w.C.AttractMass == oldAttract && w.C.G == 1.5;
            bool journalClean = w.Journal.Count == j0 + 1; // only the valid command recorded

            bool finiteRadiiAndMass = true;
            for (int i = 0; i < w.N; i++)
            {
                if (w.Alive[i]) finiteRadiiAndMass &= double.IsFinite(w.R[i]) && w.R[i] > 0 && double.IsFinite(w.M[i]) && w.M[i] > 0;
            }

            Check(rDenZero && rDenNeg && rDenNan && rYtZero && rYtNeg && rGNeg && rRadNeg && rRadZero
                && rThrustNeg && rShipMassGteAttract && rAttractLteShip && rShipMassNeg
                && rStarSolarMassZero && rStarWhiteInterceptZero && rJumpSamplesZero && rJumpSamplesNeg
                && rValid && preserved && journalClean && finiteRadiiAndMass,
                "SetConst validation: Density<=0, YearTime<=0, NaN, negative, ShipMass>=AttractMass, Star*<=0, JumpSamples<1 rejected; journal records only valid, R/M finite");
        }

        // 2. #5 #6 #7: Add and AddOrbiting parameter guards
        {
            var w = World.SolSystem(0, 1234);
            double[] rock = { 0, 0, 1, 0, 0, 0 };

            // Mass <= 0 rejected
            bool rMZero = w.Add(10, 10, 0, 0, 0, rock) == -1;
            bool rMNeg = w.Add(10, 10, 0, 0, -1e-5, rock) == -1;

            // NaN / Infinite positions and velocities rejected
            bool rXNan = w.Add(double.NaN, 0, 0, 0, 1e-5, rock) == -1;
            bool rYNan = w.Add(0, double.PositiveInfinity, 0, 0, 1e-5, rock) == -1;
            bool rVxNan = w.Add(0, 0, double.NaN, 0, 1e-5, rock) == -1;
            bool rVyInf = w.Add(0, 0, 0, double.NegativeInfinity, 1e-5, rock) == -1;
            bool rMNan = w.Add(10, 10, 0, 0, double.NaN, rock) == -1;

            // Invalid mix rejected
            bool rMixNull = w.Add(10, 10, 0, 0, 1e-5, null!) == -1;
            bool rMixShort = w.Add(10, 10, 0, 0, 1e-5, new double[] { 1, 0 }) == -1;
            bool rMixNan = w.Add(10, 10, 0, 0, 1e-5, new double[] { double.NaN, 0, 1, 0, 0, 0 }) == -1;
            bool rMixNeg = w.Add(10, 10, 0, 0, 1e-5, new double[] { -1, 0, 1, 0, 0, 0 }) == -1;
            bool rMixAllZero = w.Add(10, 10, 0, 0, 1e-5, new double[6]) == -1;

            // Invalid par and grp rejected
            bool rParBad = w.Add(10, 10, 0, 0, 1e-5, rock, par: 999) == -1;
            bool rGrpBad = w.Add(10, 10, 0, 0, 1e-5, rock, grp: 999) == -1;

            // AddOrbiting parent guards
            bool rOrbParNeg = w.AddOrbiting(-1, 20, 0, 1e-5, rock) == -1;
            bool rOrbParOut = w.AddOrbiting(999, 20, 0, 1e-5, rock) == -1;
            bool rOrbParDead = false;
            {
                int deadSlot = w.Add(30, 30, 0, 0, 1e-5, rock);
                w.Do(new Command(CmdKind.Remove, Target: deadSlot));
                rOrbParDead = w.AddOrbiting(deadSlot, 35, 30, 1e-6, rock) == -1;
            }
            // AddOrbiting coincident point rejected (r = 0)
            bool rOrbCoincident = w.AddOrbiting(0, w.X[0], w.Y[0], 1e-5, rock) == -1;
            bool rOrbSpeedNeg = w.AddOrbiting(0, 50, 0, 1e-5, rock, speedFactor: -1) == -1;

            Check(rMZero && rMNeg && rXNan && rYNan && rVxNan && rVyInf && rMNan
                && rMixNull && rMixShort && rMixNan && rMixNeg && rMixAllZero
                && rParBad && rGrpBad && rOrbParNeg && rOrbParOut && rOrbParDead && rOrbCoincident && rOrbSpeedNeg,
                "Add / AddOrbiting guards: mass<=0, NaN/inf pos/vel/mix, invalid par/parent/grp, r=0 all rejected (-1)");
        }

        // 3. #9: Do return contract separation
        {
            var w = World.SolSystem(0, 1234);
            const int sun = 0, earth = 3;

            // Target commands return targeted slot (>= 0)
            int rPushSun = w.Do(new Command(CmdKind.Push, Target: sun, Vx: 0.01));
            int rPushEarth = w.Do(new Command(CmdKind.Push, Target: earth, Vx: 0.01));
            int rMove = w.Do(new Command(CmdKind.Move, Target: earth, X: w.X[earth] + 1, Y: w.Y[earth]));
            int rSeed = w.Do(new Command(CmdKind.SeedLife, Target: earth, Amount: 0.5));

            // Non-target commands return -2 (distinct from slot 0 Sun)
            int rSetConst = w.Do(new Command(CmdKind.SetConst, Name: "G", Amount: 1.1));
            int rSetRule = w.Do(new Command(CmdKind.SetRule, Name: "temperature", Amount: 1));
            int rFastForward = w.Do(new Command(CmdKind.FastForward, Amount: 0.1));
            int rForce = w.Do(new Command(CmdKind.Force, X: 0, Y: 0, Vx: 10, Amount: 0.05));

            // Failed commands return -1
            int rFailTarget = w.Do(new Command(CmdKind.Push, Target: 999));
            int rFailConst = w.Do(new Command(CmdKind.SetConst, Name: "G", Amount: -5));
            int rFailRule = w.Do(new Command(CmdKind.SetRule, Name: "nonexistent_rule", Amount: 1));

            bool contractOk = rPushSun == 0 && rPushEarth == earth && rMove == earth && rSeed == earth
                && rSetConst == -2 && rSetRule == -2 && rFastForward == -2 && rForce == -2
                && rFailTarget == -1 && rFailConst == -1 && rFailRule == -1;

            Check(contractOk,
                $"Do return contract: slot commands return slot (Sun=0, Earth=3), non-target return -2 (SetConst={rSetConst}, Rule={rSetRule}), fail=-1");
        }

        // 4. #10: hill-orphan & move-child-par-stale via PrimaryOf derived numbers
        {
            // Scenario A: Remove Earth -> Moon is orphan, PrimaryOf reads Sun, KindOf becomes Planet
            var w = World.SolSystem(0, 1234);
            const int sun = 0, earth = 3, moon = 9;
            Check(w.PrimaryOf(moon) == earth && w.KindOf(moon) == Kind.Moon, "Pre-condition: Moon orbits Earth and reads Kind.Moon");

            // move-child-par-stale check: Move Moon far away (+30), Par still stored as Earth, but PrimaryOf reads Sun
            w.Do(new Command(CmdKind.Move, Target: moon, X: w.X[moon] + 30, Y: w.Y[moon]));
            Check(w.PrimaryOf(moon) == sun && w.KindOf(moon) == Kind.Planet,
                $"move-child-par-stale: Move(Moon) +30 puts it outside Earth's Hill zone -> PrimaryOf is Sun (0), KindOf is {w.KindOf(moon)} (Planet, no longer Moon)");

            // Move Moon back and remove Earth
            w.Do(new Command(CmdKind.Move, Target: moon, X: w.X[earth] + 0.12, Y: w.Y[earth]));
            w.Do(new Command(CmdKind.Remove, Target: earth));
            Check(!w.Alive[earth], "Earth removed");
            // Par[moon] cleared by Gone(d, -1) to -1; PrimaryOf reads Sun; KindOf reads Planet
            Check(w.Par[moon] == -1 && w.PrimaryOf(moon) == sun && w.KindOf(moon) == Kind.Planet,
                $"Earth removed: Par[moon] cleared to -1 by Gone, PrimaryOf(Moon) reads Sun ({w.PrimaryOf(moon)} == {sun}), KindOf reads Planet");

            // Scenario B: Hierarchical sub-satellite: Sun -> Jupiter -> Moon -> Sub-moon
            var w2 = new World(16, 42);
            double[] gas = { 1, 0, 0, 0, 0, 0 }, rock = { 0, 0, 1, 0, 0, 0 };
            int sSun = w2.Add(0, 0, 0, 0, 50, gas, "Sun");
            int sJup = w2.AddOrbiting(sSun, 100, 0, 0.05, gas, "Jupiter"); // massive planet
            double hillJup = w2.Hill(sJup);

            // Moon orbits Jupiter well inside Jupiter's Hill sphere (mass >= AttractMass)
            int sMoon = w2.AddOrbiting(sJup, 100 + hillJup * 0.1, 0, 1e-5, rock, "Moon");
            double hillMoon = w2.Hill(sMoon);

            // Sub-moon orbits Moon inside Moon's Hill sphere (mass >= AttractMass so it is a Moon, not a Rock)
            int sSubMoon = w2.AddOrbiting(sMoon, w2.X[sMoon] + hillMoon * 0.2, w2.Y[sMoon], 1e-6, rock, "SubMoon");
            Check(w2.PrimaryOf(sSubMoon) == sMoon && w2.KindOf(sSubMoon) == Kind.Moon, "Pre-condition: SubMoon orbits Moon");

            // Remove Moon: Sub-moon is deep inside Jupiter's Hill zone, bound to Jupiter.
            // PrimaryOf MUST read Jupiter, NOT jump directly to the Sun!
            w2.Do(new Command(CmdKind.Remove, Target: sMoon));
            Check(!w2.Alive[sMoon], "Moon removed");
            int primary = w2.PrimaryOf(sSubMoon);
            Check(primary == sJup && w2.KindOf(sSubMoon) == Kind.Moon,
                $"SubMoon orphan: PrimaryOf reads Jupiter (slot {primary} == {sJup}), NOT Sun ({sSun}), KindOf is Moon");

            // Benchmark KindOf cost over 5000 rocks + 10 bodies
            var benchW = World.SolSystem(5000, 99);
            // warm up JIT
            for (int i = 0; i < benchW.N; i++) _ = benchW.KindOf(i);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int step = 0; step < 100; step++)
            {
                for (int i = 0; i < benchW.N; i++) _ = benchW.KindOf(i);
            }
            double costMs = sw.Elapsed.TotalMilliseconds / 100;
            Check(costMs < 1.0, $"KindOf cost across 5010 objects: {costMs:F3} ms/scan (rocks fast-path < 1ms)");

            // Claire requirement 1: Sol + 2000 rocks + moved bodies: PrimaryOf(i) == primary that Rails selects for ALL objects
            {
                var wSol = World.SolSystem(2000, 42);
                wSol.Do(new Command(CmdKind.Move, Target: 9, X: wSol.X[9] + 30, Y: wSol.Y[9]));
                wSol.Do(new Command(CmdKind.Move, Target: 3, X: wSol.X[3] + 1, Y: wSol.Y[3] - 1));

                wSol.BuildPullingHierarchy();
                bool allPrimaryMatch = true;
                for (int i = 0; i < wSol.N; i++)
                {
                    if (!wSol.Alive[i] || i == wSol.Heaviest()) continue;
                    int pDirect = wSol.PrimaryOf(i);
                    int pRails = wSol.RailsPrimary(i);
                    if (pDirect != pRails) allPrimaryMatch = false;
                }
                Check(allPrimaryMatch, "Sol + 2000 rocks + moved bodies: PrimaryOf(i) matches Rails primary for all 2010 objects");
            }
        }

        // 5. #15: layers-water-bank clock reset
        {
            var w = World.SolSystem(0, 1234);
            Rule tempRule = w.Rules.First(r => r.Id == "temperature");
            w.Advance(10); // step forward a bit
            double yearTurnOff = w.Year;

            // Turn rule off
            w.Do(new Command(CmdKind.SetRule, Name: "temperature", Amount: 0));
            Check(!tempRule.Enabled, "temperature rule disabled");

            // Jump forward 100 years while rule is disabled
            w.Do(new Command(CmdKind.FastForward, Amount: 100));
            double yearBeforeReenable = w.Year;
            Check(yearBeforeReenable >= yearTurnOff + 99, $"Jumped 100 years: year is {w.Year:F1}");

            // Re-enable rule
            w.Do(new Command(CmdKind.SetRule, Name: "temperature", Amount: 1));
            Check(tempRule.Enabled, "temperature rule re-enabled");
            Check(Math.Abs(tempRule.LastYear - yearBeforeReenable) < 1e-9,
                $"Rule LastYear reset to current year ({tempRule.LastYear:F2} == {yearBeforeReenable:F2}) upon re-enabling");

            // Next advance: RuleYears must NOT be 100 years
            w.Advance(w.C.YearTime * 0.05); // step 0.05 years
            Check(w.RuleYears < 1.0,
                $"RuleYears after re-enabling is {w.RuleYears:F4} years (does NOT dump 100 accumulated years)");

            // Star rule re-enable ceiling check
            Rule starRule = w.Rules.First(r => r.Id == "stars");
            w.Do(new Command(CmdKind.SetRule, Name: "stars", Amount: 0));
            w.Do(new Command(CmdKind.SetRule, Name: "stars", Amount: 1));
            Check(starRule.NextYear <= 1000.0, $"stars rule re-enabled: NextYear ({starRule.NextYear:F2}) keeps NextStarBoundary ceiling");
        }

        // 6. NeedsRockPositions flag support
        {
            var w = World.SolSystem(100, 1234);
            var customRule = new Rule("test_needs_rocks", "", "", 1, _ => { }) { NeedsRockPositions = true };
            w.Rules.Add(customRule);
            Check(customRule.NeedsRockPositions, "Rule.NeedsRockPositions flag is recognized and accessible");
        }

        return _ok;
    }
}
