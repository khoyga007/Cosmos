using System;
using System.Linq;
using Cosmos.Core;

static class NaturalFillChecks
{
    public static bool CheckPhysics()
    {
        bool ok = true;
        void Check(bool pass, string text) { ok &= pass; Console.WriteLine($"{(pass ? "OK" : "FAILED")} natural {text}"); }
        var w = new World(256, 7);
        foreach (var rule in w.Rules.ToArray()) if (rule.Id != "roche") w.Do(new Command(CmdKind.SetRule, Name: rule.Id, Amount: 0));
        w.C.DiscOn = 0;
        int host = w.Add(0, 0, 0, 0, .05, w.Mix(("gas", 1)));
        int body = w.Add(4, 0, 0, 0, 1e-6, w.Mix(("ice", 1)));
        double r = .999 * w.RocheLimit(body, host);
        double circularSpeed = Math.Sqrt(w.C.G * (w.M[host] + w.M[body]) / r);
        w.Do(new Command(CmdKind.Move, Target: body, X: r, Vx: -.7 * circularSpeed, Vy: 1.1 * circularSpeed, Index: 1));
        w.Advance(0);
        Check(w.RocheDisruptions.Count == 1 && w.RocheDisruptions[0].Formation == "stream", $"eccentric source disrupts events={w.RocheDisruptions.Count} live={w.Live}");
        int children = 0; bool stable = true;
        for (int i = 0; i < w.N; i++) if (w.Alive[i] && w.Grp[i] > 0)
        {
            children++;
            double x = w.X[i] - w.X[host], y = w.Y[i] - w.Y[host], vx = w.Vx[i] - w.Vx[host], vy = w.Vy[i] - w.Vy[host];
            double mu = w.C.G * w.M[host], energy = (vx * vx + vy * vy) / 2 - mu / Math.Sqrt(x * x + y * y), h = x * vy - y * vx;
            double e = Math.Sqrt(Math.Max(0, 1 + 2 * energy * h * h / (mu * mu))), q = h * h / (mu * (1 + e));
            stable &= w.RocheLimit(i, host) < q;
        }
        Check(children > 0 && stable, $"emitted stream survives at its OWN periapsis children={children} stable={stable}");
        int initialEvents = w.RocheDisruptions.Count;
        for (int step = 0; step < 1000; step++) w.Advance(.002);
        Check(w.RocheDisruptions.Count == initialEvents, $"no descendant reshred through first inward passage events={initialEvents}->{w.RocheDisruptions.Count} live={w.Live}");
        var compact = new World(256, 9);
        int ns = compact.Add(0, 0, 0, 0, 70, compact.Mix(("gas", 1)));
        double end = 1 + compact.C.StarGiantFraction;
        compact.Do(new Command(CmdKind.SetStarState, Target: ns, StarState: new StellarState(1e8, end, 500)));
        int planet = compact.Add(20, 0, 0, 0, World.EarthMass, compact.Mix(World.EarthMix));
        double limit = compact.RocheLimit(planet, ns), separation = .99 * limit;
        compact.Do(new Command(CmdKind.Move, Target: planet, X: separation, Vy: Math.Sqrt(compact.C.G * (compact.M[ns] + compact.M[planet]) / separation), Index: 1));
        compact.Advance(0);
        Check(compact.RocheDisruptions.Any(d => d.Source == planet && d.Host == ns && Math.Abs(d.SourceMass / World.EarthMass - 1) < 1e-12),
            $"e Earth-mass planet disrupted by NS RocheLimit={limit:R} separation={separation:R} events={compact.RocheDisruptions.Count}");
        return ok;
    }

    public static void Run(int seed, int steps, bool detail)
    {
        var w = World.SolSystem(5000, (ulong)seed);
        int max = w.Live, full = -1, seen = 0;
        double[]? mass = detail ? new double[w.M.Length] : null, radius = detail ? new double[w.R.Length] : null,
            comp = detail ? new double[w.Comp.Length] : null, xs = detail ? new double[w.X.Length] : null, ys = detail ? new double[w.Y.Length] : null;
        int[]? gens = detail ? new int[w.Gen.Length] : null;
        for (int step = 1; step <= steps; step++)
        {
            if (detail && step <= 6000)
            {
                Array.Copy(w.M, mass!, w.N); Array.Copy(w.R, radius!, w.N);
                Array.Copy(w.Comp, comp!, w.N * w.ElementCount); Array.Copy(w.Gen, gens!, w.N);
                Array.Copy(w.X, xs!, w.N); Array.Copy(w.Y, ys!, w.N);
            }
            w.Advance(.5);
            max = Math.Max(max, w.Live);
            if (full < 0 && w.Live == w.X.Length) full = step;
            if (detail) for (int k = seen; k < w.RocheDisruptions.Count; k++)
            {
                var d = w.RocheDisruptions[k];
                Console.WriteLine($"event seed={seed} step={step} year={d.Year:R} source={d.Source}/{d.SourceGeneration} host={d.Host} name={w.Name[d.Host]} mass={d.SourceMass:R} formation={d.Formation} live={w.Live}");
                if (step <= 6000 && gens![d.Source] == d.SourceGeneration)
                {
                    int i = d.Source; double sigma = 0;
                    for (int e = 0; e < w.ElementCount; e++)
                    {
                        var roles = w.Elements[e].Roles;
                        double strength = roles.HasFlag(ElementRole.Metal) ? w.C.RocheMetalStrengthPa : roles.HasFlag(ElementRole.Ice) ? w.C.RocheIceStrengthPa : roles.HasFlag(ElementRole.Gas) ? 0 : w.C.RocheRockStrengthPa;
                        sigma += comp![i * w.ElementCount + e] / mass![i] * strength;
                    }
                    double metres = radius![i] / w.EarthRadiusRef * w.C.EarthRadiusKm * 1000;
                    Console.WriteLine($"  before massEarth={mass![i]/World.EarthMass:R} radiusM={metres:R} strengthPa={sigma:R} separation={Math.Sqrt(Math.Pow(xs![i]-xs[d.Host],2)+Math.Pow(ys![i]-ys[d.Host],2)):R} mix={string.Join(',',Enumerable.Range(0,w.ElementCount).Select(e=>$"{w.Elements[e].Id}:{comp![i*w.ElementCount+e]/mass[i]:R}"))}");
                }
            }
            seen = w.RocheDisruptions.Count;
            if (step % 1000 == 0) Console.WriteLine($"natural seed={seed} step={step} year={w.Year:R} live={w.Live} max={max} full={full} events={seen} hash={w.Hash():X16}");
        }
        Console.WriteLine($"natural FINAL seed={seed} steps={steps} live={w.Live} max={max} full={full} events={seen} hash={w.Hash():X16}");
    }
}
