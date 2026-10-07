using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Cosmos.Core;

public sealed partial class Consts
{
    public double RocheRigid = 1.26; // [P] cohesionless spherical limit, cbrt(2); Aggarwal/Oberbeck1974.
    public double RocheFluid = 2.44; // [P] fluid equilibrium; Holsapple/Michel2008.
    public double RocheIceStrengthPa = 1e6; // [P] representative ice tensile strength, strongly temperature/porosity dependent.
    public double RocheRockStrengthPa = 1e7; // [P] representative fractured rock tensile strength, not compressive strength.
    public double RocheMetalStrengthPa = 1e8; // [P] representative metal tensile strength.
    public double RochePhysicalG = 6.6743e-11; // [P] SI Newton constant; C.G scales the gravity dial.
    public double RocheSolarMassKg = 1.98847e30; // [P] NASA solar mass reference.
    public double RocheSolarRadiusKm = 696340; // [P] inherited C5 physical-radius conversion in CosmicEvents.cs.
    public double RocheDensityKgM3 = 1000; // [P] table densities are g/cm3 -> kg/m3.
    public double RocheFragments = 64; // [P] numerical object budget, not microscopic grain count.
    public double RocheRockBudget = 10000; // [P] numerical total small-object budget.
    public double RocheFragmentMass = 4e-8; // [P] representation budget, capped below AttractMass too.
    public double RocheChangesPerStep = 256; // [P] bounds zero-time cascade cost, pending bodies stay physical objects.
    public double RocheRhythmYears = 1; // [P] housekeeping rhythm; physical entry is cut independently.
    public double RocheEnergySpread = .9; // [P] Dones approximation, Hyodo2016 eq2; no spin/self-gravity fit.
    public double RocheStreamWidth = .01; // [P] numerical initial transverse stream width, as fraction of parent radius.
    public double RocheEntryTolerance = 1e-12; // [P] numerical roundoff at analytic first-entry surface.
}

// A reservoir is unresolved BOUND matter, not escaped mass. Snapshot of its formation frame; no particle
// forces/collisions until it is resolved. This explicit resolution exception was accepted in C6 STEP0.
public sealed record RocheReservoir(int Host, int HostGeneration, double X, double Y, double Mass,
    double Px, double Py, double AngularMomentum, IReadOnlyList<double> Matter);
public sealed record RocheDisruption(double Year, int Source, int SourceGeneration, int Host, int HostGeneration,
    int Group, string Formation, double SourceMass, double CapturedMass, double EscapedMass, double DissipatedEnergy);

public sealed partial class World
{
    readonly List<RocheReservoir> _rocheReservoirs = new();
    readonly List<RocheDisruption> _rocheDisruptions = new();
    public IReadOnlyList<RocheReservoir> RocheReservoirs => _rocheReservoirs.AsReadOnly();
    public IReadOnlyList<RocheDisruption> RocheDisruptions => _rocheDisruptions.AsReadOnly();
    public double RocheDissipatedEnergy { get; private set; }
    readonly List<(int Body, int Host, int BodyGen, int HostGen, double Limit, double BodyMass, double HostMass, double BodyRadius, double HostRadius)> _rocheCandidates = new();
    readonly Dictionary<int, (double Mass, double Radius, int Generation, double Density, double K, double Strength)> _rocheMaterialCache = new();
    double _rocheEarthRadiusRef;
    readonly List<(int Body, int Host, int BodyGen, int HostGen, double Year)> _rocheRockEntries = new();
    Rule _rocheRule = null!;
    int _rocheChanges;
    bool RocheEnabled => _rocheRule != null && _rocheRule.Enabled && Rules.Contains(_rocheRule) && C.G > 0;

    void InitRocheRule()
    {
        _rocheRule = new Rule("roche", "X,Y,Vx,Vy,M,R,Comp,Roche*", "objects,reservoir,escaped", C.RocheRhythmYears,
            w => { w.CollectRocheCandidates(0); w.CheckRocheHere(); });
        Rules.Add(_rocheRule);
    }

