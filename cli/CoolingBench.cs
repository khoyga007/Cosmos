using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Cosmos.Core;

static class CoolingBench
{
    public static World Scene(int rocks)
    {
        var w = World.SolSystem(rocks, 1234);
        w.Do(new Command(CmdKind.AddMatter, Target: 0, Name: "gas", Amount: -19.4));
        w.Do(new Command(CmdKind.SetStarState, Target: 0, StarState: new(w.StarLifetime(100) * 1.05, 1.05, 100, 1e8)));
        w.Do(new Command(CmdKind.FastForward, Amount: 1000));
        return w;
    }

    // The same public command sequence, in the same process, also runs on old Core DLLs.
    sealed class Backend : IDisposable
    {
        readonly AssemblyLoadContext? context;
        readonly Type world, command, kind, state;
        readonly MethodInfo make, doCmd, lifetime, hash;
        public Backend(string? path)
        {
            context = path == null ? null : new AssemblyLoadContext(path, isCollectible: true);
            Assembly assembly = context == null ? typeof(World).Assembly : context.LoadFromAssemblyPath(System.IO.Path.GetFullPath(path!));
            world = assembly.GetType("Cosmos.Core.World")!; command = assembly.GetType("Cosmos.Core.Command")!;
            kind = assembly.GetType("Cosmos.Core.CmdKind")!; state = assembly.GetType("Cosmos.Core.StellarState")!;
            make = world.GetMethod("SolSystem")!; doCmd = world.GetMethod("Do")!;
            lifetime = world.GetMethod("StarLifetime")!; hash = world.GetMethod("Hash")!;
        }
        object Cmd(string name, int target = -1, double amount = 0, string? material = null, object? snapshot = null) =>
            Activator.CreateInstance(command, new object?[] { Enum.Parse(kind, name), target, 0d, 0d, 0d, 0d, amount, 0, null, material, 0u, snapshot })!;
        void Do(object w, object cmd) => doCmd.Invoke(w, new[] { cmd });
        object Prepare()
        {
            object w = make.Invoke(null, new object?[] { 5000, 1234UL, null })!;
            double death = (double)lifetime.Invoke(w, new object[] { 100d })! * 1.05;
            Do(w, Cmd("AddMatter", 0, -19.4, "gas"));
            Do(w, Cmd("SetStarState", 0, snapshot: Activator.CreateInstance(state, new object[] { death, 1.05, 100d, 1e8 })));
            Do(w, Cmd("FastForward", amount: 1000));
            return w;
        }
        public (double Ms, ulong Hash) Measure()
        {
            object w = Prepare(), jump = Cmd("FastForward", amount: 1e8);
            var timer = Stopwatch.StartNew(); Do(w, jump); timer.Stop();
            return (timer.Elapsed.TotalMilliseconds, (ulong)hash.Invoke(w, null)!);
        }
        public void Dispose() => context?.Unload();
    }

    public static bool Paired(string beforeS1, string mergedS1)
    {
        using var old = new Backend(beforeS1);
        using var s1 = new Backend(mergedS1);
        using var current = new Backend(null);
        Backend[] backends = { old, s1, current };
        string[] labels = { "before-S1", "merged-S1", "followup" };
        var ms = new[] { new double[7], new double[7], new double[7] };
        var hashes = new ulong[3]; bool exact = true;
        for (int b = 0; b < 3; b++) { Console.WriteLine($"cooling warmup {labels[b]}"); _ = backends[b].Measure(); }
        for (int k = 0; k < 7; k++)
        {
            for (int n = 0; n < 3; n++)
            {
                int b = (k + n) % 3; var measured = backends[b].Measure();
                ms[b][k] = measured.Ms; hashes[b] = measured.Hash;
            }
            exact &= hashes[1] == hashes[2];
            Console.WriteLine($"cooling paired {k}: before-S1 {ms[0][k]:F3}, merged-S1 {ms[1][k]:F3}, followup {ms[2][k]:F3} ms; exact={hashes[1] == hashes[2]}");
        }
        for (int b = 0; b < 3; b++) Console.WriteLine($"cooling median7 {labels[b]}: {ms[b].Order().ElementAt(3):F3} ms");
        Console.WriteLine($"{(exact ? "OK    " : "FAILED")} cooling paired: merged-S1/followup exact hashes {hashes[1]:X16}/{hashes[2]:X16}; same public commands, 5250 objects, WD cooling age 1e8 + jump 1e8 yr, rotated order; setup excluded");
        return exact;
    }
}
