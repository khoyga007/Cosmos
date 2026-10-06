// Core: a world is a set of OBJECTS and one set of CONSTANTS (Yang 06/10: everything is an object with its own
// physics and parameters; no anonymous grains; the universe still has constants, and the god may change them).
//   object  = position, velocity, mass, matter table (six element groups), name, colour, parent, group.
//   derived = radius (matter + densities), "pulls others" (mass over a threshold), kind. Never set by hand.
// Pull is exact 1/r^2: no softening. Two objects that touch merge. Fixed step, seeded stream: a run repeats exactly.
// Pure .NET, no Godot types.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Cosmos.Core;

/// The laws every object obeys. One set per world; plain fields so the god can turn them.
public sealed partial class Consts
{
    public double G = 1.0;
    public double AttractMass = 1e-7; // an object this heavy or heavier pulls the others (about 0.0007 Earths)
    public double StarMass = 4.0;     // this heavy or heavier = a star (0.08 Suns)
    public double RadiusScale = 1.5;
    public readonly double[] Density;
    public Consts() : this(ElementCatalog.Elements) { }
    internal Consts(IReadOnlyList<Element> elements) { Density = elements.Select(e => e.Density).ToArray(); }
}

public enum Kind { Star, Planet, Moon, Rock }

public sealed partial class World
{
    public const int Sub = 8; // small steps per Advance: moon orbits need the finer step
    public static int NElem => ElementCatalog.Elements.Count; // default-table compatibility for existing clients
    public static readonly string[] ElemName = ElementCatalog.Elements.Select(e => e.Id).ToArray();
    public const double EarthMass = 1.5e-4; // a star of 50 = one Sun, so Earth = 50 * 3e-6

    public readonly Consts C;

    // objects: structure of arrays. Slots are stable: a dead slot is reused by a later Add, never shifted.
    // Whoever keeps a slot number across steps (a selection, a script) keeps Gen[slot] with it: a slot with another
    // Gen holds another object. Inside the core nothing points at a dead slot: Gone clears every such pointer.
    public int N;    // slots in use, dead ones included
    public int Live; // objects alive
    public readonly double[] X, Y, Vx, Vy, M, R, Comp; // Comp = mass per element group, row per object
    public readonly bool[] Alive;
    public readonly string?[] Name;
    public readonly uint[] Col;  // 0xRRGGBB for the draw layer, 0 = none
    public readonly int[] Par;   // object it was put in orbit around, -1 = none
    public readonly int[] Grp;   // index into Groups, 0 = none
    public readonly int[] Gen;   // how many objects this slot has held
    public readonly List<string> Groups = new() { "" }; // a group (a belt) = a name; its numbers come from its members
    public long Step, Merges;

    readonly Stack<int> _free = new();
    readonly int[] _att, _near; int _na;          // pulling objects and nearby sweep candidates
    readonly List<(int, int)> _hits = new();      // pairs that touched in this small step
    ulong _rng;

    public World(int capacity, ulong seed, IEnumerable<Element>? elements = null, IEnumerable<StarEvent>? starEvents = null)
    {
        Elements = Array.AsReadOnly((elements ?? ElementCatalog.Elements).ToArray());
        ElementCount = Elements.Count;
        if (ElementCount == 0) throw new ArgumentException("The element table must not be empty.", nameof(elements));
        _legacyElements = Elements.SequenceEqual(ElementCatalog.Elements);
        IndexElements(); C = new Consts(Elements);
        var events = (starEvents ?? StarEventCatalog.Events).Select(SnapshotStarEvent).ToArray();
        if (events.Select(e => e.Id).Distinct(StringComparer.Ordinal).Count() != events.Length) throw new ArgumentException("Star event ids must be unique.");
        StarEvents = Array.AsReadOnly(events);
        _legacyStarEvents = events.Length == StarEventCatalog.Events.Count && events.Zip(StarEventCatalog.Events).All(pair => StarEventCatalog.Same(pair.First, pair.Second));
        StellarEjectaMatter = new double[ElementCount];
        X = new double[capacity]; Y = new double[capacity]; Vx = new double[capacity]; Vy = new double[capacity];
        M = new double[capacity]; R = new double[capacity]; Comp = new double[capacity * ElementCount];
        Alive = new bool[capacity]; Name = new string?[capacity]; Col = new uint[capacity]; Par = new int[capacity]; Grp = new int[capacity]; Gen = new int[capacity];
        _att = new int[capacity]; _near = new int[capacity];
        Temp = new double[capacity]; _temperatureBands = new int[capacity]; _starSlots = new int[capacity]; _starLight = new double[capacity];
        InitLayers(capacity);
        InitStars(capacity);
        InitRules();
        _rng = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;
    }

