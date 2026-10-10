using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Cosmos.Core;

// Vong 4 (chi Claire giao, Cosmos issue #1): chay DAI loi dia boi tu, tim cho no vo.
// Cau hoi cho Yang: de yen lau thi luat dia co giu duoc so sach va on dinh so vat the
// khong, hay troi/phinh/no o dau do?
// Harness cli-only. Khong doi core / vat ly.
// Rut kinh nghiem vong 3: dem event/counter truc tiep theo chi so, KHONG loc theo
// e.Year; dem buoc tu luc tha NS (khong gop 96 warmup); so trong REPORT sinh tu CSV.
static class DiscLong
{
    const int Rocks = 5000;
    const int Warmup = 96;
    const int StepsA = 50000;
    const int SampleEvery = 100;
    static readonly ulong[] SeedsA = { 1234, 1002, 1011, 2001, 2002 };
    static readonly ulong[] SeedsB = { 1234, 1002 };
    static readonly double[] Jumps = { 1e2, 1e4, 1e6, 1e8 };

    static int DropNS(World w)
    {
        int ns = w.Do(new Command(CmdKind.Create, X: 100, Amount: 466700 * World.EarthMass, Mix: w.Mix(("gas", 1))));
        double birth = 10 * w.C.StarSolarMass, end = 1 + w.C.StarGiantFraction;
        if (w.Do(new Command(CmdKind.SetStarState, Target: ns, StarState: new StellarState(w.StarLifetime(birth) * end, end, birth))) < 0)
            throw new InvalidOperationException("NS fixture refused its stellar state.");
        return ns;
    }

    sealed class Ledgers
    {
        public double Mass0, Px0, Py0, Lall0;
        public double[] El0 = Array.Empty<double>();
    }

