using System;
using System.Linq;
using Cosmos.Core;

static class RocheChecks
{
    static World Scene(int capacity = 128, double factor = .999, double speed = 1, double budget = 10000, double iceStrength = 1e6)
    {
        var w = new World(capacity, 7);
        foreach (string rule in new[] { "stars", "life", "civ", "ships", "water", "temperature", "comets" }) w.Do(new Command(CmdKind.SetRule, Name: rule, Amount: 0));
        w.Do(new Command(CmdKind.SetConst, Name: "RocheRockBudget", Amount: budget));
        w.Do(new Command(CmdKind.SetConst, Name: "RocheIceStrengthPa", Amount: iceStrength));
        int p = w.Do(new Command(CmdKind.Create, X: 3, Y: -2, Vx: .2, Vy: -.1, Amount: .05, Mix: w.Mix(("gas", 1))));
        int i = w.Do(new Command(CmdKind.Create, X: 5, Y: -2, Amount: 1e-6, Mix: w.Mix(("ice", 1))));
        double radius = factor * w.RocheLimit(i, p);
        w.Do(new Command(CmdKind.Move, Target: i, X: w.X[p] + radius, Y: w.Y[p], Vx: w.Vx[p],
            Vy: w.Vy[p] + speed * Math.Sqrt(w.C.G * (w.M[p] + w.M[i]) / radius), Index: 1));
        return w;
    }

    static (double Mass, double Px, double Py, double ComX, double ComY, double L, double[] Matter) Ledger(World w)
    {
        double mass = w.EscapedMass, px = w.EscapedPx, py = w.EscapedPy, cx = 0, cy = 0, l = 0;
        var cells = (double[])w.EscapedMatter.Clone();
        for (int i = 0; i < w.N; i++) if (w.Alive[i])
        {
            mass += w.M[i]; px += w.M[i] * w.Vx[i]; py += w.M[i] * w.Vy[i]; cx += w.M[i] * w.X[i]; cy += w.M[i] * w.Y[i];
            l += w.M[i] * (w.X[i] * w.Vy[i] - w.Y[i] * w.Vx[i]);
            for (int e = 0; e < w.ElementCount; e++) cells[e] += w.Comp[i * w.ElementCount + e];
        }
        foreach (var r in w.RocheReservoirs)
        {
            mass += r.Mass; px += r.Px; py += r.Py; cx += r.Mass * r.X; cy += r.Mass * r.Y;
            l += r.X * r.Py - r.Y * r.Px + r.AngularMomentum;
            for (int e = 0; e < w.ElementCount; e++) cells[e] += r.Matter[e];
        }
        foreach (var r in w.EscapedTransfers) { cx += r.Mass * r.X; cy += r.Mass * r.Y; l += r.X * r.Py - r.Y * r.Px; }
        return (mass, px, py, cx, cy, l, cells);
    }

