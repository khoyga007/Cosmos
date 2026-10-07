using System;
using System.Collections;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Cosmos.Core;

static class RocheBench
{
    // Interleave short blocks so machine load affects both worlds at almost the same time.
    // A null pair runs identical disabled rules; its median must be within 3% before judging cost.
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
        Console.WriteLine($"roche breakdown: N{scene.N}, candidates{count}, collect {Measure(()=>collect(.5,false,true),1000):F6} ms; 8entry {Measure(()=>{for(int n=0;n<8;n++)_=entry(.5/8);},10000):F6} ms");
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
        bool stable=Math.Abs(control-1)<=.03, affordable=cost<=1.10;
        Console.WriteLine($"{(stable&&affordable?"OK    ":"FAILED")} roche cost: null within3%={stable}, enabled overhead<=10%={affordable}; baseline element5% gate is separate");
        return stable&&affordable;
    }
}
