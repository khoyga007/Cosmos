// Core audit — SPEC.md §6 P3 + §7 (Selica). Owner: Selica. This file only READS the core and prints numbers;
// it never fixes anything. Findings carry the file:line and the smallest input that shows them.
//   DEFECT = a bug. RISK = behaviour that may be wrong by design; for Claire to judge.
// Every DEFECT line is a real failing check: run with COSMOS_AUDIT_STRICT=1 and this Run() returns false (exit 1).
// Without that variable the shared `dotnet run --project cli` stays green while the lines are still printed,
// so Celine and Ariel can still tell "my change broke something" from "known audit finding".
using System;
using System.Diagnostics;
using System.Linq;
using Cosmos.Core;

static class Audit
{
    const double H = 0.5;
    static readonly double[] RockMix = { 0, 0, 1, 0, 0, 0 };
    static readonly double[] GasMix = { 1, 0, 0, 0, 0, 0 };
    static int _defects, _risks;
    static bool _strict;

    static void Head(string s) => Console.WriteLine("-- " + s);
    static void Info(string s) => Console.WriteLine("       " + s);
    static void Ok(string s) => Console.WriteLine("OK     " + s);
    static void Defect(string s) { _defects++; Console.WriteLine("DEFECT " + s); }
    static void Risk(string s) { _risks++; Console.WriteLine("RISK   " + s); }
    static void Expect(bool ok, string s) { if (ok) Ok(s); else Defect(s); }
    static void Run(World w, int steps) { for (int i = 0; i < steps; i++) w.Advance(H); }

    public static bool Run()
    {
        _strict = Environment.GetEnvironmentVariable("COSMOS_AUDIT_STRICT") == "1";
        Head("audit P3: energy, momentum, swept collision, cost");
        Measure();
        Head("audit P3: defect checks (core/World.cs, Commands.cs, Rules.cs, Rails.cs, Layers.cs, Civ.cs)");
        HashChecks();
        ConstChecks();
        AddChecks();
        SlotChecks();
        HillChecks();
        RailChecks();
        SweptChecks();
        LayerChecks();
        DeferChecks();
        TouchChecks();
        GodChecks();
        CivChecks();
        Console.WriteLine($"AUDIT: {_defects} defect line(s), {_risks} risk line(s); core not touched (P3)");
        if (_defects > 0 && _strict) { Console.WriteLine("AUDIT: STRICT -> the run FAILS"); return false; }
        if (_defects > 0) Console.WriteLine("AUDIT: not strict -> run stays green; set COSMOS_AUDIT_STRICT=1 to fail on these");
        return true;
    }

    // ---- measurements (P3 §2): drift, tunnelling, cost

    static void Energy(World w, out double total, out double ang)
    {
        double mt = 0, cx = 0, cy = 0, cvx = 0, cvy = 0;
        for (int i = 0; i < w.N; i++) if (w.Alive[i])
        { mt += w.M[i]; cx += w.M[i] * w.X[i]; cy += w.M[i] * w.Y[i]; cvx += w.M[i] * w.Vx[i]; cvy += w.M[i] * w.Vy[i]; }
        cx /= mt; cy /= mt; cvx /= mt; cvy /= mt;
        double ke = 0, pe = 0, l = 0;
        for (int i = 0; i < w.N; i++) if (w.Alive[i])
        {
            double vx = w.Vx[i] - cvx, vy = w.Vy[i] - cvy;
            ke += 0.5 * w.M[i] * (vx * vx + vy * vy);
            l += w.M[i] * ((w.X[i] - cx) * vy - (w.Y[i] - cy) * vx);
            for (int j = i + 1; j < w.N; j++) if (w.Alive[j])
            {
                double dx = w.X[i] - w.X[j], dy = w.Y[i] - w.Y[j];
                pe -= w.C.G * w.M[i] * w.M[j] / Math.Sqrt(dx * dx + dy * dy);
            }
        }
        total = ke + pe; ang = l;
    }

    static double Rel(double a, double b) => Math.Abs(b) < 1e-300 ? 0 : (a - b) / b;

    static void Measure()
    {
        // Sol, 0 rocks: E and L over 20000 steps, both kept to machine noise or not
        var w = World.SolSystem(0, 1234);
        Energy(w, out double e0, out double l0);
        Run(w, 20000);
        Energy(w, out double e1, out double l1);
        Info($"sol 0 rocks, 20000 steps @h={H}: E {e0:E8} -> {e1:E8} (rel {Rel(e1, e0):E2}), L {l0:E8} -> {l1:E8} (rel {Rel(l1, l0):E2})");

        // same, with G 1 -> 2 at the half-way step: energy is NOT conserved across the god's dial, L must stay
        var g = World.SolSystem(0, 1234);
        Energy(g, out double ga, out double la);
        Run(g, 10000);
        Energy(g, out double gb, out double lb);
        g.Do(new Command(CmdKind.SetConst, Name: "G", Amount: 2));
        Energy(g, out double gc, out double lc);
        Run(g, 10000);
        Energy(g, out double gd, out double ld);
        Info($"G 1->2 at step 10000: E {ga:E6} -> {gb:E6} (rel {Rel(gb, ga):E2}), then {gc:E6} -> {gd:E6} (rel {Rel(gd, gc):E2})");
        Info($"  jump across the dial {Rel(gc, gb):P2} (PE is linear in G), L drift {Rel(lc, lb):E2} then {Rel(ld, lc):E2}");
        Info($"  Year {g.Year:F2}, YearTime {g.C.YearTime:F2} (unchanged on purpose, Rules.cs:8-9)");

        // swept test (World.cs:212 tests d2 < (ri+Rj)^2 once per small step): a probe crossing between two
        // small steps is never touched. The window is hs = h/Sub, so the caller's h sets the threshold.
        // Two-body scene first: nothing else bends the path, so the law is clean and exact.
        const int earth = 3;
        double cross = H / World.Sub;
        Info($"swept: the core samples the pair once per small step, so at h={H} a straight meeting is missed from 2(Ri+Rj)/hs = 2(Ri+Rj)/{cross:F4} relative");
        foreach (var (tm, slow, fast) in new[] { (1e-4, 0.7, 5.0), (1e-3, 3.0, 8.0) })
        {
            var (_, _, rt, rp) = Straight(tm, H, 0.0);
            var (ma, pa, _, _) = Straight(tm, H, slow);
            var (mb, pb, _, _) = Straight(tm, H, fast);
            Info($"  target R {rt:F4} + probe R {rp:F4}: the meeting is found below {2 * (rt + rp) / cross:F2} relative — {slow:F1} -> {(ma ? "merged" : $"MISSED by {pa:F4}")}, {fast:F1} -> {(mb ? "merged" : $"MISSED by {pb:F4}")}");
        }
        var (mSlow, _, _, _) = Straight(1e-6, 0.125, 1.0);
        var (mFast, _, _, _) = Straight(1e-6, H, 1.0);
        Info($"  same target, same relative speed 1.0: h=0.125 -> {(mSlow ? "merged" : "MISSED")}, h={H} -> {(mFast ? "merged" : "MISSED")}");
        Info("  in the real system (the Sun bends the path, so the clean line above is only a floor):");
        double re = World.SolSystem(0, 1234).R[earth];
        double lastHit = 0, firstMiss = 0; bool survived = false;
        for (double v = 0.2; v <= 40; v *= 1.08)
        {
            var (hit, died) = SolProbe(earth, H, v);
            if (hit) lastHit = v; else { firstMiss = v; survived = !died; break; }
        }
        Info($"    Earth R {re:F4} straight on, relative speed: Earth is hit up to {lastHit:F2} and never from {firstMiss:F2} (clean line {2 * re / cross:F2}); Earth's orbital speed is {Math.Sqrt(50 / 45.0):F2}, so from {firstMiss / Math.Sqrt(50 / 45.0):F1}x a planet at speed the probe crosses Earth untouched and flies on (probe still alive: {survived})");

        // cost per object count, split rocks (they do not pull) from bodies (they do)
        Head("cost of Advance: rocks vs pulling bodies");
        Console.WriteLine("   rocks   ms/step   marginal us/rock");
        double prev = 0; int prevN = 0;
        foreach (int n in new[] { 0, 2_000, 5_000, 20_000, 50_000 })
        {
            double ms = PerStep(World.SolSystem(n, 1234), n >= 20_000 ? 40 : 100);
            string marg = n == 0 ? "" : $"{(ms - prev) * 1000 / (n - prevN):F3}";
            Console.WriteLine($"{n,8} {ms,9:F3}   {marg,15}");
            prev = ms; prevN = n;
        }
        Console.WriteLine("  bodies that all pull (star + n planets of 2e-4, kick loop is O(N * na)):");
        foreach (int n in new[] { 10, 50, 200, 1000 })
            Console.WriteLine($"{n,8} {PerStep(Pullers(n), 40),9:F3}");

        // RISK: AttractMass = 0 turns every rock into a puller
        var r0 = World.SolSystem(2_000, 1234);
        double t0 = PerStep(r0, 30);
        r0.Do(new Command(CmdKind.SetConst, Name: "AttractMass", Amount: 0));
        double t1 = PerStep(r0, 3);
        Risk($"attractmass-0 (Commands.cs:75, World.cs:69): AttractMass 1e-7 -> 0 at 2000 rocks {t0:F3} -> {t1:F3} ms/step ({t1 / t0:F0}x) because every rock now pulls (kick loop O(N*na)); the same dial makes rock-rock merge possible");
        Info($"  after that dial: {r0.Events.Count} event(s) in the log, {r0.Live} object(s) left");
    }

