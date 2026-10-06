// PROBE P2 (Selica, SPEC Claire 15:14) — Advance: đo tách phần.
// CHỈ ĐO. Không sửa core/, không đổi một phép toán nào, không merge.
//
// Mọi số là median của nhiều vòng paired: mỗi vòng DỰNG LẠI cảnh từ seed rồi mới đo, nên cảnh không
// bị tiến hoá (đá merge dần làm nhẹ đi) giữa các vòng. Vòng đầu chậm vì cache bị median loại.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Cosmos.Core;

static class AdvanceBench
{
    const double H = 0.5;

    static double MsPerStep(World w, int steps)
    {
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < steps; i++) w.Advance(H);
        sw.Stop();
        return sw.Elapsed.TotalMilliseconds / steps;
    }

    static double Bench(Func<World> make, int steps, int rounds, out int objects)
    {
        var v = new double[rounds];
        objects = 0;
        LastLive = 0;
        for (int r = 0; r < rounds; r++)
        {
            var w = make();
            for (int i = 0; i < 10; i++) w.Advance(H); // warm the JIT
            objects = w.N;
            v[r] = MsPerStep(w, steps);
            LastLive = w.Live;
        }
        Array.Sort(v);
        return v[rounds / 2];
    }

    static int LastLive;

    // A. cảnh Sol thật: chi phí theo số đá. SolSystem(rocks) = 10 bodies + rocks + min(240, rocks/2) vành.
    static void Rocks(int steps, int rounds)
    {
        Console.WriteLine($"# A. Sol: 10 bodies + rocks + min(240, rocks/2) ring — paired {rounds} x {steps} steps");
        Console.WriteLine($"{"rocks",7} {"N",7} {"ms/Advance",12} {"us/object",10} {"live after",10}");
        foreach (int rocks in new[] { 0, 500, 1000, 2000, 5000, 10000 })
        {
            double ms = Bench(() => World.SolSystem(rocks, 1234), steps, rounds, out int n);
            Console.WriteLine($"{rocks,7} {n,7} {ms,12:F4} {ms * 1000.0 / n,10:F3} {LastLive,10}");
        }
    }

    // B. chỉ có vật hút: 1 sao + planets, không đá. Cô lập vòng cặp O(na^2) giữa các vật hút.
    static World PullScene(int planets, ulong seed)
    {
        var w = new World(planets + 8, seed);
        int sun = w.Add(0, 0, 0, 0, 50, w.Mix(("gas", 1)), "Sun");
        for (int k = 1; k <= planets; k++)
            w.AddOrbiting(sun, 5 + 3.0 * k, 0, 3e-4, w.Mix(("rock", 1)), "P" + k);
        return w;
    }

    static void Pull(int steps, int rounds)
    {
        Console.WriteLine($"# B. pulling bodies only: 1 star + planets, no rocks — the O(na^2) pair loops");
        Console.WriteLine($"{"pulling",7} {"N",7} {"ms/Advance",12}");
        foreach (int p in new[] { 1, 10, 50, 100, 200 })
        {
            double ms = Bench(() => PullScene(p, 1234), steps, rounds, out int n);
            Console.WriteLine($"{p + 1,7} {n,7} {ms,12:F4}");
        }
    }

    // C. rule "temperature" bật vs tắt trên cùng cảnh: giá của UpdateTemperature (quét N x stars).
    static void Rules(int steps, int rounds)
    {
        Console.WriteLine($"# C. rule temperature on vs off, Sol 5000 rocks");
        double on = Bench(() => World.SolSystem(5000, 1234), steps, rounds, out int n);
        double off = Bench(() =>
        {
            var w = World.SolSystem(5000, 1234);
            w.Do(new Command(CmdKind.SetRule, Name: "temperature", Amount: 0));
            return w;
        }, steps, rounds, out int n2);
        Console.WriteLine($"  N={n}/{n2}");
        Console.WriteLine($"  temperature ON : {on,9:F4} ms/Advance");
        Console.WriteLine($"  temperature OFF: {off,9:F4} ms/Advance");
        Console.WriteLine($"  difference     : {on - off,9:F4} ms/Advance ({(on - off) / on * 100:F1}%)");
    }

    // D. CÙNG một cảnh, chỉ đổi số luồng: hash phải bằng nhau TUYỆT ĐỐI. Đây là bằng chứng bit-exact của
    //    vòng đá song song, mạnh hơn selftest vì nó chạy đúng cảnh 5250/20250 vật mà không qua journal.
    //    Threads đặt qua reflection để CHÍNH file này biên dịch được cả trên core master (không có Threads).
    static ulong HashAfter(int rocks, int steps, int threads)
    {
        var w = World.SolSystem(rocks, 1234);
        typeof(World).GetProperty("Threads")?.SetValue(w, threads);
        for (int i = 0; i < steps; i++) w.Advance(H);
        return w.Hash();
    }

    static void ThreadHash(int steps)
    {
        Console.WriteLine($"# D. same scene, hash vs thread count, {steps} steps — every row of a block must match");
        Console.WriteLine($"{"rocks",7} {"N",7} {"threads",8} {"hash",18} {"live",6} {"step",6}");
        foreach (int rocks in new[] { 100, 3000, 5000, 20000 })
        {
            var seen = new List<string>();
            foreach (int t in new[] { 1, 2, 3, 4, 8, 0 })
            {
                var w = World.SolSystem(rocks, 1234);
                typeof(World).GetProperty("Threads")?.SetValue(w, t);
                for (int i = 0; i < steps; i++) w.Advance(H);
                string tag = $"{w.Hash():X16}";
                Console.WriteLine($"{rocks,7} {w.N,7} {(t == 0 ? "auto" : t.ToString()),8} {tag,18} {w.Live,6} {w.Step,6}");
                if (!seen.Contains(tag)) seen.Add(tag);
            }
            Console.WriteLine(seen.Count == 1 ? $"  OK  all thread counts agree: {seen[0]}" : $"  FAIL {seen.Count} distinct hashes: {string.Join(" ", seen)}");
        }
    }

    // E. ms/Advance theo số luồng, ở đúng hai cỡ Claire yêu cầu.
    static void ThreadsBench(int steps, int rounds)
    {
        Console.WriteLine($"# E. ms/Advance by thread count — paired {rounds} x {steps} steps");
        Console.WriteLine($"{"rocks",7} {"N",7} {"threads",8} {"ms/Advance",12} {"vs 1 thr",9}");
        foreach (int rocks in new[] { 5000, 20000 })
        {
            double one = 0;
            foreach (int t in new[] { 1, 2, 4, 8, 0 })
            {
                int tt = t;
                double ms = Bench(() =>
                {
                    var w = World.SolSystem(rocks, 1234);
                    typeof(World).GetProperty("Threads")?.SetValue(w, tt);
                    return w;
                }, steps, rounds, out int n);
                if (one == 0) one = ms;
                Console.WriteLine($"{rocks,7} {n,7} {(t == 0 ? "auto" : t.ToString()),8} {ms,12:F4} {one / ms,8:F2}x");
            }
        }
    }

    public static void Run(string[] a)
    {
        string what = a.Length > 1 ? a[1] : "all";
        int steps = a.Length > 2 ? int.Parse(a[2]) : 40;
        int rounds = a.Length > 3 ? int.Parse(a[3]) : 7;
        Console.WriteLine($"AdvanceBench — .NET {Environment.Version}, {Environment.ProcessorCount} logical CPUs, steps={steps} rounds={rounds}");
        if (what is "rocks" or "all") Rocks(steps, rounds);
        if (what is "pull" or "all") Pull(steps, rounds);
        if (what is "rules" or "all") Rules(steps, rounds);
        if (what is "thash" or "all") ThreadHash(steps);
        if (what is "threads" or "all") ThreadsBench(steps, rounds);
    }
}