    double MaterialDensity(int i)
    {
        StarPhase phase = StarPhaseOf(i);
        if (phase != StarPhase.None)
        {
            double km = phase == StarPhase.RedGiant ? GiantRadiusFactor(i) * C.RocheSolarRadiusKm
                : StarRadius(i) / SolarRadius * C.RocheSolarRadiusKm;
            double radius = km * 1000;
            return radius > 0 ? M[i] / C.StarSolarMass * C.RocheSolarMassKg / ((4 * Math.PI / 3) * radius * radius * radius) / C.RocheDensityKgM3 : 0;
        }
        double volume = 0;
        for (int e = 0; e < ElementCount; e++) volume += Comp[i * ElementCount + e] / C.Density[e];
        return volume > 0 ? M[i] / volume : 0;
    }

    // Role rows, not object names/types. Unknown material defaults to cohesionless rigid matter.
    static readonly ElementRole[] RocheRoles = { ElementRole.Gas, ElementRole.Ice, ElementRole.Rock, ElementRole.Metal, ElementRole.Carbon, ElementRole.Radio };
    (double K, double Strength) RocheMaterial(int i)
    {
        double k = 0, strength = 0;
        for (int e = 0; e < ElementCount; e++)
        {
            double cell = Comp[i * ElementCount + e];
            if (cell == 0) continue;
            ElementRole roles = Elements[e].Roles;
            double ek = 0, es = 0; int matches = 0;
            foreach (var role in RocheRoles) if ((roles & role) != 0)
            {
                ek += role is ElementRole.Gas or ElementRole.Ice ? C.RocheFluid : C.RocheRigid;
                es += role switch { ElementRole.Gas => 0, ElementRole.Ice => C.RocheIceStrengthPa, ElementRole.Metal => C.RocheMetalStrengthPa, _ => C.RocheRockStrengthPa };
                matches++;
            }
            double fraction = cell / M[i];
            k += fraction * (matches == 0 ? C.RocheRigid : ek / matches);
            strength += fraction * (matches == 0 ? 0 : es / matches);
        }
        return (k, strength);
    }

    public double RocheLimit(int body, int host)
        => RocheLimitCore(body, host, false);

    (double Density, double K, double Strength) RocheProperties(int i, bool cached)
    {
        if (cached && _rocheMaterialCache.TryGetValue(i, out var old) && old.Mass == M[i] && old.Radius == R[i] && old.Generation == Gen[i])
            return (old.Density, old.K, old.Strength);
        double density = MaterialDensity(i); var material = RocheMaterial(i);
        if (cached) _rocheMaterialCache[i] = (M[i], R[i], Gen[i], density, material.K, material.Strength);
        return (density, material.K, material.Strength);
    }

    double RocheLimitCore(int body, int host, bool cached)
    {
        if (body < 0 || host < 0 || body >= N || host >= N || !Alive[body] || !Alive[host]
            || M[host] <= M[body] || C.G <= 0 || StarPhaseOf(body) != StarPhase.None || IsShip(body)) return 0;
        var material = RocheProperties(body, cached);
        double rho = material.Density, primaryRho = RocheProperties(host, cached).Density;
        if (!(rho > 0) || !(primaryRho > 0)) return 0;
        double radiusM = R[body] / (cached ? _rocheEarthRadiusRef : EarthRadiusRef) * C.EarthRadiusKm * 1000;
        double densitySI = rho * C.RocheDensityKgM3;
        double stress = C.RochePhysicalG * C.G * densitySI * densitySI * radiusM * radiusM;
        // Macro strength correction: tide ~d^-3, cohesive acceleration/self-gravity ~sigma/(G*rho²*R²).
        // Not the full ellipsoidal yield solver of Holsapple/Michel; small strong rocks do survive.
        double factor = stress > 0 ? Math.Cbrt(1 + material.Strength / stress) : double.PositiveInfinity;
        return material.K * R[host] * Math.Cbrt(primaryRho / rho) / factor;
    }

