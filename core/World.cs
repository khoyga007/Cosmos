// Spike core: one flat world = heavy bodies (N-body among themselves) + light grains (feel the bodies only).
// Matter: every grain is one of six element groups; every body keeps a mass table per group.
// A grain that touches a body is absorbed: its mass goes into that body's table. Elements ARE the resources (Yang 06/10).
// Pure .NET, no Godot types. Fixed step, seeded stream, so a run repeats exactly on one machine.
using System;

namespace Cosmos.Core;

public sealed class World
{
    public const double G = 1.0, EpsBody = 9.0, EpsGrain = 16.0;
    public const int NElem = 6;
    public static readonly string[] ElemName = { "gas", "ice", "rock", "metal", "carbon", "radio" };
    public const double GrainMass = 1e-6, FrostLine = 180;
    // share of each group, inside / outside the frost line; used for grains and for the starting planets
    static readonly double[] MixInner = { 0.02, 0.03, 0.55, 0.25, 0.12, 0.03 }, MixOuter = { 0.25, 0.45, 0.10, 0.04, 0.15, 0.01 }, MixStar = { 1, 0, 0, 0, 0, 0 };

    // bodies: structure of arrays
    public int Nb;
    public double[] Bx = new double[64], By = new double[64], Bvx = new double[64], Bvy = new double[64], Bm = new double[64], Br = new double[64];
    public readonly double[] Bcomp = new double[64 * NElem]; // mass per element group, row per body

    // grains: float arrays, the draw layer reads Px/Py directly
    public int Np;
    public readonly float[] Px, Py, Pvx, Pvy;
    public readonly byte[] Pe; // element group of each grain

    public long Step;
    ulong _rng;

    public World(int maxGrains, ulong seed)
    {
        Px = new float[maxGrains]; Py = new float[maxGrains]; Pvx = new float[maxGrains]; Pvy = new float[maxGrains]; Pe = new byte[maxGrains];
        _rng = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;
    }

    public double Next() // xorshift64*, 53 bits
    {
        _rng ^= _rng >> 12; _rng ^= _rng << 25; _rng ^= _rng >> 27;
        return ((_rng * 0x2545F4914F6CDD1DUL) >> 11) * (1.0 / 9007199254740992.0);
    }

    // ponytail: anything heavier than 1 is a star with a fixed size; real radius from density when bodies get types
    static double RadiusOf(double m) => m > 1 ? 8 : 2 + 10 * Math.Cbrt(m);

    public int AddBody(double x, double y, double vx, double vy, double m, double[] mix)
    {
        int i = Nb++; Bx[i] = x; By[i] = y; Bvx[i] = vx; Bvy[i] = vy; Bm[i] = m; Br[i] = RadiusOf(m);
        for (int e = 0; e < NElem; e++) Bcomp[i * NElem + e] = m * mix[e];
        return i;
    }

    public void AddGrain(double x, double y, double vx, double vy, byte e)
    {
        int i = Np++; Px[i] = (float)x; Py[i] = (float)y; Pvx[i] = (float)vx; Pvy[i] = (float)vy; Pe[i] = e;
    }

    byte Pick(double[] mix)
    {
        double u = Next();
        for (byte e = 0; e < NElem - 1; e++) { u -= mix[e]; if (u < 0) return e; }
        return NElem - 1;
    }

    // body k swallows grain i: mass and momentum go to the body, the last grain takes slot i
    void Absorb(int k, int i)
    {
        double m = Bm[k] + GrainMass;
        Bvx[k] += GrainMass * (Pvx[i] - Bvx[k]) / m; Bvy[k] += GrainMass * (Pvy[i] - Bvy[k]) / m;
        Bm[k] = m; Bcomp[k * NElem + Pe[i]] += GrainMass; Br[k] = RadiusOf(m);
        int l = --Np; Px[i] = Px[l]; Py[i] = Py[l]; Pvx[i] = Pvx[l]; Pvy[i] = Pvy[l]; Pe[i] = Pe[l];
    }

