using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

static class NsPairedBench
{
    sealed class Backend:IDisposable
    {
        readonly AssemblyLoadContext context;
        readonly Type world,command,kind,stellar;
        readonly MethodInfo make,doCmd;
        public Backend(string path)
        {
            context=new AssemblyLoadContext("ns-paired-"+Guid.NewGuid(),true);
            var assembly=context.LoadFromAssemblyPath(System.IO.Path.GetFullPath(path));
            world=assembly.GetType("Cosmos.Core.World")!;command=assembly.GetType("Cosmos.Core.Command")!;
            kind=assembly.GetType("Cosmos.Core.CmdKind")!;stellar=assembly.GetType("Cosmos.Core.StellarState")!;
            make=world.GetMethod("SolSystem")!;doCmd=world.GetMethod("Do")!;
        }
        object Cmd(string id,int target=-1,double x=0,double amount=0,double[]? mix=null,object? state=null)=>
            Activator.CreateInstance(command,new object?[]{Enum.Parse(kind,id),target,x,0d,0d,0d,amount,0,mix,null,0u,state})!;
        public (double Before,double After,ulong Hash,int Live) Measure(int rocks)
        {
            object w=make.Invoke(null,new object?[]{rocks,1234UL,null})!;
            var advance=world.GetMethod("Advance")!.CreateDelegate<Action<double>>(w);
            for(int k=0;k<32;k++)advance(.5);
            var watch=Stopwatch.StartNew();for(int k=0;k<64;k++)advance(.5);double before=watch.Elapsed.TotalMilliseconds/64;
            int ns=(int)doCmd.Invoke(w,new[]{Cmd("Create",x:100,amount:70.005,mix:new double[]{1,0,0,0,0,0})})!;
            object constants=world.GetField("C")!.GetValue(w)!;
            double end=1+(double)constants.GetType().GetField("StarGiantFraction")!.GetValue(constants)!;
            double birth=10*(double)constants.GetType().GetField("StarSolarMass")!.GetValue(constants)!;
            double life=(double)world.GetMethod("StarLifetime")!.Invoke(w,new object[]{birth})!;
            object snapshot=Activator.CreateInstance(stellar,new object[]{life*end,end,birth,0d})!;
            doCmd.Invoke(w,new[]{Cmd("SetStarState",target:ns,state:snapshot)});
            watch.Restart();for(int k=0;k<8;k++)advance(.5);double after=watch.Elapsed.TotalMilliseconds/8;
            return(before,after,(ulong)world.GetMethod("Hash")!.Invoke(w,null)!, (int)world.GetField("Live")!.GetValue(w)!);
        }
        public void Dispose()=>context.Unload();
    }
    public static bool Run(string baseline)
    {
        using var old=new Backend(baseline);using var current=new Backend(typeof(Cosmos.Core.World).Assembly.Location);
        _=old.Measure(0);_=current.Measure(0);
        var ratios=new double[7];var costs=new double[7];
        for(int k=0;k<7;k++)
        {
            (double Before,double After,ulong Hash,int Live) a,b;
            if(k%2==0){a=old.Measure(5000);b=current.Measure(5000);}else{b=current.Measure(5000);a=old.Measure(5000);}
            ratios[k]=a.After/b.After;costs[k]=b.After;
            Console.WriteLine($"ns opt pair{k+1} order{(k%2==0?"old-new":"new-old")}: old{a.After:F6}/new{b.After:F6}ms, speedup{ratios[k]:F6}, baseline{a.Before:F6}/{b.Before:F6}, hashes{a.Hash:X16}/{b.Hash:X16}, live{a.Live}/{b.Live}");
        }
        Array.Sort(ratios);Array.Sort(costs);
        bool pass=costs[3]<=8;
        Console.WriteLine($"{(pass?"OK    ":"FAILED")} NS repair paired median7 speedup{ratios[3]:F6}, new median{costs[3]:F6}ms; R handoff gate8ms (new architecture4ms remains separate)");
        return pass;
    }
}
