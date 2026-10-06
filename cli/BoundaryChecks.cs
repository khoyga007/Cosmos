using System;
using System.Collections.Generic;
using System.Linq;
using Cosmos.Core;

static class BoundaryChecks
{
    public static bool Run()
    {
        bool ok = true;
        void Check(bool pass, string line) { ok &= pass; Console.WriteLine($"{(pass ? "OK    " : "FAILED")} boundary: {line}"); }
        var hashWorld = new World(0, 1);
        Rule temperature = hashWorld.Rules.Find(r => r.Id == "temperature")!;
        RuleBoundary policy = temperature.Boundary;
        ulong normal = hashWorld.Hash();
        temperature.Boundary = RuleBoundary.None;
        Check(hashWorld.Hash() != normal, "changing a builtin boundary policy changes Hash");
        temperature.Boundary = policy;
        Check(hashWorld.Hash() == normal, "restoring the canonical policy restores the exact legacy Hash");
        int row = hashWorld.Rules.IndexOf(temperature);
        hashWorld.Rules[row] = new Rule(temperature.Id, temperature.Reads, temperature.Writes, temperature.RhythmYears, temperature.Apply);
        Check(hashWorld.Hash() != normal, "replacing a builtin row with an unflagged same-id row is visible to Hash");

        var calls = new List<(double Year, StarPhase Phase, double Years)>();
        Rule Observer(List<(double, StarPhase, double)> log) => new("extension.era", "StarFuel", "", 1e20,
            w => log.Add((w.Year, w.StarPhaseOf(0), w.RuleYears))) { Boundary = RuleBoundary.BeforeStar | RuleBoundary.After };
        var star = new World(4, 1); star.Rules.Add(Observer(calls));
        star.Do(new Command(CmdKind.Create, Amount: 50, Mix: star.Mix(("gas", 1))));
        star.Do(new Command(CmdKind.SetConst, Name: "StarLifeScale", Amount: 1e-9));
        star.Advance(0); calls.Clear();
        star.Do(new Command(CmdKind.FastForward, Amount: 10));
        Check(calls.Count == 2 && calls[0].Phase == StarPhase.MainSequence && calls[1].Phase == StarPhase.RedGiant
            && Math.Abs(calls[0].Year - 10) < 1e-9 && calls[0].Year == calls[1].Year && calls[0].Years > 9 && calls[1].Years == 0,
            "one extension row settles before giant entry and refreshes after it, without dispatcher edits");
        var replay = new World(4, 1); replay.Rules.Add(Observer(new())); int next = 0;
        replay.Replay(star.Journal, ref next); replay.Advance(0); replay.Replay(star.Journal, ref next);
        Check(replay.Hash() == star.Hash(), $"flagged-extension replay {star.Hash():X16}/{replay.Hash():X16}");

        var cooling = new World(8, 1); var edges = new List<(double Year, double Temp, double Years)>();
        cooling.Rules.Add(new Rule("extension.cooling", "Temp", "", 1e20, w => edges.Add((w.Year, w.Temp[1], w.RuleYears)))
            { Boundary = RuleBoundary.BeforeCooling | RuleBoundary.After });
        cooling.Do(new Command(CmdKind.SetConst, Name: "G", Amount: 0));
        cooling.Do(new Command(CmdKind.Create, Amount: 25.15, Mix: cooling.Mix(("gas", 1))));
        cooling.Do(new Command(CmdKind.SetStarState, Target: 0, StarState: new(1.05e10, 1.05, 50)));
        cooling.Do(new Command(CmdKind.Create, X: 45, Amount: World.EarthMass, Mix: cooling.Mix(("rock", 1))));
        cooling.Do(new Command(CmdKind.FastForward, Amount: 1)); edges.Clear();
        cooling.Do(new Command(CmdKind.FastForward, Amount: 1e8));
        Check(edges.Count == 8 && Enumerable.Range(0, edges.Count / 2).All(k => edges[2 * k].Year == edges[2 * k + 1].Year
            && edges[2 * k].Temp > edges[2 * k + 1].Temp && edges[2 * k].Years > 0 && edges[2 * k + 1].Years == 0),
            $"cooling extension gets old/new state at all four thermal/water edges ({edges.Count} callbacks)");
        return ok;
    }
}