    public static bool Run()
    {
        bool ok = true;
        void Check(bool pass, string text) { ok &= pass; Console.WriteLine($"{(pass ? "OK    " : "FAILED")} roche: {text}"); }
        var limits = new World(8, 7);
        limits.C.RocheIceStrengthPa = limits.C.RocheRockStrengthPa = 0;
        int host = limits.Add(0,0,0,0,.05,limits.Mix(("gas",1)));
        int icy = limits.Add(2,0,0,0,1e-6,limits.Mix(("ice",1)));
        int rocky = limits.Add(3,0,0,0,1e-6,limits.Mix(("rock",1)));
        limits.C.Density[limits.Elem("ice")] = limits.C.Density[limits.Elem("rock")]; limits.RecalcRadii();
        Check(Math.Abs(limits.RocheLimit(icy,host) / limits.RocheLimit(rocky,host) - 2.44/1.26) < 1e-14, "same density fluid/rigid ratio 2.44/1.26");
        int mixed = limits.Add(4,0,0,0,1e-6,limits.Mix(("ice",.5),("rock",.5)));
        Check(Math.Abs(limits.RocheLimit(mixed,host) - (limits.RocheLimit(icy,host)+limits.RocheLimit(rocky,host))/2)<1e-14,
            "equal-density mixed matter interpolates rigid/fluid coefficients by mass");
        limits.C.RocheIceStrengthPa = 1e6;
        int tiny = limits.Add(4,0,0,0,1e-14,limits.Mix(("ice",1)));
        Check(limits.RocheLimit(tiny,host) < limits.R[host], "small cohesive ice fragment survives outside host surface");
        foreach (var cfg in new[] { (128,10000.0), (2,10000.0), (128,0.0), (4,10000.0) })
        {
            var w = Scene(cfg.Item1,budget:cfg.Item2); var before = Ledger(w); int generation = w.Gen[1];
            w.Advance(0); var after = Ledger(w);
            var eventRow = w.RocheDisruptions.Single();
            double fragments = Enumerable.Range(0,w.N).Where(i => w.Alive[i] && w.Grp[i] == eventRow.Group).Sum(i => w.M[i]);
            double reserve = w.RocheReservoirs.Sum(r => r.Mass);
            Check(eventRow.Formation == "ring" && Math.Abs(fragments+reserve-1e-6) < 1e-18 && w.EscapedMass == 0,
                $"capacity{cfg.Item1}/budget{cfg.Item2}: ring {fragments:E4}+bound reservoir {reserve:E4}=parent; no false escape");
            Check(Math.Abs(before.Mass-after.Mass) < 1e-14 && Math.Abs(before.Px-after.Px) < 1e-14 && Math.Abs(before.Py-after.Py) < 1e-14
                && before.Matter.Zip(after.Matter).All(x => Math.Abs(x.First-x.Second)<1e-14), "mass, each material, both momentum components close");
            Check(Math.Abs(before.ComX-after.ComX)<1e-14 && Math.Abs(before.ComY-after.ComY)<1e-14 && Math.Abs(before.L-after.L)<1e-14,
                $"COM/L close: dL={after.L-before.L:E3}; energy dissipated {w.RocheDissipatedEnergy:E4}");
            Check(eventRow.SourceGeneration == generation && (!w.Alive[1] || w.Gen[1] != generation)
                && w.RocheDissipatedEnergy >= 0, "source retired, identity not inherited; no circularization energy creation");
            var r = new World(cfg.Item1,7); int next=0; r.Replay(w.Journal,ref next); r.Advance(0);
            Check(w.Hash()==r.Hash(),$"journal+Advance replay {w.Hash():X16}/{r.Hash():X16}");
        }
        var persistent = Scene(); persistent.Advance(0);
        double ringMass = Ledger(persistent).Mass;
        persistent.Do(new Command(CmdKind.FastForward,Amount:1e6));
        Check(Math.Abs(Ledger(persistent).Mass-ringMass)<1e-12 && persistent.Live==65 && persistent.RocheDisruptions.Count==1,
            $"1e6yr circular ring: live={persistent.Live}, breakups={persistent.RocheDisruptions.Count}, mass={Ledger(persistent).Mass:R}");
        Check(Enumerable.Range(1,persistent.N).Where(i=>persistent.Alive[i]).All(i =>
            {
                double dx=persistent.X[i]-persistent.X[0], dy=persistent.Y[i]-persistent.Y[0]; double d=Math.Sqrt(dx*dx+dy*dy);
                return d>persistent.R[0]+persistent.R[i] && d<limits.C.RocheFluid*persistent.R[0]*Math.Cbrt(.3/.9);
            }),"long-jump fragments remain outside surface and inside cohesionless Roche");
        foreach(double mult in new[]{1.415,3.0})
        {
            var w=Scene(speed:mult); var before=Ledger(w); w.Advance(0); var after=Ledger(w); var row=w.RocheDisruptions.Single();
            Check(mult<2 ? row.CapturedMass>0 && row.CapturedMass<1e-6 : row.CapturedMass==0,
                $"hyperbolic speed{mult}: captured={row.CapturedMass:R}, escaped={row.EscapedMass:R}, {row.Formation}");
            Check(Math.Abs(before.Mass-after.Mass)<1e-14 && Math.Abs(before.Px-after.Px)<1e-14 && Math.Abs(before.Py-after.Py)<1e-14
                && Math.Abs(before.ComX-after.ComX)<1e-14 && Math.Abs(before.L-after.L)<1e-14,"hyperbolic transfer M/P/COM/L closes");
            Check(Enumerable.Range(0,w.N).Where(i=>w.Alive[i] && w.Grp[i]==row.Group).All(i=>
                {
                    double dx=w.X[i]-w.X[0],dy=w.Y[i]-w.Y[0],vx=w.Vx[i]-w.Vx[0],vy=w.Vy[i]-w.Vy[0];
                    return (vx*vx+vy*vy)/2 < w.C.G*(w.M[0]+w.M[i])/Math.Sqrt(dx*dx+dy*dy);
                }),"captured stream fragments are physically bound, not only labeled captured");
        }
        var one=Scene(factor:1.2,speed:.5); var many=Scene(factor:1.2,speed:.5);
        one.Do(new Command(CmdKind.SetConst,Name:"JumpSamples",Amount:1));
        many.Do(new Command(CmdKind.SetConst,Name:"JumpSamples",Amount:200));
        one.Do(new Command(CmdKind.FastForward,Amount:.1)); many.Do(new Command(CmdKind.FastForward,Amount:.1));
        Check(one.RocheDisruptions.Count>0 && many.RocheDisruptions.Count>0
            && Math.Abs(one.RocheDisruptions[0].Year-many.RocheDisruptions[0].Year)<1e-9,
            $"first-entry 1/200 cuts: events={one.RocheDisruptions.Count}/{many.RocheDisruptions.Count}");
        var replay=new World(128,7); int cursor=0; replay.Replay(one.Journal,ref cursor);
        Check(one.Hash()==replay.Hash(),$"jump replay {one.Hash():X16}/{replay.Hash():X16}");
        var old = Scene(factor:2);
        old.Do(new Command(CmdKind.SetRule,Name:"roche",Amount:0));
        old.Do(new Command(CmdKind.FastForward,Amount:1e10));
        double oldRadius=old.RocheLimit(1,0)*1.2;
        old.Do(new Command(CmdKind.Move,Target:1,X:old.X[0]+oldRadius,Y:old.Y[0],Vx:old.Vx[0],Vy:old.Vy[0]+.5*Math.Sqrt(old.C.G*old.M[0]/oldRadius),Index:1));
        old.Do(new Command(CmdKind.SetRule,Name:"roche",Amount:1));
        old.Do(new Command(CmdKind.FastForward,Amount:.1));
        Check(old.Year>1e10 && old.RocheDisruptions.Count>0,"positive-Gyr clock makes progress across rounded first-entry dates");
        var compact=new World(8,7);
        int star=compact.Do(new Command(CmdKind.Create,Amount:150,Mix:compact.Mix(("gas",1))));
        compact.Do(new Command(CmdKind.SetStarState,Target:star,StarState:new(0,1+compact.C.StarGiantFraction,150)));
        int debris=compact.Add(4,0,0,0,1e-6,compact.Mix(("ice",1)));
        Check(compact.StarPhaseOf(star)==StarPhase.WhiteDwarf && compact.RocheLimit(debris,star)>compact.R[star]+compact.R[debris],
            "compact primary uses physical stellar density, not its gas composition density");
        var impact=World.SolSystem(0,1234);impact.Do(new Command(CmdKind.SeedLife,Target:3,Amount:.5));
        int slot=impact.Add(impact.X[3]+.01,impact.Y[3],impact.Vx[3],impact.Vy[3],1e-4,impact.Mix(("rock",1)),"impactor");
        int identity=impact.Gen[slot];for(int k=0;k<6;k++)impact.Advance(.5);
        Check((!impact.Alive[slot] || impact.Gen[slot]!=identity) && impact.Life[3]<.5,"impact source retires even when tidal debris later reuses its slot");
        var dial=Scene();int entries=dial.Journal.Count;
        Check(dial.Do(new Command(CmdKind.SetConst,Name:"RocheDensityKgM3",Amount:0))==-1
            && dial.Do(new Command(CmdKind.SetConst,Name:"RocheFragments",Amount:1.5))==-1
            && dial.Do(new Command(CmdKind.SetConst,Name:"RocheEnergySpread",Amount:2))==-1 && dial.Journal.Count==entries,
            "invalid physical conversion and numerical budget dials are refused");
        dial.Do(new Command(CmdKind.SetConst,Name:"RocheRhythmYears",Amount:2));
        Check(dial.Rules.Single(r=>r.Id=="roche").RhythmYears==2,"god rhythm dial changes actual housekeeping cadence");
        var retained=Scene(speed:1.6);double separation=retained.X[1]-retained.X[0];
        retained.Do(new Command(CmdKind.Move,Target:0,X:45,Y:0,Vx:retained.Vx[0],Vy:retained.Vy[0],Index:1));
        retained.Do(new Command(CmdKind.Move,Target:1,X:45+separation,Y:0,Vx:retained.Vx[1],Vy:retained.Vy[1],Index:1));
        int root=retained.Add(0,0,0,0,50,retained.Mix(("gas",1)));var retainedBefore=Ledger(retained);
        retained.Advance(0);var retainedAfter=Ledger(retained);
        Check(retained.RocheDisruptions.Single().CapturedMass==0 && retained.EscapedMass==0
            && retained.RocheReservoirs.Single().Host==root && Math.Abs(retainedBefore.Mass-retainedAfter.Mass)<1e-12,
            "leaving planet while star-bound retains material in root reservoir, not Escaped");
        var reversed=new World(8,7,ElementCatalog.Elements.Reverse());
        reversed.C.RocheIceStrengthPa=reversed.C.RocheRockStrengthPa=0;
        int rp=reversed.Add(0,0,0,0,.05,reversed.Mix(("gas",1))),ri=reversed.Add(3,0,0,0,1e-6,reversed.Mix(("ice",1)));
        reversed.C.Density[reversed.Elem("ice")]=reversed.C.Density[reversed.Elem("rock")];reversed.RecalcRadii();
        limits.C.RocheIceStrengthPa=0;
        Check(reversed.RocheLimit(ri,rp)==limits.RocheLimit(icy,host),"Roche material roles follow reordered element ids");
        return ok;
    }
}