    void CollectRocheCandidates(double seconds, bool orbital = false, bool includeRocks = true)
    {
        _rocheCandidates.Clear();
        _rocheMaterialCache.Clear();
        if (!RocheEnabled) return;
        _rocheEarthRadiusRef = EarthRadiusRef;
        double minDensity = C.Density.Min(), maxK = Math.Max(C.RocheRigid, C.RocheFluid);
        var hosts = new List<(int Slot, double Upper, double Drift)>();
        bool vector = !orbital && includeRocks && !_deferRocks && Vector.IsHardwareAccelerated && N >= Vector<double>.Count;
        double acceleration = 0, maxSpeedSquared = 0;
        for (int p = 0; p < N; p++) if (Alive[p])
        {
            if (vector) maxSpeedSquared = Math.Max(maxSpeedSquared,Vx[p]*Vx[p]+Vy[p]*Vy[p]);
            if (!Attracts(p)) continue;
            hosts.Add((p, maxK * R[p] * Math.Cbrt(MaterialDensity(p) / minDensity),
                Math.Sqrt(Vx[p]*Vx[p]+Vy[p]*Vy[p])*seconds));
            acceleration += C.G*M[p]/(R[p]*R[p]);
        }
        double acceleratedDrift = 2 * acceleration * seconds * seconds;
        if (vector)
        {
            // Standard SIMD broad phase over the existing SoA arrays. Only nearby lanes read material.
            double bodyBound = Math.Sqrt(maxSpeedSquared)*seconds;
            foreach (var host in hosts)
            {
                int p=host.Slot;
                double reach=host.Upper+bodyBound+host.Drift+acceleratedDrift;
                var px=new Vector<double>(X[p]); var py=new Vector<double>(Y[p]);
                var bound=new Vector<double>(reach*reach*(1+C.RocheEntryTolerance));
                int i=0;
                for (; i+Vector<double>.Count<=N; i+=Vector<double>.Count)
                {
                    var xx=new Vector<double>(X,i)-px; var yy=new Vector<double>(Y,i)-py;
                    var mask=Vector.LessThanOrEqual(xx*xx+yy*yy,bound);
                    if (Vector.EqualsAll(mask,Vector<long>.Zero)) continue;
                    for(int lane=0;lane<Vector<double>.Count;lane++) if(mask[lane]!=0) Include(i+lane);
                }
                for (; i < N; i++)
                {
                    double dx = X[i] - X[p], dy = Y[i] - Y[p];
                    if (dx*dx + dy*dy <= reach*reach) Include(i);
                }
                void Include(int body)
                {
                    if (Alive[body] && M[body]<M[p] && !IsShip(body) && StarPhaseOf(body)==StarPhase.None)
                        AddRocheCandidate(body,p);
                }
            }
            // Preserve the scalar source/host order, including simultaneous first entries.
            _rocheCandidates.Sort((a,b)=>a.Body!=b.Body?a.Body.CompareTo(b.Body):a.Host.CompareTo(b.Host));
            return;
        }
        for (int i = 0; i < N; i++)
        {
            if ((!includeRocks || _deferRocks) && !Attracts(i)) continue;
            if (!Alive[i] || IsShip(i) || StarPhaseOf(i) != StarPhase.None) continue;
            double bodyDrift = orbital ? 0 : Math.Sqrt(Vx[i]*Vx[i]+Vy[i]*Vy[i])*seconds;
            foreach (var host in hosts)
            {
                int p = host.Slot; if (M[p] <= M[i]) continue;
                double dx = X[i] - X[p], dy = Y[i] - Y[p];
                if (orbital)
                {
                    double ux = Vx[i] - Vx[p], uy = Vy[i] - Vy[p];
                    double r = Math.Sqrt(dx * dx + dy * dy), mu = C.G * (M[p] + M[i]), angular = dx * uy - dy * ux;
                    if (!(r > 0)) continue;
                    double energy = (ux * ux + uy * uy) / 2 - mu / r;
                    double eccentricity = Math.Sqrt(Math.Max(0, 1 + 2 * energy * angular * angular / (mu * mu)));
                    double peri = angular * angular / (mu * (1 + eccentricity));
                    if (peri > host.Upper && r > host.Upper) continue;
                    AddRocheCandidate(i,p); continue;
                }
                // Reject over the whole integrator call. Acceleration outside contact is surface-bounded.
                double reach = host.Upper + bodyDrift + host.Drift + acceleratedDrift;
                if (dx * dx + dy * dy <= reach * reach) AddRocheCandidate(i,p);
            }
        }
    }

    void AddRocheCandidate(int body, int host)
    {
        double limit=RocheLimitCore(body,host,true);
        if (limit>R[body]+R[host])
            _rocheCandidates.Add((body,host,Gen[body],Gen[host],limit,M[body],M[host],R[body],R[host]));
    }

