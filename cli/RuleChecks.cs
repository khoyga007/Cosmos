// Checks for the rule table (SPEC.md P1). Owner: Celine. Called once from Program.cs.
using System;
using System.Diagnostics;
using System.Linq;
using Cosmos.Core;

static class RuleChecks
{
    public static bool Run()
    {
        bool allOk = true;
        void Check(bool ok, string line) { allOk &= ok; Console.WriteLine($"{(ok ? "OK    " : "FAILED")} rules: {line}"); }

        var clock = new World(0, 1);
        double yearTime = clock.C.YearTime;
        clock.Do(new Command(CmdKind.SetConst, Name: "G", Amount: 2));
        clock.Advance(yearTime);
        Check(clock.Year == 1 && clock.C.YearTime == yearTime, $"year {clock.Year:F3}, YearTime {yearTime:F6} unchanged by G");

        string order = "";
        var first = new Rule("first", "Year", "check", 0.25, _ => order += "A");
        var second = new Rule("second", "Year", "check", 0.25, _ => order += "B");
        clock.Rules.Add(first); clock.Rules.Add(second);
        clock.Advance(0);
        bool scheduleOk = order == "AB";
        clock.Advance(yearTime / 8);
        scheduleOk &= order == "AB";
        clock.Advance(yearTime / 8);
        scheduleOk &= order == "ABAB";
        first.Enabled = false;
        clock.Advance(yearTime / 4);
        scheduleOk &= order == "ABABB";
        first.Enabled = true;
        clock.Advance(0);
        scheduleOk &= order == "ABABBA";
        Check(scheduleOk, $"rhythm/order/switch {order}, next {first.NextYear:F2}");
        clock.Advance(yearTime * 2);
        Check(order == "ABABBAAB" && first.NextYear > clock.Year, $"skipped ticks coalesced once at year {clock.Year:F2}: {order}");
        long stepBefore = clock.Step; double yearBefore = clock.Year;
        bool rejectedTime = false, rejectedRhythm = false;
        try { clock.Advance(-1); } catch (ArgumentOutOfRangeException) { rejectedTime = true; }
        try { first.RhythmYears = 0; } catch (ArgumentOutOfRangeException) { rejectedRhythm = true; }
        Check(rejectedTime && rejectedRhythm && clock.Step == stepBefore && clock.Year == yearBefore && first.RhythmYears == 0.25,
            "invalid time/rhythm rejected without advancing state");

        for (int i = 0; i < World.EventCapacity + 7; i++) clock.LogEvent(0, "check", "bounded", i);
        Check(clock.Events.Count == World.EventCapacity && clock.Events[0].A == 7 && clock.Events[^1].A == World.EventCapacity + 6,
            $"bounded log {clock.Events.Count}, retained {clock.Events[0].A}..{clock.Events[^1].A}");

        var sol = World.SolSystem(0, 1234);
        bool descending = true;
        for (int i = 2; i <= 8; i++) descending &= sol.Temp[i] < sol.Temp[i - 1];
        Check(descending && sol.BandOf(sol.Temp[3]) == TemperatureBand.Temperate && Math.Abs(sol.Temp[3] - 288) < 1e-10
            && sol.Year == 0 && sol.Events.Count == 0,
            $"Sol initial K [{string.Join(", ", sol.Temp.Skip(1).Take(8).Select(t => t.ToString("F2")))}], Earth temperate, no initial events");

        const int mars = 4;
        double start = sol.Temp[mars], dx = sol.X[mars] - sol.X[0], dy = sol.Y[mars] - sol.Y[0], d = Math.Sqrt(dx * dx + dy * dy);
        sol.Do(new Command(CmdKind.Push, Target: mars, Vx: dx / d * 1.2, Vy: dy / d * 1.2));
        for (int i = 0; i < 300; i++) sol.Advance(0.5);
        bool crossing = sol.Events.Count == 1;
        var e = crossing ? sol.Events[0] : default;
        crossing &= sol.Temp[mars] < start && e.ObjectSlot == mars && e.RuleId == "temperature" && e.Change == "band.1.0"
            && e.Year > 0 && e.A >= sol.C.FrozenEdge && e.B < sol.C.FrozenEdge
            && Math.Abs(e.C - Math.Pow(e.B / sol.C.TemperatureScale, 4)) < 1e-12;
        Check(crossing, $"Mars pushed: {start:F3} -> {sol.Temp[mars]:F3} K, events {sol.Events.Count}; "
            + $"{e.RuleId}/{e.Change} object {e.ObjectSlot} year {e.Year:F6} before {e.A:F3} after {e.B:F3} flux {e.C:F6}");

        var replay = World.SolSystem(0, 1234); int next = 0;
        for (int i = 0; i < 300; i++) { replay.Replay(sol.Journal, ref next); replay.Advance(0.5); }
        Check(sol.Hash() == replay.Hash() && sol.Temp.SequenceEqual(replay.Temp) && sol.Events.SequenceEqual(replay.Events),
            $"replay temperature/events/hash {sol.Hash():X16} / {replay.Hash():X16}");
        ulong stateHash = replay.Hash();
        replay.LogEvent(mars, "check", "hash", 1);
        Check(replay.Hash() != stateHash, "event state participates in repeat hash");

        sol.Rules[0].Enabled = false;
        var temperatures = (double[])sol.Temp.Clone(); int events = sol.Events.Count;
        for (int i = 0; i < 100; i++) sol.Advance(0.5);
        Check(temperatures.SequenceEqual(sol.Temp) && sol.Events.Count == events,
            $"temperature off: Mars {sol.Temp[mars]:F3} K unchanged across 100 advances");

        var binary = new World(4, 1);
        double[] gas = { 1, 0, 0, 0, 0, 0 }, rock = { 0, 0, 1, 0, 0, 0 };
        binary.Do(new Command(CmdKind.SetConst, Name: "G", Amount: 0));
        binary.Do(new Command(CmdKind.Create, X: -45, Amount: 50, Mix: gas));
        binary.Do(new Command(CmdKind.Create, X: 45, Amount: 50, Mix: gas));
        int planet = binary.Do(new Command(CmdKind.Create, Amount: World.EarthMass, Mix: rock));
        binary.Advance(0);
        double twoStars = binary.Temp[planet];
        binary.Do(new Command(CmdKind.Remove, Target: 1));
        binary.Advance(binary.C.YearTime * 0.01);
        double oneStar = binary.Temp[planet];
        binary.Do(new Command(CmdKind.AddMatter, Target: 0, Index: 0, Amount: 50));
        binary.Do(new Command(CmdKind.SetConst, Name: "LuminosityExponent", Amount: 2));
        binary.Advance(binary.C.YearTime * 0.01);
        Check(Math.Abs(twoStars - 288 * Math.Pow(2, 0.25)) < 1e-10 && Math.Abs(oneStar - 288) < 1e-10
            && Math.Abs(binary.Temp[planet] - 288 * Math.Sqrt(2)) < 1e-10,
            $"star light sums {twoStars:F3} K; one star {oneStar:F3}; mass/exponent edit {binary.Temp[planet]:F3}");

        binary.Do(new Command(CmdKind.Remove, Target: planet));
        int reused = binary.Do(new Command(CmdKind.Create, X: 90, Amount: World.EarthMass, Mix: rock));
        int oldEvents = binary.Events.Count;
        bool reset = reused == planet && double.IsNaN(binary.Temp[reused]);
        binary.Advance(binary.C.YearTime * 0.01);
        Check(reset && double.IsFinite(binary.Temp[reused]) && binary.Events.Count == oldEvents,
            $"slot {reused} reused: fresh temperature {binary.Temp[reused]:F3} K, no inherited crossing");

        // Alternate measurements on identical worlds; use medians to reduce scheduling noise.
        var enabled = World.SolSystem(5_000, 77); var disabled = World.SolSystem(5_000, 77);
        disabled.Rules[0].Enabled = false;
        for (int i = 0; i < 300; i++) { enabled.Advance(0.5); disabled.Advance(0.5); }
        var on = new double[5]; var off = new double[5];
        for (int i = 0; i < on.Length; i++)
        {
            if (i % 2 == 0) { on[i] = Measure(enabled); off[i] = Measure(disabled); }
            else { off[i] = Measure(disabled); on[i] = Measure(enabled); }
        }
        Array.Sort(on); Array.Sort(off);
        Check(on[2] <= off[2] * 1.10, $"5000-rock cost enabled {on[2]:F3} / disabled {off[2]:F3} ms/step, overhead {100 * (on[2] / off[2] - 1):F2}% (ceiling +10%, Claire revised criterion)");
        allOk &= JumpChecks();
        return allOk;
    }