    public double Next() // xorshift64*, 53 bits
    {
        _rng ^= _rng >> 12; _rng ^= _rng << 25; _rng ^= _rng >> 27;
        return ((_rng * 0x2545F4914F6CDD1DUL) >> 11) * (1.0 / 9007199254740992.0);
    }

    // ---- derived values

    public bool Attracts(int i) => M[i] >= C.AttractMass;

    public Kind KindOf(int i)
    {
        if (!Alive[i]) return Kind.Rock;
        if (M[i] >= C.StarMass) return Kind.Star;
        if (!Attracts(i)) return Kind.Rock;
        int p = PrimaryOf(i);
        return p >= 0 && Alive[p] && M[p] < C.StarMass ? Kind.Moon : Kind.Planet;
    }

    void SetRadius(int i)
    {
        SyncStar(i);
        if (StarPhaseOf(i) != StarPhase.None) { R[i] = StarRadius(i); return; }
        double vol = 0;
        for (int e = 0; e < ElementCount; e++) vol += Comp[i * ElementCount + e] / C.Density[e];
        R[i] = C.RadiusScale * Math.Cbrt(vol);
    }

    /// Call after the god changes a density or RadiusScale.
    public void RecalcRadii() { for (int i = 0; i < N; i++) if (Alive[i]) SetRadius(i); }

    public int Heaviest()
    {
        int k = -1;
        for (int i = 0; i < N; i++) if (Alive[i] && (k < 0 || M[i] > M[k])) k = i;
        return k;
    }

    /// Radius inside which object i, not its parent (else the heaviest object), rules small things.
    public double Hill(int i)
    {
        if (!Alive[i] || !Attracts(i)) return 0;
        BuildPullingHierarchy();
        return _hill![i];
    }

    /// Finds the real primary for object i: unified directly with the Rails pulling hierarchy.
    public int PrimaryOf(int i)
    {
        if (!Alive[i]) return -1;
        BuildPullingHierarchy();
        int na = _naPulling;
        if (na == 0) return -1;
        if (i == _ord![0]) return -1;
        if (Attracts(i)) return _prim![i];
        return FindPrimaryInHierarchy(i, na);
    }

    public int FindPrimary(int i) => PrimaryOf(i);

    // ---- making objects