    (double Time, int Body, int Host, int BodyGen, int HostGen) NextRocheEntry(double seconds)
    {
        var best = (Time: double.PositiveInfinity, Body: -1, Host: -1, BodyGen: 0, HostGen: 0);
        if (!RocheEnabled || _rocheChanges >= C.RocheChangesPerStep) return best;
        foreach (var pair in _rocheCandidates)
        {
            int i = pair.Body, p = pair.Host;
            if (!Alive[i] || !Alive[p] || Gen[i] != pair.BodyGen || Gen[p] != pair.HostGen) continue;
            // Limits stay fixed during kicks/drifts. A merge can change mass/radius, so invalidate locally.
            double radius = M[i]==pair.BodyMass && M[p]==pair.HostMass && R[i]==pair.BodyRadius && R[p]==pair.HostRadius
                ? pair.Limit : RocheLimitCore(i,p,true);
            if (!(radius > R[p] + R[i])) continue;
            double dx = X[i] - X[p], dy = Y[i] - Y[p];
            // Once the intact bodies overlap, the ordinary contact path owns the outcome.
            double contact = R[p] + R[i];
            if (dx * dx + dy * dy <= contact * contact) continue;
            double at = dx * dx + dy * dy <= radius * radius * (1 + C.RocheEntryTolerance) ? 0
                : PrimaryContactTime(dx, dy, Vx[i] - Vx[p], Vy[i] - Vy[p], C.G * (M[i] + M[p]), radius);
            if (at <= seconds && at < best.Time) best = (at, i, p, Gen[i], Gen[p]);
        }
        return best;
    }

    void CheckRocheHere()
    {
        while (true)
        {
            var entry = NextRocheEntry(0);
            if (entry.Body < 0) return;
            BreakRoche(entry.Body, entry.Host);
            CollectRocheCandidates(0);
        }
    }

    double NextRocheBoundary(double target)
    {
        if (!RocheEnabled || _rocheChanges >= C.RocheChangesPerStep) return double.PositiveInfinity;
        CollectRocheCandidates((target - Year) * C.YearTime, orbital: true, includeRocks: !_riding);
        var entry = NextRocheEntry((target - Year) * C.YearTime);
        double next = double.PositiveInfinity;
        foreach (var rock in _rocheRockEntries)
            if (rock.Year >= Year && rock.Year <= target && Alive[rock.Body] && Alive[rock.Host]
                && Gen[rock.Body] == rock.BodyGen && Gen[rock.Host] == rock.HostGen) next = Math.Min(next, rock.Year);
        if (entry.Body < 0) return next;
        // At Gyr ages a positive seconds interval can round back to the current double-valued year.
        double body = entry.Time > 0 ? Math.Max(Math.BitIncrement(Year), Year + entry.Time / C.YearTime) : Year;
        return Math.Min(next, body);
    }

    // Same origins, Kepler path and generation guards as deferred contact planning. Tiny rocks need no
    // per-chunk placement when no primary tide is crossed. Sibling flybys retain the engine's sampled limit.
    void PlanRocheRockEntries(double seconds)
    {
        _rocheRockEntries.Clear();
        if (!RocheEnabled) return;
        _rocheMaterialCache.Clear(); _rocheEarthRadiusRef = EarthRadiusRef;
        double minDensity = C.Density.Min(), maxK = Math.Max(C.RocheRigid, C.RocheFluid);
        var upper = new Dictionary<int,double>();
        for (int i = 0; i < N; i++)
        {
            if (!Alive[i] || Attracts(i)) continue;
            int p = _prim![i]; if (p < 0 || !Alive[p]) continue;
            if (!upper.TryGetValue(p,out double broad))
                upper[p] = broad = maxK * R[p] * Math.Cbrt(MaterialDensity(p) / minDensity);
            if (PrimaryContactTime(_bx![i],_by![i],_bvx![i],_bvy![i],C.G*(M[i]+M[p]),broad) > seconds) continue;
            double radius = RocheLimitCore(i,p,true); if (!(radius > R[p] + R[i])) continue;
            double at = PrimaryContactTime(_bx![i],_by![i],_bvx![i],_bvy![i],C.G*(M[i]+M[p]),radius);
            if (at <= seconds)
            {
                double year = at > 0 ? Math.Max(Math.BitIncrement(Year),Year+at/C.YearTime) : Year;
                _rocheRockEntries.Add((i,p,Gen[i],Gen[p],year));
            }
        }
    }