    // Round 2: force the original chunk path with a disabled observer that cannot affect the world.
    static Rule ReferencePath(World w)
    {
        var observer = new Rule("jump.reference", "", "", 1, _ => { }) { Enabled = false };
        w.Rules.Add(observer); return observer;
    }

    static bool JumpChecks()
    {
        bool ok = true;
        void Check(bool pass, string line) { ok &= pass; Console.WriteLine($"{(pass ? "OK    " : "FAILED")} jumpcost: {line}"); }
        int same = 0;
        foreach (ulong seed in new ulong[] { 1234, 5 }) foreach (double years in new double[] { 1, 100, 1e4, 1e6 })
        {
            var a = World.SolSystem(0, seed); var b = World.SolSystem(0, seed); var reference = ReferencePath(b);
            for (int i = 0; i < 200; i++) { a.Advance(.5); b.Advance(.5); }
            a.Do(new Command(CmdKind.FastForward, Amount: years)); b.Do(new Command(CmdKind.FastForward, Amount: years));
            b.Rules.Remove(reference);
            if (a.Hash() == b.Hash()) same++;
        }
        Check(same == 8, $"no-rock exact hashes {same}/8 match original chunk path");

        var orbit = new World(8, 1);
        double[] gas = { 1, 0, 0, 0, 0, 0 }, rock = { 0, 0, 1, 0, 0, 0 };
        int star = orbit.Add(0, 0, .001, -.002, 50, gas);
        int planet = orbit.AddOrbiting(star, 45, 0, World.EarthMass, rock);
        int satellite = orbit.AddOrbiting(planet, orbit.X[planet] + .15, orbit.Y[planet], 1e-9, rock);
        int escaped = orbit.Add(100, 0, 0, 5, 1e-9, rock);
        const double elapsed = 10000;
        orbit.Do(new Command(CmdKind.FastForward, Amount: elapsed / orbit.C.YearTime));
        double sx = orbit.X[satellite] - orbit.X[planet], sy = orbit.Y[satellite] - orbit.Y[planet];
        Check(Math.Abs(Math.Sqrt(sx * sx + sy * sy) - .15) < 1e-6 && orbit.OffRails == 1
            && Math.Abs(orbit.Y[escaped] - 5 * elapsed) < 1e-6 && Math.Abs(orbit.X[escaped] - 100) < 1e-6
            && double.IsFinite(orbit.Temp[satellite]) && double.IsFinite(orbit.Temp[escaped]),
            $"deferred satellite follows moving planet r={Math.Sqrt(sx * sx + sy * sy):F6}; escaping rock stays inertial at ({orbit.X[escaped]:F3},{orbit.Y[escaped]:F3}), off={orbit.OffRails}");

        var free = new World(4, 1); free.Add(2, 3, 1, -2, 1e-9, rock);
        free.Do(new Command(CmdKind.FastForward, Amount: 100));
        Check(Math.Abs(free.X[0] - 2 - 100 * free.C.YearTime) < 1e-8 && Math.Abs(free.Y[0] - 3 + 200 * free.C.YearTime) < 1e-8,
            "no pulling bodies: deferred rocks move by the full inertial interval");

        var watched = new World(4, 1); watched.Add(0, 0, 1, 0, 1e-9, rock);
        watched.Do(new Command(CmdKind.SetConst, Name: "JumpSamples", Amount: 2));
        var positions = new System.Collections.Generic.List<double>();
        watched.Rules.Add(new Rule("observer", "X", "check", 1, w => positions.Add(w.X[0])));
        watched.Do(new Command(CmdKind.FastForward, Amount: 100));
        Check(positions.Count == 2 && Math.Abs(positions[0] - 50 * watched.C.YearTime) < 1e-8
            && Math.Abs(positions[1] - 100 * watched.C.YearTime) < 1e-8, "custom rule sees rock positions every chunk through fallback");

        var stopped = World.SolSystem(100, 5); var before = (double[])stopped.Temp.Clone();
        stopped.Do(new Command(CmdKind.SetRule, Name: "temperature", Amount: 0));
        stopped.Do(new Command(CmdKind.FastForward, Amount: 1e6));
        Check(before.SequenceEqual(stopped.Temp), "temperature disabled: no deferred temperature writes");

        var replay = World.SolSystem(100, 5); int next = 0;
        replay.Replay(stopped.Journal, ref next);
        Check(stopped.Hash() == replay.Hash(), $"deferred journal replay {stopped.Hash():X16} / {replay.Hash():X16}");

        // Warm both paths with a small world, then measure the same seed/age with all four rules enabled.
        var warm = World.SolSystem(100, 1234); warm.Do(new Command(CmdKind.FastForward, Amount: 1e6));
        var slow = World.SolSystem(50_000, 1234); ReferencePath(slow);
        var fast = World.SolSystem(50_000, 1234);
        var sw = Stopwatch.StartNew(); slow.Do(new Command(CmdKind.FastForward, Amount: 1e6));
        double originalMs = sw.Elapsed.TotalMilliseconds;
        sw.Restart(); fast.Do(new Command(CmdKind.FastForward, Amount: 1e6)); double trimmedMs = sw.Elapsed.TotalMilliseconds;
        bool finite = true;
        for (int i = 0; i < fast.N; i++) if (fast.Alive[i]) finite &= double.IsFinite(fast.X[i]) && double.IsFinite(fast.Y[i])
            && double.IsFinite(fast.Vx[i]) && double.IsFinite(fast.Vy[i]) && double.IsFinite(fast.Temp[i]);
        Check(finite && originalMs >= 5 * trimmedMs && Math.Abs(fast.Life[3] - slow.Life[3]) < 1e-12
            && Math.Abs(fast.Pop[3] - slow.Pop[3]) < 1e-12 && Math.Abs(fast.Tech[3] - slow.Tech[3]) < 1e-12,
            $"50000 rocks / 1e6 years: original {originalMs:F3} ms, trimmed {trimmedMs:F3} ms, {originalMs / trimmedMs:F2}x (gate 5x); finite state and Earth layers agree");
        return ok;
    }

    static double Measure(World w)
    {
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 200; i++) w.Advance(0.5);
        return sw.Elapsed.TotalMilliseconds / 200;
    }
}
