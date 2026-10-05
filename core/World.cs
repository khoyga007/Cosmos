// Core: a world is a set of OBJECTS and one set of CONSTANTS (Yang 06/10: everything is an object with its own
// physics and parameters; no anonymous grains; the universe still has constants, and the god may change them).
//   object  = position, velocity, mass, matter table (six element groups), name, colour, parent, group.
//   derived = radius (matter + densities), "pulls others" (mass over a threshold), kind. Never set by hand.
// Pull is exact 1/r^2: no softening. Two objects that touch merge. Fixed step, seeded stream: a run repeats exactly.
// Pure .NET, no Godot types.
using System;
using System.Collections.Generic;

namespace Cosmos.Core;

/// The laws every object obeys. One set per world; plain fields so the god can turn them.
public sealed partial class Consts
{
    public double G = 1.0;
    public double AttractMass = 1e-7; // an object this heavy or heavier pulls the others (about 0.0007 Earths)
    public double StarMass = 4.0;     // this heavy or heavier = a star (0.08 Suns)
    public double RadiusScale = 1.5;
    public readonly double[] Density = { 0.3, 0.9, 3, 8, 2, 10 }; // per element group, order = World.ElemName
}

public enum Kind { Star, Planet, Moon, Rock }

public sealed partial class World
{
    public const int NElem = 6, Sub = 8; // Sub small steps per Advance: moon orbits need the finer step
    public static readonly string[] ElemName = { "gas", "ice", "rock", "metal", "carbon", "radio" };
    public const double EarthMass = 1.5e-4; // a star of 50 = one Sun, so Earth = 50 * 3e-6

    public readonly Consts C = new();

    // objects: structure of arrays. Slots are stable: a dead slot is reused by a later Add, never shifted.
    public int N;    // slots in use, dead ones included
    public int Live; // objects alive
    public readonly double[] X, Y, Vx, Vy, M, R, Comp; // Comp = mass per element group, row per object
    public readonly bool[] Alive;
    public readonly string?[] Name;
    public readonly uint[] Col;  // 0xRRGGBB for the draw layer, 0 = none
    public readonly int[] Par;   // object it was put in orbit around, -1 = none
    public readonly int[] Grp;   // index into Groups, 0 = none
    public readonly List<string> Groups = new() { "" }; // a group (a belt) = a name; its numbers come from its members
    public long Step, Merges;

    readonly Stack<int> _free = new();
    readonly int[] _att; int _na;                 // objects that pull, rebuilt every small step
    readonly List<(int, int)> _hits = new();      // pairs that touched in this small step
    ulong _rng;

    public World(int capacity, ulong seed)
    {
        X = new double[capacity]; Y = new double[capacity]; Vx = new double[capacity]; Vy = new double[capacity];
        M = new double[capacity]; R = new double[capacity]; Comp = new double[capacity * NElem];
        Alive = new bool[capacity]; Name = new string?[capacity]; Col = new uint[capacity]; Par = new int[capacity]; Grp = new int[capacity];
        _att = new int[capacity];
        Temp = new double[capacity]; _temperatureBands = new int[capacity]; _starSlots = new int[capacity]; _starLight = new double[capacity];
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
        if (M[i] >= C.StarMass) return Kind.Star;
        if (!Attracts(i)) return Kind.Rock;
        int p = Par[i];
        return p >= 0 && Alive[p] && M[p] < C.StarMass ? Kind.Moon : Kind.Planet;
    }