    /// Clean two-body swept test: a target of mass `tm` at rest and a 1e-9 probe fired dead centre from 3
    /// units away with relative speed v, stepped with h. Nothing else is in the scene, so the only thing that
    /// can hide the meeting is the sampling. merged = the core really merged them (the probe is gone);
    /// gap = how far outside the surfaces the closest of the core's own sample points passes (negative means
    /// the pair is found overlapping); rt, rp = the two radii.
    static (bool merged, double gap, double rt, double rp) Straight(double tm, double h, double v)
    {
        double hs = h / World.Sub, cell = v * hs;
        // start half a sampling cell off the grid that already sits nearest to 3 units away: the worst
        // phase the sampling can be in, so the table below is not decided by luck.
        double start = v > 0 ? Math.Round(3 / cell) * cell + cell / 2 : 3;
        var w = new World(4, 1);
        int tg = w.Add(0, 0, 0, 0, tm, RockMix, "target");
        int pr = w.Add(start, 0, -v, 0, 1e-9, RockMix, "probe");
        for (int s = 0; s < 4000 && w.Alive[pr]; s++) w.Advance(h);
        double rt = w.R[tg], rp = w.R[pr];
        double mn = double.MaxValue;
        if (v > 0)
            for (int k = 1; k <= 200_000; k++)
            {
                double d = Math.Abs(start - v * k * hs);
                if (d < mn) mn = d;
                if (start - v * k * hs < 0) break;   // crossed the target; the samples only get farther
            }
        return (!w.Alive[pr], mn - (rt + rp), rt, rp);
    }

    /// The same probe inside the Sol scene, aimed at body `body`. The Sun curves the path, so this reports
    /// what really happens; the law itself is measured by Straight. hit = that body gained the probe or died.
    static (bool hit, bool probeDied) SolProbe(int body, double h, double v)
    {
        var t = World.SolSystem(0, 1234);
        double m0 = t.M[body];
        int pr = t.Add(t.X[body] + 2, t.Y[body], t.Vx[body] - v, t.Vy[body], 1e-9, RockMix, "probe");
        for (int s = 0; s < 4000 && t.Alive[pr] && t.Alive[body]; s++) t.Advance(h);
        return (!t.Alive[body] || t.M[body] > m0 + 5e-10, !t.Alive[pr]);
    }

