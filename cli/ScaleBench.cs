// S0 SCALE BASELINE — Selica, SPEC.md §14 §S0 (nhánh selica/scale-baseline, base 7379c83).
// CHỈ ĐO. Không sửa core/ một dòng nào, không tối ưu, không lách: combo nào dựng không nổi thì
// in FAIL kèm lý do + số, không có đường vòng.
//
// Cảnh: K hệ, mỗi hệ 1 sao (M=50, lớn hơn AttractMass nên là puller) + 3 hành tinh (1 EarthMass,
// 0.6/1.0/1.4 AU) + rocksPer đá ở vành 2.1..3.3 AU (cùng bảng trộn như SolSystem, M ~1e-10..2e-8,
// dưới AttractMass nên là đá). Hai hệ cách nhau 4000 đơn vị; SolDist(3.3)=94.3 nên vành ngoài cùng
// của hai hệ còn cách ~3810 đơn vị, lực hút chéo 50/3810^2 = 3.4e-6 so với 5.7e-3 của sao nhà
// (0.06%): hệ này không kéo hệ kia. Khoảng cách KHÔNG đổi giá đo — vòng lặp gravity là flat, không
// có cutoff — nên đây chỉ để cảnh sạch về mặt vật lý.
//
// Dựng HOÀN TOÀN qua Commands (Create / CreateOrbiting) như §S0 yêu cầu, nên journal ghi lại được.
// Mảng Mix của đá được chia sẻ theo "nguyên tố trội" và không bao giờ sửa sau khi tạo, nên journal
// vẫn đúng (Add copy nội dung mix vào Comp lúc apply).
//
// Mọi ms/Advance là MEDIAN của `rounds` vòng paired: mỗi vòng DỰNG LẠI cảnh từ seed rồi warm JIT
// trước khi đo (khuôn AdvanceBench). Số bước mỗi vòng tự co theo trần capMs để combo lớn vẫn đo
// được; dòng in ra ghi rõ số bước thật đã dùng, nên đọc số nào cũng biết nó ở cấu hình nào.
//
// Dùng:
//   cli scale [K] [rocksPer] [steps] [rounds] [capMs]        K/rocksPer = danh sách "1,10,50,200"
//   cli scale rules [K] [rocksPer] [steps] [rounds] [capMs]  bảng giá từng rule
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Cosmos.Core;

static class ScaleBench
{
    const double H = 0.5;          // bước chuẩn của advance-bench
    const int Planets = 3;
    const ulong Seed = 1234;       // seed cố định
    const double Spacing = 4000;   // đơn vị giữa hai hệ (lực chéo 0.06% lực sao nhà, xem header)

    // RNG riêng của cli: cùng seed => cùng cảnh, và không tiêu thụ _rng của World nên không làm
    // lệch hành vi rule nào của world.
    struct Rng
    {
        ulong _s;
        public Rng(ulong seed) => _s = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;
        public double Next()
        {
            ulong x = _s; x ^= x >> 12; x ^= x << 25; x ^= x >> 27; _s = x;
            return ((x * 0x2545F4914F6CDD1DUL) >> 11) * (1.0 / 9007199254740992.0);
        }
    }

    sealed class Scene { public World W = null!; public int Fails; public double BuildMs; }

    sealed class Result
    {
        public double MsPerStep, BuildMs, HashMs, ManagedMb, WsMb, PeakMb;
        public int Steps, Warm, N, Live, Pullers, Cap, Fails, NEnd, LiveEnd, Merges;
        public long Journal, Events;
    }

    static readonly Process Proc = Process.GetCurrentProcess();
    static string[]? _ruleIds;

    static string[] AllRuleIds() => _ruleIds ??= new World(8, Seed).Rules.Select(r => r.Id).ToArray();