    /// Returns the slot, or -1 when the world is full or parameters are invalid.
    public int Add(double x, double y, double vx, double vy, double m, double[] mix, string? name = null, uint col = 0, int par = -1, int grp = 0)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(vx) || !double.IsFinite(vy)) return -1;
        if (!double.IsFinite(m) || m <= 0) return -1;
        if (mix == null || mix.Length > ElementCount) return -1;
        double sumMix = 0;
        for (int e = 0; e < mix.Length; e++)
        {
            if (!double.IsFinite(mix[e]) || mix[e] < 0) return -1;
            sumMix += mix[e];
        }
        if (Math.Abs(sumMix - 1) > 1e-11) return -1;
        if (par != -1 && (par < 0 || par >= N || !Alive[par])) return -1;
        if (grp < 0 || grp >= Groups.Count) return -1;

        int i;
        if (_free.Count > 0) i = _free.Pop(); else if (N < X.Length) i = N++; else return -1;
        X[i] = x; Y[i] = y; Vx[i] = vx; Vy[i] = vy; M[i] = m; Alive[i] = true; Name[i] = name; Col[i] = col; Par[i] = par; Grp[i] = grp; Gen[i]++;
        for (int e = 0; e < ElementCount; e++) Comp[i * ElementCount + e] = e < mix.Length ? m * mix[e] : 0;
        ResetTemperature(i); ResetLayers(i);
        ResetStars(i); SetRadius(i); Live++;
        return i;
    }

    /// New object at (x, y) on a circular orbit around `parent`, same turning sense as the planets.
    public int AddOrbiting(int parent, double x, double y, double m, double[] mix, string? name = null, uint col = 0, int grp = 0, double speedFactor = 1)
    {
        if (parent < 0 || parent >= N || !Alive[parent]) return -1;
        if (!double.IsFinite(x) || !double.IsFinite(y) || (x == X[parent] && y == Y[parent])) return -1;
        if (!double.IsFinite(m) || m <= 0) return -1;
        if (!double.IsFinite(speedFactor) || speedFactor < 0) return -1;
        double dx = x - X[parent], dy = y - Y[parent], r = Math.Sqrt(dx * dx + dy * dy);
        if (r <= 0 || !double.IsFinite(r)) return -1;
        double gm = C.G * (M[parent] + m);
        if (gm < 0) return -1;
        double v = Math.Sqrt(gm / r) * speedFactor;
        if (!double.IsFinite(v)) return -1;
        return Add(x, y, Vx[parent] - dy / r * v, Vy[parent] + dx / r * v, m, mix, name, col, parent, grp);
    }

    // the heavier one keeps its identity; mass, momentum and matter are summed
    void Merge(int a, int b)
    {
        if (IsShip(a) || IsShip(b)) { if (IsShip(a)) ShipArrives(a, b); else ShipArrives(b, a); return; }
        int k = M[a] >= M[b] ? a : b, d = k == a ? b : a;
        MergeStars(k, d);
        double m = M[k] + M[d];
        X[k] = (X[k] * M[k] + X[d] * M[d]) / m; Y[k] = (Y[k] * M[k] + Y[d] * M[d]) / m;
        Vx[k] = (Vx[k] * M[k] + Vx[d] * M[d]) / m; Vy[k] = (Vy[k] * M[k] + Vy[d] * M[d]) / m;
        for (int e = 0; e < ElementCount; e++) Comp[k * ElementCount + e] += Comp[d * ElementCount + e];
        Impact(k, M[d] / M[k]);
        M[k] = m; SetRadius(k);
        // victim, survivor slot, swallowed mass, victim generation. Not for dust: a belt falling in would push
        // everything else out of the bounded log
        if (M[d] >= C.AttractMass || Life[d] > 0 || Pop[d] > 0) LogEvent(d, "contact", "merge", k, M[d], Gen[d]);
        EndSwallowedWorld(d);
        Alive[d] = false; M[d] = 0; _free.Push(d); Live--; Merges++;
        Gone(d, k);
    }

    // ---- scenes

    // Distances are squeezed (d = 45 * AU^0.62) so all eight planets fit one screen; order and mass ratios are real.
    public static double SolDist(double au) => 45 * Math.Pow(au, 0.62);

    /// Our own system: Sun, eight planets, the Moon, and `rocks` small objects in two belts (40% asteroid, 60% Kuiper),
    /// plus Saturn's ring: min(240, rocks / 2) more, real objects like any other.
    public static World SolSystem(int rocks, ulong seed, IEnumerable<Element>? elements = null)
    {
        var w = new World(rocks + 512, seed, elements);
        int sun = w.Add(0, 0, 0, 0, 50, w.Mix(("gas", 1)), "Mặt Trời", 0xFFDB8C);
        int planet(string name, double au, double earths, uint col, params double[] mix)
        {
            double d = SolDist(au), a = w.Next() * Math.Tau;
            return w.AddOrbiting(sun, Math.Cos(a) * d, Math.Sin(a) * d, earths * EarthMass, mix, name, col);
        }
        //                                          gas   ice   rock  metal carbon radio
        planet("Sao Thủy", 0.387, 0.0553, 0x9C9C9C, w.Mix(("rock", .30), ("metal", .69), ("radio", .01)));
        planet("Sao Kim", 0.723, 0.815, 0xE8CF9A, w.Mix(("rock", .66), ("metal", .32), ("carbon", .01), ("radio", .01)));
        int earth = planet("Trái Đất", 1.0, 1.0, 0x4F8FE8, w.Mix(("ice", .01), ("rock", .66), ("metal", .32), ("carbon", .005), ("radio", .005)));
        planet("Sao Hỏa", 1.524, 0.107, 0xD0603A, w.Mix(("ice", .01), ("rock", .73), ("metal", .25), ("carbon", .005), ("radio", .005)));
        planet("Sao Mộc", 5.203, 317.8, 0xD9B48A, w.Mix(("gas", .90), ("ice", .05), ("rock", .03), ("metal", .015), ("carbon", .005)));
        int saturn = planet("Sao Thổ", 9.537, 95.2, 0xE6D29A, w.Mix(("gas", .85), ("ice", .08), ("rock", .045), ("metal", .02), ("carbon", .005)));
        planet("Sao Thiên Vương", 19.19, 14.5, 0x9FE3E8, w.Mix(("gas", .15), ("ice", .65), ("rock", .15), ("metal", .04), ("carbon", .01)));
        planet("Sao Hải Vương", 30.07, 17.1, 0x4466E0, w.Mix(("gas", .12), ("ice", .66), ("rock", .16), ("metal", .05), ("carbon", .01)));
        w.AddOrbiting(earth, w.X[earth] + 0.12, w.Y[earth], 0.0123 * EarthMass, w.Mix(("ice", .01), ("rock", .72), ("metal", .26), ("carbon", .005), ("radio", .005)), "Mặt Trăng", 0xC8C8C8);

        w.Groups.Add("Vành đai tiểu hành tinh"); w.Groups.Add("Vành đai Kuiper"); w.Groups.Add("Vành Sao Thổ");
        double[] belt = w.Mix(("ice", .05), ("rock", .60), ("metal", .20), ("carbon", .14), ("radio", .01)), kuiper = w.Mix(("gas", .03), ("ice", .72), ("rock", .15), ("metal", .02), ("carbon", .08));
        var mix = new double[w.ElementCount];
        for (int i = 0; i < rocks; i++)
        {
            bool inner = i < rocks * 40 / 100; // asteroid belt 2.1-3.3 AU, Kuiper belt 32-50 AU
            double[] table = inner ? belt : kuiper;
            double au = inner ? 2.1 + 1.2 * w.Next() : 32 + 18 * w.Next(), d = SolDist(au), a = w.Next() * Math.Tau;
            // each rock is mostly one group (drawn from the belt's table), the rest follows the table
            double u = w.Next(); int main = w.Elem("radio"); // tail of the original belt distribution, not the last table row
            for (int e = 0; e < w.ElementCount; e++) { if (e == w.Elem("radio")) continue; u -= table[e]; if (u < 0) { main = e; break; } }
            for (int e = 0; e < w.ElementCount; e++) mix[e] = 0.3 * table[e] + (e == main ? 0.7 : 0);
            double q = w.Next(), m = 1e-10 * (1 + 200 * q * q * q); // up to 2e-8: all below AttractMass
            w.AddOrbiting(sun, Math.Cos(a) * d, Math.Sin(a) * d, m, mix, null, 0, inner ? 1 : 2, 0.99 + 0.02 * w.Next());
            w.Par[w.N - 1] = -1; // rocks get no orbit line
        }
        // drawn after the belts, so the belts are the same with and without it
        double[] ringMix = w.Mix(("ice", .9), ("rock", .1));
        for (int i = Math.Min(240, rocks / 2); i > 0; i--)
        {
            double d = w.R[saturn] * (1.4 + 1.0 * w.Next()), a = w.Next() * Math.Tau;
            w.AddOrbiting(saturn, w.X[saturn] + Math.Cos(a) * d, w.Y[saturn] + Math.Sin(a) * d, 1e-10, ringMix, null, 0, 3);
            w.Par[w.N - 1] = -1;
        }
        w.RunRules();
        return w;
    }

    // ---- stepping

    public void Advance(double h)
    {
        if (!double.IsFinite(h) || h < 0 || !double.IsFinite(C.YearTime) || C.YearTime <= 0)
            throw new ArgumentOutOfRangeException(nameof(h), "Advance requires finite non-negative time and positive YearTime.");
        SteerShips(h);
        double hs = h / Sub, g = C.G;
        for (int sub = 0; sub < Sub; sub++)
        {
            _na = 0;
            for (int i = 0; i < N; i++) if (Alive[i] && M[i] >= C.AttractMass) _att[_na++] = i;

            // Outside a surface each acceleration is bounded by G*M/R². A candidate can be rejected using
            // distance already computed for gravity, without another scan of every rock/body pair.
            double maxKickDrift = 0, maxAttSpeed = 0;
            for (int k = 0; k < _na; k++)
            {
                int j = _att[k];
                maxKickDrift += Math.Abs(g) * M[j] / (R[j] * R[j]) * hs * hs;
            }
            void Kick(int i, bool sweep)
            {
                double x = X[i], y = Y[i], ri = R[i], ax = 0, ay = 0;
                int near = 0;
                double travel = sweep ? hs * (Math.Sqrt(Vx[i] * Vx[i] + Vy[i] * Vy[i]) + maxAttSpeed) + maxKickDrift : 0;
                for (int k = 0; k < _na; k++)
                {
                    int j = _att[k];
                    if (j == i) continue;
                    double dx = X[j] - x, dy = Y[j] - y, d2 = dx * dx + dy * dy, rr = ri + R[j];
                    if (d2 <= rr * rr) { if (sweep || i < j) _hits.Add((i, j)); continue; }
                    if (sweep && d2 <= (rr + travel) * (rr + travel)) _near[near++] = j;
                    double inv = g * M[j] / (d2 * Math.Sqrt(d2));
                    ax += dx * inv; ay += dy * inv;
                }
                Vx[i] += ax * hs; Vy[i] += ay * hs;
                for (int k = 0; k < near; k++)
                {
                    int j = _near[k];
                    if (SweptContact(x - X[j], y - Y[j], (Vx[i] - Vx[j]) * hs, (Vy[i] - Vy[j]) * hs, ri + R[j]))
                        _hits.Add((i, j));
                }
            }
            // Pulling objects kick first; gravity only reads positions. A rock's sweep then has both final velocities.
            for (int k = 0; k < _na; k++) Kick(_att[k], false);
            for (int k = 0; k < _na; k++)
            {
                int i = _att[k];
                maxAttSpeed = Math.Max(maxAttSpeed, Math.Sqrt(Vx[i] * Vx[i] + Vy[i] * Vy[i]));
                for (int l = k + 1; l < _na; l++)
                {
                    int j = _att[l];
                    if (SweptContact(X[i] - X[j], Y[i] - Y[j],
                        (Vx[i] - Vx[j]) * hs, (Vy[i] - Vy[j]) * hs, R[i] + R[j])) _hits.Add((i, j));
                }
            }
            for (int i = 0; i < N; i++) if (Alive[i] && !Attracts(i)) Kick(i, true);
            for (int i = 0; i < N; i++) if (Alive[i]) { X[i] += Vx[i] * hs; Y[i] += Vy[i] * hs; }

            if (_hits.Count > 0)
            {
                foreach (var (a, b) in _hits) if (Alive[a] && Alive[b]) Merge(a, b);
                _hits.Clear();
            }
        }
        Step++;
        Year += h / C.YearTime;
        RunRules();
    }
    // ponytail: two objects that both do NOT pull never collide with each other (rock through rock);
    //           add a grid broad-phase when belts should grind.

    // Relative straight drift against a circle. The axis test rejects almost all belt/body pairs cheaply.
    static bool SweptContact(double x, double y, double dx, double dy, double r)
    {
        if (x > r && x + dx > r || x < -r && x + dx < -r
            || y > r && y + dy > r || y < -r && y + dy < -r) return false;
        double d2 = dx * dx + dy * dy;
        double t = d2 == 0 ? 0 : Math.Clamp(-(x * dx + y * dy) / d2, 0, 1);
        double nx = x + dx * t, ny = y + dy * t;
        return nx * nx + ny * ny <= r * r;
    }

    /// FNV-1a over the raw state: equal hash = equal run.
    public ulong Hash()
    {
        ulong h = 14695981039346656037UL;
        void mix(ulong v) { for (int b = 0; b < 8; b++) { h ^= (v >> (b * 8)) & 0xFF; h *= 1099511628211UL; } }
        for (int i = 0; i < N; i++)
        {
            if (!Alive[i]) continue;
            mix((ulong)i); mix(BitConverter.DoubleToUInt64Bits(X[i])); mix(BitConverter.DoubleToUInt64Bits(Y[i])); mix(BitConverter.DoubleToUInt64Bits(M[i]));
            // everything the next step depends on, so that equal hash = equal run from here on
            mix(BitConverter.DoubleToUInt64Bits(Vx[i])); mix(BitConverter.DoubleToUInt64Bits(Vy[i])); mix((ulong)Par[i]);
            for (int e = 0; e < ElementCount; e++) mix(BitConverter.DoubleToUInt64Bits(Comp[i * ElementCount + e]));
        }
        mix(_rng); mix((ulong)N); mix((ulong)Step); foreach (int slot in _free) mix((ulong)slot);
        foreach (var f in typeof(Consts).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            if (f.GetValue(C) is double d) mix(BitConverter.DoubleToUInt64Bits(d));
            else if (f.GetValue(C) is double[] a) foreach (double x in a) mix(BitConverter.DoubleToUInt64Bits(x));
        }
        HashRules(mix); HashLayers(mix); HashStars(mix); HashElements(mix); HashStarEvents(mix);
        return h;
    }
}
