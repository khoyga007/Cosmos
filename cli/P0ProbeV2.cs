using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Cosmos.Core;

// P0 probe v2 (Hark, issue #2, round 2): measurement audit of the round-1 harness (cli/P0Probe.cs @8f5ccbb).
// Measure-only. Reads public World state only; core/ is untouched and no core hook is added.
// Same scene as P0Probe / NsDropChecks.Scene: SolSystem(5000, seed), 96 x Advance(.5), NS 1.4 Msun at X=100.
//
// Every number written carries a class in its column name or in the summary:
//   direct   = read from core state / core records (World arrays, Merges, RocheDisruptions, RocheReservoirs,
//              EscapedMass, Events) or an exact sum of such reads;
//   closure  = assigned by exact mass closure of direct reads within a rounding tolerance (unique solution only);
//   est      = estimate (two-body formulas, nearest-surface heuristic, straight-line/periapsis-passage guesses);
//   unknown  = not observable through the public API at step granularity; the summary names the hook it needs.
// Matter is tracked per (slot, Gen) occupant, never per slot.
//
// Commands:
//   cli p0-probe-v2 <seed> <maxSteps> <sampleEvery> <outDir>
//   cli p0-control  <seed> <maxSteps> <sampleEvery> <outDir>   (same scene, Advance + checkpoint reads only)
// Both write <outDir>/seed<seed>-checkpoints.csv with identical columns so the files can be diffed byte-for-byte.
static class P0ProbeV2
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static string R(double x) => x.ToString("R", Inv);
    static string G(double x) => x.ToString("G6", Inv);

    // Identical to P0Probe.Run's scene construction (cli/P0Probe.cs:28-36 @8f5ccbb).
    static World Scene(ulong seed, out int ns)
    {
        var w = World.SolSystem(5000, seed);
        for (int k = 0; k < 96; k++) w.Advance(.5);
        ns = w.Do(new Command(CmdKind.Create, X: 100, Amount: 466700 * World.EarthMass, Mix: w.Mix(("gas", 1))));
        double birth = 10 * w.C.StarSolarMass, end = 1 + w.C.StarGiantFraction;
        if (w.Do(new Command(CmdKind.SetStarState, Target: ns, StarState: new StellarState(w.StarLifetime(birth) * end, end, birth))) < 0)
            throw new InvalidOperationException("NS fixture refused its stellar state.");
        if (Environment.GetEnvironmentVariable("P0_THREADS") is string t) w.Threads = int.Parse(t, Inv);
        return w;
    }

    const string CheckpointHeader = "step,hash,live,N,merges,roche_events,reservoir_count,reservoir_mass,escaped_mass,roche_dissipated,ns_slot_mass,ns_slot_gen,ns_slot_alive";

    // Only scalar/array reads and Hash(); used identically by the control and the v2 harness.
    static string Checkpoint(World w, long step, int ns)
    {
        double resMass = 0; var res = w.RocheReservoirs; for (int k = 0; k < res.Count; k++) resMass += res[k].Mass;
        return string.Join(",", new object[] { step, w.Hash().ToString("X16"), w.Live, w.N, w.Merges, w.RocheDisruptions.Count, res.Count,
            R(resMass), R(w.EscapedMass), R(w.RocheDissipatedEnergy), R(w.M[ns]), w.Gen[ns], w.Alive[ns] ? 1 : 0 });
    }

    public static int Control(string[] args)
    {
        ulong seed = ulong.Parse(args[1], Inv); long maxSteps = long.Parse(args[2], Inv); int every = int.Parse(args[3], Inv);
        string outDir = args[4]; Directory.CreateDirectory(outDir);
        var w = Scene(seed, out int ns);
        var sw = Stopwatch.StartNew();
        using var cp = new StreamWriter(Path.Combine(outDir, $"seed{seed}-checkpoints.csv"));
        cp.WriteLine(CheckpointHeader); cp.WriteLine(Checkpoint(w, 0, ns));
        for (long step = 1; step <= maxSteps; step++)
        {
            w.Advance(.5);
            if (step % every == 0 || step == maxSteps) cp.WriteLine(Checkpoint(w, step, ns));
        }
        File.WriteAllText(Path.Combine(outDir, $"seed{seed}-summary.txt"),
            $"control seed {seed} steps {maxSteps} hash {w.Hash():X16} wall {sw.Elapsed.TotalSeconds:F1}s threads {w.Threads}\n");
        return 0;
    }

    public static int Run(string[] args)
    {
        ulong seed = ulong.Parse(args[1], Inv); long maxSteps = long.Parse(args[2], Inv); int every = int.Parse(args[3], Inv);
        string outDir = args[4]; Directory.CreateDirectory(outDir);
        var w = Scene(seed, out int ns);
        int cap = w.X.Length;
        double attract = w.C.AttractMass, gC = w.C.G;

        // ---- origin per occupant (slot, Gen). 0 = not belt, 1 = belt rock present at drop (groups 1..3),
        //      2 = Roche fragment descended from belt matter, 3 = fragment of unknown origin (born from an in-step source).
        var org = new byte[cap]; var orgGen = new int[cap];
        for (int i = 0; i < cap; i++) orgGen[i] = -1;
        double beltMass0 = 0; int beltCount0 = 0;
        for (int i = 0; i < w.N; i++) if (w.Alive[i])
        {
            orgGen[i] = w.Gen[i];
            if (w.Grp[i] is >= 1 and <= 3 && !w.Attracts(i)) { org[i] = 1; beltMass0 += w.M[i]; beltCount0++; }
        }
        byte OrgOf(int i, int gen) => orgGen[i] == gen ? org[i] : (byte)3;
        static bool IsBelt(byte o) => o == 1 || o == 2;

        // ---- cumulative belt ledger (mass). Each term is labelled with its class.
        double bRocheEsc = 0;          // direct: RocheDisruption.EscapedMass of belt sources
        double bResUnbound = 0;        // direct: reservoir entry = SourceMass-CapturedMass (bit-equal), belt sources
        double bResUnresolved = 0;     // direct: reservoir entry at (Host,HostGeneration) after the event, belt sources
        double bDeathCompact = 0;      // closure: belt rock deaths whose mass closes uniquely into the compact's dM
        double bDeathOtherClosure = 0; // closure: belt rock deaths whose step closes exactly on non-compact pullers
        double bDeathUnknown = 0;      // unknown: belt rock deaths in steps without a unique closure (merge target or Kill)
        double bDeathHeurCompact = 0;  // est: round-1 nearest-surface heuristic, kept for comparison only
        double bLostCompact = 0, bLostOther = 0, bLostUnknown = 0; // in-step-born belt fragments that died in the same step
        double bPartialLoss = 0;       // direct: mass decrease of surviving belt occupants (rule escapes: sublimation etc.)
        double bUnknownOrigin = 0;     // fragments/sources whose origin is unknown (born and re-disrupted in one step)
        double bTinyUnknown = 0;       // unknown: belt deaths lighter than the rounding of the receiving masses (mass closure blind)
        double bViaPullerIntoCompact = 0; // closure: belt mass a puller absorbed (single-gainer closure) before that puller merged into the compact
        double bCarriedUnknownSplit = 0;  // closure to 'other pullers' but split among several gainers unknown
        long tinyDeaths = 0; double bTinyOnlyCompactStep = 0, tinyAllOnlyCompactStep = 0;
        var pullerBelt = new double[cap]; var pullerGen = new int[cap]; for (int i = 0; i < cap; i++) pullerGen[i] = -1;
        double compactDMDirect = 0;    // direct: sum over steps of M[c] after - before (same occupant)
        double compactFromPullerEvents = 0; // direct: Events "contact/merge" with survivor = compact (B = victim mass)
        double compactFromRocksClosure = 0, compactFromLostClosure = 0, compactUnexplained = 0;
        long stepsUnique = 0, stepsAmbiguous = 0, stepsNoCompactGain = 0, stepsClosureFail = 0, resMapFail = 0;
        long rockDeaths = 0, rockDeathsCompact = 0, rockDeathsAmb = 0, pullerDeaths = 0, instepSources = 0, instepLostEvents = 0;
        long rocheEvents = 0, zeroFragEvents = 0; double consResidMaxAbs = 0, consResidSum = 0;
        double lostTotal = 0, stepClosureMaxAbs = 0;
        var pullerLog = new List<string>();

        // ---- observation of approach (every step boundary, every non-pulling object, bound or not)
        double obsMinRc = double.PositiveInfinity; long obsMinStep = -1;
        var seen1000 = new HashSet<long>(); var seen100 = new HashSet<long>(); var seen10 = new HashSet<long>();
        double periPassMinRc = double.PositiveInfinity; long periPassCount = 0, periPassLt1000 = 0; long periPassMinStep = -1;
        double deathCompactPreMinRc = double.PositiveInfinity, deathCompactPreMaxRc = 0;
        static long Key(int i, int g) => ((long)i << 32) | (uint)g;

        // ---- pre-step snapshot
        var preAlive = new bool[cap]; var preGen = new int[cap]; var preOrg = new byte[cap];
        var preX = new double[cap]; var preY = new double[cap]; var preVx = new double[cap]; var preVy = new double[cap];
        var preM = new double[cap]; var preR = new double[cap];

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
        static double Ulp(double x) { x = Math.Abs(x); return Math.BitIncrement(x) - x; }

        using var steps = new StreamWriter(Path.Combine(outDir, $"seed{seed}-steps.csv"));
        steps.WriteLine("step,live,N,merges_delta,compact_slot,compact_gen,compact_mass_direct,compact_R_direct,compact_dM_direct," +
            "rock_deaths_direct,rock_death_mass_direct,puller_deaths_direct,puller_death_mass_direct,puller_merge_events_into_compact_direct," +
            "roche_events_direct,roche_source_mass_direct,roche_escaped_direct,reservoir_added_direct,fragments_represented_closure,fragments_alive_end_direct," +
            "instep_sources_direct,instep_lost_mass_closure,puller_gain_sum_direct,puller_loss_sum_direct,step_closure_residual,compact_mode,compact_rock_mass_closure," +
            "compact_rock_mass_heuristic_est,global_conservation_residual,obs_min_r_Rc_direct,obs_n_lt10Rc,obs_n_lt100Rc,obs_n_lt1000Rc," +
            "peri_pass_n_est,peri_pass_min_q_Rc_est,reservoir_map_ok");
        using var ledger = new StreamWriter(Path.Combine(outDir, $"seed{seed}-ledger.csv"));
        ledger.WriteLine("step,belt_mass0,alive_direct,alive_bound_heaviest_est,alive_unbound_heaviest_est,roche_escaped_direct,reservoir_unbound_direct," +
            "reservoir_unresolved_direct,death_compact_closure,death_other_closure,death_unknown,lost_instep_compact_closure,lost_instep_other_closure," +
            "lost_instep_unknown,partial_loss_direct,unknown_origin,residual_unattributed,residual_over_belt,death_compact_heuristic_est,compact_dM_direct_cum," +
            "compact_from_puller_events_direct,compact_from_rocks_closure,compact_from_lost_closure,compact_unexplained,tiny_unknown,belt_via_puller_into_compact_closure,belt_other_split_unknown,alive_unknown_origin_not_in_ledger");
        using var qcsv = new StreamWriter(Path.Combine(outDir, $"seed{seed}-q.csv"));
        qcsv.WriteLine("step,compact_R,n_bound_twobody,n_bound_primary_compact,q_min_bound_all_Rc_est,q_p01_bound_all_Rc_est,q_p50_bound_all_Rc_est," +
            "q_min_primary_compact_Rc_est,q_lt1000_bound_all_est,q_lt1000_primary_compact_est,q_min_unbound_Rc_est,q_lt1000_unbound_est," +
            "obs_min_r_now_Rc_direct,obs_n_lt1000_now_direct,obs_min_r_cum_Rc_direct,obs_distinct_lt1000_cum,obs_distinct_lt100_cum,obs_distinct_lt10_cum," +
            "peri_pass_min_q_cum_Rc_est,peri_pass_lt1000_cum_est");
        using var cp = new StreamWriter(Path.Combine(outDir, $"seed{seed}-checkpoints.csv"));
        cp.WriteLine(CheckpointHeader); cp.WriteLine(Checkpoint(w, 0, ns));

        void Sample(long step)
        {
            int c = Compact(); double rc = c >= 0 ? w.R[c] : double.NaN;
            int root = w.Heaviest();
            double alive = 0, bound = 0, unbound = 0, aliveUnknownOrg = 0;
            var qAll = new List<double>(); var qPrim = new List<double>(); var qUnb = new List<double>();
            double obsNow = double.PositiveInfinity; int obsNowLt1000 = 0;
            for (int i = 0; i < w.N; i++)
            {
                if (!w.Alive[i] || w.Attracts(i)) continue;
                if (OrgOf(i, w.Gen[i]) == 3) aliveUnknownOrg += w.M[i];
                if (IsBelt(OrgOf(i, w.Gen[i])))
                {
                    alive += w.M[i];
                    double dx = w.X[i] - w.X[root], dy = w.Y[i] - w.Y[root], vx = w.Vx[i] - w.Vx[root], vy = w.Vy[i] - w.Vy[root];
                    double e = (vx * vx + vy * vy) / 2 - gC * w.M[root] / Math.Sqrt(dx * dx + dy * dy);
                    if (e < 0) bound += w.M[i]; else unbound += w.M[i];
                }
                if (c < 0) continue;
                double ddx = w.X[i] - w.X[c], ddy = w.Y[i] - w.Y[c], uvx = w.Vx[i] - w.Vx[c], uvy = w.Vy[i] - w.Vy[c];
                double r = Math.Sqrt(ddx * ddx + ddy * ddy), mu = gC * (w.M[c] + w.M[i]);
                if (r / rc < obsNow) obsNow = r / rc; if (r / rc < 1000) obsNowLt1000++;
                double eps = (uvx * uvx + uvy * uvy) / 2 - mu / r, h = ddx * uvy - ddy * uvx;
                double ecc = Math.Sqrt(Math.Max(0, 1 + 2 * eps * h * h / (mu * mu))), q = h * h / (mu * (1 + ecc));
                if (eps < 0)
                {
                    qAll.Add(q / rc);
                    if (w.PrimaryOf(i) == c) qPrim.Add(q / rc); // round-1 definition (cli/P0Probe.cs:100-102)
                }
                else qUnb.Add(q / rc);
            }
            qAll.Sort(); qPrim.Sort(); qUnb.Sort();
            static double P(List<double> l, double f) => l.Count == 0 ? double.NaN : l[Math.Min(l.Count - 1, (int)(f * l.Count))];
            qcsv.WriteLine(string.Join(",", new object[] { step, G(rc), qAll.Count, qPrim.Count, G(P(qAll, 0)), G(P(qAll, .01)), G(P(qAll, .5)),
                G(P(qPrim, 0)), qAll.Count(x => x < 1000), qPrim.Count(x => x < 1000), G(P(qUnb, 0)), qUnb.Count(x => x < 1000),
                G(obsNow), obsNowLt1000, G(obsMinRc), seen1000.Count, seen100.Count, seen10.Count, G(periPassMinRc), periPassLt1000 }));
            double accounted = alive + bRocheEsc + bResUnbound + bResUnresolved + bDeathCompact + bDeathOtherClosure + bDeathUnknown
                + bLostCompact + bLostOther + bLostUnknown + bPartialLoss + bUnknownOrigin + bTinyUnknown;
            double resid = beltMass0 - accounted;
            ledger.WriteLine(string.Join(",", new object[] { step, R(beltMass0), R(alive), R(bound), R(unbound), R(bRocheEsc), R(bResUnbound),
                R(bResUnresolved), R(bDeathCompact), R(bDeathOtherClosure), R(bDeathUnknown), R(bLostCompact), R(bLostOther), R(bLostUnknown),
                R(bPartialLoss), R(bUnknownOrigin), R(resid), G(resid / beltMass0), R(bDeathHeurCompact), R(compactDMDirect),
                R(compactFromPullerEvents), R(compactFromRocksClosure), R(compactFromLostClosure), R(compactUnexplained), R(bTinyUnknown), R(bViaPullerIntoCompact), R(bCarriedUnknownSplit), R(aliveUnknownOrg) }));
            qcsv.Flush(); ledger.Flush(); steps.Flush();
        }

        var sw = Stopwatch.StartNew();
        Sample(0);
        var srcPre = new HashSet<int>();
        var items = new List<(double Mass, int Kind, int Slot)>(); // Kind 0 = rock death, 1 = puller death, 2 = in-step lost (lump)
        for (long step = 1; step <= maxSteps; step++)
        {
            int n0 = w.N, c0 = Compact(), cGen0 = c0 >= 0 ? w.Gen[c0] : -1;
            double massAlive0 = 0;
            for (int i = 0; i < n0; i++)
            {
                preAlive[i] = w.Alive[i]; preGen[i] = w.Gen[i];
                if (!preAlive[i]) continue;
                preOrg[i] = OrgOf(i, w.Gen[i]);
                preX[i] = w.X[i]; preY[i] = w.Y[i]; preVx[i] = w.Vx[i]; preVy[i] = w.Vy[i]; preM[i] = w.M[i]; preR[i] = w.R[i];
                massAlive0 += w.M[i];
            }
            double cm0 = c0 >= 0 ? w.M[c0] : 0, rc0 = c0 >= 0 ? w.R[c0] : double.NaN, year0 = w.Year;
            long merges0 = w.Merges; double esc0 = w.EscapedMass;
            int dis0 = w.RocheDisruptions.Count, res0 = w.RocheReservoirs.Count;

            w.Advance(.5);

            double massAlive1 = 0; for (int i = 0; i < w.N; i++) if (w.Alive[i]) massAlive1 += w.M[i];
            var res = w.RocheReservoirs; var dis = w.RocheDisruptions;
            double dRes = 0; for (int k = res0; k < res.Count; k++) dRes += res[k].Mass;
            double consResid = (massAlive1 - massAlive0) + (w.EscapedMass - esc0) + dRes;
            consResidSum += consResid; consResidMaxAbs = Math.Max(consResidMaxAbs, Math.Abs(consResid));

            // ---- Roche events (direct records); map reservoir entries in append order
            srcPre.Clear();
            int rp = res0; bool mapOk = true;
            double srcMass = 0, escStep = 0, represented = 0, fragAlive = 0, instepSrcMass = 0; int nInstep = 0;
            bool allBelt = true, anyBelt = false;
            for (int k = dis0; k < dis.Count; k++)
            {
                var d = dis[k]; rocheEvents++;
                bool pre = d.Source < n0 && preAlive[d.Source] && preGen[d.Source] == d.SourceGeneration;
                byte o = pre ? preOrg[d.Source] : (anyBelt && allBelt ? (byte)2 : (byte)3); // in-step-born source: belt only if every earlier event this step was belt
                if (pre) srcPre.Add(d.Source); else { nInstep++; instepSrcMass += d.SourceMass; }
                double unb = d.SourceMass - d.CapturedMass, rUnb = 0, rUnres = 0;
                if (d.EscapedMass == 0 && unb > 0)
                {
                    if (rp < res.Count && res[rp].Mass == unb) { rUnb = unb; rp++; } else mapOk = false;
                }
                double nextUnb = double.NaN;
                if (k + 1 < dis.Count && dis[k + 1].EscapedMass == 0) nextUnb = dis[k + 1].SourceMass - dis[k + 1].CapturedMass;
                if (rp < res.Count && res[rp].Host == d.Host && res[rp].HostGeneration == d.HostGeneration
                    && res[rp].Mass <= d.CapturedMass && res[rp].Mass != nextUnb) { rUnres = res[rp].Mass; rp++; }
                double rep = d.CapturedMass - rUnres;
                int frags = 0; double fm = 0;
                byte fo = IsBelt(o) ? (byte)2 : o == 0 ? (byte)0 : (byte)3;
                for (int i = 0; i < w.N; i++) if (w.Alive[i] && w.Grp[i] == d.Group) { frags++; fm += w.M[i]; org[i] = fo; orgGen[i] = w.Gen[i]; }
                if (frags == 0) zeroFragEvents++;
                srcMass += d.SourceMass; escStep += d.EscapedMass; represented += rep; fragAlive += fm;
                if (IsBelt(o)) { anyBelt = true; if (pre) { bRocheEsc += d.EscapedMass; bResUnbound += rUnb; bResUnresolved += rUnres; } }
                else allBelt = false;
                if (!pre)
                {
                    // the source was itself born this step: its mass was already counted as "represented" of an earlier event.
                    // Its escape/reservoir parts are belt only if that earlier event was belt; mark unknown unless all events were belt.
                    if (allBelt && anyBelt) { bRocheEsc += d.EscapedMass; bResUnbound += rUnb; bResUnresolved += rUnres; }
                    else bUnknownOrigin += d.EscapedMass + rUnb + rUnres;
                }
            }
            if (rp != res.Count) mapOk = false;
            if (!mapOk) resMapFail++;
            instepSources += nInstep;
            // fragments born this step that are gone at step end (merged, sublimated, or re-disrupted in the same step)
            double lost = represented - fragAlive - instepSrcMass;
            if (Math.Abs(lost) <= 64 * Ulp(Math.Max(represented, fragAlive + instepSrcMass))) lost = 0; // subtraction rounding, not mass
            if (lost != 0) instepLostEvents++;
            lostTotal += lost;

            // ---- deaths of pre-existing occupants that were not Roche sources
            items.Clear(); int nRock = 0, nPuller = 0; double rockMass = 0, pullerMass = 0, heurCompact = 0;
            var pullersPre = new List<int>();
            for (int i = 0; i < n0; i++) if (preAlive[i] && preM[i] >= attract) pullersPre.Add(i);
            for (int i = 0; i < n0; i++)
            {
                if (!preAlive[i]) continue;
                bool died = !w.Alive[i] || w.Gen[i] != preGen[i];
                if (!died)
                {
                    if (preM[i] < attract && IsBelt(preOrg[i]) && w.M[i] < preM[i]) bPartialLoss += preM[i] - w.M[i];
                    continue;
                }
                if (srcPre.Contains(i)) continue;
                if (preM[i] >= attract) { nPuller++; pullerMass += preM[i]; items.Add((preM[i], 1, i)); continue; }
                nRock++; rockMass += preM[i]; items.Add((preM[i], 0, i));
                // round-1 heuristic (estimate): puller with nearest surface at step start
                int heir = -1; double bestGap = double.PositiveInfinity;
                foreach (int j in pullersPre)
                {
                    if (!w.Alive[j] || w.Gen[j] != preGen[j]) continue;
                    double gap = Math.Sqrt((preX[i] - preX[j]) * (preX[i] - preX[j]) + (preY[i] - preY[j]) * (preY[i] - preY[j])) - preR[j];
                    if (gap < bestGap) { bestGap = gap; heir = j; }
                }
                if (heir == c0 && c0 >= 0) { heurCompact += preM[i]; if (IsBelt(preOrg[i])) bDeathHeurCompact += preM[i]; }
            }
            rockDeaths += nRock; pullerDeaths += nPuller;
            if (lost != 0) items.Add((lost, 2, -1));

            // ---- puller mass changes (direct), compact closure
            double gain = 0, loss = 0; var gainers = new List<int>();
            foreach (int j in pullersPre)
            {
                if (!w.Alive[j] || w.Gen[j] != preGen[j]) continue;
                double dm = w.M[j] - preM[j], tol = 64 * Ulp(w.M[j]);
                if (dm > tol) { gain += dm; gainers.Add(j); } else if (dm < -tol) loss += dm;
            }
            bool cSame = c0 >= 0 && w.Alive[c0] && w.Gen[c0] == cGen0;
            double dMc = cSame ? w.M[c0] - cm0 : 0; if (cSame) compactDMDirect += dMc;
            // puller merges into the compact, from the core's own event log (direct: victim slot, survivor slot A, victim mass B)
            double pullerIntoCPre = 0, pullerIntoCB = 0; var pullersIntoC = new HashSet<int>();
            var ev = w.Events;
            for (int k = ev.Count - 1; k >= 0 && ev[k].Year >= year0; k--)
                if (ev[k].RuleId == "contact" && ev[k].Change == "merge" && (int)ev[k].A == c0 && ev[k].ObjectSlot != c0 && ev[k].B >= attract)
                {
                    int v = ev[k].ObjectSlot; pullersIntoC.Add(v); pullerIntoCB += ev[k].B;
                    double pm = v < n0 && preAlive[v] ? preM[v] : double.NaN; pullerIntoCPre += pm;
                    double carried = v < n0 && pullerGen[v] == preGen[v] ? pullerBelt[v] : 0;
                    bViaPullerIntoCompact += carried;
                    pullerLog.Add($"step {step}: puller slot {v} gen {ev[k].C} m_at_merge={ev[k].B / World.EarthMass:G6} Earth (pre-step {pm / World.EarthMass:G6}) -> compact slot {c0} (Events, direct); belt mass it carried by closure {R(carried)}; gained in-step before merging {R(ev[k].B - pm)}");
                }
            foreach (var it in items) if (it.Kind == 1 && !pullersIntoC.Contains(it.Slot)) pullerLog.Add($"step {step}: puller slot {it.Slot} m={it.Mass / World.EarthMass:G6} Earth died, not into compact per Events");
            compactFromPullerEvents += pullerIntoCPre;

            double totalIn = rockMass + pullerMass + lost;
            double closure = gain + loss - totalIn;
            double ulpMax = Math.Max(cSame ? Ulp(w.M[c0]) : 0, gainers.Select(j => Ulp(w.M[j])).DefaultIfEmpty(0).Max());
            double tolStep = 64 * (items.Count + 2) * ulpMax;
            stepClosureMaxAbs = Math.Max(stepClosureMaxAbs, Math.Abs(closure));
            bool closes = Math.Abs(closure) <= tolStep && loss == 0;
            if (!closes && items.Count > 0) stepsClosureFail++;
            double tolC = 64 * (items.Count + 2) * (cSame ? Ulp(w.M[c0]) : 0);
            // items lighter than the rounding of the receiving masses cannot be located by mass closure at all
            var rockItems = items.Where(t => t.Kind != 1 && t.Mass > tolStep).ToList();
            var tinyItems = items.Where(t => t.Kind != 1 && t.Mass <= tolStep).ToList();
            double needC = dMc - pullerIntoCPre; // compact gain not explained by pre-step masses of pullers it swallowed
            string mode; long maskC = 0;
            if (items.Count == 0 && Math.Abs(needC) <= tolC) mode = "none";
            else if (!cSame) mode = "compact_changed";
            else if (!closes) mode = "closure_fail";
            else if (rockItems.Count == 0) mode = Math.Abs(needC) <= tolC ? "only_tiny_or_pullers" : "no_subset";
            else if (Math.Abs(needC) <= tolC) mode = "no_compact_gain";
            else if (gainers.Count == 1 && gainers[0] == c0) { mode = "only_compact_gained"; maskC = -1; }
            else if (rockItems.Count <= 20)
            {
                int n = rockItems.Count, found = 0; long mask = 0;
                for (long s = 1; s < (1L << n) && found < 2; s++)
                {
                    double sum = 0; for (int b = 0; b < n; b++) if ((s >> b & 1) != 0) sum += rockItems[b].Mass;
                    if (Math.Abs(sum - needC) <= tolC) { found++; mask = s; }
                }
                if (found == 1) { mode = "unique_subset"; maskC = mask; } else mode = found == 0 ? "no_subset" : "ambiguous";
            }
            else mode = "too_many";
            bool resolved = mode is "none" or "only_tiny_or_pullers" or "no_compact_gain" or "only_compact_gained" or "unique_subset";
            if (resolved) stepsUnique += mode is "only_compact_gained" or "unique_subset" ? 1 : 0;
            if (mode == "no_compact_gain") stepsNoCompactGain++;
            if (!resolved && items.Count > 0) stepsAmbiguous++;
            // single non-compact gainer: belt mass it absorbed is carried with it (closure)
            int otherGainer = gainers.Count(j => j != c0) == 1 ? gainers.First(j => j != c0) : -1;
            double cRock = 0, cLost = 0;
            for (int b = 0; b < rockItems.Count; b++)
            {
                var it = rockItems[b]; bool inC = resolved && maskC != 0 && (maskC == -1 || (maskC >> b & 1) != 0);
                if (inC) { if (it.Kind == 2) cLost += it.Mass; else cRock += it.Mass; }
                bool belt = it.Kind == 0 ? IsBelt(preOrg[it.Slot]) : allBelt && anyBelt;
                bool unknownOrg = it.Kind == 2 && !(allBelt && anyBelt) && anyBelt;
                if (unknownOrg) { bUnknownOrigin += it.Mass; continue; }
                if (!belt) continue;
                if (inC)
                {
                    if (it.Kind == 0)
                    {
                        bDeathCompact += it.Mass; rockDeathsCompact++;
                        double dx = preX[it.Slot] - preX[c0], dy = preY[it.Slot] - preY[c0], rr = Math.Sqrt(dx * dx + dy * dy) / rc0;
                        deathCompactPreMinRc = Math.Min(deathCompactPreMinRc, rr); deathCompactPreMaxRc = Math.Max(deathCompactPreMaxRc, rr);
                    }
                    else bLostCompact += it.Mass;
                }
                else if (resolved)
                {
                    if (it.Kind == 0) bDeathOtherClosure += it.Mass; else bLostOther += it.Mass;
                    if (otherGainer >= 0) { if (pullerGen[otherGainer] != w.Gen[otherGainer]) { pullerGen[otherGainer] = w.Gen[otherGainer]; pullerBelt[otherGainer] = 0; } pullerBelt[otherGainer] += it.Mass; }
                    else bCarriedUnknownSplit += it.Mass;
                }
                else { if (it.Kind == 0) { bDeathUnknown += it.Mass; rockDeathsAmb++; } else bLostUnknown += it.Mass; }
            }
            foreach (var it in tinyItems)
            {
                bool belt = it.Kind == 0 ? IsBelt(preOrg[it.Slot]) : allBelt && anyBelt;
                if (belt) { bTinyUnknown += it.Mass; tinyDeaths++; if (mode == "only_compact_gained") bTinyOnlyCompactStep += it.Mass; }
                if (mode == "only_compact_gained") tinyAllOnlyCompactStep += it.Mass;
            }
            if (!resolved) compactUnexplained += needC;
            compactFromRocksClosure += cRock; compactFromLostClosure += cLost;

            // ---- observed approach at this step boundary (all non-pulling objects, bound or not) + periapsis-passage estimate
            int c1 = Compact(); double rc1 = c1 >= 0 ? w.R[c1] : double.NaN;
            double obsStep = double.PositiveInfinity; int lt10 = 0, lt100 = 0, lt1000 = 0, ppN = 0; double ppMin = double.PositiveInfinity;
            if (c1 >= 0)
                for (int i = 0; i < w.N; i++)
                {
                    if (!w.Alive[i] || w.Attracts(i)) continue;
                    double dx = w.X[i] - w.X[c1], dy = w.Y[i] - w.Y[c1], r = Math.Sqrt(dx * dx + dy * dy) / rc1;
                    if (r < obsStep) obsStep = r;
                    long key = Key(i, w.Gen[i]);
                    if (r < 1000) { lt1000++; seen1000.Add(key); }
                    if (r < 100) { lt100++; seen100.Add(key); }
                    if (r < 10) { lt10++; seen10.Add(key); }
                    if (c1 == c0 && cSame && i < n0 && preAlive[i] && preGen[i] == w.Gen[i])
                    {
                        double px = preX[i] - preX[c0], py = preY[i] - preY[c0], pvx = preVx[i] - preVx[c0], pvy = preVy[i] - preVy[c0];
                        double rdot0 = px * pvx + py * pvy;
                        double vx = w.Vx[i] - w.Vx[c1], vy = w.Vy[i] - w.Vy[c1], rdot1 = dx * vx + dy * vy;
                        if (rdot0 < 0 && rdot1 > 0)
                        {
                            double mu = gC * (cm0 + preM[i]), rp0 = Math.Sqrt(px * px + py * py), eps = (pvx * pvx + pvy * pvy) / 2 - mu / rp0, h = px * pvy - py * pvx;
                            double ecc = Math.Sqrt(Math.Max(0, 1 + 2 * eps * h * h / (mu * mu))), q = h * h / (mu * (1 + ecc)) / rc0;
                            ppN++; if (q < ppMin) ppMin = q;
                            if (q < 1000) periPassLt1000++;
                            if (q < periPassMinRc) { periPassMinRc = q; periPassMinStep = step; }
                        }
                    }
                }
            periPassCount += ppN;
            if (obsStep < obsMinRc) { obsMinRc = obsStep; obsMinStep = step; }

            steps.WriteLine(string.Join(",", new object[] { step, w.Live, w.N, w.Merges - merges0, c0, cGen0, R(cSame ? w.M[c0] : double.NaN), G(rc0), R(dMc),
                nRock, R(rockMass), nPuller, R(pullerMass), R(pullerIntoCPre),
                dis.Count - dis0, R(srcMass), R(escStep), R(dRes), R(represented), R(fragAlive),
                nInstep, R(lost), R(gain), R(loss), R(closure), mode, R(cRock + cLost),
                R(heurCompact), R(consResid), G(obsStep), lt10, lt100, lt1000, ppN, G(ppMin), mapOk ? 1 : 0 }));

            if (step % every == 0 || step == maxSteps) { Sample(step); cp.WriteLine(Checkpoint(w, step, ns)); cp.Flush(); }
        }

        string[] hooks = {
            "merge target per rock: Merge(a,b) (core/World.cs:209-232) increments Merges but logs only victims >= AttractMass (core/World.cs:225); a hook (victim slot, gen, survivor slot, gen, mass, substep) is needed to replace mass-closure attribution",
            "substep positions: Advance runs Sub=8 slices (core/World.cs:30, :307); no per-substep callback, so closest approach inside a step is unobserved",
            "fragments born and dead in one step: BreakRoche Add (core/Roche.cs:401) and a later Merge/Kill in the same Advance leave no record; the RocheDisruption record has no fragment count/mass (core/Roche.cs:33-34)",
            "Kill vs merge for rocks: Kill (core/Commands.cs:155-159) after EscapeRole (core/Escape.cs:45) does not record source/cause in EscapedTransfers (core/Escape.cs:30-46)",
            "Roche reservoir entries carry no event id (core/Roche.cs:31-32, :296-300); mapping to RocheDisruption is by append order",
        };
        File.WriteAllText(Path.Combine(outDir, $"seed{seed}-summary.txt"), string.Join("\n", new[] {
            $"seed {seed} (p0-probe-v2) steps after NS drop {maxSteps}, threads {w.Threads}, hash {w.Hash():X16}, wall {sw.Elapsed.TotalSeconds:F1}s",
            $"belt initial (direct): objects {beltCount0}, mass {R(beltMass0)}",
            $"global conservation per step (direct): max |dM_alive + dEscaped + dReservoir| = {R(consResidMaxAbs)}, sum = {R(consResidSum)}",
            $"roche events {rocheEvents} (direct), zero-fragment-alive-at-step-end events {zeroFragEvents}, in-step-born sources {instepSources}, reservoir mapping failures {resMapFail}",
            $"in-step lost fragment mass (closure, destination per compact closure below): total {R(lostTotal)}, steps with loss {instepLostEvents}",
            $"rock deaths (direct) {rockDeaths}; belt rock deaths into compact by closure {rockDeathsCompact}; belt rock deaths unresolved {rockDeathsAmb}; puller deaths {pullerDeaths}",
            $"compact closure steps: resolved into compact {stepsUnique}, no compact gain {stepsNoCompactGain}, unresolved (ambiguous/no-subset/too-many/closure-fail/compact-changed) {stepsAmbiguous}; steps where all puller gains != all deaths (closure fail) {stepsClosureFail}; max |step closure| {R(stepClosureMaxAbs)}",
            $"compact dM direct {R(compactDMDirect)} = puller merges (Events, direct) {R(compactFromPullerEvents)} + rocks (closure) {R(compactFromRocksClosure)} + in-step lost (closure) {R(compactFromLostClosure)} + unexplained {R(compactUnexplained)}; remainder {R(compactDMDirect - compactFromPullerEvents - compactFromRocksClosure - compactFromLostClosure - compactUnexplained)}",
            $"belt -> compact: closure {R(bDeathCompact)} (rocks) + {R(bLostCompact)} (in-step lost) + {R(bViaPullerIntoCompact)} (carried by a puller that later merged into compact) vs round-1 heuristic est {R(bDeathHeurCompact)}",
            $"belt deaths below rounding (unknown destination): {tinyDeaths} occupants, mass {R(bTinyUnknown)}; belt to other pullers with unknown split {R(bCarriedUnknownSplit)}; tiny deaths in steps where only the compact gained mass (compact or Kill, unresolvable): belt {R(bTinyOnlyCompactStep)}, all {R(tinyAllOnlyCompactStep)}",
            $"observed approach (direct, at step boundaries, all non-pulling objects): min r {G(obsMinRc)} Rc at step {obsMinStep}; distinct occupants ever < 1000 Rc: {seen1000.Count}, < 100 Rc: {seen100.Count}, < 10 Rc: {seen10.Count}",
            $"periapsis passed between boundaries (est, two-body q at step start): count {periPassCount}, min {G(periPassMinRc)} Rc at step {periPassMinStep}, with q < 1000 Rc: {periPassLt1000}",
            $"belt rocks merged into compact by closure: start-of-step distance {G(deathCompactPreMinRc)}..{G(deathCompactPreMaxRc)} Rc",
            "hooks missing (unknown until added; not added in this round):", string.Join("\n", hooks.Select(h => "  - " + h)),
            "puller log:", string.Join("\n", pullerLog) }) + "\n");
        return 0;
    }
}
