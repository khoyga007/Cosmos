using System;
using System.Collections;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Cosmos.Core;

static class RocheBench
{
    // Interleave short blocks so machine load affects both worlds at almost the same time.
    // Paired cost is a reported metric. Claire's gates are component medians: stock <=.25ms, bodies <=.02ms.
    public static bool Run()
    {
        var flags=BindingFlags.NonPublic|BindingFlags.Instance;
        var scene=World.SolSystem(5000,1234);
        for(int k=0;k<100;k++)scene.Advance(.5);
        var collect=typeof(World).GetMethod("CollectRocheCandidates",flags)!.CreateDelegate<Action<double,bool,bool>>(scene);
        var entry=typeof(World).GetMethod("NextRocheEntry",flags)!.CreateDelegate<Func<double,(double,int,int,int,int)>>(scene);
        collect(.5,false,true);
        int count=((ICollection)typeof(World).GetField("_rocheCandidates",flags)!.GetValue(scene)!).Count;
        double Measure(Action action,int repeats)
        {
            for(int n=0;n<100;n++)action();
            var watch=Stopwatch.StartNew();for(int n=0;n<repeats;n++)action();
            return watch.Elapsed.TotalMilliseconds/repeats;
        }
        double ComponentMedian(Action action)
        {
            var samples=new double[7];
            for(int n=0;n<samples.Length;n++)samples[n]=Measure(action,1000);
            Array.Sort(samples);Console.WriteLine($"roche component samples ms: {string.Join(',',samples.Select(x=>x.ToString("F6")))}");
            return samples[3];
        }
        double broad=ComponentMedian(()=>collect(.5,false,true));
        var bodies=World.SolSystem(0,1234);for(int n=0;n<100;n++)bodies.Advance(.5);
        var bodyCollect=typeof(World).GetMethod("CollectRocheCandidates",flags)!.CreateDelegate<Action<double,bool,bool>>(bodies);
        double bodyCost=ComponentMedian(()=>bodyCollect(.5,false,true));
        Console.WriteLine($"roche breakdown: N{scene.N}, candidates{count}, collect median7 {broad:F6} ms; bodies N{bodies.N} median7 {bodyCost:F6} ms; 8entry {Measure(()=>{for(int n=0;n<8;n++)_=entry(.5/8);},10000):F6} ms");
        double Paired(bool active)
        {
            var ratios=new double[7];var random=new Random(42);
            for(int round=0;round<ratios.Length;round++)
            {
                var a=World.SolSystem(5000,1234);var b=World.SolSystem(5000,1234);
                if(!active)a.Do(new Command(CmdKind.SetRule,Name:"roche",Amount:0));
                b.Do(new Command(CmdKind.SetRule,Name:"roche",Amount:0));
                for(int n=0;n<100;n++){a.Advance(.5);b.Advance(.5);}
                long ta=0,tb=0;
                long Block(World w){long start=Stopwatch.GetTimestamp();for(int n=0;n<16;n++)w.Advance(.5);return Stopwatch.GetTimestamp()-start;}
                for(int block=0;block<60;block++)
                    if(random.Next(2)==0){ta+=Block(a);tb+=Block(b);}else{tb+=Block(b);ta+=Block(a);}
                ratios[round]=(double)ta/tb;
                Console.WriteLine($"roche {(active?"on/off":"null off/off")} pair{round}: {ta*1000.0/Stopwatch.Frequency/960:F6}/{tb*1000.0/Stopwatch.Frequency/960:F6} ms, delta{100*(ratios[round]-1):F2}%, events{a.RocheDisruptions.Count}/{b.RocheDisruptions.Count}");
            }
            Array.Sort(ratios);Console.WriteLine($"roche {(active?"on/off":"null off/off")} median7: {100*(ratios[3]-1):F2}%");
            return ratios[3];
        }
        double control=Paired(false), cost=Paired(true);
        bool stable=Math.Abs(control-1)<=.03, pass=broad<=.25 && bodyCost<=.02;
        Console.WriteLine($"{(pass?"OK    ":"FAILED")} roche cost: stock broad<=.25ms={broad<=.25}, bodies<=.02ms={bodyCost<=.02}; paired cost metric {100*(cost-1):F2}%, null within3%={stable}; element5% gate is separate");
        return pass;
    }
}