    static List<int> List(string[] a, int i, string def) =>
        (a.Length > i ? a[i] : def).Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => int.Parse(x.Trim(), CultureInfo.InvariantCulture)).ToList();

    // ---- dựng cảnh (chỉ qua Commands)

    // capOverride > 0 = cố tình cấp THIẾU chỗ để ĐO trần capacity (World.cs:184 `else return -1`).
    static Scene Build(int systems, int rocksPer, int capOverride = 0)
    {
        int per = 1 + Planets + rocksPer;
        var s = new Scene();
        var sw = Stopwatch.StartNew();
        var w = new World(capOverride > 0 ? capOverride : systems * per + 16, Seed);
        s.W = w;
        double[] starMix = w.Mix(("gas", 1));
        double[] planetMix = w.Mix(("ice", .01), ("rock", .66), ("metal", .32), ("carbon", .005), ("radio", .005));
        double[] belt = w.Mix(("ice", .05), ("rock", .60), ("metal", .20), ("carbon", .14), ("radio", .01));
        // một mảng mix cho mỗi nguyên tố trội, tạo một lần rồi chia sẻ (bất biến sau khi tạo)
        int radio = w.Elem("radio");
        var byMain = new double[w.ElementCount][];
        for (int main = 0; main < w.ElementCount; main++)
        {
            byMain[main] = new double[w.ElementCount];
            for (int e = 0; e < w.ElementCount; e++) byMain[main][e] = 0.3 * belt[e] + (e == main ? 0.7 : 0);
        }
        int cols = (int)Math.Ceiling(Math.Sqrt(systems));
        var rng = new Rng(Seed);
        for (int k = 0; k < systems; k++)
        {
            double cx = (k % cols) * Spacing, cy = (k / cols) * Spacing;
            int star = w.Do(new Command(CmdKind.Create, X: cx, Y: cy, Amount: 50, Mix: starMix, Name: "S" + k));
            if (star < 0) { s.Fails++; continue; }
            for (int p = 0; p < Planets; p++)
            {
                double d = World.SolDist(0.6 + 0.4 * p), a = rng.Next() * Math.Tau;
                if (w.Do(new Command(CmdKind.CreateOrbiting, Target: star, X: cx + Math.Cos(a) * d, Y: cy + Math.Sin(a) * d,
                        Amount: World.EarthMass, Mix: planetMix)) < 0) s.Fails++;
            }
            for (int i = 0; i < rocksPer; i++)
            {
                double d = World.SolDist(2.1 + 1.2 * rng.Next()), a = rng.Next() * Math.Tau;
                double u = rng.Next(); int main = radio;
                for (int e = 0; e < w.ElementCount; e++) { if (e == radio) continue; u -= belt[e]; if (u < 0) { main = e; break; } }
                double q = rng.Next(), m = 1e-10 * (1 + 200 * q * q * q);
                if (w.Do(new Command(CmdKind.CreateOrbiting, Target: star, X: cx + Math.Cos(a) * d, Y: cy + Math.Sin(a) * d,
                        Amount: m, Mix: byMain[main])) < 0) s.Fails++;
            }
        }
        sw.Stop();
        s.BuildMs = sw.Elapsed.TotalMilliseconds;
        return s;
    }

    // ---- đo 1 cấu hình (một pha: rules nguyên trạng, hoặc tắt một danh sách rule)

    static Result Phase(int systems, int rocksPer, int wantSteps, int rounds, double capMs, string[]? offRules, int capOverride = 0)
    {
        wantSteps = Math.Max(1, wantSteps);
        var res = new Result { Steps = wantSteps };
        // probe trên cảnh riêng: 1 Advance để biết giá, rồi co số bước cho vừa trần capMs
        {
            var sp = Build(systems, rocksPer, capOverride);
            if (offRules != null) foreach (string id in offRules) sp.W.Do(new Command(CmdKind.SetRule, Name: id, Amount: 0));
            var swp = Stopwatch.StartNew(); sp.W.Advance(H); swp.Stop();
            double est = swp.Elapsed.TotalMilliseconds;
            if (est > 1e-4) res.Steps = Math.Clamp((int)Math.Floor(capMs / (2 * est)), 1, wantSteps);
        }
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        res.Warm = Math.Min(10, res.Steps);
        var v = new double[rounds];
        for (int r = 0; r < rounds; r++)
        {
            var sc = Build(systems, rocksPer, capOverride);
            if (offRules != null) foreach (string id in offRules) sc.W.Do(new Command(CmdKind.SetRule, Name: id, Amount: 0));
            if (r == rounds - 1)
            {
                var w = sc.W;
                res.BuildMs = sc.BuildMs; res.Fails = sc.Fails;
                res.N = w.N; res.Live = w.Live; res.Cap = w.X.Length;
                res.Journal = w.Journal.Count; res.Events = w.Events.Count;
                res.Pullers = 0;
                for (int i = 0; i < w.N; i++) if (w.Alive[i] && w.Attracts(i)) res.Pullers++;
                var hv = new double[3];
                for (int k = 0; k < 3; k++) { var swh = Stopwatch.StartNew(); w.Hash(); swh.Stop(); hv[k] = swh.Elapsed.TotalMilliseconds; }
                Array.Sort(hv); res.HashMs = hv[1];
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                res.ManagedMb = GC.GetTotalMemory(false) / 1048576.0;
                Proc.Refresh();
                res.WsMb = Proc.WorkingSet64 / 1048576.0;
                try { res.PeakMb = Proc.PeakWorkingSet64 / 1048576.0; } catch { res.PeakMb = double.NaN; }
            }
            GC.Collect();
            for (int i = 0; i < res.Warm; i++) sc.W.Advance(H);
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < res.Steps; i++) sc.W.Advance(H);
            sw.Stop();
            v[r] = sw.Elapsed.TotalMilliseconds / res.Steps;
            if (r == rounds - 1) { res.NEnd = sc.W.N; res.LiveEnd = sc.W.Live; res.Merges = (int)sc.W.Merges; }
        }
        Array.Sort(v);
        res.MsPerStep = v[rounds / 2];
        return res;
    }

    // World.cs:364 _chunks = clamp(N/512, 1, 16) — dưới 1024 vật thì pass đá chạy MỘT luồng.
    static string ChunkHint(int n) => $"{Math.Clamp(n / 512, 1, 16)}/16";

    public static void Run(string[] a)
    {
        if (a.Length > 1 && a[1] == "rules") { RulesMode(a); return; }
        var sys = List(a, 1, "1,10,50,200");
        var rk = List(a, 2, "500,5000");
        int steps = a.Length > 3 ? int.Parse(a[3], CultureInfo.InvariantCulture) : 40;
        int rounds = a.Length > 4 ? int.Parse(a[4], CultureInfo.InvariantCulture) : 7;
        double cap = a.Length > 5 ? double.Parse(a[5], CultureInfo.InvariantCulture) : 2000;
        // cap=NNN ở BẤT KỲ vị trí nào: cấp thiếu chỗ để đo trần capacity (World.cs:184). Không phải workaround.
        int capOverride = 0;
        foreach (string x in a) if (x.StartsWith("cap=", StringComparison.Ordinal)) capOverride = int.Parse(x[4..], CultureInfo.InvariantCulture);
        if (capOverride > 0) Console.WriteLine($"# cap={capOverride} — CỐ TÌNH cấp thiếu chỗ: đo trần capacity, không phải cảnh chuẩn");

        Console.WriteLine("# S0 BASELINE — Cosmos scale, advance NGUYÊN TRẠNG (đá bay flat × MỌI vật hút × 8 sub-step)");
        Console.WriteLine($"# .NET {Environment.Version}, {Environment.ProcessorCount} logical CPUs, Release, seed {Seed}, H={H}, median {rounds} vòng, steps≤{steps}, cap {cap:F0} ms/pha");
        Console.WriteLine($"# cảnh dựng qua Commands: mỗi hệ 1 sao(M=50) + {Planets} hành tinh(EarthMass) + rocksPer đá vành 2.1-3.3 AU; hai hệ cách {Spacing}");
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        double baseMb = GC.GetTotalMemory(false) / 1048576.0;
        Console.WriteLine($"# nền managed sau GC = {baseMb:F1} MB (đã gồm 1 World(8) của AllRuleIds) — MB dưới đây trừ nền này ra mới là giá của cảnh");
        Console.WriteLine("# K      rocksPer  N/cap                 fails build_ms pullers rocks     live    journal   events      chunks");
        foreach (int k in sys)
            foreach (int r in rk)
                One(k, r, steps, rounds, cap, capOverride);
        Console.WriteLine("# rules-off = tắt CẢ 8 rule qua Commands (SetRule Amount:0) trên cùng cảnh; rule-cost = ms/Advance(rules nguyên trạng) - ms/Advance(rules-off).");
        Console.WriteLine("# Rule có NHỊP năm (temperature 0.01yr ≈ 5.4 Advance, comets 0.1yr ≈ 54, life/civ lâu hơn nhiều), nên với steps nhỏ rule có thể KHÔNG cháy trong cửa sổ đo => rule-cost ở đây là phần đã trả thật, không phải giá khi rule cháy. Bảng giá từng rule: `cli scale rules`.");
    }

    static void One(int systems, int rocksPer, int wantSteps, int rounds, double cap, int capOverride = 0)
    {
        Result on, off;
        try
        {
            on = Phase(systems, rocksPer, wantSteps, rounds, cap, null, capOverride);
            off = Phase(systems, rocksPer, wantSteps, rounds, cap, AllRuleIds(), capOverride);
        }
        catch (OutOfMemoryException e)
        {
            Console.WriteLine($"K={systems,-6} rocksPer={rocksPer,-9} FAIL OutOfMemory: {e.Message}");
            return;
        }
        string cap2 = on.Fails > 0 ? $"  <<< CAPACITY HIT: {on.Fails} lệnh Create/CreateOrbiting trả -1 (World.cs:184)" : "";
        Console.WriteLine($"K={systems,-6} rocksPer={rocksPer,-9} {on.N}/{on.Cap,-10} {on.Fails,5} {on.BuildMs,8:F0} {on.Pullers,7} {on.N - on.Pullers,-9} {on.Live,7} {on.Journal,9} {on.Events,6}/{World.EventCapacity} {ChunkHint(on.N),7}{cap2}");
        Console.WriteLine($"    ms/Advance={on.MsPerStep,10:F3}  (steps {on.Steps}/{wantSteps}, warm {on.Warm}, median {rounds})   rules-off={off.MsPerStep,9:F3}   rule-cost={on.MsPerStep - off.MsPerStep,8:F3}   us/vật={on.MsPerStep * 1000 / on.N,8:F3}");
        Console.WriteLine($"    Hash={on.HashMs,9:F1} ms ({on.HashMs * 1000 / Math.Max(1, on.N),6:F3} us/vật)   managed={on.ManagedMb,6:F0} MB   WS={on.WsMb,6:F0} MB   peak={on.PeakMb,6:F0} MB");
        Console.WriteLine($"    sau {on.Steps} bước: N {on.N} -> {on.NEnd} (live {on.LiveEnd}, merges {on.Merges}) — NEnd < N = vật biến mất giữa run, giá mỗi Advance KHÔNG cố định");
    }

    static void RulesMode(string[] a)
    {
        var sys = List(a, 2, "1,50,200");
        var rk = List(a, 3, "500");
        int steps = a.Length > 4 ? int.Parse(a[4], CultureInfo.InvariantCulture) : 40;
        int rounds = a.Length > 5 ? int.Parse(a[5], CultureInfo.InvariantCulture) : 3;
        double cap = a.Length > 6 ? double.Parse(a[6], CultureInfo.InvariantCulture) : 500;
        string[] ids = AllRuleIds();
        Console.WriteLine("# S0 per-rule — cùng cảnh, TẮT HẾT rule rồi bật lại từng cái một; delta = giá riêng của rule đó");
        Console.WriteLine($"# median {rounds} vòng, steps≤{steps}, cap {cap:F0} ms/pha, seed {Seed}, H={H}. Delta âm = dưới nhiễu đo.");
        foreach (int k in sys)
            foreach (int r in rk)
            {
                Result off = Phase(k, r, steps, rounds, cap, ids);
                Console.WriteLine($"K={k} rocksPer={r}: rules OFF = {off.MsPerStep:F3} ms/Advance (N={off.N}, steps {off.Steps}, build {off.BuildMs:F0} ms)");
                var rows = new List<(string Id, double Ms, int Steps)>();
                foreach (string id in ids)
                {
                    Result only = Phase(k, r, steps, rounds, cap, ids.Where(x => x != id).ToArray());
                    rows.Add((id, only.MsPerStep - off.MsPerStep, only.Steps));
                }
                foreach (var (id, ms, st) in rows.OrderByDescending(x => x.Ms))
                    Console.WriteLine($"    +{id,-12} {ms,10:F3} ms/Advance  ({(off.MsPerStep > 0 ? ms / off.MsPerStep * 100 : 0),6:F1}% của rules-off, steps {st})");
            }
    }
}
