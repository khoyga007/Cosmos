using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Cosmos.Core;

// Belt probe (Hark, issue #2, round 4, Claire): FoldBelt + WithdrawDiscs on claire/heat-disc @bd2a966.
// Measure-only: reads public World state between calls; core/ is untouched. Scene = World.SolSystem(5000, seed), as game/Main.cs:145.
//
//   cli belt-probe <outDir>          (BELT_THREADS=1 in the environment pins the rock pass to one thread)
//
// Item 1: ledger before/after FoldBelt(sun,1,40) and FoldBelt(sun,2,40).
// Item 2: after both folds, WithdrawDiscs in nine cases, ledger + placement checks of every new object.
// Item 3: 100 calls at the same place.
// Item 4: after a withdraw, 2000 x Advance(.5), fate of the new objects; baseline = the unfolded belts' rocks.
static class BeltProbe
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static string R(double x) => x.ToString("R", Inv);
    static string G(double x) => x.ToString("G6", Inv);
    static readonly ulong[] Seeds = { 1234, 1002, 1011 };
    const int Keep = 40;

    sealed class Led
    {
        public double Mass, Px, Py, L, Mx, My, Sum_mv, Sum_mrv, DiscMass, ObjMass, DiscL, Heat;
        public double[] Matter = Array.Empty<double>();
        public int Live, Discs, Cells, Folded;
    }

    static int Sun(World w) { int s = -1; for (int i = 0; i < w.N; i++) if (w.Alive[i] && (s < 0 || w.M[i] > w.M[s])) s = i; return s; }

    // mass, matter, momentum, angular momentum about the origin, mass moment. Discs count as riding on their host
    // (DiscMassOn at the host's velocity) plus their own signed angular momentum about the host (DiscAngular).
    static Led Ledger(World w)
    {
        int ne = w.ElementCount; var l = new Led { Matter = new double[ne] };
        l.Mass = w.EscapedMass; l.Px = w.EscapedPx; l.Py = w.EscapedPy;
        for (int e = 0; e < ne; e++) l.Matter[e] = w.EscapedMatter[e] - w.NucleosynthesisDelta[e];
        for (int i = 0; i < w.N; i++) if (w.Alive[i])
        {
            double m = w.M[i]; l.Live++;
            l.Mass += m; l.ObjMass += m; l.Px += m * w.Vx[i]; l.Py += m * w.Vy[i];
            double h = w.X[i] * w.Vy[i] - w.Y[i] * w.Vx[i]; l.L += m * h; l.Mx += m * w.X[i]; l.My += m * w.Y[i];
            l.Sum_mv += m * Math.Sqrt(w.Vx[i] * w.Vx[i] + w.Vy[i] * w.Vy[i]); l.Sum_mrv += Math.Abs(m * h);
            for (int e = 0; e < ne; e++) l.Matter[e] += w.Comp[i * ne + e];
        }
        foreach (var r in w.RocheReservoirs)
        {
            l.Mass += r.Mass; l.Px += r.Px; l.Py += r.Py;
            for (int e = 0; e < ne; e++) l.Matter[e] += r.Matter[e];
        }
        foreach (var d in w.Discs)
        {
            l.Discs++; l.Heat += d.Heat;
            if (d.Host < 0) continue;
            int h = d.Host; double t = d.Total;
            l.Mass += t; l.DiscMass += t; l.Px += t * w.Vx[h]; l.Py += t * w.Vy[h];
            double da = w.DiscAngular(d); l.DiscL += da;
            l.L += t * (w.X[h] * w.Vy[h] - w.Y[h] * w.Vx[h]) + da; l.Mx += t * w.X[h]; l.My += t * w.Y[h];
            l.Sum_mv += t * Math.Sqrt(w.Vx[h] * w.Vx[h] + w.Vy[h] * w.Vy[h]); l.Sum_mrv += Math.Abs(da);
            for (int k = 0; k < d.Cells; k++) { double cm = 0; for (int e = 0; e < ne; e++) { double x = w.DiscCellMatter(d, k, e); l.Matter[e] += x; cm += x; } if (cm > 0) l.Cells++; }
        }
        return l;
    }

    static string LedHeader(World w) =>
        "dMass_rel,dPx_over_sum_mv,dPy_over_sum_mv,dL_over_sum_abs_mrv,dCoM_x,dCoM_y,max_elem_resid_rel,elem_worst," + string.Join(",", Enumerable.Range(0, w.ElementCount).Select(e => $"elem_{w.Elements[e].Name}_resid_rel"));

    static string LedDiff(World w, Led a, Led b, out double maxElem, out string worst)
    {
        int ne = w.ElementCount; maxElem = 0; worst = "-";
        var cells = new string[ne];
        for (int e = 0; e < ne; e++)
        {
            double d = b.Matter[e] - a.Matter[e], rel = a.Matter[e] != 0 ? d / Math.Abs(a.Matter[e]) : d;
            cells[e] = G(rel); if (Math.Abs(rel) > Math.Abs(maxElem)) { maxElem = rel; worst = w.Elements[e].Name; }
        }
        double mass = b.Mass - a.Mass;
        double cmx = b.Mx / b.Mass - a.Mx / a.Mass, cmy = b.My / b.Mass - a.My / a.Mass;
        return string.Join(",", new[] { G(mass / a.Mass), G((b.Px - a.Px) / a.Sum_mv), G((b.Py - a.Py) / a.Sum_mv), G((b.L - a.L) / a.Sum_mrv), G(cmx), G(cmy), G(maxElem), worst }.Concat(cells));
    }

    static World Folded(ulong seed, out int sun, out int folded1, out int folded2, out Led l0, out Led l1, out Led l2)
    {
        var w = World.SolSystem(5000, seed);
        if (Environment.GetEnvironmentVariable("BELT_THREADS") is string t) w.Threads = int.Parse(t, Inv);
        sun = Sun(w); l0 = Ledger(w);
        folded1 = w.FoldBelt(sun, 1, Keep); l1 = Ledger(w);
        folded2 = w.FoldBelt(sun, 2, Keep); l2 = Ledger(w);
        return w;
    }

    static int Fill(World w, int free)
    {
        // fill the world with inert far-away specks until `free` slots are left; they are not part of any belt (Grp 0)
        var mix = w.Mix(("rock", 1)); int added = 0;
        while (w.X.Length - w.Live > free)
        {
            int i = w.Add(1e7 + added, 1e7, 0, 0, 1e-12, mix);
            if (i < 0) break; added++;
        }
        return added;
    }

    sealed record Spot(string Name, double Dx, double Dy, double Rad, int Free, bool Abs = false);
    static Spot[] Spots() => new[]
    {
        new Spot("a_mid_asteroid_rad5", World.SolDist(2.7), 0, 5, -1),
        new Spot("b_whole_system_rad1000", 0, 0, 1000, -1),
        new Spot("c_outside_all_rings", 5000, 5000, 10, -1, true),
        new Spot("d1_on_sun_rad5", 0, 0, 5, -1),
        new Spot("d2_on_sun_rad100", 0, 0, 100, -1),
        new Spot("e1_nearly_full_5free_whole", 0, 0, 1000, 5),
        new Spot("e2_full_0free_whole", 0, 0, 1000, 0),
        new Spot("e3_nearly_full_5free_mid_asteroid", World.SolDist(2.7), 0, 5, 5),
    };

    public static int Run(string[] args)
    {
        string outDir = args[1]; Directory.CreateDirectory(outDir);
        var log = new StreamWriter(Path.Combine(outDir, "console.txt")) { AutoFlush = true };
        void P(string s) { Console.WriteLine(s); log.WriteLine(s); }
        P($"belt-probe: SolSystem(5000, seed), FoldBelt keep={Keep}, threads={Environment.GetEnvironmentVariable("BELT_THREADS") ?? "default"}, {Environment.ProcessorCount} cpu");
        Item1(outDir, P);
        Item2(outDir, P);
        Item3(outDir, P);
        Item4(outDir, P);
        log.Dispose();
        return 0;
    }

    // ---------------------------------------------------------------- item 1
    static void Item1(string outDir, Action<string> P)
    {
        using var csv = new StreamWriter(Path.Combine(outDir, "item1-fold-ledger.csv"));
        var w0 = World.SolSystem(5000, 1234);
        csv.WriteLine("seed,stage,returned,live,discs,disc_cells_nonempty,disc_mass,disc_heat,obj_mass,total_mass,Px,Py,L,CoM_x,CoM_y," + LedHeader(w0));
        foreach (ulong seed in Seeds)
        {
            var w = World.SolSystem(5000, seed);
            if (Environment.GetEnvironmentVariable("BELT_THREADS") is string t) w.Threads = int.Parse(t, Inv);
            int sun = Sun(w); var l0 = Ledger(w);
            int n1 = w.FoldBelt(sun, 1, Keep); var l1 = Ledger(w);
            int n2 = w.FoldBelt(sun, 2, Keep); var l2 = Ledger(w);
            int n3 = w.FoldBelt(sun, 1, Keep); int n4 = w.FoldBelt(sun, 2, Keep); var l3 = Ledger(w);
            void Row(string stage, int ret, Led l, Led prev)
            {
                string d = prev == null ? string.Join(",", Enumerable.Repeat("", 8 + w.ElementCount)) : LedDiff(w, prev, l, out _, out _);
                csv.WriteLine($"{seed},{stage},{ret},{l.Live},{l.Discs},{l.Cells},{R(l.DiscMass)},{R(l.Heat)},{R(l.ObjMass)},{R(l.Mass)},{R(l.Px)},{R(l.Py)},{R(l.L)},{R(l.Mx / l.Mass)},{R(l.My / l.Mass)},{d}");
            }
            Row("start", 0, l0, null); Row("after_fold_g1", n1, l1, l0); Row("after_fold_g2", n2, l2, l1); Row("repeat_g1_g2", n3 + n4, l3, l2);
            LedDiff(w, l0, l2, out double me, out string worst);
            P($"[1] seed {seed}: live {l0.Live}->{l2.Live} (g1 folded {n1}, g2 folded {n2}; repeat call folds {n3}+{n4}); discs {l2.Discs}, nonempty rings {l2.Cells}, disc mass {G(l2.DiscMass)}, heat {G(l2.Heat)}");
            double lPred = (l2.Mx - l0.Mx) * w.Vy[sun] - (l2.My - l0.My) * w.Vx[sun];
            P($"    dL predicted from CoM shift x host velocity: {G(lPred / l0.Sum_mrv)} of Σ|m r×v|; actual/predicted {(lPred != 0 ? G((l2.L - l0.L) / lPred) : "-")}");
            P($"    total (start->after both): dMass_rel {G((l2.Mass - l0.Mass) / l0.Mass)}, dPx/Σm|v| {G((l2.Px - l0.Px) / l0.Sum_mv)}, dPy/Σm|v| {G((l2.Py - l0.Py) / l0.Sum_mv)}, dL/Σ|m r×v| {G((l2.L - l0.L) / l0.Sum_mrv)}, dCoM ({G(l2.Mx / l2.Mass - l0.Mx / l0.Mass)}, {G(l2.My / l2.Mass - l0.My / l0.Mass)}), worst element {worst} {G(me)}");
        }
    }

    // ---------------------------------------------------------------- item 2
    sealed class Placement
    {
        public int Made, Attractors, SpinPositive, SpinNegative, Grp0, ParHost, NamedOrAlt;
        public double MaxRingErr, MaxHandOver, MaxSpeedErr, MaxRadial, MaxEcc, MinR = double.MaxValue, MaxR, MinMass = double.MaxValue, MaxMass, MaxCompErr, MaxSpeedErrWithDisc;
        public int RingsTouched;
        public double MadeMass;
    }

    static Placement Measure(World w, int host, bool[] preAlive, int[] preGen, double[] ringR, double cx, double cy, double rad)
    {
        var p = new Placement(); int ne = w.ElementCount; var rings = new HashSet<int>();
        double gm = w.C.G * w.M[host], gmd = w.C.G * (w.M[host] + w.DiscMassOn(host));
        for (int i = 0; i < w.N; i++)
        {
            if (!w.Alive[i] || (i < preAlive.Length && preAlive[i] && preGen[i] == w.Gen[i])) continue;
            p.Made++; double m = w.M[i]; p.MadeMass += m; if (w.Grp[i] == 0) p.Grp0++; if (w.Par[i] == host) p.ParHost++; if (w.Name[i] != null) p.NamedOrAlt++;
            p.MinMass = Math.Min(p.MinMass, m); p.MaxMass = Math.Max(p.MaxMass, m); if (w.Attracts(i)) p.Attractors++;
            double dx = w.X[i] - w.X[host], dy = w.Y[i] - w.Y[host], ux = w.Vx[i] - w.Vx[host], uy = w.Vy[i] - w.Vy[host], r = Math.Sqrt(dx * dx + dy * dy);
            double spin = dx * uy - dy * ux, speed = Math.Sqrt(ux * ux + uy * uy), rad_v = (dx * ux + dy * uy) / r;
            if (spin > 0) p.SpinPositive++; else p.SpinNegative++;
            p.MinR = Math.Min(p.MinR, r); p.MaxR = Math.Max(p.MaxR, r);
            double best = double.MaxValue; int bk = -1;
            for (int k = 0; k < ringR.Length; k++) { double e = Math.Abs(r - ringR[k]) / ringR[k]; if (e < best) { best = e; bk = k; } }
            p.MaxRingErr = Math.Max(p.MaxRingErr, best); rings.Add(bk);
            p.MaxHandOver = Math.Max(p.MaxHandOver, Math.Sqrt((w.X[i] - cx) * (w.X[i] - cx) + (w.Y[i] - cy) * (w.Y[i] - cy)) / rad);
            p.MaxSpeedErr = Math.Max(p.MaxSpeedErr, Math.Abs(speed / Math.Sqrt(gm / r) - 1));
            p.MaxSpeedErrWithDisc = Math.Max(p.MaxSpeedErrWithDisc, Math.Abs(speed / Math.Sqrt(gmd / r) - 1));
            p.MaxRadial = Math.Max(p.MaxRadial, Math.Abs(rad_v) / speed);
            double energy = speed * speed / 2 - gm / r, h = Math.Abs(spin), ecc2 = 1 + 2 * energy * h * h / (gm * gm);
            p.MaxEcc = Math.Max(p.MaxEcc, Math.Sqrt(Math.Max(0, ecc2)));
            double comp = 0; for (int e = 0; e < ne; e++) comp += w.Comp[i * ne + e];
            p.MaxCompErr = Math.Max(p.MaxCompErr, Math.Abs(comp - m) / m);
        }
        p.RingsTouched = rings.Count;
        return p;
    }

    static double[] RingRadii(World w)
    {
        var l = new List<double>();
        foreach (var d in w.Discs) if (d.Cold && d.Host >= 0) for (int k = 0; k < d.Cells; k++) if (w.DiscCellMass(d, k) > 0) l.Add(w.DiscCellRadius(d, k));
        return l.ToArray();
    }

    static void Item2(string outDir, Action<string> P)
    {
        using var csv = new StreamWriter(Path.Combine(outDir, "item2-withdraw-cases.csv"));
        var w0 = World.SolSystem(5000, 1234);
        csv.WriteLine("seed,case,cx,cy,rad,free_slots_before,returned,made_by_diff,live_before,live_after,rings_nonempty_before,rings_touched_by_new,disc_mass_before,disc_mass_after,made_mass,made_mass_over_disc_before,obj_mass_min,obj_mass_max,attractors_made(M>=AttractMass),spin_positive,spin_negative," +
            "new_with_Grp0,new_with_Par_eq_host,dL_predicted_from_CoM_shift_over_sum,dL_actual_over_pred,max_ring_radius_relerr,max_dist_over_hand_rad,max_speed_over_circ_minus1(G*M_host),max_speed_over_circ_minus1(G*(M_host+disc)),max_radial_over_speed,max_ecc,r_min,r_max,max_comp_vs_M_relerr," + LedHeader(w0) + ",heat_before,heat_after,hash_unchanged");
        foreach (ulong seed in Seeds)
            foreach (var s in Spots())
            {
                var w = Folded(seed, out int sun, out _, out _, out _, out _, out _);
                int filled = s.Free >= 0 ? Fill(w, s.Free) : 0;
                double cx = s.Abs ? s.Dx : w.X[sun] + s.Dx, cy = s.Abs ? s.Dy : w.Y[sun] + s.Dy;
                RunWithdraw(w, sun, seed, s.Name, cx, cy, s.Rad, csv, P, filled);
            }
        // bad arguments: nothing may change
        foreach (ulong seed in Seeds)
        {
            var w = Folded(seed, out int sun, out _, out _, out _, out _, out _);
            var bad = new (string, double, double, double)[] { ("g1_rad_0", 0, 0, 0), ("g2_rad_negative", 0, 0, -5), ("g3_rad_NaN", 0, 0, double.NaN), ("g4_cx_Inf", double.PositiveInfinity, 0, 10), ("g5_rad_Inf", 0, 0, double.PositiveInfinity) };
            foreach (var (name, x, y, rd) in bad)
            {
                var before = Ledger(w); ulong h0 = w.Hash(); int live0 = w.Live;
                int ret; string err = "";
                try { ret = w.WithdrawDiscs(x, y, rd); } catch (Exception ex) { ret = -999; err = ex.GetType().Name; }
                P($"[2] seed {seed} {name}: returned {ret}{err}, live {live0}->{w.Live}, hash unchanged {w.Hash() == h0}");
            }
        }
    }

    static void RunWithdraw(World w, int sun, ulong seed, string name, double cx, double cy, double rad, StreamWriter csv, Action<string> P, int filled)
    {
        var before = Ledger(w); var ringR = RingRadii(w); ulong h0 = w.Hash();
        var preAlive = new bool[w.X.Length]; var preGen = new int[w.X.Length];
        for (int i = 0; i < w.N; i++) { preAlive[i] = w.Alive[i]; preGen[i] = w.Gen[i]; }
        int free0 = w.X.Length - w.Live, live0 = w.Live;
        int ret = w.WithdrawDiscs(cx, cy, rad);
        var after = Ledger(w); var pl = Measure(w, sun, preAlive, preGen, ringR, cx, cy, rad);
        string diff = LedDiff(w, before, after, out double me, out string worst);
        double hb = before.Heat, ha = after.Heat;
        double lPred = (after.Mx - before.Mx) * w.Vy[sun] - (after.My - before.My) * w.Vx[sun];
        csv.WriteLine($"{seed},{name},{G(cx)},{G(cy)},{G(rad)},{free0},{ret},{pl.Made},{live0},{w.Live},{ringR.Length},{pl.RingsTouched},{R(before.DiscMass)},{R(after.DiscMass)},{R(pl.MadeMass)},{G(pl.MadeMass / before.DiscMass)}," +
            $"{(pl.Made > 0 ? G(pl.MinMass) : "")},{(pl.Made > 0 ? G(pl.MaxMass) : "")},{pl.Attractors},{pl.SpinPositive},{pl.SpinNegative}," +
            $"{pl.Grp0},{pl.ParHost},{G(lPred / before.Sum_mrv)},{(lPred != 0 ? G((after.L - before.L) / lPred) : "")}," +
            $"{(pl.Made > 0 ? G(pl.MaxRingErr) : "")},{(pl.Made > 0 ? G(pl.MaxHandOver) : "")},{(pl.Made > 0 ? G(pl.MaxSpeedErr) : "")},{(pl.Made > 0 ? G(pl.MaxSpeedErrWithDisc) : "")},{(pl.Made > 0 ? G(pl.MaxRadial) : "")},{(pl.Made > 0 ? G(pl.MaxEcc) : "")},{(pl.Made > 0 ? G(pl.MinR) : "")},{(pl.Made > 0 ? G(pl.MaxR) : "")},{(pl.Made > 0 ? G(pl.MaxCompErr) : "")}," +
            $"{diff},{R(hb)},{R(ha)},{w.Hash() == h0}");
        P($"[2] seed {seed} {name}: free {free0}, returned {ret} (diff {pl.Made}), rings touched {pl.RingsTouched}/{ringR.Length}, made mass {G(pl.MadeMass)} = {G(pl.MadeMass / before.DiscMass)} of disc; obj mass {(pl.Made > 0 ? G(pl.MinMass) + ".." + G(pl.MaxMass) : "-")}, attractors {pl.Attractors}; resid mass {G((after.Mass - before.Mass) / before.Mass)}, P {G((after.Px - before.Px) / before.Sum_mv)},{G((after.Py - before.Py) / before.Sum_mv)}, L {G((after.L - before.L) / before.Sum_mrv)}, elem worst {worst} {G(me)}; ring relerr {G(pl.MaxRingErr)}, spin+/- {pl.SpinPositive}/{pl.SpinNegative}, Grp0 {pl.Grp0}/{pl.Made}, Par=host {pl.ParHost}/{pl.Made}, dL pred/Σ {G(lPred / before.Sum_mrv)} actual/pred {(lPred != 0 ? G((after.L - before.L) / lPred) : "-")}, speed/circ-1 {G(pl.MaxSpeedErr)}, radial/speed {G(pl.MaxRadial)}, ecc {G(pl.MaxEcc)}, hand {G(pl.MaxHandOver)}");
    }

    // ---------------------------------------------------------------- item 3
    static void Item3(string outDir, Action<string> P)
    {
        using var csv = new StreamWriter(Path.Combine(outDir, "item3-repeat100.csv"));
        csv.WriteLine("seed,spot,call,returned,live,disc_mass,disc_mass_over_start,nonempty_rings,discs,made_mass_total_over_start,obj_mass_min_this_call,obj_mass_max_this_call,mass_resid_rel_total,L_resid_total_over_sum");
        foreach (ulong seed in Seeds)
            foreach (var s in Spots().Take(2))
            {
                var w = Folded(seed, out int sun, out _, out _, out _, out _, out _);
                double cx = w.X[sun] + s.Dx, cy = w.Y[sun] + s.Dy;
                var start = Ledger(w); double start0 = start.DiscMass; int zeroCalls = -1; int lastMade = -1;
                for (int call = 1; call <= 100; call++)
                {
                    var preAlive = new bool[w.X.Length]; var preGen = new int[w.X.Length];
                    for (int i = 0; i < w.N; i++) { preAlive[i] = w.Alive[i]; preGen[i] = w.Gen[i]; }
                    int ret = w.WithdrawDiscs(cx, cy, s.Rad);
                    var pl = Measure(w, sun, preAlive, preGen, new[] { 1.0 }, cx, cy, s.Rad);
                    var now = Ledger(w);
                    csv.WriteLine($"{seed},{s.Name},{call},{ret},{w.Live},{R(now.DiscMass)},{G(now.DiscMass / start0)},{now.Cells},{now.Discs},{G((start0 - now.DiscMass) / start0)},{(ret > 0 ? G(pl.MinMass) : "")},{(ret > 0 ? G(pl.MaxMass) : "")},{G((now.Mass - start.Mass) / start.Mass)},{G((now.L - start.L) / start.Sum_mrv)}");
                    if (ret == 0 && zeroCalls < 0) zeroCalls = call;
                    lastMade = ret;
                    if (call == 1 || call == 2 || call == 3 || call == 10 || call == 50 || call == 100)
                        P($"[3] seed {seed} {s.Name} call {call}: made {ret}, live {w.Live}/{w.X.Length}, disc mass left {G(now.DiscMass / start0)} of start, rings {now.Cells}, discs {now.Discs}, mass resid {G((now.Mass - start.Mass) / start.Mass)}");
                }
                P($"[3] seed {seed} {s.Name}: first call that made nothing: {(zeroCalls < 0 ? "none in 100" : zeroCalls.ToString())}; last call made {lastMade}");
            }
    }

    // ---------------------------------------------------------------- item 4
    static void Item4(string outDir, Action<string> P)
    {
        using var fate = new StreamWriter(Path.Combine(outDir, "item4-fate.csv"));
        using var samples = new StreamWriter(Path.Combine(outDir, "item4-samples.csv"));
        fate.WriteLine("seed,run,tracked,alive_end,bound_end,unbound_end,gone_roche,gone_merge_or_other,gone_escaped_transfer,max_ecc_seen,max_r_seen,min_r_seen,roche_new_all,roche_new_tracked,merges_new,escaped_mass_new,live_start,live_end,ms_total,years_simulated");
        samples.WriteLine("seed,run,step,tracked_alive,tracked_unbound,tracked_max_ecc,tracked_min_r,tracked_max_r,live");
        foreach (ulong seed in Seeds)
        {
            // baseline: the unfolded belts' rocks, same scene, same steps
            var wb = World.SolSystem(5000, seed);
            if (Environment.GetEnvironmentVariable("BELT_THREADS") is string t) wb.Threads = int.Parse(t, Inv);
            int sb = Sun(wb); var tr = new List<(int, int)>();
            for (int i = 0; i < wb.N; i++) if (wb.Alive[i] && (wb.Grp[i] == 1 || wb.Grp[i] == 2) && wb.Name[i] == null) tr.Add((i, wb.Gen[i]));
            Track(wb, sb, tr, seed, "baseline_unfolded_belt_rocks", fate, samples, P);

            foreach (var s in Spots().Take(2))
            {
                var w = Folded(seed, out int sun, out _, out _, out _, out _, out _);
                double cx = w.X[sun] + s.Dx, cy = w.Y[sun] + s.Dy;
                var preAlive = new bool[w.X.Length]; var preGen = new int[w.X.Length];
                for (int i = 0; i < w.N; i++) { preAlive[i] = w.Alive[i]; preGen[i] = w.Gen[i]; }
                w.WithdrawDiscs(cx, cy, s.Rad);
                var trk = new List<(int, int)>();
                for (int i = 0; i < w.N; i++) if (w.Alive[i] && !(i < preAlive.Length && preAlive[i] && preGen[i] == w.Gen[i])) trk.Add((i, w.Gen[i]));
                Track(w, sun, trk, seed, "withdraw_" + s.Name, fate, samples, P);
            }
            // control: fold only, 2000 steps, the 40+40 kept objects (the rocks the fold did not touch)
            var wc = Folded(seed, out int sc, out _, out _, out _, out _, out _);
            var tc = new List<(int, int)>();
            for (int i = 0; i < wc.N; i++) if (wc.Alive[i] && (wc.Grp[i] == 1 || wc.Grp[i] == 2) && wc.Name[i] == null) tc.Add((i, wc.Gen[i]));
            Track(wc, sc, tc, seed, "control_folded_kept40x2_no_withdraw", fate, samples, P);
        }
    }

    static void Track(World w, int sun, List<(int Slot, int Gen)> trk, ulong seed, string run, StreamWriter fate, StreamWriter samples, Action<string> P)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        int cap = w.X.Length; int liveStart = w.Live; double year0 = w.Year;
        var status = new int[trk.Count]; // 0 alive, 1 roche, 2 merge/other, 3 escaped transfer
        double maxEcc = 0, maxR = 0, minR = double.MaxValue;
        int rocheAll0 = w.RocheDisruptions.Count, esc0 = w.EscapedTransfers.Count; long merges0 = w.Merges; double escMass0 = w.EscapedMass;
        int rocheTracked = 0; int rocheSeen = rocheAll0, escSeen = esc0;
        var idx = new Dictionary<long, int>(); for (int n = 0; n < trk.Count; n++) idx[((long)trk[n].Slot << 32) | (uint)trk[n].Gen] = n;
        void Sample(int step, bool write)
        {
            int alive = 0, unb = 0; double me = 0, mn = double.MaxValue, mx = 0;
            double gm = w.C.G * (w.M[sun] + w.DiscMassOn(sun));
            for (int n = 0; n < trk.Count; n++)
            {
                var (i, g) = trk[n];
                if (status[n] != 0) continue;
                if (!(w.Alive[i] && w.Gen[i] == g)) continue;
                alive++;
                double dx = w.X[i] - w.X[sun], dy = w.Y[i] - w.Y[sun], ux = w.Vx[i] - w.Vx[sun], uy = w.Vy[i] - w.Vy[sun], r = Math.Sqrt(dx * dx + dy * dy);
                double e = (ux * ux + uy * uy) / 2 - gm / r, h = dx * uy - dy * ux;
                if (e >= 0) { unb++; } else me = Math.Max(me, Math.Sqrt(Math.Max(0, 1 + 2 * e * h * h / (gm * gm))));
                mn = Math.Min(mn, r); mx = Math.Max(mx, r);
            }
            maxEcc = Math.Max(maxEcc, me); if (alive > 0) { maxR = Math.Max(maxR, mx); minR = Math.Min(minR, mn); }
            if (write) samples.WriteLine($"{seed},{run},{step},{alive},{unb},{G(me)},{(alive > 0 ? G(mn) : "")},{(alive > 0 ? G(mx) : "")},{w.Live}");
        }
        Sample(0, true);
        for (int step = 1; step <= 2000; step++)
        {
            w.Advance(.5);
            // deaths this step
            for (int n = 0; n < trk.Count; n++)
            {
                if (status[n] != 0) continue;
                var (i, g) = trk[n];
                if (w.Alive[i] && w.Gen[i] == g) continue;
                status[n] = 2;
            }
            var rd = w.RocheDisruptions; for (; rocheSeen < rd.Count; rocheSeen++)
            {
                var r = rd[rocheSeen];
                if (idx.TryGetValue(((long)r.Source << 32) | (uint)r.SourceGeneration, out int n)) { rocheTracked++; if (status[n] != 1) status[n] = 1; }
            }
            var et = w.EscapedTransfers; for (; escSeen < et.Count; escSeen++)
            {
                var x = et[escSeen];
                if (idx.TryGetValue(((long)x.Source << 32) | (uint)x.Generation, out int n) && status[n] != 1) status[n] = 3;
            }
            bool write = step <= 10 || (step <= 300 && step % 10 == 0) || step % 50 == 0;
            Sample(step, write);
        }
        int alive2 = 0, bound = 0, unb2 = 0, roche = 0, merge = 0, esc = 0;
        double gm2 = w.C.G * (w.M[sun] + w.DiscMassOn(sun));
        for (int n = 0; n < trk.Count; n++)
        {
            var (i, g) = trk[n];
            if (status[n] == 1) roche++; else if (status[n] == 3) esc++; else if (status[n] == 2) merge++;
            else if (w.Alive[i] && w.Gen[i] == g)
            {
                alive2++;
                double dx = w.X[i] - w.X[sun], dy = w.Y[i] - w.Y[sun], ux = w.Vx[i] - w.Vx[sun], uy = w.Vy[i] - w.Vy[sun], r = Math.Sqrt(dx * dx + dy * dy);
                if ((ux * ux + uy * uy) / 2 - gm2 / r >= 0) unb2++; else bound++;
            }
        }
        sw.Stop();
        fate.WriteLine($"{seed},{run},{trk.Count},{alive2},{bound},{unb2},{roche},{merge},{esc},{G(maxEcc)},{G(maxR)},{G(minR == double.MaxValue ? 0 : minR)},{w.RocheDisruptions.Count - rocheAll0},{rocheTracked},{w.Merges - merges0},{G(w.EscapedMass - escMass0)},{liveStart},{w.Live},{sw.ElapsedMilliseconds},{G(w.Year - year0)}");
        P($"[4] seed {seed} {run}: tracked {trk.Count}; after 2000 steps alive {alive2} (bound {bound}, unbound {unb2}), gone: roche {roche}, merge/other {merge}, escaped-transfer {esc}; max ecc seen {G(maxEcc)}; r {G(minR == double.MaxValue ? 0 : minR)}..{G(maxR)}; roche records new {w.RocheDisruptions.Count - rocheAll0} (tracked {rocheTracked}), world merges new {w.Merges - merges0}; {sw.ElapsedMilliseconds} ms (1 thread), {G(w.Year - year0)} yr simulated");
    }
}