    // So sach tai mot thoi diem. Tra ve tuple de ghi CSV.
    static void Sample(World w, Ledgers L, StreamWriter csv, StreamWriter flags, string runId,
        int stepDrop, double msAvg, double msBase, ref int lastDiscForm, ref double lastDiscTotal,
        ref bool msFlagged)
    {
        var iv = CultureInfo.InvariantCulture;
        int nEl = w.ElementCount;

        // --- khoi tam & dong luong ---
        double mSum = 0, px = w.EscapedPx, py = w.EscapedPy, xc = 0, yc = 0, vxc = 0, vyc = 0;
        for (int i = 0; i < w.N; i++) if (w.Alive[i]) { mSum += w.M[i]; px += w.M[i] * w.Vx[i]; py += w.M[i] * w.Vy[i]; }
        for (int i = 0; i < w.N; i++) if (w.Alive[i]) { xc += w.M[i] * w.X[i]; yc += w.M[i] * w.Y[i]; vxc += w.M[i] * w.Vx[i]; vyc += w.M[i] * w.Vy[i]; }
        if (mSum > 0) { xc /= mSum; yc /= mSum; vxc /= mSum; vyc /= mSum; }

        // dong luong khoi cua dia: di theo sao chu
        foreach (var d in w.Discs)
        {
            if (d.Host >= 0 && w.Alive[d.Host]) { px += d.Total * w.Vx[d.Host]; py += d.Total * w.Vy[d.Host]; }
        }

        // mo-men dong luong quanh khoi tam: vat song + dia (quanh host + van chuyen) 
        double lLive = 0;
        for (int i = 0; i < w.N; i++) if (w.Alive[i])
                lLive += w.M[i] * ((w.X[i] - xc) * (w.Vy[i] - vyc) - (w.Y[i] - yc) * (w.Vx[i] - vxc));
        double lDiscHost = 0, lTrans = 0, discMass = 0;
        foreach (var d in w.Discs)
        {
            lDiscHost += w.DiscAngular(d);
            discMass += d.Total;
            if (d.Host >= 0 && w.Alive[d.Host])
                lTrans += d.Total * ((w.X[d.Host] - xc) * (w.Vy[d.Host] - vyc) - (w.Y[d.Host] - yc) * (w.Vx[d.Host] - vxc));
        }
        double lOrbital = lLive + lDiscHost + lTrans;
        double lAll = lOrbital + w.DiscAccretedAngular;

        // khoi luong: vat song + dia + reservoir + escaped
        double mass = w.EscapedMass + discMass;
        for (int i = 0; i < w.N; i++) if (w.Alive[i]) mass += w.M[i];
        double resMass = 0;
        foreach (var r in w.RocheReservoirs) resMass += r.Mass;
        mass += resMass;

        // tung nguyen to
        var el = new double[nEl];
        for (int e = 0; e < nEl; e++) el[e] = w.EscapedMatter[e] - w.NucleosynthesisDelta[e];
        for (int i = 0; i < w.N; i++) if (w.Alive[i])
                for (int e = 0; e < nEl; e++) el[e] += w.Comp[i * nEl + e];
        foreach (var r in w.RocheReservoirs)
            for (int e = 0; e < nEl; e++) el[e] += r.Matter[e];
        foreach (var d in w.Discs)
            for (int k = 0; k < d.Cells; k++)
                for (int e = 0; e < nEl; e++) el[e] += w.DiscCellMatter(d, k, e);

        // breakup theo Formation (dem truc tiep theo chi so, khong loc Year)
        int fDisc = 0, fRing = 0, fStream = 0;
        foreach (var d in w.RocheDisruptions)
        {
            if (d.Formation == "disc") fDisc++;
            else if (d.Formation == "ring") fRing++;
            else fStream++;
        }

        int heavy = w.Heaviest();
        var cols = new List<string>
        {
            runId, stepDrop.ToString(), w.Step.ToString(), w.Year.ToString("F3", iv),
            w.Live.ToString(), w.N.ToString(), w.Merges.ToString(),
            w.RocheDisruptions.Count.ToString(), fDisc.ToString(), fRing.ToString(), fStream.ToString(),
            w.RocheReservoirs.Count.ToString(), resMass.ToString("E6", iv),
            w.Discs.Count.ToString(), discMass.ToString("E6", iv),
            w.DiscAccretedMass.ToString("E6", iv), w.DiscAccretedAngular.ToString("E6", iv),
            w.DiscResidualAngular.ToString("E6", iv),
            heavy.ToString(), w.StarPhaseOf(heavy).ToString(), w.M[heavy].ToString("E6", iv),
            Rel(mass, L.Mass0).ToString("E3", iv),
        };
        for (int e = 0; e < nEl; e++) cols.Add(Rel(el[e], L.El0[e]).ToString("E3", iv));
        cols.Add(Rel(px, L.Px0).ToString("E3", iv));
        cols.Add(Rel(py, L.Py0).ToString("E3", iv));
        cols.Add(lOrbital.ToString("E6", iv));
        cols.Add(Rel(lAll, L.Lall0).ToString("E3", iv));
        cols.Add(msAvg.ToString("F2", iv));

        // --- co do ---
        var red = new List<string>();
        foreach (var s in cols) if (s == "NaN" || s == "Infinity" || s == "-Infinity") red.Add("NaN/Inf trong so do");
        for (int i = 0; i < w.N; i++) if (w.Alive[i] && w.M[i] < 0) { red.Add($"khoi luong am o slot {i}"); break; }
        foreach (var d in w.Discs) if (d.Total < 0) { red.Add("disc Total am"); break; }
        foreach (var r in w.RocheReservoirs) if (r.Mass < 0) { red.Add("reservoir Mass am"); break; }
        if (discMass > lastDiscTotal && fDisc == lastDiscForm) red.Add($"disc Total tang {lastDiscTotal:E3}->{discMass:E3} ma khong co breakup moi");
        if (w.Live == w.X.Length) red.Add("Live cham tran 5512");
        if (!msFlagged && msBase > 0 && msAvg > 2 * msBase) { red.Add($"ms/Advance {msAvg:F1} > 2x baseline {msBase:F1}"); msFlagged = true; }
        if (Math.Abs(Rel(mass, L.Mass0)) > 1e-9) red.Add($"residual khoi luong {Rel(mass, L.Mass0):E2} > 1e-9");
        lastDiscForm = fDisc; lastDiscTotal = discMass;

        cols.Add(red.Count == 0 ? "" : string.Join(" | ", red));
        csv.WriteLine(string.Join(",", cols));
        foreach (var r in red)
        {
            string line = $"REDFLAG {runId} stepDrop={stepDrop} wstep={w.Step}: {r}";
            Console.WriteLine(line);
            flags.WriteLine(line);
        }
        csv.Flush(); flags.Flush();
    }

