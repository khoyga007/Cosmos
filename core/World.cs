// Spike core: one flat world = heavy bodies (N-body among themselves) + light grains (feel the bodies only).
// Pure .NET, no Godot types. Fixed step, seeded stream, so a run repeats exactly on one machine.
using System;

namespace Cosmos.Core;

public sealed class World
{
    public const double G = 1.0, EpsBody = 9.0, EpsGrain = 16.0;

    // bodies: structure of arrays
    public int Nb;
    public double[] Bx = new double[64], By = new double[64], Bvx = new double[64], Bvy = new double[64], Bm = new double[64];

    // grains: float arrays, the draw layer reads Px/Py directly
    public int Np;
    public readonly float[] Px, Py, Pvx, Pvy;

    public long Step;
    ulong _rng;

    public World(int maxGrains, ulong seed)
    {
        Px = new float[maxGrains]; Py = new float[maxGrains]; Pvx = new float[maxGrains]; Pvy = new float[maxGrains];
        _rng = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;
    }

    public double Next() // xorshift64*, 53 bits
    {
        _rng ^= _rng >> 12; _rng ^= _rng << 25; _rng ^= _rng >> 27;
        return ((_rng * 0x2545F4914F6CDD1DUL) >> 11) * (1.0 / 9007199254740992.0);
    }

    public int AddBody(double x, double y, double vx, double vy, double m)
    {
        int i = Nb++; Bx[i] = x; By[i] = y; Bvx[i] = vx; Bvy[i] = vy; Bm[i] = m; return i;
    }

    public void AddGrain(double x, double y, double vx, double vy)
    {
        int i = Np++; Px[i] = (float)x; Py[i] = (float)y; Pvx[i] = (float)vx; Pvy[i] = (float)vy;
    }

    /// One star, `planets` planets on circular orbits, `grains` grains in a wide disc.
    public static World Solar(int grains, int planets, ulong seed)
    {
        var w = new World(grains, seed);
        const double M = 50;
        w.AddBody(0, 0, 0, 0, M);
        for (int k = 0; k < planets; k++)
        {
            double d = 50 + 45.0 * k, a = w.Next() * Math.Tau, v = Math.Sqrt(G * M / d);
            w.AddBody(Math.Cos(a) * d, Math.Sin(a) * d, -Math.Sin(a) * v, Math.Cos(a) * v, 1.5e-4 * (1 + 30 * w.Next()));
        }
        for (int i = 0; i < grains; i++)
        {
            double d = 30 + 420 * Math.Sqrt(w.Next()), a = w.Next() * Math.Tau, v = Math.Sqrt(G * M / d);
            w.AddGrain(Math.Cos(a) * d, Math.Sin(a) * d, -Math.Sin(a) * v, Math.Cos(a) * v);
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
        for (int i = 0; i < Np; i++)
        {
            float x = Px[i], y = Py[i], ax = 0, ay = 0;
            for (int k = 0; k < Nb; k++)
            {
                float dx = (float)Bx[k] - x, dy = (float)By[k] - y, q = dx * dx + dy * dy + (float)EpsGrain;
                float inv = (float)(G * Bm[k]) / (q * MathF.Sqrt(q));
                ax += dx * inv; ay += dy * inv;
            }
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
        for (int i = 0; i < Nb; i++) { mix(BitConverter.DoubleToUInt64Bits(Bx[i])); mix(BitConverter.DoubleToUInt64Bits(By[i])); }
        for (int i = 0; i < Np; i++) { mix(BitConverter.SingleToUInt32Bits(Px[i])); mix(BitConverter.SingleToUInt32Bits(Py[i])); }
        return h;
    }
}
