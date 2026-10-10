using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Cosmos.Core;

// Vong 5, viec 2: chay dai ban gop vanh dai.
// Sol 5000 da + FoldBelt(sun,1,40) + FoldBelt(sun,2,40); 50.000 buoc x 5 seed,
// co va khong tha NS canh Sol; so voi ban khong gop cung seed.
// Theo doi: so vat the, cham tran, ms/buoc, so khoi luong + nguyen to + dong luong,
// khoi luong vanh nguoi (phai dung yen).
static class BeltLong
{
    const int Rocks = 5000;
    const int Steps = 50000;
    const int SampleEvery = 100;
    static readonly ulong[] Seeds = { 1234, 1002, 1011, 2001, 2002 };

    static int FindSun(World w)
    {
        int sun = 0;
        for (int i = 1; i < w.N; i++) if (w.M[i] > w.M[sun]) sun = i;
        return sun;
    }

    static int DropNS(World w)
    {
        int ns = w.Do(new Command(CmdKind.Create, X: 100, Amount: 466700 * World.EarthMass, Mix: w.Mix(("gas", 1))));
        double birth = 10 * w.C.StarSolarMass, end = 1 + w.C.StarGiantFraction;
        w.Do(new Command(CmdKind.SetStarState, Target: ns, StarState: new StellarState(w.StarLifetime(birth) * end, end, birth)));
        return ns;
    }

    static double Rel(double v, double v0) => v0 == 0 ? (v == 0 ? 0 : double.PositiveInfinity) : (v - v0) / Math.Abs(v0);