    static double Rel(double v, double v0)
    {
        if (v0 == 0) return v == 0 ? 0 : double.PositiveInfinity;
        return (v - v0) / Math.Abs(v0);
    }

    static Ledgers TakeBaseline(World w)
    {
        var L = new Ledgers();
        int nEl = w.ElementCount;
        L.El0 = new double[nEl];
        double mSum = 0, xc = 0, yc = 0, vxc = 0, vyc = 0;
        L.Px0 = w.EscapedPx; L.Py0 = w.EscapedPy;
        for (int i = 0; i < w.N; i++) if (w.Alive[i]) { mSum += w.M[i]; L.Px0 += w.M[i] * w.Vx[i]; L.Py0 += w.M[i] * w.Vy[i]; }
        for (int i = 0; i < w.N; i++) if (w.Alive[i]) { xc += w.M[i] * w.X[i]; yc += w.M[i] * w.Y[i]; vxc += w.M[i] * w.Vx[i]; vyc += w.M[i] * w.Vy[i]; }
        if (mSum > 0) { xc /= mSum; yc /= mSum; vxc /= mSum; vyc /= mSum; }
        foreach (var d in w.Discs)
            if (d.Host >= 0 && w.Alive[d.Host]) { L.Px0 += d.Total * w.Vx[d.Host]; L.Py0 += d.Total * w.Vy[d.Host]; }

        double lLive = 0;
        for (int i = 0; i < w.N; i++) if (w.Alive[i])
                lLive += w.M[i] * ((w.X[i] - xc) * (w.Vy[i] - vyc) - (w.Y[i] - yc) * (w.Vx[i] - vxc));
        double lDiscHost = 0, lTrans = 0, discMass = 0;
        foreach (var d in w.Discs)
        {
            lDiscHost += w.DiscAngular(d); discMass += d.Total;
            if (d.Host >= 0 && w.Alive[d.Host])
                lTrans += d.Total * ((w.X[d.Host] - xc) * (w.Vy[d.Host] - vyc) - (w.Y[d.Host] - yc) * (w.Vx[d.Host] - vxc));
        }
        L.Lall0 = lLive + lDiscHost + lTrans + w.DiscAccretedAngular;

        L.Mass0 = w.EscapedMass + discMass;
        for (int i = 0; i < w.N; i++) if (w.Alive[i]) L.Mass0 += w.M[i];
        foreach (var r in w.RocheReservoirs) L.Mass0 += r.Mass;
        for (int e = 0; e < nEl; e++) L.El0[e] = w.EscapedMatter[e] - w.NucleosynthesisDelta[e];
        for (int i = 0; i < w.N; i++) if (w.Alive[i])
                for (int e = 0; e < nEl; e++) L.El0[e] += w.Comp[i * nEl + e];
        foreach (var r in w.RocheReservoirs)
            for (int e = 0; e < nEl; e++) L.El0[e] += r.Matter[e];
        foreach (var d in w.Discs)
            for (int k = 0; k < d.Cells; k++)
                for (int e = 0; e < nEl; e++) L.El0[e] += w.DiscCellMatter(d, k, e);
        return L;
    }

