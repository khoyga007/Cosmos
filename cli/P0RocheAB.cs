using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Cosmos.Core;

// Vong 3 (chi Celine giao, Cosmos issue #1): doi chung nguyen nhan P0 bang bat/tat Roche.
// Cau hoi cho Yang: Roche dong gop bao nhieu vao viec sinh them vat the va lam day
// suc chua sau khi tha sao neutron? Tat rieng Roche co tranh duoc cham tran khong?
// Day la thi nghiem chan doan, KHONG phai de xuat tat Roche trong game.
// Harness cli-only. Khong doi core / vat ly.
static class P0RocheAB
{
    const int Rocks = 5000;
    const int Warmup = 96;
    const int Steps = 2000;
    static readonly ulong[] Seeds = { 1234, 1002, 1011 };

    // Ban sao y het cli/NsDropChecks.cs:Drop — cung canh tha NS nhu cac run soak.
    static int Drop(World w)
    {
        int ns = w.Do(new Command(CmdKind.Create, X: 100, Amount: 466700 * World.EarthMass, Mix: w.Mix(("gas", 1))));
        double birth = 10 * w.C.StarSolarMass, end = 1 + w.C.StarGiantFraction;
        if (w.Do(new Command(CmdKind.SetStarState, Target: ns, StarState: new StellarState(w.StarLifetime(birth) * end, end, birth))) < 0)
            throw new InvalidOperationException("NS fixture refused its stellar state.");
        return ns;
    }

    static double MassLedger(World w)
    {
        double s = w.EscapedMass;
        for (int i = 0; i < w.N; i++) if (w.Alive[i]) s += w.M[i];
        foreach (var r in w.RocheReservoirs) s += r.Mass;
        return s;
    }

    sealed class VariantRun
    {
        public readonly ulong Seed; public readonly string Variant;
        public readonly World W; public readonly int NsSlot;
        public int FirstFullStep = -1;
        public int MaxLive;
        public long FragSpawned, NsAbsorbed;
        public bool EventsEverFull;
        public double Ledger0;
        public int Disrupt0;
        double lastEvtYear;
        readonly StreamWriter csv;

        public VariantRun(ulong seed, string variant, StreamWriter csv)
        {
            Seed = seed; Variant = variant; this.csv = csv;
            W = World.SolSystem(Rocks, seed);
            W.Threads = 1;
            for (int k = 0; k < Warmup; k++) W.Advance(.5);
            NsSlot = -1;
            if (variant != "A") NsSlot = Drop(W);
            if (variant == "C")
            {
                W.Do(new Command(CmdKind.SetRule, Name: "roche", Amount: 0));
            }
            Disrupt0 = W.RocheDisruptions.Count;
            Ledger0 = MassLedger(W);
            MaxLive = W.Live;
            lastEvtYear = W.Year;
        }

        void CountNewEvents()
        {
            if (W.Events.Count == World.EventCapacity) EventsEverFull = true;
            foreach (var e in W.Events)
            {
                if (!(e.Year > lastEvtYear)) continue;
                if (e.RuleId == "roche" && (e.Change == "roche.ring" || e.Change == "roche.stream"))
                    FragSpawned += (long)e.C;
                else if (e.RuleId == "contact" && e.Change == "merge" && (int)e.A == NsSlot)
                    NsAbsorbed++;
            }
            lastEvtYear = W.Year;
        }

        public void Sample()
        {
            CountNewEvents();
            if (W.Live > MaxLive) MaxLive = W.Live;
            int compactAlive = 0; double compactMass = 0;
            for (int i = 0; i < W.N; i++)
                if (W.Alive[i] && W.StarPhaseOf(i) != StarPhase.None) { compactAlive++; compactMass += W.M[i]; }
            double resMass = 0; foreach (var r in W.RocheReservoirs) resMass += r.Mass;
            double ledger = MassLedger(W);
            double resid = Math.Abs(ledger - Ledger0);
            bool nsAlive = NsSlot >= 0 && W.Alive[NsSlot];
            var iv = CultureInfo.InvariantCulture;
            csv.WriteLine(string.Join(",", Seed, Variant, W.Step, W.Year.ToString("F2", iv),
                W.Live, W.N, W.X.Length, W.Merges, W.RocheDisruptions.Count, W.RocheReservoirs.Count,
                resMass.ToString("E6", iv),
                NsSlot, nsAlive ? W.Gen[NsSlot] : -1, nsAlive ? 1 : 0,
                nsAlive ? W.StarPhaseOf(NsSlot).ToString() : "dead",
                nsAlive ? W.M[NsSlot].ToString("E6", iv) : "0",
                nsAlive ? W.R[NsSlot].ToString("E6", iv) : "0",
                compactAlive, compactMass.ToString("E6", iv),
                W.EscapedMass.ToString("E6", iv),
                resid.ToString("E6", iv), (resid / Math.Abs(Ledger0)).ToString("E3", iv),
                FragSpawned, NsAbsorbed, FirstFullStep >= 0 ? 1 : 0));
        }

        public void Advance()
        {
            W.Advance(.5);
            // Kiem tra cham tran moi buoc (dinh nghia: Live == suc chua, khong con slot trong).
            if (FirstFullStep < 0 && W.Live == W.X.Length) FirstFullStep = (int)W.Step;
        }
    }

