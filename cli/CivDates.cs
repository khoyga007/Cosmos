// P5 probe: the dates a civilisation is given, told at several chunk sizes on the same scene.
// Same seed, same total years, same rules, same rails: only how many years each run of the rule table has to
// cover differs. Any date that moves when that changes was never dated at all — it was sampled at whatever
// boundary the run happened to end on.
//
// A step of one year is NOT a valid reference here: cli -- civ-dates stab shows a 268-unit Advance throws
// Earth to x=-97287 by year 1000, so a "small step" run is a different solar system, not the same one sampled
// finer. Cutting a jump keeps the physics (closed two-body ride) and changes only the rule-table stretch.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Cosmos.Core;

static class CivDates
{
    const int Earth = 3;

    public static int Run(string[] args)
    {
        if (args.Length > 1 && args[1] == "trace") return Trace(args.Length > 2 ? double.Parse(args[2], CultureInfo.InvariantCulture) : 1e6);
        if (args.Length > 1 && args[1] == "stab") return Stab();
        double total = args.Length > 1 ? double.Parse(args[1], CultureInfo.InvariantCulture) : 1e6;

        int[] parts = { 1, 10, 100, 1000 };
        var runs = parts.Select(p => ($"{p} jump", Split(total, p))).ToList();

        var w0 = runs[0].Item2;
        Console.WriteLine($"scene SolSystem(0,1234)  total {total:G6} nam  YearTime {w0.C.YearTime:F4}  JumpSamples {w0.C.JumpSamples:F0}");
        Console.WriteLine($"each jump of {total:G4}/N nam is cut into 200 chunks, so the rule table covers {total / 200:N0} nam per run at N=1 and {total / parts[^1] / 200:N0} nam at N={parts[^1]}");
        Console.WriteLine();

        Table("CIV (w.Chronicle, slot Earth)", runs, w => w.Chronicle.Where(c => c.Event.ObjectSlot == Earth)
            .Select(c => (c.Event.Change, c.Event.Year)));
        Table("LIFE (w.Events, slot Earth)", runs, w => w.Events.Where(e => e.ObjectSlot == Earth && e.RuleId != "temperature")
            .Select(e => (e.Change, e.Year)));

        Console.WriteLine();
        Console.WriteLine("STATE at the end");
        foreach (var (name, w) in runs)
        {
            var civ = w.Civ[Earth];
            string colonies = string.Join(" ", Enumerable.Range(1, 9).Where(i => w.Pop[i] > 0).Select(i => $"{w.Name[i]}={w.Pop[i]:F3}"));
            Console.WriteLine($"  {name,-9} year {w.Year:G8}  hash {w.Hash():X16}  tech {w.Tech[Earth]:F2} stage {w.TechStage(Earth)}  pop {w.Pop[Earth]:F4}  civ {civ}  worlds {w.WorldsOf(civ)}  [{colonies}]");
        }
        return 0;
    }

    // One scene, one jump size: the years arrive in `parts` equal jumps.
    static World Split(double total, int parts)
    {
        var w = World.SolSystem(0, 1234);
        for (int k = 0; k < parts; k++) w.Do(new Command(CmdKind.FastForward, Amount: total / parts));
        return w;
    }

    // How big a slice of time one Advance may swallow before the scene stops being the scene. A step of one
    // year is 268 time units handed to a pull integrator that usually gets 0.5.
    static int Stab()
    {
        const double years = 1000;
        double[] hs = { 0.5, 1, 2, 5, 10, 26.82, 67.06, 134.12, 268.23 };
        Console.WriteLine($"SolSystem(0,1234) after {years:G0} years, one Advance of h time units each:");
        Console.WriteLine($"  {"h",9} {"nam/buoc",10} {"so buoc",10} {"Temp[3]",9} {"X[3]",12} {"Y[3]",12} {"Live",5}  {"hash",18}");
        foreach (double h in hs)
        {
            var w = World.SolSystem(0, 1234);
            int n = (int)Math.Round(years * w.C.YearTime / h);
            for (int s = 0; s < n; s++) w.Advance(h);
            Console.WriteLine($"  {h,9:F2} {h / w.C.YearTime,10:F5} {n,10} {w.Temp[3],9:F2} {w.X[3],12:F5} {w.Y[3],12:F5} {w.Live,5}  {w.Hash(),18:X16}");
        }
        return 0;
    }

    // The state of Earth every `every` years: one 1e5-year jump at a time against a 1e6-year jump.
    static int Trace(double total)
    {
        const double every = 1e5;
        int marks = (int)Math.Round(total / every);
        Console.WriteLine($"Earth every {every:G0} years: jumps of {every:G0} vs one jump of {total:G0}");
        Console.WriteLine($"  {"nam",9} | {"Temp",7} {"Water",5} {"WatYrs",9} {"Life",6} {"RichYrs",9} {"Pop",7} {"Tech",5} {"st",2} | {"Temp",7} {"Water",5} {"WatYrs",9} {"Life",6} {"RichYrs",9} {"Pop",7} {"Tech",5} {"st",2}");
        Console.WriteLine($"  {"",9} | {"--- 10 x 1e5 ---",-64} | {"--- 1 x 1e6 ---",-64}");

        var many = World.SolSystem(0, 1234);
        var once = World.SolSystem(0, 1234);
        for (int m = 1; m <= marks; m++)
        {
            many.Do(new Command(CmdKind.FastForward, Amount: every));
            once.Do(new Command(CmdKind.FastForward, Amount: every));
            Console.WriteLine($"  {many.Year,9:F0} | {Row(many)} | {Row(once)}");
        }
        return 0;
    }

    static string Row(World w)
    {
        int i = Earth;
        return $"{w.Temp[i],7:F1} {w.Water[i],5} {w.WaterYears[i],9:F0} {w.Life[i],6:F3} {w.RichYears[i],9:F0} {w.Pop[i],7:F4} {w.Tech[i],5:F2} {w.TechStage(i),2}";
    }

    // Every change any run logged, on one line each, with the year each run gave it.
    static void Table(string title, List<(string Name, World W)> runs, Func<World, IEnumerable<(string Change, double Year)>> pick)
    {
        var perRun = runs.Select(r => pick(r.Item2).ToList()).ToList();
        var order = new List<string>();
        foreach (var list in perRun)
            foreach (var (change, _) in list)
                if (!order.Contains(change)) order.Add(change);

        Console.WriteLine($"=== {title}: {order.Count} moc ===");
        Console.Write($"  {"moc",-22}");
        foreach (var r in runs) Console.Write($"{r.Item1,16}");
        Console.Write($"{"lech 1 vs cuoi",18}");
        Console.WriteLine();

        foreach (string change in order)
        {
            var years = perRun.Select(list =>
            {
                var hit = list.Where(x => x.Change == change).Select(x => (double?)x.Year).ToList();
                return hit.Count == 0 ? (double?)null : hit[0];
            }).ToList();
            var counts = perRun.Select(list => list.Count(x => x.Change == change)).ToList();

            Console.Write($"  {change,-22}");
            foreach (var y in years) Console.Write(y is null ? $"{"-",16}" : $"{y.Value,16:F1}");
            double? a = years[0], c = years[^1];
            Console.Write(a is null || c is null ? $"{"-",18}" : $"{c.Value - a.Value,18:F1}");
            if (counts.Distinct().Count() > 1) Console.Write($"   ⚠ so lan khac nhau {string.Join("/", counts)}");
            Console.WriteLine();
        }
        Console.WriteLine();
    }
}