    static string Header(int nEl)
    {
        var cols = new List<string> { "run", "step_drop", "wstep", "year", "live", "n", "merges",
            "disr_total", "disr_disc", "disr_ring", "disr_stream", "reservoirs", "reservoir_mass",
            "discs", "disc_mass", "disc_accreted_mass", "disc_accreted_angular", "disc_resid_angular",
            "heavy_slot", "heavy_phase", "heavy_mass", "mass_resid_rel" };
        for (int e = 0; e < nEl; e++) cols.Add($"el{e}_resid_rel");
        cols.Add("px_resid_rel"); cols.Add("py_resid_rel"); cols.Add("L_orbital"); cols.Add("L_all_resid_rel");
        cols.Add("ms_per_adv"); cols.Add("redflags");
        return string.Join(",", cols);
    }

    // Chay mot canh: tra ve (hashCuoi, coLoi). Ghi CSV rieng cho run.
    static (ulong hash, bool ok) RunScene(string outDir, string runId, ulong seed, double discOn,
        int steps, int sampleEvery, int jumpAfter = -1, double jumpYears = 0, int postJumpSteps = 0)
    {
        string csvPath = Path.Combine(outDir, $"disc-long-{runId}.csv");
        using var csv = new StreamWriter(csvPath);
        using var flags = new StreamWriter(Path.Combine(outDir, "redflags.log"), append: true);
        var w = World.SolSystem(Rocks, seed);
        w.Threads = 1;
        w.C.DiscOn = discOn;
        for (int k = 0; k < Warmup; k++) w.Advance(.5);
        DropNS(w);
        var L = TakeBaseline(w);
        csv.WriteLine(Header(w.ElementCount));

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var winSw = System.Diagnostics.Stopwatch.StartNew();
        double winMs = 0; int winCount = 0, winSize = 100;
        double msAvg = 0, msBaseAcc = 0; int msBaseN = 0; double msBase = 0;
        int lastDiscForm = 0; double lastDiscTotal = 0; bool msFlagged = false;
        bool ok = true;
        int stepDrop = 0;

        void OneAdvance()
        {
            winSw.Restart();
            w.Advance(.5);
            winSw.Stop();
            double ms = winSw.Elapsed.TotalMilliseconds;
            winMs += ms; winCount++;
            if (msBaseN < 1000) { msBaseAcc += ms; msBaseN++; if (msBaseN == 1000) msBase = msBaseAcc / 1000; }
            if (winCount >= winSize) { msAvg = winMs / winCount; winMs = 0; winCount = 0; }
            stepDrop++;
        }

        try
        {
            Sample(w, L, csv, flags, runId, 0, 0, 0, ref lastDiscForm, ref lastDiscTotal, ref msFlagged);
            for (int s = 1; s <= steps; s++)
            {
                OneAdvance();
                if (jumpAfter > 0 && s == jumpAfter)
                {
                    Sample(w, L, csv, flags, runId + "-prejump", stepDrop, msAvg, msBase, ref lastDiscForm, ref lastDiscTotal, ref msFlagged);
                    w.Do(new Command(CmdKind.FastForward, Amount: jumpYears));
                    Sample(w, L, csv, flags, runId + "-postjump", stepDrop, msAvg, msBase, ref lastDiscForm, ref lastDiscTotal, ref msFlagged);
                    for (int p = 1; p <= postJumpSteps; p++)
                    {
                        OneAdvance();
                        if (p % 50 == 0) Sample(w, L, csv, flags, runId + "-settle", stepDrop, msAvg, msBase, ref lastDiscForm, ref lastDiscTotal, ref msFlagged);
                    }
                    break;
                }
                if (s % sampleEvery == 0)
                    Sample(w, L, csv, flags, runId, stepDrop, msAvg, msBase, ref lastDiscForm, ref lastDiscTotal, ref msFlagged);
            }
            Sample(w, L, csv, flags, runId, stepDrop, msAvg, msBase, ref lastDiscForm, ref lastDiscTotal, ref msFlagged);
        }
        catch (Exception ex)
        {
            string line = $"REDFLAG {runId} stepDrop={stepDrop} wstep={w.Step}: EXCEPTION {ex.GetType().Name}: {ex.Message}";
            Console.WriteLine(line); flags.WriteLine(line); ok = false;
        }
        sw.Stop();
        ulong hash = 0;
        try { hash = w.Hash(); } catch { }
        Console.WriteLine($"disc-long: {runId} xong — steps={stepDrop}, live={w.Live}, discs={w.Discs.Count}, " +
            $"swallowed={w.DiscAccretedMass / World.EarthMass:F1} Earths, hash={hash:X16}, wall={sw.Elapsed.TotalSeconds:F0}s");
        return (hash, ok);
    }

