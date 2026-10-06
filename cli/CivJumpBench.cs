// What the civ dating fix costs: the same jump, on the old Core DLL and on this one, in one process.
// A jump is always cut into at most JumpSamples chunks, so 1e6 and 1e9 years cost the same NUMBER of runs of
// the rule table; what differs is how many years each of those runs has to cover. Both are timed here.
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Cosmos.Core;

static class CivJumpBench
{
    sealed class Backend : IDisposable
    {
        readonly AssemblyLoadContext? context;
        readonly Type world, command, kind;
        readonly MethodInfo make, doCmd, hash;
        public Backend(string? path)
        {
            context = path == null ? null : new AssemblyLoadContext(path, isCollectible: true);
            Assembly assembly = context == null ? typeof(World).Assembly : context.LoadFromAssemblyPath(System.IO.Path.GetFullPath(path!));
            world = assembly.GetType("Cosmos.Core.World")!; command = assembly.GetType("Cosmos.Core.Command")!;
            kind = assembly.GetType("Cosmos.Core.CmdKind")!;
            make = world.GetMethod("SolSystem")!; doCmd = world.GetMethod("Do")!; hash = world.GetMethod("Hash")!;
        }
        object Cmd(string name, double amount = 0) =>
            Activator.CreateInstance(command, new object?[] { Enum.Parse(kind, name), -1, 0d, 0d, 0d, 0d, amount, 0, null, null, 0u, null })!;
        void Do(object w, object cmd) => doCmd.Invoke(w, new[] { cmd });
        public (double Ms, ulong Hash) Measure(double years)
        {
            object w = make.Invoke(null, new object?[] { 5000, 1234UL, null })!;
            object jump = Cmd("FastForward", years);
            var timer = Stopwatch.StartNew(); Do(w, jump); timer.Stop();
            object? hashValue = hash.Invoke(w, null);
            return (timer.Elapsed.TotalMilliseconds, hashValue is null ? 0UL : (ulong)hashValue);
        }
        public void Dispose() => context?.Unload();
    }

    public static int Run(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("usage: civ-jump <before.dll> [rounds]");
            return 1;
        }
        int rounds = args.Length > 2 ? int.Parse(args[2]) : 5;
        using var before = new Backend(args[1]);
        using var after = new Backend(null);
        Backend[] backends = { before, after };
        string[] labels = { "truoc", "sau" };
        double[] years = { 1e6, 1e9 };

        Console.WriteLine($"SolSystem(5000,1234) = 5250 vat; mot lenh FastForward, {rounds} vong, thu tu xoay");
        bool any = false;
        foreach (double y in years)
        {
            var ms = new[] { new double[rounds], new double[rounds] };
            var hashes = new ulong[2];
            for (int b = 0; b < 2; b++) _ = backends[b].Measure(y); // warm up
            for (int k = 0; k < rounds; k++)
                for (int n = 0; n < 2; n++)
                {
                    int b = (k + n) % 2;
                    var m = backends[b].Measure(y);
                    ms[b][k] = m.Ms; hashes[b] = m.Hash;
                }
            var med = ms.Select(v => v.OrderBy(x => x).ElementAt(rounds / 2)).ToArray();
            var min = ms.Select(v => v.Min()).ToArray();
            Console.WriteLine($"  jump {y:G0} nam : median {med[0]:F3} -> {med[1]:F3} ms ({(med[0] > 0 ? med[1] / med[0] : 1):F2}x)   min {min[0]:F3} -> {min[1]:F3} ms ({(min[0] > 0 ? min[1] / min[0] : 1):F2}x)");
            Console.WriteLine($"                 hash {labels[0]} {hashes[0]:X16} -> {labels[1]} {hashes[1]:X16}  {(hashes[0] == hashes[1] ? "bit-exact" : "KHAC (co y)")}");
            any = true;
        }
        return any ? 0 : 1;
    }
}