    static double PerStep(World w, int steps)
    {
        for (int i = 0; i < 20; i++) w.Advance(H);
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < steps; i++) w.Advance(H);
        return sw.Elapsed.TotalMilliseconds / steps;
    }

    static World Pullers(int n)
    {
        var w = new World(n + 64, 5);
        int sun = w.Add(0, 0, 0, 0, 50, GasMix, "sun");
        for (int i = 0; i < n; i++)
        {
            double d = 20 + 60 * w.Next(), a = w.Next() * Math.Tau;
            w.AddOrbiting(sun, Math.Cos(a) * d, Math.Sin(a) * d, 2e-4, RockMix);
        }
        return w;
    }

    // ---- World.cs:235-253 — what the hash covers (Step joined it in master 4718358, World.cs:247)

    static void HashChecks()
    {
        var a = new World(4, 1);
        a.Add(0, 0, 0, 0, 3e-4, RockMix, "a");
        var b = new World(4, 1);
        b.Add(0, 0, 7, 0, 3e-4, RockMix, "a");
        Expect(a.Hash() != b.Hash(),
            $"hash-velocity (World.cs:244 mixes Vx/Vy/Par, World.cs:245 Comp): two worlds alike in position and mass, Vx 0 vs 7, hash {(a.Hash() == b.Hash() ? "EQUAL" : "differ")} — the gap Celine found in review is closed");

        var c = new World(4, 1);
        int p = c.Add(0, 0, 0, 0, 3e-4, RockMix, "p");
        ulong h0 = c.Hash();
        c.Do(new Command(CmdKind.Push, Target: p, Vx: 100, Vy: -50));
        Expect(c.Hash() != h0,
            $"hash-momentum (World.cs:244): Push of (100, -50) moves the hash, so a lost Push that has not yet moved anything is now visible to the SPEC boundary 'scene + journal = same world'");

        var d = World.SolSystem(0, 1234);
        ulong k0 = d.Hash();
        d.Do(new Command(CmdKind.SetConst, Name: "JumpSamples", Amount: 7));
        Expect(d.Hash() != k0,
            $"hash-constants (World.cs:248-252 reflects over every Consts instance field, all of them double or double[]): Do(SetConst JumpSamples 7) {(d.Hash() == k0 ? "leaves the hash EQUAL" : "moves the hash")}");

        // the consequence: a journal whose SetConst row was dropped replays to the same hash — the replay
        // check in Program.cs cannot catch it, and Program.cs:85 has to compare C.G by hand
        var src = new World(8, 3);
        int s0 = src.Add(0, 0, 0, 0, 3e-4, RockMix, "p");
        src.Add(1, 0, 0, 0.2, 1e-4, RockMix, "q");
        src.Do(new Command(CmdKind.SetConst, Name: "JumpSamples", Amount: 7));
        Run(src, 200);
        var same = new World(8, 3);
        same.Add(0, 0, 0, 0, 3e-4, RockMix, "p");
        same.Add(1, 0, 0, 0.2, 1e-4, RockMix, "q");
        var dropped = src.Journal.Where(j => j.Cmd.Kind != CmdKind.SetConst).ToList();
        int next = 0;
        for (int s = 0; s < 200; s++) { same.Replay(dropped, ref next); same.Advance(H); }
        Expect(src.Hash() != same.Hash(),
            $"hash-replay (Commands.cs:75, World.cs:248-252): a journal with its SetConst row removed replays to hash {same.Hash():X16} vs live {src.Hash():X16} ({(src.Hash() == same.Hash() ? "EQUAL, so the difference is invisible" : "differs")}); {src.Journal.Count} journal rows, {dropped.Count} replayed");

        // Step used to sit outside the widened hash while Replay keys on it (Commands.cs:45), so two worlds at
        // different journal positions looked alike; master 4718358 mixes it (World.cs:247). The third world is the
        // control: same state and same step count, so an equal hash there pins the difference above on Step itself.
        var s1 = new World(4, 1);
        s1.Add(0, 0, 0, 0, 3e-4, RockMix, "p");
        var s2 = new World(4, 1);
        s2.Add(0, 0, 0, 0, 3e-4, RockMix, "p");
        var s3 = new World(4, 1);
        s3.Add(0, 0, 0, 0, 3e-4, RockMix, "p");
        s1.Advance(0);
        s2.Advance(0); s2.Advance(0);
        s3.Advance(0); s3.Advance(0);
        Expect(s1.Hash() != s2.Hash() && s2.Hash() == s3.Hash(),
            $"hash-step (World.cs:247 mixes Step, Commands.cs:45 Replay applies a row only when journal[next].Step == Step): A at Step {s1.Step} hashes {s1.Hash():X16}, B at Step {s2.Step} hashes {s2.Hash():X16}, a control world also at Step {s3.Step} hashes {s3.Hash():X16} — a journal row A applies and B skips can no longer hide behind an equal hash");
    }

    // ---- Commands.cs / World.cs — the god's dial has no domain

    static void ConstChecks()
    {
        var w = World.SolSystem(0, 1234);
        int live0 = w.Live;
        w.Do(new Command(CmdKind.SetConst, Name: "Density[2]", Amount: 0));
        Run(w, 20);
        Expect(w.Live == live0,
            $"setconst-density-0 (Commands.cs:75 -> World.cs:79-84 SetRadius, vol = comp/density): Density[2] 1 -> 0 makes R infinite on every rock-bearing body, so the whole system merges into {w.Live} of {live0} objects in {(w.Live == live0 ? "no" : "20")} steps, sun included, no undo");

        var q = World.SolSystem(0, 1234);
        q.Do(new Command(CmdKind.SetConst, Name: "Density[2]", Amount: 0));
        Expect(double.IsFinite(q.R[0]),
            $"setconst-density-0-nan (World.cs:82 vol += Comp/C.Density[e], 0/0 = NaN): the Sun carries no rock, so its rock term is 0/0 and R[0] = {q.R[0]}; every touching test is then d2 < rr*rr with rr NaN, i.e. false, so the Sun can never be touched again while rock-bearing bodies get R = {q.R[3]} and merge with everything");

        var y = World.SolSystem(0, 1234);
        bool threw = false; string what = "";
        try { y.Do(new Command(CmdKind.SetConst, Name: "YearTime", Amount: 0)); y.Advance(H); }
        catch (Exception e) { threw = true; what = e.GetType().Name; }
        Expect(!threw, $"setconst-yeartime-0 (Commands.cs:75 accepts any finite value, World.cs:192 throws): Do(SetConst YearTime 0) is applied and the next Advance throws {what} — the window dies and the world is not saved");

        var n = World.SolSystem(0, 1234);
        double r0 = n.R[3];
        n.Do(new Command(CmdKind.SetConst, Name: "RadiusScale", Amount: 200));
        Run(n, 3);
        Risk($"radius-dial-keeps-mass (Commands.cs:77 RecalcRadii): RadiusScale 1.5 -> 200 grows Earth R {r0:F4} -> {n.R[3]:F2} and every pair that now overlaps merges: Live {n.Live} of 10 after 3 steps; turning a draw-scale dial down again does not bring the object back");
    }

    // ---- World.cs:108-125 — Add / AddOrbiting guard nothing

    static void AddChecks()
    {
        var neg = new World(4, 1);
        int k = neg.Add(0, 0, 0, 0, -1e-4, RockMix, "neg");
        Expect(k < 0,
            $"add-negative-mass (World.cs:108 takes any double): Add(m=-1e-4) returns slot {k} and stays alive, so G*M<0 repels every pulling body; Commands.cs:55 blocks it at the boundary, Add does not");

        bool threw = false;
        string err = "no";
        var o = new World(2, 1);
        o.Add(0, 0, 0, 0, 3e-4, RockMix, "p");
        try { o.AddOrbiting(7, 1, 0, 1e-6, RockMix); }
        catch (Exception e) { threw = true; err = e.GetType().Name; }
        Expect(!threw, $"addorbiting-bad-parent (World.cs:120 reads X[parent] with no check): AddOrbiting(parent=7) on a 2-slot world throws {err}; Commands.cs:57 is the only guard");

        var nan = new World(4, 1);
        nan.Add(double.NaN, 0, 0, 0, 3e-4, RockMix, "nan");
        int good = nan.Add(0, 0, 0, 0, 3e-4, RockMix, "good");
        Run(nan, 20);
        Expect(double.IsFinite(nan.X[good]),
            $"add-nan (World.cs:108): one object added at x=NaN turns the other object's x into {nan.X[good]} in 20 steps (its gravity is NaN, d2=NaN, so it also never merges: Live {nan.Live}); nothing in the core refuses a non-finite state, only Advance's h is checked (World.cs:192)");
    }

    // ---- slot identity

    static void SlotChecks()
    {
        var w = new World(8, 1);
        w.Add(0, 0, 0, 0, 3e-4, RockMix, "A");
        int b = w.Add(0.01, 0, 0, 0, 1e-4, RockMix, "B");
        Run(w, 4);
        int c = w.Add(5, 0, 0, 0, 2e-4, RockMix, "C");
        Expect(b != c,
            $"slot-identity (World.cs:111 reuses a freed slot, Commands.cs:136 pushes it): B died in a merge, the next Add returned slot {c} = B's slot and it is now '{w.Name[c]}'; a slot index held by the window or a tool silently denotes a different object — the core has no generation counter to detect it");

        Expect(w.Water[c] == -1 && w.Life[c] == 0 && double.IsNaN(w.Temp[c]) && w.Par[c] == -1 && w.Grp[c] == 0,
            $"slot-reuse-clean (World.cs:112-115): the reused slot {c} starts with Water {w.Water[c]}, Life {w.Life[c]:F2}, Temp {(double.IsNaN(w.Temp[c]) ? "NaN" : w.Temp[c].ToString("F1"))}, Par {w.Par[c]}, Grp {w.Grp[c]} — nothing leaks from the dead object");

        var j = new World(4, 1);
        int r1 = j.Do(new Command(CmdKind.SetConst, Name: "G", Amount: 2));
        int r2 = j.Do(new Command(CmdKind.Create, X: 9, Y: 9, Amount: 1e-5, Mix: RockMix));
        Expect(r1 != r2,
            $"do-return (Commands.cs:38 Math.Max(r, 0)): on a fresh world SetConst returns {r1}, and a Create that landed in slot 0 also returns {r2}; -2 means 'applied, no object' and is reported as 0, so a caller cannot tell the two apart");
    }

    // ---- Hill when the parent is gone

    static void HillChecks()
    {
        var w = World.SolSystem(0, 1234);
        const int earth = 3, moon = 9;
        double h0 = w.Hill(moon), k0 = (int)w.KindOf(moon);
        w.Do(new Command(CmdKind.Remove, Target: earth));
        double h1 = w.Hill(moon), k1 = (int)w.KindOf(moon);
        w.Advance(H);
        Expect(h1 <= 3 * h0,
            $"hill-orphan (World.cs:97-103 falls back to Heaviest()): Remove(Earth) moves the Moon's zone {h0:F4} -> {h1:F4} ({h1 / h0:F1}x) and Kind {k0} -> {k1} with no signal that the parent is gone; Hill() answers about the Sun while looking like the same call");

        // Merge re-parents the children to the survivor (World.cs:139) while Kill clears them (Commands.cs:137):
        // two different answers to the same event. Check the Merge half is coherent, in a scene with nothing
        // else in it so the outcome is not a guess.
        var mg = new World(8, 1);
        int host = mg.Add(0, 0, 0, 0, 1e-3, RockMix, "host");
        int orb = mg.AddOrbiting(host, 0.6, 0, 1e-5, RockMix, "moon");
        int heavy = mg.Add(0.01, 0, 0, 0, 2e-3, RockMix, "impact");
        Run(mg, 4);
        Expect(mg.Par[orb] == heavy && mg.Live == 2,
            $"merge-reparents-moon (World.cs:139): a body heavier than the host merged into it, so the moon's Par is now {mg.Par[orb]} = slot {heavy} (host slot {host}, host alive {mg.Alive[host]}), Kind {(int)mg.KindOf(orb)}, live {mg.Live} of 3; Kill instead clears children to -1 (Commands.cs:137), so the same event answers two ways");
    }

    // ---- the swept test the core's own note asks for (World.cs:232)

    /// Fires the same probe at the same target from `n` start offsets spread evenly across one sampling cell
    /// (v * hs), so the only difference between the runs is where the core's tests happen to land. Returns how
    /// many of them ended in a merge, plus the two radii. Every one of them is a dead-centre hit in geometry.
    static (int merged, int n, double rt, double rp) PhaseSweep(double tm, double h, double v, int n)
    {
        var w0 = new World(4, 1);
        int t0 = w0.Add(0, 0, 0, 0, tm, RockMix, "target");
        int p0 = w0.Add(3, 0, -v, 0, 1e-9, RockMix, "probe");
        double rt = w0.R[t0], rp = w0.R[p0];
        double cell = v * h / World.Sub;
        int steps = (int)(4 / (v * h)) + 4, merged = 0;
        for (int j = 0; j < n; j++)
        {
            var w = new World(4, 1);
            w.Add(0, 0, 0, 0, tm, RockMix, "target");
            int pr = w.Add(3 + cell * j / n, 0, -v, 0, 1e-9, RockMix, "probe");
            for (int s = 0; s < steps && w.Alive[pr]; s++) w.Advance(h);
            if (!w.Alive[pr]) merged++;
        }
        return (merged, n, rt, rp);
    }

    static void SweptChecks()
    {
        double hs = H / World.Sub;
        var sol = World.SolSystem(0, 1234);
        double lr = sol.C.RadiusScale * Math.Cbrt(1e-9);
        double Line(double rr) => 2 * (rr + lr) / hs;

        // same encounter, same speed, only the caller's step changes
        var (mSpec, n, rt, rp) = PhaseSweep(1e-6, H, 1.0, 40);
        var (mFine, _, _, _) = PhaseSweep(1e-6, 0.125, 1.0, 40);
        Expect(mSpec == n && mFine == n,
            $"swept-phase (World.cs:212 tests d2 < (ri+Rj)^2 once per small step, hs = h/Sub at World.cs:195): one probe, one target of R {rt:F4}, relative speed 1.0, fired dead centre 40 times from 40 start offsets spread across a single sampling cell ({1.0 * hs:F4} apart) — every one of them a hit in geometry. At the SPEC step h={H} only {mSpec} of {n} ended in a merge; at h=0.125, a step World.cs:192 accepts just as readily, {mFine} of {n} did. The other {n - mSpec} passed through and flew on, no error raised: the outcome is set by where the sampling happens to land, not by the scene");
        // same encounter, same speed and step, only the target's size changes
        var (mPlanet, n2, rpl, rpp) = PhaseSweep(1e-3, H, 1.0, 40);
        var (mMoon, _, rm, _) = PhaseSweep(1e-6, H, 1.0, 40);
        var (mRock, _, rrk, _) = PhaseSweep(1e-8, H, 1.0, 40);
        Expect(mRock == n2 && mMoon == n2,
            $"swept-small-body (World.cs:212, SPEC step h={H} Sub={World.Sub} -> hs={hs:F4}): the same dead-centre probe at the same relative speed 1.0, changing only the target, merges {mPlanet} of {n2} times against a planet-scale target (R {rpl:F4}, probe R {rpp:F4}), {mMoon} of {n2} against a Moon-scale one (R {rm:F4}) and {mRock} of {n2} against a belt-rock one (R {rrk:F4}); the meeting is only ever found when 2(Ri+Rj)/hs = {Line(rt):F2} exceeds the closing speed, i.e. {Line(sol.R[9]):F2} for the Moon, {Line(sol.R[1]):F2} for Mercury, {Line(sol.R[4]):F2} for Mars, {Line(sol.R[3]):F2} for Earth, while crossing orbits close at 1.0-1.7 — so accretion onto anything small is a lottery on the sampling phase (the sol run's 4 merges of 5000 rocks is that lottery)");
    }

    // ---- Rails.cs

    static void RailChecks()
    {
        // an unbound object rides a straight line; OffRails is reset per chunk (Rails.cs:118), not per jump
        var w = World.SolSystem(0, 1234);
        w.Add(80, 0, 0, 4.0, 1e-9, RockMix, "escapee"); // escape speed at 80 is sqrt(2*50/80) = 1.12
        w.Do(new Command(CmdKind.FastForward, Amount: 1e6));
        int chunks = (int)Math.Clamp(Math.Ceiling(1e6 / 1.0), 1, Math.Max(1, w.C.JumpSamples));
        Expect(w.OffRails >= chunks,
            $"rails-offrails-per-chunk (Rails.cs:118 resets OffRails inside Ride): a 1e6-year jump ran {chunks} chunk(s), the escapee was off rails in each, OffRails reports {w.OffRails}; the field answers 'the last chunk', not 'the jump'");

        // the jump must not lose an object that is bound, and must move it about one turn
        var s = World.SolSystem(0, 1234);
        double d0 = Math.Sqrt(Math.Pow(s.X[3] - s.X[0], 2) + Math.Pow(s.Y[3] - s.Y[0], 2));
        int live0 = s.Live;
        s.Do(new Command(CmdKind.FastForward, Amount: 1));
        double d1 = Math.Sqrt(Math.Pow(s.X[3] - s.X[0], 2) + Math.Pow(s.Y[3] - s.Y[0], 2));
        Expect(s.Live == live0 && Math.Abs(d1 / d0 - 1) < 0.1,
            $"rails-bound: one-year jump keeps {s.Live}/{live0} objects and Earth's distance {d0:F3} -> {d1:F3} ({100 * Math.Abs(d1 / d0 - 1):F2}%)");

        // two planets on the SAME orbit, half a turn apart, jumping a whole period: Claire's case, must not fly off
        var t = World.SolSystem(0, 1234);
        int sun = 0, ea = 3;
        double rr = Math.Sqrt(Math.Pow(t.X[ea] - t.X[sun], 2) + Math.Pow(t.Y[ea] - t.Y[sun], 2));
        int twin = t.Add(t.X[sun] - (t.X[ea] - t.X[sun]), t.Y[sun] - (t.Y[ea] - t.Y[sun]), 0, 0, 1.5e-4, RockMix, "twin");
        // give it Earth's velocity mirrored: same orbit, opposite side, same direction of travel
        t.Vx[twin] = t.Vx[ea]; t.Vy[twin] = t.Vy[ea];
        t.Do(new Command(CmdKind.FastForward, Amount: 5));
        double sep = Math.Sqrt(Math.Pow(t.X[twin] - t.X[ea], 2) + Math.Pow(t.Y[twin] - t.Y[ea], 2));
        Expect(sep < 4 * rr && t.Alive[twin],
            $"rails-coorbital: a twin on Earth's orbit, opposite side, after a 5-year jump sits {sep:F1} from Earth (orbit radius {rr:F1}, start {2 * rr:F1}) and is {(t.Alive[twin] ? "alive" : "gone")}");

        // Ride (Rails.cs:106-182) moves objects along orbits and straight lines and runs no pair test at all,
        // so a jump cannot produce a collision. Two bodies meeting head-on: stepping merges them, a jump does not.
        World Pair()
        {
            var q = new World(8, 1);
            q.Add(0, 0, 0, 0, 1e-3, RockMix, "target");
            q.Add(3, 0, -1, 0, 1.5e-4, RockMix, "shot");
            return q;
        }
        var st = Pair();
        for (int k = 0; k < 40 && st.Live > 1; k++) st.Advance(H);
        var jp = Pair();
        jp.Do(new Command(CmdKind.FastForward, Amount: 1));
        Expect(st.Live == jp.Live,
            $"jump-no-contact (Rails.cs:106-182 Ride has no pair test, Rails.cs:168 solves each lump with Kepler, which sends an unbound object down a straight line at Rails.cs:193): the same scene, a 1.5e-4 body 3 units from a 1e-3 body closing at relative speed 1, ends with {st.Live} object(s) under 40 steps but {jp.Live} object(s) after a 1-year jump — the shot is now at x {jp.X[1]:F1}, {jp.Merges} merge(s) during the jump; a jump is also a wall that nothing can hit");
    }

    // ---- Layers.cs

    static void LayerChecks()
    {
        // water years are banked across a period when the rule was off, then life starts on them
        var w = World.SolSystem(0, 1234);
        const int earth = 3;
        Run(w, 200);
        Expect(w.Water[earth] == (int)WaterState.Liquid,
            $"layers-water-start: Earth after 200 steps: Water {(WaterState)w.Water[earth]}, WaterYears {w.WaterYears[earth]:F2}, Life {w.Life[earth]:F4}, Temp {w.Temp[earth]:F1}");

        var b = World.SolSystem(0, 1234);
        Run(b, 200);
        double wy0 = b.WaterYears[earth];
        b.Do(new Command(CmdKind.SetRule, Name: "water", Amount: 0));
        b.Do(new Command(CmdKind.FastForward, Amount: 5e5));
        double wy1 = b.WaterYears[earth];
        b.Do(new Command(CmdKind.SetRule, Name: "water", Amount: 1));
        b.Advance(H);
        double credited = b.WaterYears[earth] - wy1;
        b.Do(new Command(CmdKind.FastForward, Amount: 1e4));
        Expect(credited < 1e4,
            $"layers-water-bank (Layers.cs:87 WaterYears += RuleYears, Rules.cs:121 RuleYears = Year - LastYear): with the water rule off for 5e5 years WaterYears stayed {wy0:F0} -> {wy1:F0}, then the first run after switching it back credited {credited:F0} years in one step; one more 1e4-year jump and Life is {b.Life[earth]:F5}, started on years nobody measured");

        // the three layer rules must be dt-independent: a jump in one call vs many small calls
        var big = World.SolSystem(0, 1234);
        big.Do(new Command(CmdKind.FastForward, Amount: 2e6));
        var many = World.SolSystem(0, 1234);
        for (int i = 0; i < 20; i++) many.Do(new Command(CmdKind.FastForward, Amount: 1e5));
        bool near = Math.Abs(big.Life[earth] - many.Life[earth]) < 0.05 && Math.Abs(big.Pop[earth] - many.Pop[earth]) < 0.05;
        Expect(near,
            $"layers-dt: 2e6 years in one jump vs twenty 1e5-year jumps: Life {big.Life[earth]:F4} vs {many.Life[earth]:F4}, Pop {big.Pop[earth]:F4} vs {many.Pop[earth]:F4}, Tech {big.Tech[earth]:F4} vs {many.Tech[earth]:F4}");
        Info($"  gear: big Year {big.Year:F0} vs many {many.Year:F0}; events {big.Events.Count} vs {many.Events.Count}");

        // a merge into a living planet must cut life and must not resurrect a dead one
        var m = World.SolSystem(0, 1234);
        m.Do(new Command(CmdKind.SeedLife, Target: earth, Amount: 0.5));
        double before = m.Life[earth], pm0 = m.M[earth];
        int rock = m.Add(m.X[earth] + 0.01, m.Y[earth], m.Vx[earth], m.Vy[earth], 1e-4, RockMix, "impactor");
        Run(m, 6);
        Expect(m.Alive[rock] == false && m.Life[earth] < before,
            $"layers-impact (Layers.cs:179-186): a rock of {1e-4 / World.EarthMass:F2} Earth hit Earth of {pm0 / World.EarthMass:F2} Earth at orbital speed, share {1e-4 / pm0:F3} against the log threshold ImpactScale/10 = {m.C.ImpactScale / 10:E2}, life {before:F4} -> {m.Life[earth]:F4}, {m.Events.Count} event(s); only the mass share counts, the relative speed plays no part");
    }

    // ---- Rails.cs:28-90 — the deferred-rock jump path (Celine's trim 969cd49, one formula for either placement since master 4718358)

    // Rocks ride the orbit they had at the jump start on either path since master 4718358 (Rails.cs:39-44); a rule
    // table that is not the builtin one only moves their placement from the end of the jump to after every chunk
    // (Rails.cs:57-60). This adds one rule whose rhythm equals the fastest builtin one, so the chunk count stays put
    // and only the rock work differs.
    static void AddModRule(World w) => w.Rules.Add(new Rule("audit-noop", "M", "", 0.01, static _ => { }));

    static double JumpMs(World w, double years)
    {
        var sw = Stopwatch.StartNew();
        w.Do(new Command(CmdKind.FastForward, Amount: years));
        sw.Stop();
        return sw.Elapsed.TotalMilliseconds;
    }

    static int Rocks(World w)
    {
        int n = 0;
        for (int i = 0; i < w.N; i++) if (w.Alive[i] && !w.Attracts(i)) n++;
        return n;
    }

    // rocks a body already overlaps: the next collision test (World.cs:212) would merge them
    static int Touching(World w)
    {
        int n = 0;
        for (int i = 0; i < w.N; i++)
        {
            if (!w.Alive[i] || w.Attracts(i)) continue;
            for (int j = 0; j < w.N; j++)
            {
                if (!w.Alive[j] || !w.Attracts(j)) continue;
                double dx = w.X[i] - w.X[j], dy = w.Y[i] - w.Y[j], rr = w.R[i] + w.R[j];
                if (dx * dx + dy * dy < rr * rr) { n++; break; }
            }
        }
        return n;
    }

    static void DeferChecks()
    {
        const int n = 5000;
        var trim = World.SolSystem(n, 7);
        var full = World.SolSystem(n, 7);
        AddModRule(full);
        double msT = JumpMs(trim, 1e6), msF = JumpMs(full, 1e6);
        Expect(trim.Year > 0 && msT < msF,
            $"defer-cost (Rails.cs:39-44 decides deferral, Rails.cs:122/164/181 leave rocks out of the chunks): one 1e6-year jump over {n} rocks costs {msT:F0} ms on the builtin rule table against {msF:F0} ms in the same scene once one extra no-op rule is added ({msF / Math.Max(msT, 1e-9):F1}x); same years, same chunk count, only the rock work differs");

        // rocks are left out of every rule tick inside the jump, but Rails.cs:60 clears the flag before the RunRules
        // that are meant to see them (every chunk once the table is not the builtin one), so the two must agree
        var hot = World.SolSystem(n, 7);
        Run(hot, 20);
        int rock = -1;
        for (int i = 0; i < hot.N && rock < 0; i++) if (hot.Alive[i] && !hot.Attracts(i)) rock = i;
        double before = hot.Temp[rock];
        hot.Do(new Command(CmdKind.FastForward, Amount: 1e4));
        var hot2 = World.SolSystem(n, 7);
        AddModRule(hot2);
        Run(hot2, 20);
        hot2.Do(new Command(CmdKind.FastForward, Amount: 1e4));
        double rel = Math.Abs(hot.Temp[rock] - hot2.Temp[rock]) / Math.Max(Math.Abs(hot2.Temp[rock]), 1e-9);
        Expect(!double.IsNaN(hot.Temp[rock]) && rel < 0.05,
            $"defer-rock-temp (Rules.cs:88 skips rocks while _deferRocks; Rails.cs:60 clears the flag before the RunRules that see them): after a 1e4-year jump rock {rock} reads {hot.Temp[rock]:F1} K on the builtin table against {hot2.Temp[rock]:F1} K with one extra rule in it — placement once at the end against placement after every chunk ({rel:P2} apart; {before:F1} K before the jump) — the belt is not left frozen at its pre-jump temperature");

        // each rock rides the primary it had at the jump start, over the time gone by (Rails.cs:45-49 Ride(0) keeps
        // that orbit, Rails.cs:70-90 PlaceRocks solves the one arc from it), and master 4718358 gives both placements
        // that same formula. Compare them at the default chunk cap and at a cap 20x finer: what shrinks with the cap
        // is the coarse reference, what stays is the jump itself.
        static double DriftAt(double cap, out double bodies, out int moved, out int touching, out long merges)
        {
            var a = World.SolSystem(2000, 11);
            var b = World.SolSystem(2000, 11);
            AddModRule(b);
            a.Do(new Command(CmdKind.SetConst, Name: "JumpSamples", Amount: cap));
            b.Do(new Command(CmdKind.SetConst, Name: "JumpSamples", Amount: cap));
            a.Do(new Command(CmdKind.FastForward, Amount: 1e4));
            b.Do(new Command(CmdKind.FastForward, Amount: 1e4));
            bodies = 0; moved = 0;
            double worst = 0;
            for (int i = 0; i < a.N && i < b.N; i++)
            {
                if (!a.Alive[i] || !b.Alive[i]) continue;
                double dx = a.X[i] - b.X[i], dy = a.Y[i] - b.Y[i], d = Math.Sqrt(dx * dx + dy * dy);
                if (a.Attracts(i)) { if (d / Math.Max(a.R[i], 1e-12) > bodies) bodies = d / Math.Max(a.R[i], 1e-12); }
                else { if (d > worst) worst = d; if (d > a.R[i]) moved++; }
            }
            touching = Touching(a) - Touching(b);
            long m0 = a.Merges - b.Merges;
            Run(a, 20); Run(b, 20);
            merges = a.Merges - b.Merges - m0;
            return worst;
        }
        double coarse = DriftAt(200, out double b1, out int mv1, out int tc1, out long mg1);
        double fine = DriftAt(4000, out double b2, out int mv2, out int tc2, out long mg2);
        // control: the extra no-op rule is inert step by step (same rhythm as temperature, does nothing), so the
        // gap above cannot be the rule itself
        var c1 = World.SolSystem(2000, 11);
        var c2 = World.SolSystem(2000, 11);
        AddModRule(c2);
        Run(c1, 50); Run(c2, 50);
        double ctrl = 0;
        for (int i = 0; i < c1.N; i++) if (c1.Alive[i]) ctrl = Math.Max(ctrl, Math.Max(Math.Abs(c1.X[i] - c2.X[i]), Math.Abs(c1.Y[i] - c2.Y[i])));
        Info($"  control: 50 ordinary steps with and without the no-op rule: furthest position difference {ctrl:E2}");
        Expect(fine < 1.0,
            $"defer-divergence (Rails.cs:39-44 decides deferral from the rule table alone, Rails.cs:45-49 keeps each rock's jump-start orbit, Rails.cs:70-90 PlaceRocks replays that one arc — one formula for either placement since master 4718358): the same scene jumped 1e4 years with the builtin table and with one extra no-op rule ends with rocks {coarse:E2} units apart at the default chunk cap and {fine:E2} at cap 4000, {mv2} of 2000 further apart than their own radius, bodies {b2:F4} radii, rocks-touching-a-body {tc2}, merges over the next 20 steps {mg2}; refining the reference 20x does not shrink the gap ({coarse:E2} -> {fine:E2}) and the rule that moves placement to every chunk is inert over 50 ordinary steps ({ctrl:E2}) — placing the belt once at the end or after every chunk now agrees to the last bit, so the trim costs time and no longer physics");
    }

    // ---- Layers.cs — Touched, the stretch-exact clock (master 969cd49)

    static void TouchChecks()
    {
        const int earth = 3;

        var h = World.SolSystem(0, 1234);
        Run(h, 20);
        ulong h0 = h.Hash();
        double t0 = h.Touched[earth];
        h.Touched[earth] = t0 + 1;
        Expect(h.Hash() != h0,
            $"touched-in-hash (Layers.cs:194 mixes Touched for every alive slot): Touched[Earth] {t0:F4} -> {t0 + 1:F4} leaves the hash {(h.Hash() == h0 ? "EQUAL" : "changed")}, so two worlds that disagree about when the last touch happened cannot pass for the same world");
        h.Touched[earth] = t0;

        // a seed years after the rule's own last tick must be credited only from the seed onwards
        var lw = World.SolSystem(0, 1234);
        Run(lw, 200);
        lw.Do(new Command(CmdKind.SetRule, Name: "life", Amount: 0));
        lw.Do(new Command(CmdKind.FastForward, Amount: 1e6));
        lw.Do(new Command(CmdKind.SeedLife, Target: earth, Amount: 0.5));
        double gap = lw.Year - lw.Touched[earth];
        lw.Do(new Command(CmdKind.SetRule, Name: "life", Amount: 1));
        lw.Advance(H);
        Expect(lw.Life[earth] < 0.6,
            $"touched-seed-after-gap (Layers.cs:101 dt = Min(RuleYears, Year - Touched), Layers.cs:183 sets it on Impact too): life was off for 1e6 years, a seed of 0.5 landed {gap:E2} year(s) before the rule's own last run, and the first tick moved it to {lw.Life[earth]:F4} — the million years nobody measured are not credited to the seed");

        // an impact is a touch: the same history must not care how the jump after it was cut
        var ib = World.SolSystem(0, 1234);
        var im = World.SolSystem(0, 1234);
        foreach (var w in new[] { ib, im })
        {
            w.Do(new Command(CmdKind.SeedLife, Target: earth, Amount: 0.5));
            w.Add(w.X[earth] + 0.01, w.Y[earth], w.Vx[earth], w.Vy[earth], 1e-4, RockMix, "impactor");
            Run(w, 8);
        }
        ib.Do(new Command(CmdKind.FastForward, Amount: 2e6));
        for (int i = 0; i < 20; i++) im.Do(new Command(CmdKind.FastForward, Amount: 1e5));
        bool same = Math.Abs(ib.Life[earth] - im.Life[earth]) < 0.05 && Math.Abs(ib.Pop[earth] - im.Pop[earth]) < 0.05;
        Expect(same,
            $"touched-impact-split (Layers.cs:183 Touched = Year on Impact, Layers.cs:101 uses it): after a real hit on Earth, 2e6 years in one jump vs twenty 1e5-year jumps: Life {ib.Life[earth]:F4} vs {im.Life[earth]:F4}, Pop {ib.Pop[earth]:F4} vs {im.Pop[earth]:F4}, Tech {ib.Tech[earth]:F4} vs {im.Tech[earth]:F4}");
    }

    // ---- Commands.cs:95-125 — the two god commands that arrived with master (Move, Force)

    static void GodChecks()
    {
        // the journal must replay them: Move and Force write velocity, which only the widened hash covers
        var src = World.SolSystem(0, 1234);
        int probe = src.Add(src.X[3] + 2, src.Y[3], src.Vx[3], src.Vy[3], 1e-5, RockMix, "probe");
        src.Do(new Command(CmdKind.Push, Target: probe, Vx: 0.3, Vy: -0.2));
        src.Do(new Command(CmdKind.Move, Target: probe, X: 12, Y: 3, Vx: 0, Vy: 1));
        src.Do(new Command(CmdKind.Force, X: 12, Y: 3, Vx: 2, Amount: 0.1));
        Run(src, 50);
        var rep = World.SolSystem(0, 1234);
        rep.Add(rep.X[3] + 2, rep.Y[3], rep.Vx[3], rep.Vy[3], 1e-5, RockMix, "probe");
        int next = 0;
        for (int s = 0; s < 50; s++) { rep.Replay(src.Journal, ref next); rep.Advance(H); }
        Expect(src.Hash() == rep.Hash(),
            $"move-force-replay (Commands.cs:43-46 keys Replay on Step, World.cs:244 mixes Vx/Vy): a journal of {src.Journal.Count} rows holding Push, Move and Force replays to hash {rep.Hash():X16} against the live {src.Hash():X16} ({(src.Hash() == rep.Hash() ? "the same" : "different")}), {next} rows consumed");

        // the hand's falloff is (1 - d/rad); a body dead on the centre has no side to be shoved to, so the core
        // skips it on purpose (Commands.cs:120, ruled intended in master 4718358). Measured below as a fact, then
        // filed as a risk and not a defect: it bites only if a Force centre can land exactly on a body.
        var f = new World(8, 1);
        int centre = f.Add(0, 0, 0, 0, 3e-4, RockMix, "centre");
        int near = f.Add(0.001, 0, 0, 0, 3e-4, RockMix, "near");
        int far = f.Add(0.4, 0, 0, 0, 3e-4, RockMix, "far");
        f.Do(new Command(CmdKind.Force, X: 0, Y: 0, Vx: 0.5, Amount: 1));
        Expect(Math.Abs(f.Vx[centre]) < 1e-12 && Math.Abs(f.Vx[near]) > 4 * Math.Abs(f.Vx[far]),
            $"force-centre-hole (Commands.cs:120 keeps d2 == 0 a no-op, Commands.cs:121 k = Amount*(1-d/rad)/d): one Force of radius 0.5 and Amount 1 at (0, 0) leaves the body exactly at the centre at {Math.Abs(f.Vx[centre]):E2}, moves one 0.001 away by {Math.Abs(f.Vx[near]):F4} and one at 0.4 by {Math.Abs(f.Vx[far]):F4} — the (1 - d/rad) falloff holds everywhere except on the tile the hand is aimed at");
        Risk($"force-centre-hole: the body the hand is pointed at is the only one it cannot move, and its neighbour 0.001 off-centre is thrown {Math.Abs(f.Vx[near]) / Math.Max(Math.Abs(f.Vx[far]), 1e-12):F1}x harder than one at 0.4 — a hole in the tool's meaning the moment a Force centre lands exactly on a body, by design until then (Commands.cs:120)");

        // Move carries what the moved body holds (Commands.cs:95-111, master 4718358): anything lighter, inside its
        // zone and bound to it keeps its place around it, so Par and Kind stay true. Kill is still the only command
        // that clears a child (Commands.cs:137).
        var mv = World.SolSystem(0, 1234);
        const int earth = 3, moon = 9;
        double zone = mv.Hill(earth);
        double r0 = Math.Sqrt(Math.Pow(mv.X[moon] - mv.X[earth], 2) + Math.Pow(mv.Y[moon] - mv.Y[earth], 2));
        mv.Do(new Command(CmdKind.Move, Target: earth, X: mv.X[earth] + 30, Y: mv.Y[earth], Vx: mv.Vx[earth], Vy: mv.Vy[earth]));
        double r = Math.Sqrt(Math.Pow(mv.X[moon] - mv.X[earth], 2) + Math.Pow(mv.Y[moon] - mv.Y[earth], 2));
        Expect(Math.Abs(r - r0) < 1e-12 && r < zone && mv.Par[moon] == earth,
            $"move-par-stale (Commands.cs:95-111 carries the lighter bodies the moved object holds, Commands.cs:137 clears children only on Kill, World.cs:76 decides Kind from Par): Move(Earth) 30 units away leaves the Moon {r:F4} units from it against {r0:F4} before and a Hill zone of {zone:F4} (World.cs:97-102), Par answers {mv.Par[moon]} and KindOf {(int)mv.KindOf(moon)} (2 = Moon) — the moon rides along at its own offset and the panel stays right");

        // the same command with the held body as the target: the new loop carries the children of the moved object
        // only, and nothing else re-derives Par or Kind when the god drags the child itself
        var mc = World.SolSystem(0, 1234);
        double zoneC = mc.Hill(earth);
        mc.Do(new Command(CmdKind.Move, Target: moon, X: mc.X[moon] + 30, Y: mc.Y[moon], Vx: mc.Vx[moon], Vy: mc.Vy[moon]));
        double rc = Math.Sqrt(Math.Pow(mc.X[moon] - mc.X[earth], 2) + Math.Pow(mc.Y[moon] - mc.Y[earth], 2));
        Expect(!(mc.Par[moon] == earth && rc > 10 * zoneC),
            $"move-child-par-stale (Commands.cs:95-111 carries the children of the moved object, so a drag on the child itself re-derives nothing; Commands.cs:137 clears Par only on Kill, World.cs:76 reads Kind from Par): Move(Moon) 30 units out leaves it {rc:F2} units from Earth, {rc / zoneC:F0}x outside the parent's Hill zone ({zoneC:F4}, World.cs:97-102), while Par still answers {mc.Par[moon]} = Earth and KindOf still answers {(int)mc.KindOf(moon)} (2 = Moon) — the rails drop it (primaryOf wants it inside the zone) and the panel keeps calling it a moon");
    }

    // ---- core/Civ.cs — a ship, its goal, the people it carries (round 5 hunt)

    /// The core's own ShipMix (Civ.cs:44) is private; mirrored so a hand-made ship is the object
    /// UpdateShips builds: all metal, C.ShipMass, with the ship cells pointed somewhere.
    static readonly double[] ShipMix = { 0, 0, 0, 1, 0, 0 };

    static int MakeShip(World w, int civ, int to, double tech, double x, double y, double vx, double vy)
    {
        int s = w.Add(x, y, vx, vy, w.C.ShipMass, ShipMix, "Tàu kiểm");
        w.ShipCiv[s] = civ; w.ShipTo[s] = to; w.ShipFrom[s] = -1; w.ShipTech[s] = tech; w.ShipBorn[s] = w.Year;
        return s;
    }

    static double Gap(World w, int a, int b)
    {
        double dx = w.X[a] - w.X[b], dy = w.Y[a] - w.Y[b];
        return Math.Sqrt(dx * dx + dy * dy);
    }

    static void CivChecks()
    {
        Head("audit P3 round 3: core/Civ.cs — ship goals, colonies, the mass dial, the Chronicle");

        // (1) the goal is a bare slot index: Ok() asks bounds and Alive, never "is it still the same object"
        var w = new World(8, 1);
        int goal = w.Add(0, 0, 0, 0, 3e-4, RockMix, "đích");
        w.Add(80, 0, 0, 0, 1e-5, RockMix, "khác");
        int ship = MakeShip(w, 0, goal, 2, 5, 0, -1, 0);
        w.Do(new Command(CmdKind.Remove, Target: goal));
        int fresh = w.Do(new Command(CmdKind.Create, X: -80, Y: 0, Amount: 3e-4, Mix: RockMix));
        double away = Gap(w, ship, fresh);
        w.Do(new Command(CmdKind.FastForward, Amount: 1));
        Expect(w.Pop[fresh] == 0 && w.Civ[fresh] < 0,
            $"ship-goal-slot-reuse (Commands.cs:48 Ok = 0 <= i < N && Alive, Civ.cs:124 LandShips calls Land(ShipTo[i])): the ship's goal was slot {goal}, Remove then Create reused it (fresh slot {fresh}, reused {fresh == goal}) for a different body {away:F1} units away, and one jump lands the colony on that stranger (Pop {w.Pop[fresh]:E2}, Civ {w.Civ[fresh]}, Chronicle {w.Chronicle.Count}) — the target is an index, so a recycled slot silently redirects a ship and its people to an object they were never sent to");

        // (2) two peoples, one goal: Land() answers the second arrival with nothing at all
        var t = new World(8, 1);
        int land = t.Add(0, 0, 0, 0, 3e-4, RockMix, "đất");
        MakeShip(t, 0, land, 2, 4, 0, -1, 0);
        MakeShip(t, 1, land, 2, 5, 0, -1.2, 0);
        t.Do(new Command(CmdKind.FastForward, Amount: 1));
        int colonies = t.Chronicle.Count(e => e.Event.Change == "civ.colony");
        int second = t.Chronicle.Count(e => e.Event.Change == "civ.colony" && e.Civ == 1);
        Expect(colonies == 2 && second == 1,
            $"ship-two-civs-one-goal (Civ.cs:104-109 Land falls out of `Pop > 0 && Civ != civ` with no else, Civ.cs:125 kills the ship either way): two ships of civ 0 and civ 1 sent to the same empty world, one jump — the world belongs to civ {t.Civ[land]} (Pop {t.Pop[land]:E2}) and the Chronicle holds {colonies} colony line(s), {second} of them for the second people: its ship, its tech and its people are gone with no event, so the story cannot say a colony failed");

        // (3) ShipMass is a dial with no range, and the mass-derived predicates never ask IsShip
        var m = new World(8, 1);
        int home = m.Add(0, 0, 0, 0, 3e-4, RockMix, "nhà");
        int rider = MakeShip(m, 1, -1, 2, 3, 0, -0.5, 0);
        m.Do(new Command(CmdKind.SetConst, Name: "ShipMass", Amount: 1e-5));
        int hull = MakeShip(m, 0, home, 2, 0.5, 0, 0, 0);
        m.ShipTo[rider] = hull;
        bool isWorld = m.IsWorld(hull), solid = m.IsSolid(hull), pulls = m.Attracts(hull);
        m.Do(new Command(CmdKind.FastForward, Amount: 1));
        int hullLines = m.Chronicle.Count(e => e.Event.Change == "civ.colony" && e.Event.ObjectSlot == hull);
        Expect(!isWorld && !solid,
            $"ship-mass-world (Civ.cs:18 ShipMass is a Const with no range, Layers.cs:70 IsWorld = AttractMass <= M < StarMass, Civ.cs:59 IsSolid = rock+metal share — neither asks IsShip): ShipMass 1e-12 -> 1e-5 (AttractMass is 1e-7) and the hull answers IsWorld {isWorld}, IsSolid {solid}, Attracts {pulls}, so the whole layer stack runs on a ship (Layers.cs:139 gates the civ rule on IsWorld alone, Civ.cs:135 lets ShipGoal pick any IsSolid body) and a second ship sent to it founds a colony on the hull ({hullLines} colony line(s) naming slot {hull})");

        // (4) the same dial at StarMass turns a ship into a sun
        var k = new World(4, 1);
        k.Add(0, 0, 0, 0, k.C.StarMass, GasMix, "sao");
        k.Do(new Command(CmdKind.SetConst, Name: "ShipMass", Amount: 4.0));
        int big = MakeShip(k, 0, 0, 2, 20, 0, -0.5, 0);
        k.Advance(H);
        Expect(k.KindOf(big) != Kind.Star,
            $"ship-mass-star (Civ.cs:18, World.cs:73 KindOf answers Star for M >= StarMass, Civ.cs:199 fills _starSlots with every M >= StarMass): ShipMass 4.0 = StarMass makes the ship itself a star — KindOf {k.KindOf(big)} (World.cs:22 Kind is {{ Star, Planet, Moon, Rock }}), radius {k.R[big]:F4} — so SteerShips now dodges any ship as if it were a sun (Civ.cs:205-215 keeps {k.R[big] * 1.5:F4} units clear) and the temperature rules treat it as a light source");

        // (5) a ship in flight when a jump starts must not fly through it
        var jw = new World(8, 1);
        int jgoal = jw.Add(0, 0, 0, 0, 3e-4, RockMix, "đích");
        MakeShip(jw, 0, jgoal, 2, 30, 0, -1, 0);
        jw.Do(new Command(CmdKind.FastForward, Amount: 1));
        bool flying = false;
        for (int i = 0; i < jw.N; i++) if (jw.Alive[i] && jw.IsShip(i)) flying = true;
        Expect(!flying && jw.Pop[jgoal] > 0,
            $"ship-jump-lands (Civ.cs:30 LandShips is the first statement of Jump, Civ.cs:119-127 lands then kills): a ship in flight when a jump starts never survives it — a live ship after the jump: {flying}; the people are on the goal (Pop {jw.Pop[jgoal]:E2}, Civ {jw.Civ[jgoal]}, Chronicle {jw.Chronicle.Count})");

        // (6) the star itself as the goal
        var gs = new World(8, 1);
        int sun = gs.Add(0, 0, 0, 0, 50, GasMix, "sao");
        int fly = MakeShip(gs, 0, sun, 2, gs.R[sun] + 0.3, 0, -0.4, 0);
        bool blew = false; string why = "none";
        try { for (int s = 0; s < 200 && gs.Alive[fly]; s++) gs.Advance(H); }
        catch (Exception e) { blew = true; why = e.GetType().Name; }
        Expect(!blew && gs.Pop[sun] == 0 && !gs.Alive[fly],
            $"ship-star-goal (Civ.cs:208 skips the star it targets, Civ.cs:113-117 ShipArrives -> Land -> Civ.cs:103 IsSolid refuses a star): a ship sent into the star it aims at — exception {why}, star Pop {gs.Pop[sun]}, ship alive {gs.Alive[fly]} after {gs.Year:F1} years; the ship is simply lost, which is the answer the code intends");

        // (7) Commands on a ship must survive the journal, ship cells included
        var a = new World(8, 1);
        int ga = a.Add(0, 0, 0, 0, 3e-4, RockMix, "đích");
        int sa = MakeShip(a, 0, ga, 2, 20, 0, -0.5, 0);
        a.Do(new Command(CmdKind.Move, Target: sa, X: 25, Y: 1, Vx: 0, Vy: -0.5));
        a.Do(new Command(CmdKind.Force, X: 25, Y: 1, Vx: 2, Amount: 0.05));
        a.Do(new Command(CmdKind.Push, Target: sa, Vx: -0.1, Vy: 0));
        Run(a, 20);
        var b = new World(8, 1);
        b.Add(0, 0, 0, 0, 3e-4, RockMix, "đích");
        MakeShip(b, 0, 0, 2, 20, 0, -0.5, 0);
        int next = 0;
        for (int s = 0; s < 20; s++) { b.Replay(a.Journal, ref next); b.Advance(H); }
        Expect(a.Hash() == b.Hash(),
            $"ship-replay (World.cs:239-246 mixes X/Y/Vx/Vy/M/Par per slot, Civ.cs:229-230 adds Civ/ShipCiv/ShipTo/ShipFrom/ShipTech/ShipBorn when ShipCiv >= 0, Commands.cs:43-46 Replay keys on Step): a journal of {a.Journal.Count} rows holding Move, Force and Push on a ship replays to hash {b.Hash():X16} against the live {a.Hash():X16}, {next} rows consumed — a ship's own cells are inside the hash");

        // (8) a ship that dies leaves no line behind
        var lw = new World(8, 1);
        int keeper = lw.Add(0, 0, 0, 0, 3e-4, RockMix, "đích");
        int doomed = lw.Add(40, 0, 0, 0, 3e-4, RockMix, "đích cũ");
        int lostShip = MakeShip(lw, 0, doomed, 2, 4, 0, -0.1, 0);
        int staleShip = MakeShip(lw, 0, keeper, 2, 6, 0, -0.1, 0);
        lw.ShipBorn[staleShip] = -40; // 40 years in flight, C.ShipLifeYears is 30
        lw.Do(new Command(CmdKind.Remove, Target: doomed));
        Run(lw, 1100);
        Risk($"ship-lost-silent (Civ.cs:153 kills a ship whose ShipTo is dead or older than ShipLifeYears ({lw.C.ShipLifeYears:F0} yr), Civ.cs:69-74 CivEvent is the only writer to the Chronicle and no kill path calls it): after {lw.Year:F2} years the ship sent to a removed world is alive {lw.Alive[lostShip]} and the ship that outlived its own life is alive {lw.Alive[staleShip]}, with {lw.Chronicle.Count} Chronicle line(s) for two lost ships — an expired ship, a stranded ship and a ship the hand removes all leave the story without a word");

        // (9) the launch path indexes a civ list that nothing guarantees
        var ci = new World(8, 1);
        ci.Add(0, 0, 0, 0, 3e-4, RockMix, "dân");
        ci.Add(9, 0, 0, 0, 3e-4, RockMix, "đích");
        ci.Pop[0] = 1; ci.Tech[0] = 3; ci.Civ[0] = 0;
        bool crash = false; string cw = "none";
        try { Run(ci, 2); } catch (Exception e) { crash = true; cw = e.GetType().Name; }
        Expect(!crash,
            $"civ-launch-index (Civ.cs:168 _civLaunches[civ]++ walks a private list only NewCiv grows (Civ.cs:83), while Civ[]/ShipCiv[] are public cells (Civ.cs:38-41) that no path validates): a world where Pop[0] = 1, Tech[0] = 3, Civ[0] = 0 but Civs is empty throws {cw} straight out of Advance on the first rule run — input is a hand-written civ cell (a tool or a loaded save), not a command, and the whole window dies with it");
        Info("[chưa kiểm: dàn dựng] Civ.cs:168 writes the civ.ship.first line and bumps _civLaunches BEFORE Civ.cs:180 Add and Civ.cs:181 `if (ship < 0) continue`, and World.cs:111 returns -1 only when _free is empty and N == X.Length — so at full capacity the Chronicle records a first ship that was never built. Staging needs a real civ (NewCiv is private, CivRiseYears = " + ci.C.CivRiseYears + " years, Layers.cs:144), which this audit did not reach headless.");

        // (10) the three ship dials: 0 and negative are absorbed, a negative thrust is not
        var z = new World(8, 1);
        int zg = z.Add(0, 0, 0, 0, 3e-4, RockMix, "đích");
        int zs = MakeShip(z, 0, zg, 2, 30, 0, -0.2, 0);
        z.Do(new Command(CmdKind.SetConst, Name: "ShipThrust", Amount: 0));
        z.Do(new Command(CmdKind.SetConst, Name: "ShipSpeed", Amount: -5));
        Run(z, 4);
        string absorbed = $"thrust 0 with speed -5 leaves the ship finite ({double.IsFinite(z.X[zs])}, Vx {z.Vx[zs]:E2}, X {z.X[zs]:F2})";
        var th = new World(8, 1);
        int tgoal = th.Add(0, 0, 0, 0, 3e-4, RockMix, "đích");
        int ts = MakeShip(th, 0, tgoal, 2, 30, 0, -0.2, 0);
        th.Do(new Command(CmdKind.SetConst, Name: "ShipThrust", Amount: -1));
        Run(th, 4);
        bool poisoned = !double.IsFinite(th.Vx[ts]) || !double.IsFinite(th.X[ts]) || !double.IsFinite(th.Y[ts]);
        Expect(!poisoned,
            $"ship-thrust-negative (Civ.cs:22 ShipThrust, Civ.cs:203 closing = Math.Clamp(Math.Sqrt(0.5 * C.ShipThrust * d), 0.02, Math.Max(0.02, C.ShipSpeed)), Civ.cs:219 Vx[i] += wx, Commands.cs:154-156 Consts.Set rejects only a non-finite INPUT): SetConst ShipThrust -1 is accepted, 0.5 * thrust * d is negative, Math.Sqrt turns it into NaN and Math.Clamp passes NaN through (every comparison with NaN is false), so Civ.cs:204/216/219 write NaN into the ship's own cells — Vx {th.Vx[ts]}, X {th.X[ts]}, Y {th.Y[ts]} after 4 steps while the goal keeps Vx {th.Vx[tgoal]}; the ship is then skipped by Civ.cs:201 (!(d > 0) is true for NaN) and can never touch, land or die again — {absorbed}, so only the negative branch escapes");

        // (11) 30 years of flight is the only clock a ship has
        var yv = new World(8, 1);
        int ygoal = yv.Add(0, 0, 0, 0, 3e-4, RockMix, "đích");
        int yfar = MakeShip(yv, 0, ygoal, 2, 3e7, 0, 0, 0);
        int ysteps = (int)Math.Ceiling(31 * yv.C.YearTime / H);
        Run(yv, ysteps);
        Expect(!yv.Alive[yfar] && yv.Pop[ygoal] == 0 && yv.Year > yv.C.ShipLifeYears,
            $"ship-life-clock (Civ.cs:23 ShipLifeYears, Civ.cs:153 kills a ship older than it, Civ.cs:203 caps the closing speed at ShipSpeed = {yv.C.ShipSpeed:F2}): a ship sent to a goal {Gap(yv, yfar, ygoal):E1} units away is dead at {yv.Year:F1} years with the goal Pop {yv.Pop[ygoal]} ({ysteps} steps at h = {H}) — at that cap a ship covers at most {yv.C.ShipSpeed * yv.C.ShipLifeYears * yv.C.YearTime:E1} units in its whole {yv.C.ShipLifeYears:F0}-year life, so any order further than that is a guaranteed loss of the ship and its people, and no line is written to say so");
    }
}