    /// One star, `planets` planets on circular orbits, `grains` grains in a wide disc.
    public static World Solar(int grains, int planets, ulong seed)
    {
        var w = new World(grains, seed);
        const double M = 50;
        w.AddBody(0, 0, 0, 0, M, MixStar);
        for (int k = 0; k < planets; k++)
        {
            double d = 50 + 45.0 * k, a = w.Next() * Math.Tau, v = Math.Sqrt(G * M / d);
            w.AddBody(Math.Cos(a) * d, Math.Sin(a) * d, -Math.Sin(a) * v, Math.Cos(a) * v, 1.5e-4 * (1 + 30 * w.Next()), d < FrostLine ? MixInner : MixOuter);
        }
        for (int i = 0; i < grains; i++)
        {
            double d = 30 + 420 * Math.Sqrt(w.Next()), a = w.Next() * Math.Tau, v = Math.Sqrt(G * M / d);
            w.AddGrain(Math.Cos(a) * d, Math.Sin(a) * d, -Math.Sin(a) * v, Math.Cos(a) * v, w.Pick(d < FrostLine ? MixInner : MixOuter));
        }
        return w;
    }

    public void Advance(double h)
    {
        // bodies: pairwise, kick then drift
        for (int i = 0; i < Nb; i++)
        {
            double ax = 0, ay = 0;
            for (int j = 0; j < Nb; j++)
            {
                if (j == i) continue;
                double dx = Bx[j] - Bx[i], dy = By[j] - By[i], q = dx * dx + dy * dy + EpsBody, inv = G * Bm[j] / (q * Math.Sqrt(q));
                ax += dx * inv; ay += dy * inv;
            }
            Bvx[i] += ax * h; Bvy[i] += ay * h;
        }
        for (int i = 0; i < Nb; i++) { Bx[i] += Bvx[i] * h; By[i] += Bvy[i] * h; }

        // grains: each feels every body. Sequential on purpose: same result every run.
        float hf = (float)h;
        Span<float> r2 = stackalloc float[Nb];
        for (int k = 0; k < Nb; k++) r2[k] = (float)(Br[k] * Br[k]);
        for (int i = 0; i < Np; i++)
        {
            float x = Px[i], y = Py[i], ax = 0, ay = 0;
            int hit = -1;
            for (int k = 0; k < Nb; k++)
            {
                float dx = (float)Bx[k] - x, dy = (float)By[k] - y, d2 = dx * dx + dy * dy, q = d2 + (float)EpsGrain;
                if (d2 < r2[k]) { hit = k; break; }
                float inv = (float)(G * Bm[k]) / (q * MathF.Sqrt(q));
                ax += dx * inv; ay += dy * inv;
            }
            if (hit >= 0) { Absorb(hit, i); r2[hit] = (float)(Br[hit] * Br[hit]); i--; continue; }
            float vx = Pvx[i] + ax * hf, vy = Pvy[i] + ay * hf;
            Pvx[i] = vx; Pvy[i] = vy; Px[i] = x + vx * hf; Py[i] = y + vy * hf;
        }
        Step++;
    }

    /// FNV-1a over the raw state: equal hash = equal run.
    public ulong Hash()
    {
        ulong h = 14695981039346656037UL;
        void mix(ulong v) { for (int b = 0; b < 8; b++) { h ^= (v >> (b * 8)) & 0xFF; h *= 1099511628211UL; } }
        for (int i = 0; i < Nb; i++) { mix(BitConverter.DoubleToUInt64Bits(Bx[i])); mix(BitConverter.DoubleToUInt64Bits(By[i])); mix(BitConverter.DoubleToUInt64Bits(Bm[i])); }
        for (int i = 0; i < Np; i++) { mix(BitConverter.SingleToUInt32Bits(Px[i])); mix(BitConverter.SingleToUInt32Bits(Py[i])); mix(Pe[i]); }
        return h;
    }
}