    public static bool Run(string outDir)
    {
        bool ok = true;
        Directory.CreateDirectory(outDir);
        string csvPath = Path.Combine(outDir, "p0-roche-ab.csv");
        using var csv = new StreamWriter(csvPath);
        csv.WriteLine("seed,variant,step,year,live,n,capacity,merges,disruptions,reservoirs,reservoir_mass," +
            "ns_slot,ns_gen,ns_alive,ns_phase,ns_mass,ns_radius,compact_alive,compact_mass,escaped_mass," +
            "ledger_resid_abs,ledger_resid_rel,frag_spawned_cum,ns_absorbed_cum,full");
        var summaries = new List<string>();
        summaries.Add("seed,variant,steps,first_full_step,max_live,final_live,final_merges,final_disruptions," +
            "final_reservoirs,final_reservoir_mass,frag_spawned_total,ns_absorbed_total,ns_final_alive," +
            "ns_final_phase,ns_final_mass,ledger_resid_abs,ledger_resid_rel,roche_off_verified,wall_seconds,events_buffer_ever_full");

        var finals = new Dictionary<string, (ulong hash, int live, int n, long merges, int disr, int resv, double esc)>();

        foreach (ulong seed in Seeds)
        {
            foreach (string variant in new[] { "A", "B", "C" })
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var r = new VariantRun(seed, variant, csv);
                r.Sample(); // step 0 (sau warmup + drop)
                for (int s = 1; s <= Steps; s++)
                {
                    r.Advance();
                    if (s <= 300 || s % 50 == 0) r.Sample();
                }
                // Dam bao mau cuoi duoc ghi ngay ca khi 2000 khong roi vao chu ky 50.
                r.Sample();
                sw.Stop();
                csv.Flush();

                var w = r.W;
                bool rocheOffOk = variant != "C" || w.RocheDisruptions.Count == r.Disrupt0;
                if (!rocheOffOk)
                {
                    Console.WriteLine($"FAILED p0rocheab: seed {seed} variant C them disruption ({r.Disrupt0} -> {w.RocheDisruptions.Count})");
                    ok = false;
                }
                double resMass = 0; foreach (var x in w.RocheReservoirs) resMass += x.Mass;
                double ledger = MassLedger(w);
                double resid = Math.Abs(ledger - r.Ledger0);
                bool nsAlive = r.NsSlot >= 0 && w.Alive[r.NsSlot];
                var iv = CultureInfo.InvariantCulture;
                summaries.Add(string.Join(",", seed, variant, Steps, r.FirstFullStep, r.MaxLive,
                    w.Live, w.Merges, w.RocheDisruptions.Count, w.RocheReservoirs.Count,
                    resMass.ToString("E6", iv), r.FragSpawned, r.NsAbsorbed,
                    nsAlive ? 1 : 0, nsAlive ? w.StarPhaseOf(r.NsSlot).ToString() : "dead",
                    nsAlive ? w.M[r.NsSlot].ToString("E6", iv) : "0",
                    resid.ToString("E6", iv), (resid / Math.Abs(r.Ledger0)).ToString("E3", iv),
                    variant == "C" ? (rocheOffOk ? 1 : 0) : -1,
                    sw.Elapsed.TotalSeconds.ToString("F1", iv), r.EventsEverFull ? 1 : 0));
                finals[$"{seed}/{variant}"] = (w.Hash(), w.Live, w.N, w.Merges,
                    w.RocheDisruptions.Count, w.RocheReservoirs.Count, w.EscapedMass);
                Console.WriteLine($"p0rocheab: seed {seed} variant {variant} xong — " +
                    $"first_full={(r.FirstFullStep < 0 ? "khong" : r.FirstFullStep.ToString())}, " +
                    $"max_live={r.MaxLive}, final_live={w.Live}, merges={w.Merges}, " +
                    $"disruptions={w.RocheDisruptions.Count}, frag_spawned={r.FragSpawned}, " +
                    $"ns_absorbed_events={r.NsAbsorbed}, wall={sw.Elapsed.TotalSeconds:F0}s");
            }
        }

        // Doi chung phu: seed1234/B khong thu thap diagnostic trong loop.
        // Hash/counters cuoi phai khop ban B co do — lech thi dung va bao repro.
        {
            var w = World.SolSystem(Rocks, 1234);
            w.Threads = 1;
            for (int k = 0; k < Warmup; k++) w.Advance(.5);
            Drop(w);
            for (int s = 1; s <= Steps; s++) w.Advance(.5);
            var ref_ = finals["1234/B"];
            bool match = w.Hash() == ref_.hash && w.Live == ref_.live && w.N == ref_.n
                && w.Merges == ref_.merges && w.RocheDisruptions.Count == ref_.disr
                && w.RocheReservoirs.Count == ref_.resv && w.EscapedMass == ref_.esc;
            Console.WriteLine(match
                ? "OK     p0rocheab: doi chung phu seed1234/B khop ban B co do (hash + counters)"
                : "FAILED p0rocheab: doi chung phu seed1234/B LECH ban B co do — dung, bao repro");
            if (!match) ok = false;
        }

        File.WriteAllLines(Path.Combine(outDir, "summary.csv"), summaries);
        Console.WriteLine(ok ? "OK     p0rocheab: 9 run + doi chung phu hoan tat"
                            : "FAILED p0rocheab: co muc khong dat, xem log");
        return ok;
    }
}
