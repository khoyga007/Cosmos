using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Cosmos.Core;

static class RocheQueryChecks
{
    public static bool Run()
    {
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var contact=typeof(World).GetMethod("PrimaryContactTime",BindingFlags.Static|BindingFlags.NonPublic)!
            .CreateDelegate<Func<double,double,double,double,double,double,double>>();
        int queries=0,differences=0;var rng=new Random(1234);
        void Compare(World w)
        {
            var collect=typeof(World).GetMethod("CollectRocheCandidates",flags)!.CreateDelegate<Action<double,bool,bool>>(w);
            var query=typeof(World).GetMethod("NextRocheEntry",flags)!.CreateDelegate<Func<double,(double,int,int,int,int)>>(w);
            var limit=typeof(World).GetMethod("RocheLimitCore",flags)!.CreateDelegate<Func<int,int,bool,double>>(w);
            var density=typeof(World).GetMethod("MaterialDensity",flags)!.CreateDelegate<Func<int,double>>(w);
            foreach(double seconds in new[] { 0d,1e-9,.001,.0625,.5,10d,100d })
            {
                collect(seconds,false,true);
                var candidates=(List<(int Body,int Host,int BodyGen,int HostGen)>)typeof(World).GetField("_rocheCandidates",flags)!.GetValue(w)!;
                var snapshot=candidates.ToArray();
                // Frozen original collector predicates, including SIMD-lane tolerance and scalar tail.
                bool vector=Vector.IsHardwareAccelerated&&w.N>=Vector<double>.Count;
                double acceleration=0,maxSpeed=0,minDensity=w.C.Density.Min(),maxK=Math.Max(w.C.RocheRigid,w.C.RocheFluid);
                var hosts=new List<(int Slot,double Upper,double Drift)>();
                for(int p=0;p<w.N;p++)if(w.Alive[p])
                {
                    if(vector)maxSpeed=Math.Max(maxSpeed,w.Vx[p]*w.Vx[p]+w.Vy[p]*w.Vy[p]);
                    if(!w.Attracts(p))continue;
                    hosts.Add((p,maxK*w.R[p]*Math.Cbrt(density(p)/minDensity),Math.Sqrt(w.Vx[p]*w.Vx[p]+w.Vy[p]*w.Vy[p])*seconds));
                    acceleration+=w.C.G*w.M[p]/(w.R[p]*w.R[p]);
                }
                var expectedCandidates=new List<(int,int,int,int)>();
                for(int i=0;i<w.N;i++)
                {
                    if(!w.Alive[i]||w.IsShip(i)||w.StarPhaseOf(i)!=StarPhase.None)continue;
                    double drift=(vector?Math.Sqrt(maxSpeed):Math.Sqrt(w.Vx[i]*w.Vx[i]+w.Vy[i]*w.Vy[i]))*seconds;
                    foreach(var host in hosts)
                    {
                        int p=host.Slot;if(w.M[p]<=w.M[i])continue;
                        double dx=w.X[i]-w.X[p],dy=w.Y[i]-w.Y[p],reach=host.Upper+drift+host.Drift+2*acceleration*seconds*seconds;
                        bool lane=vector&&i<w.N-w.N%Vector<double>.Count;
                        if(dx*dx+dy*dy<=reach*reach*(lane?1+w.C.RocheEntryTolerance:1)&&limit(i,p,true)>w.R[p]+w.R[i])
                            expectedCandidates.Add((i,p,w.Gen[i],w.Gen[p]));
                    }
                }
                if(!snapshot.SequenceEqual(expectedCandidates))
                {
                    differences++;if(differences<=5)Console.WriteLine($"FAILED roche-collector: dt{seconds:R} old{expectedCandidates.Count}/new{snapshot.Length}, order/set mismatch");
                }
                var expected=(Time:double.PositiveInfinity,Body:-1,Host:-1,BodyGen:0,HostGen:0);
                // Frozen pre-cull query (a1c832f). This oracle intentionally retains its floating-point
                // solver, ordering, and tolerance; testing a duplicate of the new filter would prove nothing.
                foreach(var pair in snapshot)
                {
                    int i=pair.Body,p=pair.Host;
                    if(!w.Alive[i]||!w.Alive[p]||w.Gen[i]!=pair.BodyGen||w.Gen[p]!=pair.HostGen)continue;
                    double radius=limit(i,p,true);if(!(radius>w.R[p]+w.R[i]))continue;
                    double dx=w.X[i]-w.X[p],dy=w.Y[i]-w.Y[p],surface=w.R[p]+w.R[i];
                    if(dx*dx+dy*dy<=surface*surface)continue;
                    double at=dx*dx+dy*dy<=radius*radius*(1+w.C.RocheEntryTolerance)?0:
                        contact(dx,dy,w.Vx[i]-w.Vx[p],w.Vy[i]-w.Vy[p],w.C.G*(w.M[i]+w.M[p]),radius);
                    if(at<=seconds&&at<expected.Time)expected=(at,i,p,w.Gen[i],w.Gen[p]);
                }
                var actual=query(seconds);queries++;
                if(actual!=expected||!snapshot.SequenceEqual(candidates))
                {
                    differences++;
                    if(differences<=5)Console.WriteLine($"FAILED roche-query: dt{seconds:R} expected{expected} actual{actual}, candidates{snapshot.Length}");
                }
            }
        }
        Compare(World.SolSystem(5000,1234));
        var ns=World.SolSystem(5000,1234);
        int star=ns.Add(100,0,0,0,70.005,ns.Mix(("gas",1)));double end=1+ns.C.StarGiantFraction,birth=10*ns.C.StarSolarMass;
        ns.Do(new Command(CmdKind.SetStarState,Target:star,StarState:new StellarState(ns.StarLifetime(birth)*end,end,birth)));
        Compare(ns);ns.Advance(0);Compare(ns);
        // Dense rings; randomized mixed bodies; bound, unbound and nearly parabolic trajectories.
        for(int scene=0;scene<200;scene++)
        {
            var w=new World(128,(ulong)scene+1);
            int host=w.Add(0,0,0,0,scene%2==0?70.005:.05,w.Mix(("gas",1)));
            if(scene%2==0)w.Do(new Command(CmdKind.SetStarState,Target:host,StarState:new StellarState(w.StarLifetime(birth)*end,end,birth)));
            for(int i=0;i<64;i++)
            {
                double angle=rng.NextDouble()*Math.Tau,r=Math.Pow(10,-2+4*rng.NextDouble());
                double mass=Math.Pow(10,-14+10*rng.NextDouble());double ice=rng.NextDouble();
                double escape=Math.Sqrt(2*w.C.G*(w.M[host]+mass)/r);
                double speed=scene%5==0?escape*(1+(rng.NextDouble()-.5)*1e-12):escape*(.1+2*rng.NextDouble());
                double heading=scene%3==0?angle+Math.PI/2:rng.NextDouble()*Math.Tau;
                w.Add(r*Math.Cos(angle),r*Math.Sin(angle),speed*Math.Cos(heading),speed*Math.Sin(heading),mass,w.Mix(("ice",ice),("rock",1-ice)));
            }
            if(scene%4==0){w.C.RocheIceStrengthPa=0;w.C.RocheRockStrengthPa=0;}
            Compare(w);
        }
        bool ok=differences==0;
        Console.WriteLine($"{(ok?"OK    ":"FAILED")} roche-query: {queries} old/new queries, {differences} differences; same candidates, exact time/source/host/generations");
        return ok;
    }
}
