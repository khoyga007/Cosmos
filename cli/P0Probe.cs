using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Cosmos.Core;

// P0 probe (Hark, issue #2, round 1): measure-only harness for the NS-drop scene. Reads public World state only;
// core/ is untouched. Scene = NsDropChecks.Scene with a variable SolSystem seed:
// SolSystem(5000, seed), 96 x Advance(.5), NS 1.4 Msun at X=100 (SetStarState to NS at birth 10 Msun).
// Usage: cli p0-probe <seed> <maxSteps> <sampleEvery> <outDir> [stableWindow]
static class P0Probe
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static int Run(string[] args)
    {
        ulong seed = ulong.Parse(args[1], Inv);
        long maxSteps = long.Parse(args[2], Inv);
        int every = int.Parse(args[3], Inv);
        string outDir = args[4];
        long stableWindow = args.Length > 5 ? long.Parse(args[5], Inv) : 50000;
        Directory.CreateDirectory(outDir);
        double wallBudget = Environment.GetEnvironmentVariable("P0_WALL_SECONDS") is string wb ? double.Parse(wb, Inv) : 0;

        var w = World.SolSystem(5000, seed);
        for (int k = 0; k < 96; k++) w.Advance(.5);
        int ns = w.Do(new Command(CmdKind.Create, X: 100, Amount: 466700 * World.EarthMass, Mix: w.Mix(("gas", 1))));
        double birth = 10 * w.C.StarSolarMass, end = 1 + w.C.StarGiantFraction;
        if (w.Do(new Command(CmdKind.SetStarState, Target: ns, StarState: new StellarState(w.StarLifetime(birth) * end, end, birth))) < 0)
            throw new InvalidOperationException("NS fixture refused its stellar state.");

        int cap = w.X.Length, E = w.ElementCount;
        if (Environment.GetEnvironmentVariable("P0_THREADS") is string t) w.Threads = int.Parse(t, Inv);
        // ---- origin of every slot's matter: 1 = belt (groups 1..3 at drop: asteroid, Kuiper, Saturn ring), 0 = other.
        var origin = new byte[cap]; var originGen = new int[cap];
        double beltMass0 = 0; int beltCount0 = 0;
        for (int i = 0; i < w.N; i++) if (w.Alive[i])
        {
            originGen[i] = w.Gen[i];
            if (w.Grp[i] is >= 1 and <= 3) { origin[i] = 1; beltMass0 += w.M[i]; beltCount0++; }
        }
        // cumulative belt-mass fates
        double beltToCompact = 0, beltToOther = 0, beltRocheEscaped = 0, beltRocheReservoir = 0, beltSublimated = 0;
        long mergeCompact = 0, mergeOther = 0, mergeUnattributed = 0, rocheEvents = 0, rocheFragments = 0, rocheZeroFrag = 0;
        double mergedEnergyIntoCompact = 0; // two-body energy (wrt compact) carried by rocks at the moment they merged into it
        long pullerDeaths = 0, pullerIntoCompact = 0; var pullerLog = new List<string>();
        long capFirstStep = -1, attemptsFull = 0; int liveMax = 0;
        double dmCompactCheck = 0, dmCompactAttributed = 0;

        var preAlive = new bool[cap]; var preGen = new int[cap];
        var preX = new double[cap]; var preY = new double[cap]; var preVx = new double[cap]; var preVy = new double[cap];
        var preM = new double[cap]; var preR = new double[cap]; var preOrigin = new byte[cap]; var preOriginGen = new int[cap];
        int preDisruptions = w.RocheDisruptions.Count;

        using var series = new StreamWriter(Path.Combine(outDir, $"seed{seed}.csv"));
        series.WriteLine("step,year,live,pullers,N,free_slots,compact_slot,compact_phase,compact_mass_earth,compact_R,merge_compact,merge_other,merge_unattr,roche_events,roche_fragments,roche_zero_frag,belt_frac_compact,belt_frac_other,belt_frac_roche_escaped,belt_frac_reservoir,belt_frac_sublimated,belt_frac_alive_bound,belt_frac_alive_unbound,cloud_n,cloud_E,cloud_E_plus_merged,roche_dissipated,peri_min_Rc,peri_p01_Rc,peri_p10_Rc,peri_p50_Rc,peri_lt10Rc,peri_lt100Rc,peri_lt1000Rc,ms_per_step");
        using var hist = new StreamWriter(Path.Combine(outDir, $"seed{seed}-peri.csv"));
        hist.WriteLine("step,log10_q_over_Rc_bin_lo,count");

        int Compact()
        {
            int best = -1;
            for (int i = 0; i < w.N; i++) if (w.Alive[i])
            {
                var ph = w.StarPhaseOf(i);
                if ((ph == StarPhase.NeutronStar || ph == StarPhase.BlackHole) && (best < 0 || w.M[i] > w.M[best])) best = i;
            }
            return best;
        }

        var sw = Stopwatch.StartNew(); double lastMs = 0; long lastStepForMs = 0;
        long lastLiveChange = 0; int lastLive = w.Live, lastPullers = -1; long lastMergeCompact = 0, lastRoche = 0, quietSince = 0;
        string stopReason = "maxSteps";

        void Sample(long step)
        {
            int c = Compact();
            int pullers = 0; for (int i = 0; i < w.N; i++) if (w.Alive[i] && w.Attracts(i)) pullers++;
            // belt matter still alive: bound / unbound wrt the heaviest object (two-body)
            int root = w.Heaviest();
            double aliveBound = 0, aliveUnbound = 0;
            double cloudE = 0; int cloudN = 0; var peri = new List<double>();
            double gmc = c >= 0 ? w.C.G * w.M[c] : 0, rc = c >= 0 ? w.R[c] : 0;
            for (int i = 0; i < w.N; i++)
            {
                if (!w.Alive[i] || w.Attracts(i)) continue;
                if (origin[i] == 1)
                {
                    double dx = w.X[i] - w.X[root], dy = w.Y[i] - w.Y[root], vx = w.Vx[i] - w.Vx[root], vy = w.Vy[i] - w.Vy[root];
                    double e = (vx * vx + vy * vy) / 2 - w.C.G * w.M[root] / Math.Sqrt(dx * dx + dy * dy);
                    if (e < 0) aliveBound += w.M[i]; else aliveUnbound += w.M[i];
                }
                if (c < 0) continue;
                {
                    double dx = w.X[i] - w.X[c], dy = w.Y[i] - w.Y[c], vx = w.Vx[i] - w.Vx[c], vy = w.Vy[i] - w.Vy[c];
                    double r = Math.Sqrt(dx * dx + dy * dy), v2 = vx * vx + vy * vy, mu = w.C.G * (w.M[c] + w.M[i]);
                    double eps = v2 / 2 - mu / r;
                    if (eps >= 0) continue;
                    // bound to the compact object only if inside its Hill-like region: primary must be the compact one
                    if (w.PrimaryOf(i) != c) continue;
                    double h = dx * vy - dy * vx, ecc = Math.Sqrt(Math.Max(0, 1 + 2 * eps * h * h / (mu * mu)));
                    double q = h * h / (mu * (1 + ecc));
                    cloudE += w.M[i] * eps; cloudN++;
                    if (rc > 0) peri.Add(q / rc);
                }
            }
            peri.Sort();
            double P(double f) => peri.Count == 0 ? double.NaN : peri[Math.Min(peri.Count - 1, (int)(f * peri.Count))];
            int lt(double x) => peri.Count(p => p < x);
            double now = sw.Elapsed.TotalMilliseconds, ms = step > lastStepForMs ? (now - lastMs) / (step - lastStepForMs) : 0;
            lastMs = now; lastStepForMs = step;
            double B(double x) => beltMass0 > 0 ? x / beltMass0 : 0;
            series.WriteLine(string.Join(",", new object[] {
                step, w.Year.ToString("R", Inv), w.Live, pullers, w.N, cap - w.Live, c, c >= 0 ? w.StarPhaseOf(c).ToString() : "none",
                c >= 0 ? (w.M[c] / World.EarthMass).ToString("G6", Inv) : "nan", rc.ToString("G6", Inv),
                mergeCompact, mergeOther, mergeUnattributed, rocheEvents, rocheFragments, rocheZeroFrag,
                B(beltToCompact).ToString("G6", Inv), B(beltToOther).ToString("G6", Inv), B(beltRocheEscaped).ToString("G6", Inv),
                B(beltRocheReservoir).ToString("G6", Inv), B(beltSublimated).ToString("G6", Inv), B(aliveBound).ToString("G6", Inv), B(aliveUnbound).ToString("G6", Inv),
                cloudN, cloudE.ToString("R", Inv), (cloudE + mergedEnergyIntoCompact).ToString("R", Inv), w.RocheDissipatedEnergy.ToString("R", Inv),
                P(0).ToString("G6", Inv), P(.01).ToString("G6", Inv), P(.10).ToString("G6", Inv), P(.5).ToString("G6", Inv),
                lt(10), lt(100), lt(1000), ms.ToString("F3", Inv) }));
            series.Flush();
            var bins = new SortedDictionary<int, int>();
            foreach (double p in peri) { int b = (int)Math.Floor(Math.Log10(Math.Max(p, 1e-12)) * 2); bins[b] = bins.GetValueOrDefault(b) + 1; }
            foreach (var kv in bins) hist.WriteLine($"{step},{(kv.Key / 2.0).ToString(Inv)},{kv.Value}");
            hist.Flush();
            // stability bookkeeping
            if (pullers != lastPullers || mergeCompact != lastMergeCompact || rocheEvents != lastRoche || w.Live != lastLive) quietSince = step;
            lastPullers = pullers; lastMergeCompact = mergeCompact; lastRoche = rocheEvents; lastLive = w.Live;
        }

        Sample(0);
        for (long step = 1; step <= maxSteps; step++)
        {
            int n0 = w.N, c0 = Compact();
            for (int i = 0; i < n0; i++)
            {
                preAlive[i] = w.Alive[i]; preGen[i] = w.Gen[i]; preOrigin[i] = origin[i]; preOriginGen[i] = originGen[i];
                if (!preAlive[i]) continue;
                preX[i] = w.X[i]; preY[i] = w.Y[i]; preVx[i] = w.Vx[i]; preVy[i] = w.Vy[i]; preM[i] = w.M[i]; preR[i] = w.R[i];
            }
            double cm0 = c0 >= 0 ? w.M[c0] : 0;
            long merges0 = w.Merges;
            double esc0 = w.EscapedMass;
            w.Advance(.5);

            // Roche events this step
            var sources = new HashSet<int>();
            for (int k = preDisruptions; k < w.RocheDisruptions.Count; k++)
            {
                var d = w.RocheDisruptions[k]; rocheEvents++; sources.Add(d.Source);
                bool belt = d.Source < n0 && preAlive[d.Source] && preGen[d.Source] == d.SourceGeneration ? preOrigin[d.Source] == 1 : origin[d.Source] == 1 && originGen[d.Source] == d.SourceGeneration;
                // fragments of this event = live slots now in its group
                int frags = 0; double fragMass = 0;
                for (int i = 0; i < w.N; i++) if (w.Alive[i] && w.Grp[i] == d.Group)
                {
                    frags++; fragMass += w.M[i];
                    origin[i] = belt ? (byte)1 : (byte)0; originGen[i] = w.Gen[i];
                }
                rocheFragments += frags; if (frags == 0) rocheZeroFrag++;
                if (belt)
                {
                    beltRocheEscaped += d.EscapedMass;
                    beltRocheReservoir += d.SourceMass - d.EscapedMass - fragMass;
                }
            }
            preDisruptions = w.RocheDisruptions.Count;

            // deaths that are not Roche sources: merges (rocks only ever merge into a puller) or full sublimation
            long merged = w.Merges - merges0; long attributed = 0;
            var pullersPre = new List<int>();
            for (int i = 0; i < n0; i++) if (preAlive[i] && preM[i] >= w.C.AttractMass) pullersPre.Add(i);
            for (int i = 0; i < n0; i++)
            {
                if (!preAlive[i]) continue;
                bool died = !w.Alive[i] || w.Gen[i] != preGen[i];
                if (!died || sources.Contains(i)) continue;
                bool pullerDeath = preM[i] >= w.C.AttractMass;
                // heir = puller whose surface was nearest at step start
                int heir = -1; double bestGap = double.PositiveInfinity;
                foreach (int j in pullersPre)
                {
                    if (j == i || !w.Alive[j] || w.Gen[j] != preGen[j]) continue;
                    double gap = Math.Sqrt((preX[i] - preX[j]) * (preX[i] - preX[j]) + (preY[i] - preY[j]) * (preY[i] - preY[j])) - preR[j];
                    if (gap < bestGap) { bestGap = gap; heir = j; }
                }
                attributed++;
                if (pullerDeath)
                {
                    pullerDeaths++;
                    if (heir == c0 && c0 >= 0) { pullerIntoCompact++; dmCompactAttributed += preM[i]; }
                    pullerLog.Add($"step {step}: puller slot {i} m={preM[i] / World.EarthMass:G6} Earth -> heir {heir}{(heir == c0 ? " (compact)" : "")}");
                    continue;
                }
                bool beltRock = preOrigin[i] == 1 && preOriginGen[i] == preGen[i];
                if (heir == c0 && c0 >= 0)
                {
                    mergeCompact++; dmCompactAttributed += preM[i];
                    if (beltRock) beltToCompact += preM[i];
                    double dx = preX[i] - preX[c0], dy = preY[i] - preY[c0], vx = preVx[i] - preVx[c0], vy = preVy[i] - preVy[c0];
                    mergedEnergyIntoCompact += preM[i] * ((vx * vx + vy * vy) / 2 - w.C.G * (cm0 + preM[i]) / Math.Sqrt(dx * dx + dy * dy));
                }
                else { mergeOther++; if (beltRock) beltToOther += preM[i]; }
                if (!w.Alive[i]) origin[i] = 0; else if (w.Gen[i] != preGen[i] && originGen[i] != w.Gen[i]) origin[i] = 0;
            }
            if (attributed > merged) { /* some deaths were sublimation (Kill), not merges */ }
            mergeUnattributed += merged - attributed; // negative = deaths that were not merges (sublimation Kill)
            if (c0 >= 0 && w.Alive[c0]) dmCompactCheck += w.M[c0] - cm0;

            if (w.Live > liveMax) liveMax = w.Live;
            if (capFirstStep < 0 && w.Live >= cap) capFirstStep = step;
            if (w.Live >= cap) attemptsFull++;

            if (step % every == 0 || (step <= 2000 && step % 50 == 0))
            {
                Sample(step);
                if (wallBudget > 0 && sw.Elapsed.TotalSeconds > wallBudget) { stopReason = $"wall budget {wallBudget}s reached (not stable, not 1e6)"; maxSteps = step; break; }
                if (step - quietSince >= stableWindow && step >= 2 * stableWindow) { stopReason = $"stable: no change in live/pullers/compact merges/roche for {step - quietSince} steps"; maxSteps = step; break; }
            }
        }
        if (maxSteps % every != 0) Sample(maxSteps);
        File.WriteAllText(Path.Combine(outDir, $"seed{seed}-summary.txt"), string.Join("\n", new[] {
            $"seed {seed}", $"stop {stopReason} at step {Math.Min(maxSteps, w.Step - 96)} (world step {w.Step}, year {w.Year.ToString("R", Inv)})",
            $"capacity {cap}, live max {liveMax}, first full step {capFirstStep}, steps at full {attemptsFull}",
            $"belt initial objects {beltCount0}, mass {beltMass0.ToString("R", Inv)}",
            $"rock merges into compact {mergeCompact}, into other pullers {mergeOther}; puller deaths {pullerDeaths} (into compact {pullerIntoCompact}); world Merges delta minus attributed deaths {mergeUnattributed}",
            $"compact dM attributed {dmCompactAttributed.ToString("R", Inv)} vs measured {dmCompactCheck.ToString("R", Inv)}",
            $"roche events {rocheEvents}, fragments {rocheFragments}, zero-fragment events {rocheZeroFrag}",
            string.Join("\n", pullerLog), $"hash {w.Hash():X16}", $"wall {sw.Elapsed.TotalSeconds:F1}s" }) + "\n");
        return 0;
    }
}