    void Reservoir(int host, double x, double y, double mass, double vx, double vy, double angular, double[] matter)
    {
        if (!(mass > 0)) return;
        _rocheReservoirs.Add(new(host, Gen[host], x, y, mass, mass * vx, mass * vy, angular, Array.AsReadOnly(matter)));
    }

    void BreakRoche(int i, int p)
    {
        if (!Alive[i] || !Alive[p]) return;
        double mass = M[i], pm = M[p], x = X[i], y = Y[i], vx = Vx[i], vy = Vy[i]; int generation = Gen[i];
        double dx = x - X[p], dy = y - Y[p], r = Math.Sqrt(dx * dx + dy * dy), ux = vx - Vx[p], uy = vy - Vy[p];
        // Individual debris binds to the host, without counting the destroyed body's mass as a central attractor.
        double mu = C.G * pm, kinetic = (ux * ux + uy * uy) / 2, specific = kinetic - mu / r, h = dx * uy - dy * ux;
        double captured = mass, roche = RocheLimit(i, p);
        double halfWidth = C.RocheEnergySpread * R[i];
        if (specific >= 0)
        {
            BuildPullingHierarchy(); double hill = _hill![p];
            double stable = hill > 0 && hill < double.MaxValue ? -C.G * pm / hill : 0;
            // Uniform nonspinning radial slices; freeze at disruption entry, not at a future periapsis.
            // The inner slices retain the original velocity but are more tightly bound. Unlike relabeling
            // an arbitrary fraction at the same position/velocity, these captured streams really have E<0.
            double criticalRadius = mu / ((ux * ux + uy * uy) / 2 - stable);
            captured *= halfWidth > 0 ? Math.Clamp((criticalRadius - (r - halfWidth)) / (2 * halfWidth), 0, 1) : 0;
        }
        double bodyRadius = R[i];
        var original = new double[ElementCount];
        for (int e = 0; e < ElementCount; e++) original[e] = Comp[i * ElementCount + e];
        double unbound = mass - captured;
        double fraction = captured / mass;
        double captureOffset = specific >= 0 ? -halfWidth * (1 - fraction) : 0;
        double outgoingOffset = specific >= 0 ? halfWidth * fraction : 0;
        double captureX = x + dx / r * captureOffset, captureY = y + dy / r * captureOffset;
        double outgoingX = x + dx / r * outgoingOffset, outgoingY = y + dy / r * outgoingOffset;
        double capturedRadius = r + captureOffset, capturedAngular = h * capturedRadius / r;
        int root = Heaviest();
        double rx = outgoingX - X[root], ry = outgoingY - Y[root], rvx = vx - Vx[root], rvy = vy - Vy[root];
        bool leavesSystem = root == p || (rx * rx + ry * ry > 0 && (rvx * rvx + rvy * rvy) / 2 >= C.G * M[root] / Math.Sqrt(rx * rx + ry * ry));
        var outgoing = original.Select(cell => cell * (unbound / mass)).ToArray();
        if (leavesSystem) Escape(i, "roche.unbound", unbound, outgoing, unbound * vx, unbound * vy, outgoingX, outgoingY);
        else Reservoir(root, outgoingX, outgoingY, unbound, vx, vy, 0, outgoing);

        int small = 0; for (int s = 0; s < N; s++) if (Alive[s] && !Attracts(s)) small++;
        int count = (int)Math.Max(0, Math.Min(Math.Min(C.RocheFragments, X.Length - Live + 1), C.RocheRockBudget - small));
        count -= count % 2;
        double represented = Math.Min(captured, count * Math.Min(C.RocheFragmentMass, C.AttractMass * .5));
        if (!(represented > 0)) { count = 0; represented = 0; }
        double unresolved = captured - represented;
        double reduced = captured > 0 ? pm * captured / (pm + captured) : 0;
        double angular = reduced * capturedAngular, circular = captured > 0 ? Math.Pow(angular / captured, 2) / (C.G * pm) : 0;
        double beforeEnergy = reduced * (ux * ux + uy * uy) / 2 - C.G * pm * captured / capturedRadius;
        double circularEnergy = circular > 0 ? -C.G * pm * captured / (2 * circular) : double.PositiveInfinity;
        bool ring = captured > 0 && circular > R[p] && circular < roche && circularEnergy <= beforeEnergy;
        double centerX = ring ? (pm * X[p] + captured * captureX) / (pm + captured) : captureX;
        double centerY = ring ? (pm * Y[p] + captured * captureY) / (pm + captured) : captureY;
        double centerVx = ring ? (pm * Vx[p] + captured * vx) / (pm + captured) : vx;
        double centerVy = ring ? (pm * Vy[p] + captured * vy) / (pm + captured) : vy;
        double dissipated = ring ? beforeEnergy - circularEnergy : 0;
        if (ring) { X[p] = centerX; Y[p] = centerY; Vx[p] = centerVx; Vy[p] = centerVy; }
        if (Life[i] > 0) LogEvent(i, "life", "life.end", Temp[i], Water[i], LifeStage(i));
        if (Pop[i] > 0) CivEvent(i, Civ[i], "civ.end", 0, Temp[i], TechStage(i));
        Life[i] = RichYears[i] = Pop[i] = Tech[i] = 0;
        _hits.RemoveAll(pair => pair.Item1 == i || pair.Item2 == i); // cannot merge a newborn in a queued stale slot.
        Kill(i); _rocheChanges++;
        int group = Groups.Count; Groups.Add($"roche.{i}.{generation}");
        var remaining = original.Select(cell => cell * (captured / mass)).ToArray();
        double speed = ring ? Math.CopySign(Math.Sqrt(C.G * pm / circular), h) : 0;
        double boundRadius = kinetic > 0 ? mu / kinetic : double.PositiveInfinity;
        double streamWidth = Math.Min(bodyRadius * C.RocheStreamWidth,
            .5 * Math.Sqrt(Math.Max(0, (boundRadius - capturedRadius) * (boundRadius + capturedRadius))));
        // Opposite pairs keep net momentum and COM. Zero-width circular ring adds no velocity noise/energy.
        for (int n = 0; n < count; n++)
        {
            int pair = n / 2; double angle = Math.Tau * pair / (count / 2), sign = n % 2 == 0 ? 1 : -1;
            double xx = ring ? sign * circular * Math.Cos(angle) : -sign * dy / r * streamWidth;
            double yy = ring ? sign * circular * Math.Sin(angle) : sign * dx / r * streamWidth;
            double fragment = represented / count;
            var mix = original.Select(cell => cell / mass).ToArray();
            int slot = Add(centerX + xx, centerY + yy, centerVx - (ring ? sign * speed * Math.Sin(angle) : 0),
                centerVy + (ring ? sign * speed * Math.Cos(angle) : 0), fragment, mix, par: p, grp: group);
            if (slot < 0) throw new InvalidOperationException("Reserved Roche fragment slots disappeared.");
            Touched[slot] = Year;
            for (int e = 0; e < ElementCount; e++) remaining[e] -= Comp[slot * ElementCount + e];
        }
        Reservoir(p, centerX, centerY, unresolved, centerVx, centerVy, ring ? angular * (unresolved / captured) : 0, remaining);
        RocheDissipatedEnergy += dissipated;
        _rocheDisruptions.Add(new(Year, i, generation, p, Gen[p], group, ring ? "ring" : "stream", mass, captured, leavesSystem ? unbound : 0, dissipated));
        LogEvent(p, "roche", ring ? "roche.ring" : "roche.stream", mass, captured, count);
    }

    void HashRoche(Action<ulong> mix)
    {
        void Number(double value) => mix(BitConverter.DoubleToUInt64Bits(value));
        Number(RocheDissipatedEnergy); mix((ulong)_rocheReservoirs.Count);
        foreach (var r in _rocheReservoirs)
        {
            mix((ulong)r.Host); mix((ulong)r.HostGeneration); Number(r.X); Number(r.Y); Number(r.Mass);
            Number(r.Px); Number(r.Py); Number(r.AngularMomentum); foreach (double c in r.Matter) Number(c);
        }
        mix((ulong)_rocheDisruptions.Count);
        foreach (var d in _rocheDisruptions)
        {
            Number(d.Year); mix((ulong)d.Source); mix((ulong)d.SourceGeneration); mix((ulong)d.Host); mix((ulong)d.HostGeneration);
            mix((ulong)d.Group); foreach (char ch in d.Formation) mix(ch);
            Number(d.SourceMass); Number(d.CapturedMass); Number(d.EscapedMass); Number(d.DissipatedEnergy);
        }
    }
}
