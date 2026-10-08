using System;
using System.Diagnostics;
using System.Linq;
using Cosmos.Core;

static class NsDropChecks
{
    static World Scene(int rocks=5000)
    {
        var w=World.SolSystem(rocks,1234);
        for(int k=0;k<96;k++)w.Advance(.5);
        Drop(w);return w;
    }
    static int Drop(World w)
    {
        int ns=w.Do(new Command(CmdKind.Create,X:100,Amount:466700*World.EarthMass,Mix:w.Mix(("gas",1))));
        double birth=10*w.C.StarSolarMass,end=1+w.C.StarGiantFraction;
        if(w.Do(new Command(CmdKind.SetStarState,Target:ns,StarState:new StellarState(w.StarLifetime(birth)*end,end,birth)))<0)
            throw new InvalidOperationException("NS fixture refused its stellar state.");
        return ns;
    }
    static double[] Ledger(World w)
    {
        var a=new double[3+w.ElementCount];a[0]=w.EscapedMass;a[1]=w.EscapedPx;a[2]=w.EscapedPy;
        for(int e=0;e<w.ElementCount;e++)a[e+3]=w.EscapedMatter[e]-w.NucleosynthesisDelta[e];
        for(int i=0;i<w.N;i++)if(w.Alive[i])
        {
            a[0]+=w.M[i];a[1]+=w.M[i]*w.Vx[i];a[2]+=w.M[i]*w.Vy[i];
            for(int e=0;e<w.ElementCount;e++)a[e+3]+=w.Comp[i*w.ElementCount+e];
        }
        foreach(var r in w.RocheReservoirs)
        {
            a[0]+=r.Mass;a[1]+=r.Px;a[2]+=r.Py;
            for(int e=0;e<w.ElementCount;e++)a[e+3]+=r.Matter[e];
        }
        return a;
    }
    public static bool Run(bool benchmark=true)
    {
        bool ok=true;void Check(bool pass,string line){ok&=pass;Console.WriteLine($"{(pass?"OK    ":"FAILED")} ns-drop: {line}");}
        var w=Scene();var generations=(int[])w.Gen.Clone();var before=Ledger(w);w.Advance(0);var after=Ledger(w);
        Check(w.RocheDisruptions.All(d=>d.SourceGeneration==generations[d.Source]),
            $"zero-time events {w.RocheDisruptions.Count}: original sources only, no same-time child cascade");
        bool stable=Enumerable.Range(0,w.N).Where(i=>w.Alive[i]&&w.Grp[i]>=4).All(i=>
            w.RocheLimit(i,w.Par[i]) < double.Hypot(w.X[i]-w.X[w.Par[i]],w.Y[i]-w.Y[w.Par[i]]));
        Check(stable,"every newborn debris object survives its own host's Roche limit");
        Check(before.Zip(after).All(p=>Math.Abs(p.First-p.Second)<3e-12),"zero-time M/P/each Comp close with reservoirs and Escaped ledger");
        int disruptions=w.RocheDisruptions.Count,reservoirs=w.RocheReservoirs.Count;w.Advance(0);
        Check(w.RocheDisruptions.Count==disruptions&&w.RocheReservoirs.Count==reservoirs,"repeated zero-time step adds no breakup/reservoir work");
        // Zero strength: no resolved size survives inside the fluid limit, but bound matter must remain.
        var fluid=new World(128,7);fluid.C.RocheIceStrengthPa=fluid.C.RocheRockStrengthPa=fluid.C.RocheMetalStrengthPa=0;
        int host=fluid.Add(0,0,0,0,.05,fluid.Mix(("gas",1)));
        int body=fluid.Add(3,0,0,0,1e-6,fluid.Mix(("gas",1)));double r=.99*fluid.RocheLimit(body,host);
        fluid.Do(new Command(CmdKind.Move,Target:body,X:r,Vy:Math.Sqrt(fluid.C.G*fluid.M[host]/r),Index:1));
        var fb=Ledger(fluid);fluid.Advance(0);var fa=Ledger(fluid);
        Check(fluid.Live==1&&fluid.RocheDisruptions.Count==1&&fluid.RocheReservoirs.Count==1&&fluid.EscapedMass==0
            &&fb.Zip(fa).All(p=>Math.Abs(p.First-p.Second)<1e-14),"cohesionless inner cloud stays bound/accounted, no unstable resolved fragments");
        if(!ok)return false; // Fail-first regressions need not benchmark a known runaway cascade.
        bool noCap=true;for(int k=0;k<200;k++)
        {
            int old=w.RocheDisruptions.Count;w.Advance(.5);
            if(k>=136)noCap&=w.RocheDisruptions.Count-old<w.C.RocheChangesPerStep;
        }
        Check(noCap&&w.Live<=w.X.Length&&w.RocheDisruptions.Count<2*w.X.Length&&w.RocheReservoirs.Count<2*w.X.Length,
            $"200-step counts bounded/no late cap: live{w.Live}, disruptions{w.RocheDisruptions.Count}, reservoirs{w.RocheReservoirs.Count}, hash{w.Hash():X16}");
        var replay=World.SolSystem(5000,1234);int next=0;
        while(replay.Step<w.Step)
        {
            replay.Replay(w.Journal,ref next);
            replay.Advance(replay.Step is 96 or 97?0:.5);
        }
        Check(replay.Hash()==w.Hash(),$"NS-drop with zero-time steps and 200 Advances replays {replay.Hash():X16}/{w.Hash():X16}");
        var natural=World.SolSystem(5000,1234);
        natural.Do(new Command(CmdKind.SetRule,Name:"civ",Amount:0)); // isolate stellar matter: civ deliberately turns metal/radio into rock waste.
        // Separate outward-moving stars: an at-rest massive pair would collide long before either dies.
        int nsStar=natural.Do(new Command(CmdKind.Create,X:1000,Vx:20,Amount:500,Mix:natural.Mix(("gas",1))));
        int bhStar=natural.Do(new Command(CmdKind.Create,X:-1000,Vx:-20,Amount:1500,Mix:natural.Mix(("gas",1))));
        var nb=Ledger(natural);var jumpWatch=Stopwatch.StartNew();
        natural.Do(new Command(CmdKind.FastForward,Amount:1e10));jumpWatch.Stop();var na=Ledger(natural);
        bool naturalClosure=Math.Abs(nb[0]-na[0])<1e-8;
        for(int e=3;e<nb.Length;e++)naturalClosure&=Math.Abs(nb[e]-na[e])<1e-8;
        Console.WriteLine("ns-drop natural ledger deltas: "+string.Join(',',na.Zip(nb,(a,b)=>(a-b).ToString("R"))));
        Check(natural.StarPhaseOf(nsStar)==StarPhase.NeutronStar&&natural.StarPhaseOf(bhStar)==StarPhase.BlackHole&&naturalClosure,
            $"natural NS/BH deaths +1e10yr: {jumpWatch.Elapsed.TotalMilliseconds:F3}ms, phases{natural.StarPhaseOf(nsStar)}/{natural.StarPhaseOf(bhStar)}, ledgerClose={naturalClosure}, hash{natural.Hash():X16}");
        var naturalReplay=World.SolSystem(5000,1234);next=0;naturalReplay.Replay(natural.Journal,ref next);
        Check(naturalReplay.Hash()==natural.Hash(),$"natural-death jump journal replay {naturalReplay.Hash():X16}/{natural.Hash():X16}");
        if(benchmark)ok&=Bench(5000);
        return ok;
    }
    public static bool Bench(int rocks)
    {
        var ratios=new double[7];var costs=new double[7];
        for(int pair=0;pair<ratios.Length;pair++)
        {
            var w=World.SolSystem(rocks,1234);for(int k=0;k<32;k++)w.Advance(.5);
            var sw=Stopwatch.StartNew();for(int k=0;k<64;k++)w.Advance(.5);double before=sw.Elapsed.TotalMilliseconds/64;
            Drop(w);sw.Restart();for(int k=0;k<8;k++)w.Advance(.5);double after=sw.Elapsed.TotalMilliseconds/8;
            ratios[pair]=after/before;costs[pair]=after;
            Console.WriteLine($"ns-drop pair{pair+1} rocks{rocks}: before{before:F6}/after{after:F6}ms ratio{ratios[pair]:F6}, hash{w.Hash():X16}, live{w.Live}, disrupt{w.RocheDisruptions.Count}, reserve{w.RocheReservoirs.Count}");
        }
        Array.Sort(ratios);Array.Sort(costs);bool pass=rocks!=5000||costs[3]<=8;
        Console.WriteLine($"{(pass?"OK    ":"FAILED")} ns-drop: rocks{rocks} median7 after{costs[3]:F6}ms (5k gate<=8ms), paired after/before{ratios[3]:F6} diagnostic");
        return pass;
    }
}
