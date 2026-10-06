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
        Head("audit P3: defect checks (core/World.cs, Commands.cs, Rules.cs, Rails.cs, Layers.cs)");
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

        // swept test (World.cs:210 tests d2 < (ri+Rj)^2 once per small step): a probe crossing between two
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

    // ---- World.cs:233-253 — what the hash covers

    static void HashChecks()
    {
        var a = new World(4, 1);
        a.Add(0, 0, 0, 0, 3e-4, RockMix, "a");
        var b = new World(4, 1);
        b.Add(0, 0, 7, 0, 3e-4, RockMix, "a");
        Expect(a.Hash() != b.Hash(),
            $"hash-velocity (World.cs:242 mixes Vx/Vy/Par, World.cs:243 Comp): two worlds alike in position and mass, Vx 0 vs 7, hash {(a.Hash() == b.Hash() ? "EQUAL" : "differ")} — the gap Celine found in review is closed");

        var c = new World(4, 1);
        int p = c.Add(0, 0, 0, 0, 3e-4, RockMix, "p");
        ulong h0 = c.Hash();
        c.Do(new Command(CmdKind.Push, Target: p, Vx: 100, Vy: -50));
        Expect(c.Hash() != h0,
            $"hash-momentum (World.cs:242): Push of (100, -50) moves the hash, so a lost Push that has not yet moved anything is now visible to the SPEC boundary 'scene + journal = same world'");

        var d = World.SolSystem(0, 1234);
        ulong k0 = d.Hash();
        d.Do(new Command(CmdKind.SetConst, Name: "JumpSamples", Amount: 7));
        Expect(d.Hash() != k0,
            $"hash-constants (World.cs:246-250 reflects over every Consts instance field, all of them double or double[]): Do(SetConst JumpSamples 7) {(d.Hash() == k0 ? "leaves the hash EQUAL" : "moves the hash")}");

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
            $"hash-replay (Commands.cs:75, World.cs:246-250): a journal with its SetConst row removed replays to hash {same.Hash():X16} vs live {src.Hash():X16} ({(src.Hash() == same.Hash() ? "EQUAL, so the difference is invisible" : "differs")}); {src.Journal.Count} journal rows, {dropped.Count} replayed");

        // The one thing the widened hash still does not cover: Step, which Replay keys on (Commands.cs:45).
        // h = 0 changes no state, so two worlds at different journal positions still hash the same.
        var s1 = new World(4, 1);
        s1.Add(0, 0, 0, 0, 3e-4, RockMix, "p");
        var s2 = new World(4, 1);
        s2.Add(0, 0, 0, 0, 3e-4, RockMix, "p");
        s1.Advance(0);
        s2.Advance(0); s2.Advance(0);
        Expect(s1.Hash() != s2.Hash(),
            $"hash-step (World.cs:237-251 mixes no Step, Commands.cs:45 Replay applies a row only when journal[next].Step == Step): after two zero-length steps world B is at Step {s2.Step} and world A at Step {s1.Step}, same hash {(s1.Hash() == s2.Hash() ? "EQUAL" : "differ")} — a journal row that arrived at step {s1.Step} is applied by A and skipped forever by B, which then blocks every later row");
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
        Expect(!threw, $"setconst-yeartime-0 (Commands.cs:75 accepts any finite value, World.cs:191 throws): Do(SetConst YearTime 0) is applied and the next Advance throws {what} — the window dies and the world is not saved");

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
            $"add-nan (World.cs:108): one object added at x=NaN turns the other object's x into {nan.X[good]} in 20 steps (its gravity is NaN, d2=NaN, so it also never merges: Live {nan.Live}); nothing in the core refuses a non-finite state, only Advance's h is checked (World.cs:191)");
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
            $"slot-identity (World.cs:111 reuses a freed slot, Commands.cs:126 pushes it): B died in a merge, the next Add returned slot {c} = B's slot and it is now '{w.Name[c]}'; a slot index held by the window or a tool silently denotes a different object — the core has no generation counter to detect it");

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

        // Merge re-parents the children to the survivor (World.cs:138) while Kill clears them (Commands.cs:127):
        // two different answers to the same event. Check the Merge half is coherent, in a scene with nothing
        // else in it so the outcome is not a guess.
        var mg = new World(8, 1);
        int host = mg.Add(0, 0, 0, 0, 1e-3, RockMix, "host");
        int orb = mg.AddOrbiting(host, 0.6, 0, 1e-5, RockMix, "moon");
        int heavy = mg.Add(0.01, 0, 0, 0, 2e-3, RockMix, "impact");
        Run(mg, 4);
        Expect(mg.Par[orb] == heavy && mg.Live == 2,
            $"merge-reparents-moon (World.cs:138): a body heavier than the host merged into it, so the moon's Par is now {mg.Par[orb]} = slot {heavy} (host slot {host}, host alive {mg.Alive[host]}), Kind {(int)mg.KindOf(orb)}, live {mg.Live} of 3; Kill instead clears children to -1 (Commands.cs:127), so the same event answers two ways");
    }

    // ---- the swept test the core's own note asks for (World.cs:228-230)

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
            $"swept-phase (World.cs:210 tests d2 < (ri+Rj)^2 once per small step, hs = h/Sub at World.cs:193): one probe, one target of R {rt:F4}, relative speed 1.0, fired dead centre 40 times from 40 start offsets spread across a single sampling cell ({1.0 * hs:F4} apart) — every one of them a hit in geometry. At the SPEC step h={H} only {mSpec} of {n} ended in a merge; at h=0.125, a step World.cs:191 accepts just as readily, {mFine} of {n} did. The other {n - mSpec} passed through and flew on, no error raised: the outcome is set by where the sampling happens to land, not by the scene");
        // same encounter, same speed and step, only the target's size changes
        var (mPlanet, n2, rpl, rpp) = PhaseSweep(1e-3, H, 1.0, 40);
        var (mMoon, _, rm, _) = PhaseSweep(1e-6, H, 1.0, 40);
        var (mRock, _, rrk, _) = PhaseSweep(1e-8, H, 1.0, 40);
        Expect(mRock == n2 && mMoon == n2,
            $"swept-small-body (World.cs:210, SPEC step h={H} Sub={World.Sub} -> hs={hs:F4}): the same dead-centre probe at the same relative speed 1.0, changing only the target, merges {mPlanet} of {n2} times against a planet-scale target (R {rpl:F4}, probe R {rpp:F4}), {mMoon} of {n2} against a Moon-scale one (R {rm:F4}) and {mRock} of {n2} against a belt-rock one (R {rrk:F4}); the meeting is only ever found when 2(Ri+Rj)/hs = {Line(rt):F2} exceeds the closing speed, i.e. {Line(sol.R[9]):F2} for the Moon, {Line(sol.R[1]):F2} for Mercury, {Line(sol.R[4]):F2} for Mars, {Line(sol.R[3]):F2} for Earth, while crossing orbits close at 1.0-1.7 — so accretion onto anything small is a lottery on the sampling phase (the sol run's 4 merges of 5000 rocks is that lottery)");
    }

    // ---- Rails.cs

    static void RailChecks()
    {
        // an unbound object rides a straight line; OffRails is reset per chunk (Rails.cs:112), not per jump
        var w = World.SolSystem(0, 1234);
        w.Add(80, 0, 0, 4.0, 1e-9, RockMix, "escapee"); // escape speed at 80 is sqrt(2*50/80) = 1.12
        w.Do(new Command(CmdKind.FastForward, Amount: 1e6));
        int chunks = (int)Math.Clamp(Math.Ceiling(1e6 / 1.0), 1, Math.Max(1, w.C.JumpSamples));
        Expect(w.OffRails >= chunks,
            $"rails-offrails-per-chunk (Rails.cs:112 resets OffRails inside Ride): a 1e6-year jump ran {chunks} chunk(s), the escapee was off rails in each, OffRails reports {w.OffRails}; the field answers 'the last chunk', not 'the jump'");

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

        // Ride (Rails.cs:100-176) moves objects along orbits and straight lines and runs no pair test at all,
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
            $"jump-no-contact (Rails.cs:100-176 Ride has no pair test, Rails.cs:162 solves each lump with Kepler, which sends an unbound object down a straight line at Rails.cs:187): the same scene, a 1.5e-4 body 3 units from a 1e-3 body closing at relative speed 1, ends with {st.Live} object(s) under 40 steps but {jp.Live} object(s) after a 1-year jump — the shot is now at x {jp.X[1]:F1}, {jp.Merges} merge(s) during the jump; a jump is also a wall that nothing can hit");
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
            $"layers-water-bank (Layers.cs:85 WaterYears += RuleYears, Rules.cs:121 RuleYears = Year - LastYear): with the water rule off for 5e5 years WaterYears stayed {wy0:F0} -> {wy1:F0}, then the first run after switching it back credited {credited:F0} years in one step; one more 1e4-year jump and Life is {b.Life[earth]:F5}, started on years nobody measured");

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
            $"layers-impact (Layers.cs:177-183): a rock of {1e-4 / World.EarthMass:F2} Earth hit Earth of {pm0 / World.EarthMass:F2} Earth at orbital speed, share {1e-4 / pm0:F3} against the log threshold ImpactScale/10 = {m.C.ImpactScale / 10:E2}, life {before:F4} -> {m.Life[earth]:F4}, {m.Events.Count} event(s); only the mass share counts, the relative speed plays no part");
    }

    // ---- Rails.cs:28-84 — the deferred-rock jump path (Celine's trim, master 969cd49+)

    // Jump() takes the trimmed path only while the rule table is exactly the builtin one (Rails.cs:34-35).
    // One extra rule the jump does not know about turns the same jump back into the full path, with the same
    // chunk count because its rhythm equals the fastest builtin one, so only the rock handling differs.
    static void Untrim(World w) => w.Rules.Add(new Rule("audit-noop", "M", "", 0.01, static _ => { }));

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

    // rocks a body already overlaps: the next collision test (World.cs:210) would merge them
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
        Untrim(full);
        double msT = JumpMs(trim, 1e6), msF = JumpMs(full, 1e6);
        Expect(trim.Year > 0 && msT < msF,
            $"defer-cost (Rails.cs:34-38 decides defer, Rails.cs:116/158/175 leave rocks out of the chunks): one 1e6-year jump over {n} rocks costs {msT:F0} ms on the builtin rule table against {msF:F0} ms in the same scene once one extra no-op rule is added ({msF / Math.Max(msT, 1e-9):F1}x); same years, same chunk count, only the rock work differs");

        // rocks are left out of every rule tick inside the jump, but Rails.cs:55 clears the flag before the last
        // RunRules, so the temperatures that come out of a jump must match the path that never defers
        var hot = World.SolSystem(n, 7);
        Run(hot, 20);
        int rock = -1;
        for (int i = 0; i < hot.N && rock < 0; i++) if (hot.Alive[i] && !hot.Attracts(i)) rock = i;
        double before = hot.Temp[rock];
        hot.Do(new Command(CmdKind.FastForward, Amount: 1e4));
        var hot2 = World.SolSystem(n, 7);
        Untrim(hot2);
        Run(hot2, 20);
        hot2.Do(new Command(CmdKind.FastForward, Amount: 1e4));
        double rel = Math.Abs(hot.Temp[rock] - hot2.Temp[rock]) / Math.Max(Math.Abs(hot2.Temp[rock]), 1e-9);
        Expect(!double.IsNaN(hot.Temp[rock]) && rel < 0.05,
            $"defer-rock-temp (Rules.cs:88 skips rocks while _deferRocks; Rails.cs:55 clears it before the last RunRules): after a 1e4-year jump rock {rock} reads {hot.Temp[rock]:F1} K in the trimmed path against {hot2.Temp[rock]:F1} K in the untrimmed one ({rel:P2} apart; {before:F1} K before the jump) — the belt is not left frozen at its pre-jump temperature");

        // the trim freezes each rock's primary for the whole jump (Rails.cs:41-46 Ride(0) picks it, Rails.cs:65-84
        // FinishRocks solves that one arc from the pre-jump state). Is it the same belt? Compare the two paths at
        // the default chunk cap and at a cap 20x finer: what shrinks with the cap was the coarse reference, what
        // stays is the trim itself.
        static double DriftAt(double cap, out double bodies, out int moved, out int touching, out long merges)
        {
            var a = World.SolSystem(2000, 11);
            var b = World.SolSystem(2000, 11);
            Untrim(b);
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
        Untrim(c2);
        Run(c1, 50); Run(c2, 50);
        double ctrl = 0;
        for (int i = 0; i < c1.N; i++) if (c1.Alive[i]) ctrl = Math.Max(ctrl, Math.Max(Math.Abs(c1.X[i] - c2.X[i]), Math.Abs(c1.Y[i] - c2.Y[i])));
        Info($"  control: 50 ordinary steps with and without the no-op rule: furthest position difference {ctrl:E2}");
        Expect(fine < 1.0,
            $"defer-divergence (Rails.cs:34-35 decides the trim from the rule table, Rails.cs:41-46/65-84 freeze the rock's primary for the whole jump): the same scene jumped 1e4 years by the trimmed path and by the untrimmed one ends with rocks {coarse:E2} units apart at the default chunk cap and {fine:E2} at cap 4000, {mv2} of 2000 further apart than their own radius, bodies {b2:F4} radii, rocks-touching-a-body {tc2}, merges over the next 20 steps {mg2}; refining the reference 20x does not shrink the gap ({coarse:E2} -> {fine:E2}) and the rule that switches paths is inert over 50 ordinary steps ({ctrl:E2}), so FastForward puts the belt somewhere else wherever any extra rule is installed in the table");
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
            $"touched-in-hash (Layers.cs:192 mixes Touched for every alive slot): Touched[Earth] {t0:F4} -> {t0 + 1:F4} leaves the hash {(h.Hash() == h0 ? "EQUAL" : "changed")}, so two worlds that disagree about when the last touch happened cannot pass for the same world");
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
            $"touched-seed-after-gap (Layers.cs:99 dt = Min(RuleYears, Year - Touched), Layers.cs:181 sets it on Impact too): life was off for 1e6 years, a seed of 0.5 landed {gap:E2} year(s) before the rule's own last run, and the first tick moved it to {lw.Life[earth]:F4} — the million years nobody measured are not credited to the seed");

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
            $"touched-impact-split (Layers.cs:181 Touched = Year on Impact, Layers.cs:99 uses it): after a real hit on Earth, 2e6 years in one jump vs twenty 1e5-year jumps: Life {ib.Life[earth]:F4} vs {im.Life[earth]:F4}, Pop {ib.Pop[earth]:F4} vs {im.Pop[earth]:F4}, Tech {ib.Tech[earth]:F4} vs {im.Tech[earth]:F4}");
    }

    // ---- Commands.cs:95-115 — the two god commands that arrived with master (Move, Force)

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
            $"move-force-replay (Commands.cs:43-46 keys Replay on Step, World.cs:242 mixes Vx/Vy): a journal of {src.Journal.Count} rows holding Push, Move and Force replays to hash {rep.Hash():X16} against the live {src.Hash():X16} ({(src.Hash() == rep.Hash() ? "the same" : "different")}), {next} rows consumed");

        // the hand's falloff is (1 - d/rad), but the loop splits on d2 == 0 (Commands.cs:110)
        var f = new World(8, 1);
        int centre = f.Add(0, 0, 0, 0, 3e-4, RockMix, "centre");
        int near = f.Add(0.001, 0, 0, 0, 3e-4, RockMix, "near");
        int far = f.Add(0.4, 0, 0, 0, 3e-4, RockMix, "far");
        f.Do(new Command(CmdKind.Force, X: 0, Y: 0, Vx: 0.5, Amount: 1));
        Expect(Math.Abs(f.Vx[centre]) > 1e-9,
            $"force-centre-hole (Commands.cs:110 skips d2 == 0, Commands.cs:111 k = Amount*(1-d/rad)/d): one Force of radius 0.5 and Amount 1 at (0, 0) moves the body exactly at the centre by {Math.Abs(f.Vx[centre]):E2}, one 0.001 away by {Math.Abs(f.Vx[near]):F4} and one at 0.4 by {Math.Abs(f.Vx[far]):F4} — the object the hand is aimed at is the one object it cannot move, and a neighbour inside a radius of the centre is thrown harder than the far one");

        // Move re-derives nothing: Par still points at a parent the object no longer orbits (Kill clears it)
        var mv = World.SolSystem(0, 1234);
        const int earth = 3, moon = 9;
        double zone = mv.Hill(earth);
        mv.Do(new Command(CmdKind.Move, Target: earth, X: mv.X[earth] + 30, Y: mv.Y[earth], Vx: mv.Vx[earth], Vy: mv.Vy[earth]));
        double dx = mv.X[moon] - mv.X[earth], dy = mv.Y[moon] - mv.Y[earth];
        double r = Math.Sqrt(dx * dx + dy * dy);
        Expect(!(mv.Par[moon] >= 0 && r > 10 * zone),
            $"move-par-stale (Commands.cs:95-100 re-derives nothing, Commands.cs:127 clears children only on Kill, World.cs:76 decides Kind from Par): Move(Earth) 30 units away leaves the Moon {r:F2} units out, {r / zone:F0}x outside its parent's Hill zone ({zone:F4}, World.cs:97-102), while Par still answers {mv.Par[moon]} and KindOf still answers {(int)mv.KindOf(moon)} (2 = Moon) — the rails drop it (primaryOf wants it inside the zone) and the panel keeps calling it a moon");
    }
}
