using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Cosmos.Core;

// P0 disc probe v3 (Hark, issue #2, round 3, Claire): P0 re-measured on the disc core (claire/heat-disc @c4598a5),
// law ON and OFF. Measure-only: reads public World state between Advance calls; core/ is untouched.
// Scene = P0ProbeV2.Scene (identical to cli/DiscChecks.cs P0: SolSystem(5000,seed), DiscOn set, 96 x Advance(.5), NS drop).
//
// Classes, as in v2:  direct = read from core state/records or an exact sum of them;  closure = assigned by exact
// mass closure of direct reads in one step (unique solution only);  est = formula on step-boundary state (the core
// acts at substep positions the API does not expose);  unknown = not observable, the summary names why.
//
//   cli p0-disc-v3 <seed> <steps> <outDir>        (P0_DISC=0|1 and P0_THREADS=1 in the environment)
static class P0DiscV3
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static string R(double x) => x.ToString("R", Inv);
    static string G(double x) => x.ToString("G6", Inv);
    static long Key(int i, int g) => ((long)i << 32) | (uint)g;

    static readonly string[] Origins = { "belt_rock", "kuiper_ice", "saturn_ring_ice", "moon", "planet", "sun", "compact", "other", "unknown" };
    const int OBelt = 0, OKuiper = 1, ORing = 2, OMoon = 3, OPlanet = 4, OSun = 5, OCompact = 6, OOther = 7, OUnknown = 8;
    static readonly string[] Forms = { "disc", "ring", "stream" };
    static int FormIndex(string f) => f == "disc" ? 0 : f == "ring" ? 1 : 2;

    public static int Run(string[] args)
    {
        ulong seed = ulong.Parse(args[1], Inv); int steps = int.Parse(args[2], Inv); string outDir = args[3];
        Directory.CreateDirectory(outDir);
        var w = P0ProbeV2.Scene(seed, out int ns);
        int cap = w.X.Length, ne = w.ElementCount; double gC = w.C.G, vapor = w.C.DiscVaporEnergy, earth = World.EarthMass;
        int sunSlot = w.Heaviest() == ns ? -1 : w.Heaviest();
        for (int i = 0; i < w.N; i++) if (w.Alive[i] && w.Name[i] == "Mặt Trời") sunSlot = i;

        // ---- origin per occupant (slot, Gen), persists after death so a breakup record can be traced
        var origin = new Dictionary<long, int>();
        int Classify(int i)
        {
            if (i == ns) return OCompact;
            if (i == sunSlot) return OSun;
            int g = w.Grp[i];
            if (g == 1) return OBelt; if (g == 2) return OKuiper; if (g == 3) return ORing;
            if (w.Name[i] == "Mặt Trăng") return OMoon;
            if (g == 0 && w.Attracts(i)) return OPlanet;
            return OOther;
        }
        for (int i = 0; i < w.N; i++) if (w.Alive[i]) origin[Key(i, w.Gen[i])] = Classify(i);

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
        string HostKind(int h, int c) => h == c ? "compact" : h == sunSlot ? "sun" : h >= 0 && w.Alive[h] ? (w.Attracts(h) ? "puller" : "small") : "gone";

        // ---- ledger start (direct)
        double[] Ledger()
        {
            var a = new double[3 + ne]; a[0] = w.EscapedMass; a[1] = w.EscapedPx; a[2] = w.EscapedPy;
            for (int e = 0; e < ne; e++) a[e + 3] = w.EscapedMatter[e] - w.NucleosynthesisDelta[e];
            for (int i = 0; i < w.N; i++) if (w.Alive[i])
            {
                a[0] += w.M[i]; a[1] += w.M[i] * w.Vx[i]; a[2] += w.M[i] * w.Vy[i];
                for (int e = 0; e < ne; e++) a[e + 3] += w.Comp[i * ne + e];
            }
            foreach (var r in w.RocheReservoirs) { a[0] += r.Mass; a[1] += r.Px; a[2] += r.Py; for (int e = 0; e < ne; e++) a[e + 3] += r.Matter[e]; }
            foreach (var d in w.Discs)
            {
                if (d.Host < 0) continue; // frozen discs: counted separately in frozenDisc
                a[0] += d.Total; a[1] += d.Total * w.Vx[d.Host]; a[2] += d.Total * w.Vy[d.Host];
                for (int k = 0; k < d.Cells; k++) for (int e = 0; e < ne; e++) a[e + 3] += w.DiscCellMatter(d, k, e);
            }
            return a;
        }
        var start = Ledger();
        double pScale = 0; for (int i = 0; i < w.N; i++) if (w.Alive[i]) pScale += w.M[i] * Math.Sqrt(w.Vx[i] * w.Vx[i] + w.Vy[i] * w.Vy[i]);
        int startLive = w.Live;

        // ---- outputs
        using var stepsCsv = new StreamWriter(Path.Combine(outDir, $"seed{seed}-steps.csv"));
        stepsCsv.WriteLine("step,live_direct,N_direct,merges_direct,breaks_disc_direct,breaks_ring_direct,breaks_stream_direct,new_disc_direct,new_ring_direct,new_stream_direct," +
            "disc_count_direct,disc_mass_all_E_direct,disc_mass_on_compact_E_direct,disc_mass_on_sun_E_direct,disc_accreted_E_direct,compact_slot,compact_phase,compact_mass_E_direct," +
            "compact_merges_events_direct,compact_merges_closure,compact_merges_ambiguous_steps,compact_victim_mass_E_closure,reservoir_mass_E_direct,escaped_mass_E_direct," +
            "to_disc_from_" + string.Join("_E_direct,to_disc_from_", Origins) + "_E_direct," +
            "to_disc_on_compact_E_direct,to_disc_straight_fall_E_closure,near_rmax_est,near_rmax_body,near_total_est,near_unbound_est,near_outside_own_roche_hot_est,near_outside_own_roche_cold_est,near_inside_own_roche_hot_est,near_inside_own_roche_cold_est,ms_per_advance");
        using var recCsv = new StreamWriter(Path.Combine(outDir, $"seed{seed}-records.csv"));
        recCsv.WriteLine("step,index,formation_direct,host_slot,host_kind,source_slot,source_gen,source_origin,origin_class,source_mass_E_direct,captured_mass_E_direct,escaped_mass_E_direct,bound_fraction_direct,dissipated_direct,dissipated_over_vapor_direct," +
            "excess_over_vapor_est,specific_energy_est,source_dist_over_rocheLimit_est");
        using var nearCsv = new StreamWriter(Path.Combine(outDir, $"seed{seed}-near.csv"));
        nearCsv.WriteLine("step,slot,gen,origin,mass_E,dist_over_rmax,dist_over_own_roche,specific_energy_est,bound_est,excess_over_vapor_est,circular_over_hostR_est,reason_est");
        using var ledCsv = new StreamWriter(Path.Combine(outDir, $"seed{seed}-ledger.csv"));
        ledCsv.WriteLine("step,mass_total_direct,mass_resid_rel,alive_mass,disc_mass,reservoir_mass,escaped_mass,accreted_in_host_mass_info,frozen_disc_mass,px_resid_over_sum_m_v,py_resid_over_sum_m_v," +
            string.Join(",", Enumerable.Range(0, ne).Select(e => $"elem_{w.Elements[e].Name}_resid_rel")));
        var cp = new StreamWriter(Path.Combine(outDir, $"seed{seed}-checkpoints.csv"));
        cp.WriteLine(P0ProbeV2.CheckpointHeader); cp.WriteLine(P0ProbeV2.Checkpoint(w, 0, ns));

        // ---- cumulative counters
        var formCum = new long[3]; var toDiscByOrigin = new double[Origins.Length];
        double toDiscOnCompact = 0, straightFall = 0, compactVictimMass = 0; long compactMergesEvents = 0, compactMergesClosure = 0, ambiguousSteps = 0;
        int recSeen = w.RocheDisruptions.Count;
        var preAlive = new bool[cap]; var preGen = new int[cap]; var preM = new double[cap];
        var preX = new double[cap]; var preY = new double[cap]; var preVx = new double[cap]; var preVy = new double[cap];
        var discRefs = new List<Disc>(); var discFed0 = new Dictionary<Disc, double>(); var discTot0 = new Dictionary<Disc, double>();
        var streams = new List<string>();
        var sw = Stopwatch.StartNew();
        int lastNearN = 0, lastNearUnb = 0, lastOutHot = 0, lastOutCold = 0, lastInHot = 0, lastInCold = 0; double lastRmax = 0; string lastRmaxBody = "";

        (double spec, double excessRatio, double circOverR, double rOverRoche) Energetics(double m, double x, double y, double vx, double vy, int c, double cx, double cy, double cvx, double cvy, double rl)
        {
            double pm = w.M[c] + w.DiscMassOn(c), dx = x - cx, dy = y - cy, r = Math.Sqrt(dx * dx + dy * dy), ux = vx - cvx, uy = vy - cvy;
            double mu = gC * pm, kin = (ux * ux + uy * uy) / 2, spec = kin - mu / r, h = dx * uy - dy * ux;
            double reduced = pm * m / (pm + m), angular = reduced * h, circ = Math.Pow(angular / m, 2) / (gC * pm);
            double before = reduced * kin - gC * pm * m / r, circE = circ > 0 ? -gC * pm * m / (2 * circ) : double.PositiveInfinity;
            return (spec, spec < 0 ? (before - circE) / (vapor * m) : double.NaN, circ / w.R[c], rl > 0 ? r / rl : double.NaN);
        }

        for (int step = 1; step <= steps; step++)
        {
            // pre-step snapshot
            int c0 = Compact(); int c0gen = c0 >= 0 ? w.Gen[c0] : -1; double c0M = c0 >= 0 ? w.M[c0] : 0;
            for (int i = 0; i < w.N; i++) { preAlive[i] = w.Alive[i]; preGen[i] = w.Gen[i]; preM[i] = w.M[i]; preX[i] = w.X[i]; preY[i] = w.Y[i]; preVx[i] = w.Vx[i]; preVy[i] = w.Vy[i]; }
            int preN = w.N; long preMerges = w.Merges; double preYear = w.Year;
            discFed0.Clear(); discTot0.Clear();
            foreach (var d in w.Discs) { if (!discRefs.Contains(d)) discRefs.Add(d); discFed0[d] = d.Fed; discTot0[d] = d.Total; }
            int preEsc = w.EscapedTransfers.Count;
            int preEvCount = w.Events.Count; RuleEvent? preEvLast = preEvCount > 0 ? w.Events[preEvCount - 1] : null;

            w.Advance(.5);

            int c1 = Compact();
            foreach (var d in w.Discs) if (!discRefs.Contains(d)) discRefs.Add(d);
            // new occupants -> origin
            var stepOrigins = new List<int>();
            for (int i = 0; i < w.N; i++) if (w.Alive[i] && (i >= preN || !preAlive[i] || preGen[i] != w.Gen[i]))
            {
                int o = OOther; string gname = w.Grp[i] < w.Groups.Count ? w.Groups[w.Grp[i]] : "";
                if (gname.StartsWith("roche.", StringComparison.Ordinal))
                {
                    var parts = gname.Split('.');
                    o = origin.TryGetValue(Key(int.Parse(parts[1], Inv), int.Parse(parts[2], Inv)), out int po) ? po : OUnknown;
                }
                origin[Key(i, w.Gen[i])] = o;
            }
            // breakup records of this step
            var newForm = new int[3]; double toDiscCapturedCompact = 0; double toDiscCapturedAll = 0;
            var recs = w.RocheDisruptions;
            for (int k = recSeen; k < recs.Count; k++)
            {
                var r = recs[k]; int f = FormIndex(r.Formation); newForm[f]++; formCum[f]++;
                string oclass = "direct";
                if (!origin.TryGetValue(Key(r.Source, r.SourceGeneration), out int o))
                {
                    // source born and broken inside this step: same origin as every earlier source of the step, if unique
                    var distinct = stepOrigins.Distinct().ToList();
                    if (distinct.Count == 1) { o = distinct[0]; oclass = "closure"; } else { o = OUnknown; oclass = "unknown"; }
                }
                stepOrigins.Add(o);
                if (f == 0)
                {
                    toDiscByOrigin[o] += r.CapturedMass; toDiscCapturedAll += r.CapturedMass;
                    if (r.Host == c1 || r.Host == c0) { toDiscOnCompact += r.CapturedMass; toDiscCapturedCompact += r.CapturedMass; }
                }
                // est energetics from the source's step-start state vs its host's step-start state
                string est = "NA,NA,NA";
                int s = r.Source, h = r.Host;
                if (s < preN && preAlive[s] && preGen[s] == r.SourceGeneration && h < preN && preAlive[h])
                {
                    double rl = w.Alive[h] ? SafeRoche(w, s, h) : double.NaN;
                    var en = Energetics(r.SourceMass, preX[s], preY[s], preVx[s], preVy[s], h, preX[h], preY[h], preVx[h], preVy[h], double.NaN);
                    est = $"{G(en.excessRatio)},{G(en.spec)},NA";
                }
                double bf = r.SourceMass > 0 ? r.CapturedMass / r.SourceMass : double.NaN;
                string line = $"{step},{k},{r.Formation},{h},{HostKind(h, c1)},{s},{r.SourceGeneration},{Origins[o]},{oclass},{R(r.SourceMass / earth)},{R(r.CapturedMass / earth)},{R(r.EscapedMass / earth)},{G(bf)},{R(r.DissipatedEnergy)},{G(r.CapturedMass > 0 ? r.DissipatedEnergy / (vapor * r.CapturedMass) : double.NaN)},{est}";
                recCsv.WriteLine(line);
                if (f == 2) streams.Add(line);
            }
            recSeen = recs.Count;
            // disc feed vs straight fall (closure): captured mass of disc records minus what discs were fed this step
            double fedStep = 0, fedCompact = 0, accCompactDisc = 0;
            foreach (var d in discRefs)
            {
                double fed0 = discFed0.TryGetValue(d, out var a0) ? a0 : 0, tot0 = discTot0.TryGetValue(d, out var t0) ? t0 : 0;
                double dfed = d.Fed - fed0; fedStep += dfed;
                bool onCompact = d.Host >= 0 && (d.Host == c1 || d.Host == c0);
                if (onCompact) { fedCompact += dfed; accCompactDisc += dfed - (d.Total - tot0); }
            }
            straightFall += toDiscCapturedAll - fedStep;
            // compact victim mass (closure): dM - accreted from its discs - straight falls into it
            long evMerges = 0; double evMass = 0;
            var evs = w.Events;
            // events of this step: merges log at Year == step start (core/World.cs:323,328), so Year cannot separate steps;
            // the log is bounded (core/Rules.cs:124), so locate the last pre-step event by value when it is full.
            int evStart = preEvCount;
            if (evs.Count == RuleEventCap && preEvLast is RuleEvent last) { evStart = 0; for (int k = evs.Count - 1; k >= 0; k--) if (evs[k].Equals(last)) { evStart = k + 1; break; } }
            for (int k = evStart; k < evs.Count; k++)
                if (evs[k].RuleId == "contact" && evs[k].Change == "merge" && c1 >= 0 && (int)evs[k].A == c1) { evMerges++; evMass += evs[k].B; }
            compactMergesEvents += evMerges;
            if (c0 >= 0 && c1 == c0 && w.Gen[c1] == c0gen)
            {
                double victim = (w.M[c1] - c0M) - accCompactDisc - (toDiscCapturedCompact - fedCompact);
                double tol = 64 * 8 * (Math.BitIncrement(w.M[c1]) - w.M[c1]);
                compactVictimMass += victim;
                // deaths this step that are not Roche sources and not escapes: merges or kills
                var esc = new HashSet<long>(); var et = w.EscapedTransfers; for (int k = preEsc; k < et.Count; k++) esc.Add(Key(et[k].Source, et[k].Generation));
                var srcs = new HashSet<long>(); for (int k = recs.Count - newForm.Sum(); k < recs.Count; k++) srcs.Add(Key(recs[k].Source, recs[k].SourceGeneration));
                double deadMass = 0; int dead = 0;
                for (int i = 0; i < preN; i++) if (preAlive[i] && (!w.Alive[i] || w.Gen[i] != preGen[i]) && i != c0 && !srcs.Contains(Key(i, preGen[i])) && !esc.Contains(Key(i, preGen[i]))) { deadMass += preM[i]; dead++; }
                if (Math.Abs(victim) <= tol) { }                       // nothing merged into the compact
                else if (dead > 0 && Math.Abs(victim - deadMass) <= tol + 64 * dead * (Math.BitIncrement(deadMass) - deadMass)) compactMergesClosure += dead;
                else ambiguousSteps++;                                   // in-step-born victims or a split among gainers
            }

            bool sample = step <= 300 || step % 50 == 0 || step == steps;
            if (step % 50 == 0 || step == steps) cp.WriteLine(P0ProbeV2.Checkpoint(w, step, ns));
            if (!sample) continue;

            // near-compact census (est, step boundary)
            if (c1 >= 0)
            {
                bool listAll = step % 50 == 0 || step == steps;
                double rmax = 0; string rbody = "";
                var rl = new double[w.N];
                for (int i = 0; i < w.N; i++) if (w.Alive[i] && i != c1) { rl[i] = SafeRoche(w, i, c1); if (rl[i] > rmax) { rmax = rl[i]; rbody = $"{i}:{Origins[origin.GetValueOrDefault(Key(i, w.Gen[i]), OUnknown)]}"; } }
                int n = 0, unb = 0, oh = 0, oc = 0, ih = 0, ic = 0;
                for (int i = 0; i < w.N; i++) if (w.Alive[i] && i != c1)
                {
                    double dx = w.X[i] - w.X[c1], dy = w.Y[i] - w.Y[c1], dist = Math.Sqrt(dx * dx + dy * dy);
                    if (!(dist < rmax)) continue;
                    n++;
                    var en = Energetics(w.M[i], w.X[i], w.Y[i], w.Vx[i], w.Vy[i], c1, w.X[c1], w.Y[c1], w.Vx[c1], w.Vy[c1], rl[i]);
                    bool bound = en.spec < 0, inside = dist <= rl[i], hot = bound && en.excessRatio >= 1;
                    string reason = !bound ? "unbound" : inside ? (hot ? "inside_own_roche_hot_not_yet_broken" : "inside_own_roche_cold") : (hot ? "outside_own_roche_hot_not_broken" : "outside_own_roche_cold");
                    if (!bound) unb++; else if (inside) { if (hot) ih++; else ic++; } else { if (hot) oh++; else oc++; }
                    if (listAll) nearCsv.WriteLine($"{step},{i},{w.Gen[i]},{Origins[origin.GetValueOrDefault(Key(i, w.Gen[i]), OUnknown)]},{R(w.M[i] / earth)},{G(dist / rmax)},{G(dist / rl[i])},{G(en.spec)},{(bound ? 1 : 0)},{G(en.excessRatio)},{G(en.circOverR)},{reason}");
                }
                lastNearN = n; lastNearUnb = unb; lastOutHot = oh; lastOutCold = oc; lastInHot = ih; lastInCold = ic; lastRmax = rmax; lastRmaxBody = rbody;
            }
            double discAll = 0, discC = 0, discSun = 0, frozen = 0;
            foreach (var d in w.Discs) { discAll += d.Total; if (d.Host == c1) discC += d.Total; if (d.Host == sunSlot && sunSlot >= 0 && w.Alive[sunSlot]) discSun += d.Total; if (d.Host < 0) frozen += d.Total; }
            double res = 0; foreach (var r in w.RocheReservoirs) res += r.Mass;
            stepsCsv.WriteLine(string.Join(",", new object[] { step, w.Live, w.N, w.Merges, formCum[0], formCum[1], formCum[2], newForm[0], newForm[1], newForm[2],
                w.Discs.Count, R(discAll / earth), R(discC / earth), R(discSun / earth), R(w.DiscAccretedMass / earth), c1, c1 >= 0 ? w.StarPhaseOf(c1).ToString() : "none", R(c1 >= 0 ? w.M[c1] / earth : 0),
                compactMergesEvents, compactMergesClosure, ambiguousSteps, R(compactVictimMass / earth), R(res / earth), R(w.EscapedMass / earth) })
                + "," + string.Join(",", toDiscByOrigin.Select(x => R(x / earth))) + "," + string.Join(",", new object[] {
                R(toDiscOnCompact / earth), R(straightFall / earth), G(lastRmax), lastRmaxBody, lastNearN, lastNearUnb, lastOutHot, lastOutCold, lastInHot, lastInCold, (sw.Elapsed.TotalMilliseconds / step).ToString("F2", Inv) }));
            var now = Ledger(); double alive = 0; for (int i = 0; i < w.N; i++) if (w.Alive[i]) alive += w.M[i];
            ledCsv.WriteLine(string.Join(",", new object[] { step, R(now[0]), G((now[0] - start[0]) / start[0]), R(alive), R(discAll - frozen), R(res), R(w.EscapedMass), R(w.DiscAccretedMass), R(frozen),
                G((now[1] - start[1]) / pScale), G((now[2] - start[2]) / pScale) }) + "," +
                string.Join(",", Enumerable.Range(0, ne).Select(e => start[e + 3] != 0 ? G((now[e + 3] - start[e + 3]) / start[e + 3]) : (now[e + 3] == 0 ? "0" : "inf"))));
        }
        cp.Dispose();
        double discEnd = 0; foreach (var d in w.Discs) discEnd += d.Total;
        var sum = new List<string>
        {
            $"seed {seed} (p0-disc-v3) DiscOn {w.C.DiscOn} steps {steps} threads {w.Threads} hash {w.Hash():X16} wall {sw.Elapsed.TotalSeconds:F1}s",
            $"start live {startLive}, end live {w.Live}, capacity {cap}",
            $"breaks cumulative (direct): disc {formCum[0]} ring {formCum[1]} stream {formCum[2]}",
            $"disc mass end (direct) {discEnd / earth:G6} E; accreted (direct) {w.DiscAccretedMass / earth:G6} E; to-disc on compact (direct) {toDiscOnCompact / earth:G6} E; straight fall (closure) {straightFall / earth:G6} E",
            "to-disc by source origin (direct, E): " + string.Join(" ", Origins.Select((o, k) => $"{o}={toDiscByOrigin[k] / earth:G6}")),
            $"compact merges: events(direct, victims>=AttractMass) {compactMergesEvents}; closure {compactMergesClosure}; ambiguous steps {ambiguousSteps}; victim mass (closure) {compactVictimMass / earth:G6} E",
            $"streams: {streams.Count}",
        };
        File.WriteAllLines(Path.Combine(outDir, $"seed{seed}-summary.txt"), sum);
        return 0;
    }

    const int RuleEventCap = 1024; // core/Rules.cs:69 EventCapacity
    static double SafeRoche(World w, int body, int host) { try { return w.RocheLimit(body, host); } catch { return double.NaN; } }
}
