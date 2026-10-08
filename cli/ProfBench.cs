using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Cosmos.Core;

static class ProfBench
{
    public static void Run(string[] args)
    {
        int rocks = args.Length > 1 ? int.Parse(args[1], CultureInfo.InvariantCulture) : 5000;
        int steps = args.Length > 2 ? int.Parse(args[2], CultureInfo.InvariantCulture) : 40;
        var w = World.SolSystem(rocks, 1234); w.ResetProf();
        var watch = Stopwatch.StartNew();
        for (int i = 0; i < steps; i++) w.Advance(.5);
        watch.Stop();
        Console.WriteLine($"prof rocks={rocks} steps={steps} ms/Advance={watch.Elapsed.TotalMilliseconds / Math.Max(1, steps):F6} hash={w.Hash():X16}");
        Console.WriteLine(w.Prof);
        foreach (var rule in w.Rules) Console.WriteLine($"rule {rule.Id}: applications={w.RuleApplications(rule.Id)}");
    }

    // Load BOTH binaries by the same route; using the S0 builder preserves identical command scenes.
    sealed class Backend : IDisposable
    {
        readonly AssemblyLoadContext context;
        readonly MethodInfo build, getHash;
        readonly MethodInfo? reset;
        readonly Type world;
        public readonly string Path;
        public Backend(string cliPath, bool expectProf)
        {
            Path = System.IO.Path.GetFullPath(cliPath);
            context = new AssemblyLoadContext("prof-pair-" + Guid.NewGuid(), true);
            var core = context.LoadFromAssemblyPath(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path)!, "Cosmos.Core.dll"));
            world = core.GetType("Cosmos.Core.World")!;
            if ((world.GetProperty("Prof") != null) != expectProf) throw new InvalidOperationException("wrong baseline/profile backend: " + Path);
            var cli = context.LoadFromAssemblyPath(Path);
            build = cli.GetType("ScaleBench")!.GetMethod("Build", BindingFlags.Static | BindingFlags.NonPublic)!;
            getHash = world.GetMethod("Hash")!;
            reset = world.GetMethod("ResetProf");
        }
        public (double Ms, ulong Hash, string Work) Measure(int systems, int rocks, int steps)
        {
            object scene = build.Invoke(null, new object[] { systems, rocks, 0 })!;
            object w = scene.GetType().GetField("W")!.GetValue(scene)!;
            var advance = world.GetMethod("Advance")!.CreateDelegate<Action<double>>(w);
            for (int i = 0; i < 2; i++) advance(.5);
            reset?.Invoke(w, null);
            var watch = Stopwatch.StartNew();
            for (int i = 0; i < steps; i++) advance(.5);
            watch.Stop();
            ulong hash = (ulong)getHash.Invoke(w, null)!;
            string work = world.GetProperty("Prof")?.GetValue(w)?.ToString() ?? "baseline";
            return (watch.Elapsed.TotalMilliseconds / steps, hash, work);
        }
        public void Dispose() => context.Unload();
    }

    public static bool Paired(string baselineCli)
    {
        using var baseline = new Backend(baselineCli, false);
        using var prof = new Backend(typeof(ProfBench).Assembly.Location, true);
        _ = baseline.Measure(1, 500, 40); _ = prof.Measure(1, 500, 40);
        bool ok = true;
        foreach (int systems in new[] { 50, 200 })
        {
            int steps = systems == 50 ? 40 : 8; // at least one temperature tick; never a rules-free one-step window
            var ratios = new double[7]; bool hashes = true;
            for (int pair = 0; pair < 7; pair++)
            {
                (double Ms, ulong Hash, string Work) before, after;
                if ((pair & 1) == 0) { before = baseline.Measure(systems, 500, steps); after = prof.Measure(systems, 500, steps); }
                else { after = prof.Measure(systems, 500, steps); before = baseline.Measure(systems, 500, steps); }
                ratios[pair] = after.Ms / before.Ms; hashes &= before.Hash == after.Hash;
                Console.WriteLine($"prof pair K={systems}x500 #{pair+1} order={((pair & 1) == 0 ? "base-prof" : "prof-base")} base={before.Ms:F6} prof={after.Ms:F6}ms ratio={ratios[pair]:F8} hashes={before.Hash:X16}/{after.Hash:X16} steps={steps}");
                Console.WriteLine(after.Work);
                Console.Out.Flush();
            }
            Array.Sort(ratios);
            bool pass = hashes && ratios[3] <= 1.01;
            ok &= pass;
            Console.WriteLine($"{(pass ? "OK    " : "FAILED")} prof overhead K={systems}x500 median7={100*(ratios[3]-1):F6}% (<=1%), hashes={hashes}; no absolute S1 speed gate claimed");
            Console.Out.Flush();
        }
        return ok;
    }
}
