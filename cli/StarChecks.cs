// SPEC 8: checks use commands, derived phases and conservation including matter that left the domain.
using System;
using System.Diagnostics;
using System.Linq;
using Cosmos.Core;

static class StarChecks
{
    static readonly double[] Gas = { 1, 0, 0, 0, 0, 0 };
    static readonly double[] Rock = { 0, 0, 1, 0, 0, 0 };
    static int Jump(World w, double years) => w.Do(new Command(CmdKind.FastForward, Amount: years));
    static void Set(World w, string field, double value) => w.Do(new Command(CmdKind.SetConst, Name: field, Amount: value));
    static World Single(double suns, double vx = 0, double vy = 0)
    {
        var w = new World(16, 1); w.Do(new Command(CmdKind.Create, Amount: suns * 50, Mix: Gas, Vx: vx, Vy: vy)); return w;
    }
    static double Px(World w) => Enumerable.Range(0, w.N).Where(i => w.Alive[i]).Sum(i => w.M[i] * w.Vx[i]) + w.StellarEjectaPx;
    static double Py(World w) => Enumerable.Range(0, w.N).Where(i => w.Alive[i]).Sum(i => w.M[i] * w.Vy[i]) + w.StellarEjectaPy;

    public static bool Run()
    {
        bool ok = true;
        void Check(bool pass, string line) { ok &= pass; Console.WriteLine($"{(pass ? "OK    " : "FAILED")} stars: {line}"); }
        var sun = Single(1);
        double lifetime = sun.StarLifetime(50), dwarf = sun.StarLifetime(5), massive = sun.StarLifetime(500);
        Check(Math.Abs(lifetime / 1e10 - 1) < .05 && dwarf > 13.8e9 && dwarf > lifetime && lifetime > massive && massive < 3e7,
            $"lifetimes .1/1/10 Suns {dwarf:E6}/{lifetime:E6}/{massive:E6} yr; scale={sun.C.StarLifeScale}");
        var red = Single(.1); var blue = Single(10);
        Check(sun.StarLuminosity(0) == 1 && sun.StarSpectralClass(0) == StarSpectrum.G
            && red.StarSpectralClass(0) == StarSpectrum.M && blue.StarSurfaceTemperature(0) > sun.StarSurfaceTemperature(0)
            && red.StarRadius(0) < sun.StarRadius(0) && blue.StarRadius(0) > sun.StarRadius(0),
            $"mass -> light/size/colour: Sun 1 Lsun/{sun.StarSurfaceTemperature(0):F0}K/{sun.StarSpectralClass(0)}, red {red.StarSurfaceTemperature(0):F0}K, blue {blue.StarSurfaceTemperature(0):F0}K");

        foreach (int rocks in new[] { 0, 5000 })
        {
            var sol = World.SolSystem(rocks, 1234); Jump(sol, 1e6);
            double reference = rocks == 0 ? 288.10794065768596 : 288.10794065744284; // measured b149837 DLL, same seed/inputs
            Check(Math.Abs(sol.Temp[3] - reference) <= 1e-9 && sol.StarLuminosity(0) == 1 && sol.M[0] == 50,
                $"Sol +1e6yr, {rocks} rocks: Earth {sol.Temp[3]:R}K vs master {reference:R}, delta {sol.Temp[3] - reference:E2}");
        }

        var brown = Single(.05); double youngLight = brown.StarLuminosity(0);
        Jump(brown, 1e9);
        Check(brown.StarPhaseOf(0) == StarPhase.BrownDwarf && !brown.IsWorld(0) && brown.StarFuel[0] == 0
            && brown.StarLuminosity(0) < youngLight && brown.StarLuminosity(0) > 0,
            $"brown dwarf: not IsWorld, no H fuel burn, light {youngLight:E3} -> {brown.StarLuminosity(0):E3}");
        var ignited = Single(.05); Set(ignited, "StarLifeScale", 1e-8); Jump(ignited, 10);
        ignited.Do(new Command(CmdKind.AddMatter, Target: 0, Index: 0, Amount: 47.5));
        bool main = ignited.StarPhaseOf(0) == StarPhase.MainSequence && ignited.StarFuel[0] == 0 && ignited.StarInitialMass[0] == 50;
        Jump(ignited, 150);
        Check(main && ignited.StarPhaseOf(0) == StarPhase.WhiteDwarf, "brown dwarf gains mass -> H ignition -> correct progenitor and remnant, no stored type");
        var edited = Single(1); Set(edited, "StarLifeScale", 1e-8);
        edited.Do(new Command(CmdKind.AddMatter, Target: 0, Index: 0, Amount: 450)); Jump(edited, 1);
        Check(edited.StarInitialMass[0] == 500 && edited.StarPhaseOf(0) == StarPhase.NeutronStar,
            "main-sequence mass edit changes lifetime and remnant fate through progenitor numbers");
        var dial = Single(1); dial.Advance(0); Set(dial, "StarLifeScale", 1e-10);
        dial.Advance(dial.C.YearTime * 1.01);
        bool changed = dial.StarPhaseOf(0) == StarPhase.RedGiant && dial.Events.Any(e => e.Change == "star.giant" && Math.Abs(e.Year - 1) < 1e-12);
        dial.Advance(dial.C.YearTime * .05);
        Check(changed && dial.StarPhaseOf(0) == StarPhase.WhiteDwarf && dial.Events.Any(e => e.Change == "star.remnant.white" && Math.Abs(e.Year - 1.05) < 1e-12),
            "clock dial changed after first tick: Advance reschedules giant/death at 1 / 1.05 years");
        var removed = Single(10); Set(removed, "StarLifeScale", 1e-8);
        removed.Rules.Remove(removed.Rules.Find(r => r.Id == "stars")!);
        Jump(removed, 10);
        Check(Math.Abs(removed.Year - 10) < 1e-12 && removed.StarFuel[0] == 0 && removed.Events.All(e => e.RuleId != "stars"),
            $"removed stellar rule: jump completes, year={removed.Year:R}, fuel={removed.StarFuel[0]:R}, no unhandled boundary");

        foreach (double mass in new[] { 1.0, 10.0, 30.0 })
        {
            var a = Single(mass, .02, -.03); var b = Single(mass, .02, -.03);
            Set(a, "JumpSamples", 1); Set(b, "JumpSamples", 200);
            double end = a.StarLifetime(mass * 50) * (1 + a.C.StarGiantFraction), beforeMass = a.M[0];
            double px = Px(a), py = Py(a);
            var timer = Stopwatch.StartNew(); Jump(a, end * 1.1); Jump(b, end * 1.1); timer.Stop();
            var ae = a.Events.Where(e => e.RuleId == "stars").ToArray(); var be = b.Events.Where(e => e.RuleId == "stars").ToArray();
            bool same = ae.Length == be.Length && ae.Length >= 2;
            for (int i = 0; same && i < ae.Length; i++) same &= ae[i].Change == be[i].Change && Math.Abs(ae[i].Year - be[i].Year) <= 1e-4;
            StarPhase expected = mass < 8 ? StarPhase.WhiteDwarf : mass <= 20 ? StarPhase.NeutronStar : StarPhase.BlackHole;
            Check(a.StarPhaseOf(0) == expected && b.StarPhaseOf(0) == expected && same
                && Math.Abs(a.M[0] + a.StellarEjectaMass - beforeMass) < 1e-10
                && Math.Abs(a.Comp.Take(World.NElem).Sum() + a.StellarEjectaMatter.Sum() - beforeMass) < 1e-10
                && Math.Abs(Px(a) - px) < 1e-10 && Math.Abs(Py(a) - py) < 1e-10,
                $"{mass:F0} Suns -> {a.StarPhaseOf(0)}, mass {a.M[0] / 50:F3} Suns, lost {a.StellarEjectaMass:F3}; 1/200 phase years [{string.Join(",", ae.Select(e => $"{e.Change}@{e.Year:R}"))}], momentum conserved, {timer.Elapsed.TotalMilliseconds:F1}ms");
            Check(Math.Abs(a.StarAge[0] - b.StarAge[0]) < 1e-4 && Math.Abs(a.StarFuel[0] - b.StarFuel[0]) < 1e-12
                && Math.Abs(a.StarLuminosity(0) - b.StarLuminosity(0)) < 1e-12,
                $"{mass:F0} Suns stretch-exact age/fuel/cooling; light {a.StarLuminosity(0):E3}, radius {a.R[0]:E3}");
            if (mass == 10) Check(ae.Last().Year < 3e7 && ae.Any(e => e.Change == "star.nova"), "10-Sun nova and neutron remnant before 30 Myr");
            if (mass == 30) Check(a.StarLuminosity(0) == 0 && a.StarSurfaceTemperature(0) == 0 && a.R[0] < 1e-2, "black hole is dark, tiny Schwarzschild-like radius");
        }

        var giant = Single(1); Set(giant, "StarLifeScale", 1e-9);
        int planet = giant.Do(new Command(CmdKind.CreateOrbiting, Target: 0, X: 45, Amount: World.EarthMass, Mix: Rock));
        double mainRadius = giant.R[0]; Jump(giant, giant.StarLifetime(50));
        Check(giant.StarPhaseOf(0) == StarPhase.RedGiant && giant.R[0] >= mainRadius * 99.9 && giant.Temp[planet] > giant.C.ScorchedEdge
            && giant.StarSurfaceTemperature(0) <= giant.C.StarGiantTemperature,
            $"giant: radius {mainRadius:F3}->{giant.R[0]:F3}, planet {giant.Temp[planet]:F0}K");
        giant.Advance(0);
        Check(!giant.Alive[planet] && giant.StarPhaseOf(0) == StarPhase.RedGiant, "normal collision swallows inner planet without rejuvenating exhausted core");

        // Keep this biosphere outside the giant's envelope; isolate nova damage from the contact check above.
        var nova = Single(10); Set(nova, "StarNovaRange", 40000);
        int world = nova.Do(new Command(CmdKind.CreateOrbiting, Target: 0, X: 20000, Amount: World.EarthMass,
            Mix: new double[] { 0, .01, .66, .32, .005, .005 }));
        nova.Do(new Command(CmdKind.SeedLife, Target: world, Amount: .8));
        nova.Do(new Command(CmdKind.SetRule, Name: "life", Amount: 0));
        nova.Do(new Command(CmdKind.SetRule, Name: "civ", Amount: 0));
        Jump(nova, 3e7);
        Check(nova.Alive[world] && nova.Life[world] < .8 && nova.Events.Any(e => e.RuleId == "impact" && e.ObjectSlot == world),
            $"nova damages nearby biosphere: life .8 -> {nova.Life[world]:E3}");

        var merged = new World(8, 1); Set(merged, "G", 0);
        int x = merged.Do(new Command(CmdKind.Create, X: -1e6, Amount: 50, Mix: Gas, Vx: -.01));
        int y = merged.Do(new Command(CmdKind.Create, X: 1e6, Amount: 100, Mix: Gas, Vx: .01));
        Jump(merged, 1e8);
        double fuel = (merged.StarFuel[x] * 50 + merged.StarFuel[y] * 100) / 150, momentum = Px(merged);
        merged.Do(new Command(CmdKind.Move, Target: x, X: merged.X[y], Y: merged.Y[y], Vx: merged.Vx[x], Vy: merged.Vy[x]));
        merged.Advance(0);
        Check(merged.Alive[y] && !merged.Alive[x] && merged.M[y] == 150 && Math.Abs(merged.StarFuel[y] - fuel) < 1e-12
            && Math.Abs(Px(merged) - momentum) < 1e-10, $"two stars merge: mass={merged.M[y]:F1}, burnt fuel mixed={merged.StarFuel[y]:F9}, momentum kept");
        merged.Do(new Command(CmdKind.Remove, Target: y));
        int reused = merged.Do(new Command(CmdKind.Create, Amount: 50, Mix: Gas));
        Check(reused == y && merged.StarAge[reused] == 0 && merged.StarFuel[reused] == 0 && merged.StarInitialMass[reused] == 50,
            "reused slot resets stellar age, fuel, initial mass");

        // Selica's blocker: a surviving 1.2-Sun burning core must not inherit the swallowed WD's exhausted floor.
        var accreted = Single(1.2); Set(accreted, "G", 0); Set(accreted, "StarLifeScale", 1e-8);
        int wd = accreted.Do(new Command(CmdKind.Create, X: 1e6, Amount: 100, Mix: Gas));
        Jump(accreted, 20);
        Set(accreted, "StarLifeScale", accreted.C.StarLifeScale); // settle both clocks at the current year before measuring the mixture
        bool setup = accreted.StarPhaseOf(0) == StarPhase.MainSequence && accreted.StarPhaseOf(wd) == StarPhase.WhiteDwarf
            && Math.Abs(accreted.M[wd] / 50 - .612) < 1e-12;
        double accretedFuel = (accreted.StarFuel[0] * accreted.M[0] + accreted.StarFuel[wd] * accreted.M[wd]) / (accreted.M[0] + accreted.M[wd]);
        int accretedEvents = accreted.Events.Count;
        double accretedEjecta = accreted.StellarEjectaMass;
        accreted.Do(new Command(CmdKind.Move, Target: wd, X: accreted.X[0], Y: accreted.Y[0]));
        accreted.Advance(0);
        Check(setup && accreted.Alive[0] && !accreted.Alive[wd] && accreted.StarPhaseOf(0) == StarPhase.MainSequence
            && Math.Abs(accreted.M[0] / 50 - 1.812) < 1e-12 && Math.Abs(accreted.StarFuel[0] - accretedFuel) < 1e-12
            && accreted.Events.Count == accretedEvents && accreted.StellarEjectaMass == accretedEjecta,
            $"1.2-Sun MS swallows .612-Sun WD: {accreted.StarPhaseOf(0)}, mass {accreted.M[0] / 50:F3} Suns, fuel {accreted.StarFuel[0]:F9} (mixed {accretedFuel:F9}), no spurious death/ejecta");
        var accretedReplay = new World(16, 1); int accretedNext = 0;
        accretedReplay.Replay(accreted.Journal, ref accretedNext); accretedReplay.Advance(0);
        Check(accretedReplay.Hash() == accreted.Hash(), $"WD accretion replay {accreted.Hash():X16}/{accretedReplay.Hash():X16}");

        var deadCore = Single(2); Set(deadCore, "G", 0); Set(deadCore, "StarLifeScale", 1e-8); Jump(deadCore, 20);
        int food = deadCore.Do(new Command(CmdKind.Create, X: 1e6, Amount: World.EarthMass, Mix: Rock));
        double deadFuel = deadCore.StarFuel[0];
        deadCore.Do(new Command(CmdKind.Move, Target: food, X: deadCore.X[0], Y: deadCore.Y[0])); deadCore.Advance(0);
        Check(deadCore.Alive[0] && !deadCore.Alive[food] && deadCore.StarPhaseOf(0) == StarPhase.WhiteDwarf && deadCore.StarFuel[0] >= deadFuel,
            "surviving white dwarf swallows a planet without rejuvenating its exhausted core");

        var orbit = Single(1); Set(orbit, "StarLifeScale", 1e-9);
        int far = orbit.Do(new Command(CmdKind.CreateOrbiting, Target: 0, X: 10000, Amount: World.EarthMass, Mix: Rock));
        Jump(orbit, orbit.StarLifetime(50) * (1 + orbit.C.StarGiantFraction));
        double rx = orbit.X[far] - orbit.X[0], ry = orbit.Y[far] - orbit.Y[0], vx = orbit.Vx[far] - orbit.Vx[0], vy = orbit.Vy[far] - orbit.Vy[0];
        double r = Math.Sqrt(rx * rx + ry * ry), mu = orbit.C.G * (orbit.M[0] + orbit.M[far]);
        double semiMajor = 1 / (2 / r - (vx * vx + vy * vy) / mu);
        Check(orbit.StarPhaseOf(0) == StarPhase.WhiteDwarf && semiMajor > 10000 && double.IsFinite(semiMajor),
            $"mass loss widens orbital semimajor through gravity: 10000 -> {semiMajor:F1}; impulsive loss, no forced planet velocity");

        var ordinary = new World(128, 1); var observed = new World(128, 1);
        foreach (var w in new[] { ordinary, observed })
        {
            w.Do(new Command(CmdKind.Create, Amount: 500, Mix: Gas));
            for (int i = 0; i < 100; i++) w.Do(new Command(CmdKind.CreateOrbiting, Target: 0, X: 10000 + i * 100, Amount: 1e-9, Mix: Rock));
        }
        var view = new Rule("star.observer", "X", "", 1, _ => { }) { Enabled = false }; observed.Rules.Add(view);
        Jump(ordinary, 1e9); Jump(observed, 1e9); observed.Rules.Remove(view); // exactly 5e6 years per original chunk
        Check(ordinary.Hash() == observed.Hash() && ordinary.StarPhaseOf(0) == StarPhase.NeutronStar
            && ordinary.X.All(double.IsFinite) && ordinary.Vx.All(double.IsFinite),
            $"5-Myr chunks crossing nova: rock origins rebased, observer-independent hash {ordinary.Hash():X16}/{observed.Hash():X16}");

        var live = Single(10); Set(live, "StarLifeScale", .01); Jump(live, 3e5);
        live.Do(new Command(CmdKind.SetConst, Name: "StarLifeScale", Amount: .001)); Jump(live, 1e6);
        var replay = new World(16, 1); int next = 0; replay.Replay(live.Journal, ref next);
        Check(live.Hash() == replay.Hash(), $"journal replay across giant/nova/remnant hash {live.Hash():X16}/{replay.Hash():X16}");
        ulong hash = live.Hash(); live.StarFuel[0] -= .001;
        Check(live.Hash() != hash, "stellar fuel participates in Hash");
        return ok;
    }
}