    public static bool Run(string outDir)
    {
        bool ok = true;
        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, "redflags.log"), "");
        var summary = new List<string>
            { "run,seed,disc_on,steps,jump_years,final_live,final_discs,disc_accreted_earths,final_hash,wall_note,ok" };

        // Determinism: seed 1234, 2 lan 5000 buoc
        ulong h1 = 0, h2 = 0;
        {
            var r1 = RunScene(outDir, "det-1234a", 1234, 1.0, 5000, 1000);
            var r2 = RunScene(outDir, "det-1234b", 1234, 1.0, 5000, 1000);
            h1 = r1.hash; h2 = r2.hash; ok &= r1.ok && r2.ok;
            Console.WriteLine(h1 == h2
                ? $"OK     disc-long: determinism seed1234 2x5000 buoc khop hash {h1:X16}"
                : $"FAILED disc-long: determinism LECH {h1:X16} vs {h2:X16}");
            if (h1 != h2) ok = false;
            summary.Add($"det-1234a,1234,1,5000,,{""},{""},{""},{h1:X16},,{(r1.ok ? 1 : 0)}");
            summary.Add($"det-1234b,1234,1,5000,,{""},{""},{""},{h2:X16},,{(r2.ok ? 1 : 0)}");
        }

        // Canh C: doi chung DiscOn=0, 5000 buoc
        {
            var rc = RunScene(outDir, "C-1234", 1234, 0.0, 5000, 100);
            ok &= rc.ok;
            summary.Add($"C-1234,1234,0,5000,,,,{rc.hash:X16},,{(rc.ok ? 1 : 0)}");
        }

        // Canh A: 5 seed x 50000 buoc, DiscOn=1
        foreach (ulong seed in SeedsA)
        {
            var ra = RunScene(outDir, $"A-{seed}", seed, 1.0, StepsA, SampleEvery);
            ok &= ra.ok;
            summary.Add($"A-{seed},{seed},1,{StepsA},,,,{ra.hash:X16},,{(ra.ok ? 1 : 0)}");
        }

        // Canh B: nhay thoi gian sau 500 buoc
        foreach (ulong seed in SeedsB)
            foreach (double jy in Jumps)
            {
                string jname = jy >= 1e8 ? "1e8" : jy >= 1e6 ? "1e6" : jy >= 1e4 ? "1e4" : "1e2";
                var rb = RunScene(outDir, $"B-{seed}-jump{jname}", seed, 1.0, 500, 100, jumpAfter: 500, jumpYears: jy, postJumpSteps: 200);
                ok &= rb.ok;
                summary.Add($"B-{seed}-jump{jname},{seed},1,500+200,{jname},,,,{rb.hash:X16},,{(rb.ok ? 1 : 0)}");
            }

        File.WriteAllLines(Path.Combine(outDir, "summary.csv"), summary);
        Console.WriteLine(ok ? "OK     disc-long: tat ca cac canh hoan tat"
                            : "FAILED disc-long: co canh loi hoac co do, xem redflags.log");
        return ok;
    }
}