    void SetRadius(int i)
    {
        double vol = 0;
        for (int e = 0; e < NElem; e++) vol += Comp[i * NElem + e] / C.Density[e];
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

    /// Radius inside which object i, not its primary (parent, else the heaviest object), rules small things.
    public double Hill(int i)
    {
        int p = Par[i] >= 0 && Alive[Par[i]] ? Par[i] : Heaviest();
        if (p == i || p < 0) return double.MaxValue;
        double dx = X[i] - X[p], dy = Y[i] - Y[p];
        return Math.Sqrt(dx * dx + dy * dy) * Math.Cbrt(M[i] / (3 * M[p]));
    }

    // ---- making objects

    /// Returns the slot, or -1 when the world is full.
    public int Add(double x, double y, double vx, double vy, double m, double[] mix, string? name = null, uint col = 0, int par = -1, int grp = 0)
    {
        int i;
        if (_free.Count > 0) i = _free.Pop(); else if (N < X.Length) i = N++; else return -1;
        X[i] = x; Y[i] = y; Vx[i] = vx; Vy[i] = vy; M[i] = m; Alive[i] = true; Name[i] = name; Col[i] = col; Par[i] = par; Grp[i] = grp;
        for (int e = 0; e < NElem; e++) Comp[i * NElem + e] = m * mix[e];
        SetRadius(i); Live++;
        ResetTemperature(i);
        return i;
    }

    /// New object at (x, y) on a circular orbit around `parent`, same turning sense as the planets.
    public int AddOrbiting(int parent, double x, double y, double m, double[] mix, string? name = null, uint col = 0, int grp = 0, double speedFactor = 1)
    {
        double dx = x - X[parent], dy = y - Y[parent], r = Math.Sqrt(dx * dx + dy * dy);
        double v = Math.Sqrt(C.G * (M[parent] + m) / r) * speedFactor;
        return Add(x, y, Vx[parent] - dy / r * v, Vy[parent] + dx / r * v, m, mix, name, col, parent, grp);
    }

    // the heavier one keeps its identity; mass, momentum and matter are summed
    void Merge(int a, int b)
    {
        int k = M[a] >= M[b] ? a : b, d = k == a ? b : a;
        double m = M[k] + M[d];
        X[k] = (X[k] * M[k] + X[d] * M[d]) / m; Y[k] = (Y[k] * M[k] + Y[d] * M[d]) / m;
        Vx[k] = (Vx[k] * M[k] + Vx[d] * M[d]) / m; Vy[k] = (Vy[k] * M[k] + Vy[d] * M[d]) / m;
        for (int e = 0; e < NElem; e++) Comp[k * NElem + e] += Comp[d * NElem + e];
        M[k] = m; SetRadius(k);
        Alive[d] = false; M[d] = 0; _free.Push(d); Live--; Merges++;
        for (int i = 0; i < N; i++) if (Par[i] == d) Par[i] = k;
    }

    // ---- scenes

    // Distances are squeezed (d = 45 * AU^0.62) so all eight planets fit one screen; order and mass ratios are real.
    public static double SolDist(double au) => 45 * Math.Pow(au, 0.62);

    /// Our own system: Sun, eight planets, the Moon, and `rocks` small objects in two belts (40% asteroid, 60% Kuiper).
    public static World SolSystem(int rocks, ulong seed)
    {
        var w = new World(rocks + 256, seed);
        int sun = w.Add(0, 0, 0, 0, 50, new double[] { 1, 0, 0, 0, 0, 0 }, "Mặt Trời", 0xFFDB8C);
        int planet(string name, double au, double earths, uint col, params double[] mix)
        {
            double d = SolDist(au), a = w.Next() * Math.Tau;
            return w.AddOrbiting(sun, Math.Cos(a) * d, Math.Sin(a) * d, earths * EarthMass, mix, name, col);
        }
        //                                          gas   ice   rock  metal carbon radio
        planet("Sao Thủy", 0.387, 0.0553, 0x9C9C9C, 0, 0, 0.30, 0.69, 0, 0.01);
        planet("Sao Kim", 0.723, 0.815, 0xE8CF9A, 0, 0, 0.66, 0.32, 0.01, 0.01);
        int earth = planet("Trái Đất", 1.0, 1.0, 0x4F8FE8, 0, 0.01, 0.66, 0.32, 0.005, 0.005);
        planet("Sao Hỏa", 1.524, 0.107, 0xD0603A, 0, 0.01, 0.73, 0.25, 0.005, 0.005);
        planet("Sao Mộc", 5.203, 317.8, 0xD9B48A, 0.90, 0.05, 0.03, 0.015, 0.005, 0);
        planet("Sao Thổ", 9.537, 95.2, 0xE6D29A, 0.85, 0.08, 0.045, 0.02, 0.005, 0);
        planet("Sao Thiên Vương", 19.19, 14.5, 0x9FE3E8, 0.15, 0.65, 0.15, 0.04, 0.01, 0);
        planet("Sao Hải Vương", 30.07, 17.1, 0x4466E0, 0.12, 0.66, 0.16, 0.05, 0.01, 0);
        w.AddOrbiting(earth, w.X[earth] + 0.12, w.Y[earth], 0.0123 * EarthMass, new[] { 0, 0.01, 0.72, 0.26, 0.005, 0.005 }, "Mặt Trăng", 0xC8C8C8);

        w.Groups.Add("Vành đai tiểu hành tinh"); w.Groups.Add("Vành đai Kuiper");
        double[] belt = { 0, 0.05, 0.60, 0.20, 0.14, 0.01 }, kuiper = { 0.03, 0.72, 0.15, 0.02, 0.08, 0 };
        var mix = new double[NElem];
        for (int i = 0; i < rocks; i++)
        {
            bool inner = i < rocks * 40 / 100; // asteroid belt 2.1-3.3 AU, Kuiper belt 32-50 AU
            double[] table = inner ? belt : kuiper;
            double au = inner ? 2.1 + 1.2 * w.Next() : 32 + 18 * w.Next(), d = SolDist(au), a = w.Next() * Math.Tau;
            // each rock is mostly one group (drawn from the belt's table), the rest follows the table
            double u = w.Next(); int main = NElem - 1;
            for (int e = 0; e < NElem - 1; e++) { u -= table[e]; if (u < 0) { main = e; break; } }
            for (int e = 0; e < NElem; e++) mix[e] = 0.3 * table[e] + (e == main ? 0.7 : 0);
            double q = w.Next(), m = 1e-10 * (1 + 200 * q * q * q); // up to 2e-8: all below AttractMass
            w.AddOrbiting(sun, Math.Cos(a) * d, Math.Sin(a) * d, m, mix, null, 0, inner ? 1 : 2, 0.99 + 0.02 * w.Next());
            w.Par[w.N - 1] = -1; // rocks get no orbit line
        }
        w.RunRules();
        return w;
    }

    // ---- stepping

    public void Advance(double h)
    {
        if (!double.IsFinite(h) || h < 0 || !double.IsFinite(C.YearTime) || C.YearTime <= 0)
            throw new ArgumentOutOfRangeException(nameof(h), "Advance requires finite non-negative time and positive YearTime.");
        double hs = h / Sub, g = C.G;
        for (int sub = 0; sub < Sub; sub++)
        {
            _na = 0;
            for (int i = 0; i < N; i++) if (Alive[i] && M[i] >= C.AttractMass) _att[_na++] = i;

            // kick: every object feels every object that pulls. Positions do not move here, so order does not matter.
            for (int i = 0; i < N; i++)
            {
                if (!Alive[i]) continue;
                double x = X[i], y = Y[i], ri = R[i], ax = 0, ay = 0;
                bool heavy = M[i] >= C.AttractMass;
                for (int k = 0; k < _na; k++)
                {
                    int j = _att[k];
                    if (j == i) continue;
                    double dx = X[j] - x, dy = Y[j] - y, d2 = dx * dx + dy * dy, rr = ri + R[j];
                    if (d2 < rr * rr) { if (!heavy || i < j) _hits.Add((i, j)); continue; } // touching: merge after the drift, once per pair
                    double inv = g * M[j] / (d2 * Math.Sqrt(d2));
                    ax += dx * inv; ay += dy * inv;
                }
                Vx[i] += ax * hs; Vy[i] += ay * hs;
            }
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
    //           add a grid broad-phase when belts should grind. A fast object can also skip across a body
    //           smaller than one small step of travel; add a swept test if that shows.

    /// FNV-1a over the raw state: equal hash = equal run.
    public ulong Hash()
    {
        ulong h = 14695981039346656037UL;
        void mix(ulong v) { for (int b = 0; b < 8; b++) { h ^= (v >> (b * 8)) & 0xFF; h *= 1099511628211UL; } }
        for (int i = 0; i < N; i++)
        {
            if (!Alive[i]) continue;
            mix((ulong)i); mix(BitConverter.DoubleToUInt64Bits(X[i])); mix(BitConverter.DoubleToUInt64Bits(Y[i])); mix(BitConverter.DoubleToUInt64Bits(M[i]));
        }
        HashRules(mix);
        return h;
    }
}
