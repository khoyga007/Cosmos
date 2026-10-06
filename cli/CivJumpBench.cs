// What the civ dating fix costs: the SAME jump on the old Core and on this one, in one process, both loaded
// the same way (a collectible AssemblyLoadContext each), so neither side gets a warm-assembly advantage.
// parts = how many FastForward commands the total is split into: 1 is the case the fix leaves untouched,
// 100 is the case it changes, because a jump is cut into at most JumpSamples chunks and a small part keeps
// the rule table running at its own rhythm.
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

static class CivJumpBench
{
    const int earth = 3;

    sealed class Backend : IDisposable
    {
        readonly AssemblyLoadContext context;
        readonly Type world, command, kind;
        readonly MethodInfo make, doCmd, hash;
        readonly FieldInfo chronicle;
        public Backend(string path)
        {
            context = new AssemblyLoadContext(System.IO.Path.GetFileNameWithoutExtension(path) + "-" + Guid.NewGuid().ToString("N")[..6], isCollectible: true);
            Assembly assembly = context.LoadFromAssemblyPath(System.IO.Path.GetFullPath(path));
            world = assembly.GetType("Cosmos.Core.World")!; command = assembly.GetType("Cosmos.Core.Command")!;
            kind = assembly.GetType("Cosmos.Core.CmdKind")!;
            make = world.GetMethod("SolSystem")!; doCmd = world.GetMethod("Do")!; hash = world.GetMethod("Hash")!;
            chronicle = world.GetField("Chronicle")!;
        }
        object Cmd(string name, double amount = 0) =>
            Activator.CreateInstance(command, new object?[] { Enum.Parse(kind, name), -1, 0d, 0d, 0d, 0d, amount, 0, null, null, 0u, null })!;
        double Birth(object w)
        {
            if (chronicle.GetValue(w) is not System.Collections.IEnumerable rows) return double.NaN;
            foreach (object row in rows)
            {
                object ev = row.GetType().GetField("Item2")!.GetValue(row)!;
                if ((string)ev.GetType().GetProperty("Change")!.GetValue(ev)! != "civ.start") continue;
                if ((int)ev.GetType().GetProperty("ObjectSlot")!.GetValue(ev)! != earth) continue;
                return (double)ev.GetType().GetProperty("Year")!.GetValue(ev)!;
            }
            return double.NaN;
        }
        public (double Ms, ulong Hash, double Birth) Measure(double years, int rocks, int parts)
        {
            object w = make.Invoke(null, new object?[] { rocks, 1234UL, null })!;
            object jump = Cmd("FastForward", years / parts);
            var timer = Stopwatch.StartNew();
            for (int p = 0; p < parts; p++) doCmd.Invoke(w, new[] { jump });
            timer.Stop();
            object? hashValue = hash.Invoke(w, null);
            return (timer.Elapsed.TotalMilliseconds, hashValue is null ? 0UL : (ulong)hashValue, Birth(w));
        }
        public void Dispose() { context.Unload(); }
    }

    public static int Run(string[] args)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("usage: civ-jump <old.dll> <new.dll> [rounds] [rocks] [parts...]");
            return 1;
        }
        int rounds = args.Length > 3 ? int.Parse(args[3]) : 21;
        int rocks = args.Length > 4 ? int.Parse(args[4]) : 0;
        int[] partsList = args.Length > 5 ? args[5..].Select(int.Parse).ToArray() : new[] { 1, 100 };
        using var oldCore = new Backend(args[1]);
        using var newCore = new Backend(args[2]);
        Backend[] backends = { oldCore, newCore };
        string[] labels = { "truoc", "sau" };
        double[] years = { 1e6, 1e9 };

        Console.WriteLine($"SolSystem({rocks},1234) = {rocks + 250} vat; {rounds} vong, thu tu xoay; ca hai core nap qua AssemblyLoadContext");
        foreach (double y in years)
            foreach (int parts in partsList)
            {
                var ms = new[] { new double[rounds], new double[rounds] };
                var hashes = new ulong[2];
                var births = new double[2];
                for (int b = 0; b < 2; b++) _ = backends[b].Measure(y, rocks, parts); // warm up
                for (int k = 0; k < rounds; k++)
                    for (int n = 0; n < 2; n++)
                    {
                        int b = (k + n) % 2;
                        var m = backends[b].Measure(y, rocks, parts);
                        ms[b][k] = m.Ms; hashes[b] = m.Hash; births[b] = m.Birth;
                    }
                var med = ms.Select(v => v.OrderBy(x => x).ElementAt(rounds / 2)).ToArray();
                var min = ms.Select(v => v.Min()).ToArray();
                Console.WriteLine($"  jump {y:G0} nam / {parts} lenh : median {med[0]:F3} -> {med[1]:F3} ms ({(med[0] > 0 ? med[1] / med[0] : 1):F2}x)   min {min[0]:F3} -> {min[1]:F3} ms ({(min[0] > 0 ? min[1] / min[0] : 1):F2}x)");
                Console.WriteLine($"      hash {labels[0]} {hashes[0]:X16} -> {labels[1]} {hashes[1]:X16}  {(hashes[0] == hashes[1] ? "bit-exact" : "KHAC (fix doi moc)")};  civ.start {(double.IsNaN(births[0]) ? "khong co" : births[0].ToString("F1"))} -> {(double.IsNaN(births[1]) ? "khong co" : births[1].ToString("F1"))}");
            }
        return 0;
    }
}