    static void RunOne(string outDir, string runId, ulong seed, bool fold, bool dropNs)
    {
        var iv = CultureInfo.InvariantCulture;
        string csvPath = Path.Combine(outDir, $"belt-long-{runId}.csv");
        using var csv = new StreamWriter(csvPath);
        using var flags = new StreamWriter(Path.Combine(outDir, "redflags.log"), append: true);

        var w = World.SolSystem(Rocks, seed);
        w.Threads = 1;
        int sun = FindSun(w);
        int folded1 = 0, folded2 = 0;
        if (fold)
        {
            folded1 = w.FoldBelt(sun, 1, 40);
            folded2 = w.FoldBelt(sun, 2, 40);
        }
        if (dropNs) DropNS(w);

        // baseline ledgers
        int nEl = w.ElementCount;
        double mass0 = w.EscapedMass, px0 = w.EscapedPx, py0 = w.EscapedPy;
        var el0 = new double[nEl];
        for (int e = 0; e < nEl; e++) el0[e] = w.EscapedMatter[e] - w.NucleosynthesisDelta[e];
        for (int i = 0; i < w.N; i++) if (w.Alive[i])
            {
                mass0 += w.M[i]; px0 += w.M[i] * w.Vx[i]; py0 += w.M[i] * w.Vy[i];
                for (int e = 0; e < nEl; e++) el0[e] += w.Comp[i * nEl + e];
            }
        foreach (var r in w.RocheReservoirs)
        {
            mass0 += r.Mass;
            for (int e = 0; e < nEl; e++) el0[e] += r.Matter[e];
        }
        foreach (var d in w.Discs)
        {
            mass0 += d.Total; px0 += d.Total * w.Vx[d.Host]; py0 += d.Total * w.Vy[d.Host];
            for (int k = 0; k < d.Cells; k++)
                for (int e = 0; e < nEl; e++) el0[e] += w.DiscCellMatter(d, k, e);
        }
        double cold0 = 0;
        foreach (var d in w.Discs) if (d.Cold) cold0 += d.Total;

        var header = new List<string> { "run", "step", "year", "live", "merges", "discs", "cold_discs",
            "cold_mass", "disc_mass_hot", "disc_accreted", "mass_resid_rel", "px_resid_rel", "py_resid_rel",
            "heavy_phase", "heavy_mass", "ms_per_step", "redflags" };
        for (int e = 0; e < nEl; e++) header.Add($"el{e}_resid_rel");
        csv.WriteLine(string.Join(",", header));

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var winSw = System.Diagnostics.Stopwatch.StartNew();
        double winMs = 0; int winCount = 0; double msAvg = 0, msBase = 0;
        bool msFlagged = false, coldFlagged = false;

        void Sample(int step)
        {
            double mass = w.EscapedMass, px = w.EscapedPx, py = w.EscapedPy;
            var el = new double[nEl];
            for (int e = 0; e < nEl; e++) el[e] = w.EscapedMatter[e] - w.NucleosynthesisDelta[e];
            for (int i = 0; i < w.N; i++) if (w.Alive[i])
                {
                    mass += w.M[i]; px += w.M[i] * w.Vx[i]; py += w.M[i] * w.Vy[i];
                    for (int e = 0; e < nEl; e++) el[e] += w.Comp[i * nEl + e];
                }
            foreach (var r in w.RocheReservoirs)
            {
                mass += r.Mass;
                for (int e = 0; e < nEl; e++) el[e] += r.Matter[e];
            }
            double coldMass = 0, hotMass = 0; int coldN = 0;
            foreach (var d in w.Discs)
            {
                mass += d.Total;
                if (d.Host >= 0 && w.Alive[d.Host]) { px += d.Total * w.Vx[d.Host]; py += d.Total * w.Vy[d.Host]; }
                for (int k = 0; k < d.Cells; k++)
                    for (int e = 0; e < nEl; e++) el[e] += w.DiscCellMatter(d, k, e);
                if (d.Cold) { coldMass += d.Total; coldN++; } else hotMass += d.Total;
            }
            int heavy = w.Heaviest();
            var cols = new List<string> { runId, step.ToString(), w.Year.ToString("F2", iv),
                w.Live.ToString(), w.Merges.ToString(), w.Discs.Count.ToString(), coldN.ToString(),
                coldMass.ToString("E6", iv), hotMass.ToString("E6", iv), w.DiscAccretedMass.ToString("E6", iv),
                Rel(mass, mass0).ToString("E3", iv), Rel(px, px0).ToString("E3", iv), Rel(py, py0).ToString("E3", iv),
                w.StarPhaseOf(heavy).ToString(), w.M[heavy].ToString("E6", iv), msAvg.ToString("F2", iv) };
            var red = new List<string>();
            if (w.Live == w.X.Length) red.Add("cham tran 5512");
            if (!msFlagged && msBase > 0 && msAvg > 2 * msBase) { red.Add($"ms {msAvg:F1} > 2x base {msBase:F1}"); msFlagged = true; }
            if (Math.Abs(Rel(mass, mass0)) > 1e-9) red.Add($"mass resid {Rel(mass, mass0):E2}");
            if (!coldFlagged && cold0 > 0 && Math.Abs(Rel(coldMass, cold0)) > 1e-9) { red.Add($"vanh nguoi doi {Rel(coldMass, cold0):E2}"); coldFlagged = true; }
            foreach (var d in w.Discs) if (d.Total < 0) { red.Add("disc am"); break; }
            cols.Add(red.Count == 0 ? "" : string.Join(" | ", red));
            for (int e = 0; e < nEl; e++) cols.Add(Rel(el[e], el0[e]).ToString("E3", iv));
            csv.WriteLine(string.Join(",", cols));
            foreach (var r in red) { string line = $"REDFLAG {runId} step={step}: {r}"; Console.WriteLine(line); flags.WriteLine(line); }
            csv.Flush(); flags.Flush();
        }

        Console.WriteLine($"belt-long: {runId} start — fold={fold} ns={dropNs} folded={folded1}+{folded2} live0={w.Live} cold0={cold0:E3}");
        try
        {
            Sample(0);
            for (int s = 1; s <= Steps; s++)
            {
                winSw.Restart(); w.Advance(.5); winSw.Stop();
                double ms = winSw.Elapsed.TotalMilliseconds;
                winMs += ms; winCount++;
                if (winCount >= 100) { msAvg = winMs / winCount; winMs = 0; winCount = 0; }
                if (s == 1000) msBase = msAvg;
                if (s % SampleEvery == 0) Sample(s);
            }
            Sample(Steps);
        }
        catch (Exception ex)
        {
            string line = $"REDFLAG {runId}: EXCEPTION {ex.GetType().Name}: {ex.Message}";
            Console.WriteLine(line); flags.WriteLine(line);
        }
        sw.Stop();
        ulong hash = 0; try { hash = w.Hash(); } catch { }
        Console.WriteLine($"belt-long: {runId} xong — live={w.Live} hash={hash:X16} wall={sw.Elapsed.TotalSeconds:F0}s");
    }

    public static bool Run(string outDir)
    {
        return RunFiltered(outDir, null);
    }

    public static bool RunBnOnly(string outDir)
    {
        return RunFiltered(outDir, "BN");
    }

    static bool RunFiltered(string outDir, string? prefix)
    {
        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, "redflags.log"), "");
        bool ok = true;
        // 5 seed x (belt+NS, belt, nobelt) = 15 run
        foreach (ulong seed in Seeds)
        {
            try
            {
                if (prefix == null || prefix == "BN") RunOne(outDir, $"BN-{seed}", seed, fold: true, dropNs: true);
                if (prefix == null) RunOne(outDir, $"B-{seed}", seed, fold: true, dropNs: false);
                if (prefix == null) RunOne(outDir, $"N-{seed}", seed, fold: false, dropNs: false);
            }
            catch (Exception ex) { Console.WriteLine($"FAILED {seed}: {ex.Message}"); ok = false; }
        }
        Console.WriteLine(ok ? "OK     belt-long: xong" : "FAILED belt-long");
        return ok;
    }
}
