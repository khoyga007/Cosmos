using System;
using System.Collections.Generic;

namespace Cosmos.Core;

// Matter leaving the simulated system. StellarEjecta* measures emitted shells, never added twice.
public readonly record struct EscapedTransfer(double Year, int Source, int Generation, string Cause, double X, double Y,
    double Mass, double Px, double Py, IReadOnlyList<double> Matter);

public sealed partial class World
{
    public double EscapedMass { get; private set; }
    public double EscapedPx { get; private set; }
    public double EscapedPy { get; private set; }
    public readonly double[] EscapedMatter;
    public readonly double[] NucleosynthesisDelta;
    readonly List<EscapedTransfer> _escapedTransfers = new();
    public IReadOnlyList<EscapedTransfer> EscapedTransfers => _escapedTransfers.AsReadOnly();

    void Escape(int i, string cause, double mass, double[] matter, double px, double py, double? originX = null, double? originY = null)
    {
        if (!(mass > 0)) return;
        EscapedMass += mass; EscapedPx += px; EscapedPy += py;
        for (int e = 0; e < ElementCount; e++) EscapedMatter[e] += matter[e];
        // Keep discrete stellar transfers for the future interstellar environment. Volatile streams
        // use the cumulative ledger: one entry per grain per rule tick would grow without bound.
        _escapedTransfers.Add(new(Year, i, Gen[i], cause, originX ?? X[i], originY ?? Y[i], mass, px, py, Array.AsReadOnly((double[])matter.Clone())));
    }

    void EscapeRole(int i, ElementRole role, double factor, bool trimResidual = false)
    {
        double lost = 0; int offset = i * ElementCount;
        foreach (int e in _roleElements[role])
        {
            double before = Comp[offset + e], after = before * factor;
            if (trimResidual && after <= 1e-18) after = 0;
            Comp[offset + e] = after;
            double cellLoss = before - after;
            EscapedMatter[e] += cellLoss; lost += cellLoss;
        }
        EscapedMass += lost; EscapedPx += lost * Vx[i]; EscapedPy += lost * Vy[i];
        // Re-sum AFTER residual trimming. The old ice path computed M before erasing its last cells.
        M[i] = 0;
        for (int e = 0; e < ElementCount; e++) M[i] += Comp[offset + e];
        if (M[i] == 0) Kill(i); else SetRadius(i);
    }

    void HashEscape(Action<ulong> mix)
    {
        void Number(double x) => mix(BitConverter.DoubleToUInt64Bits(x));
        Number(EscapedMass); Number(EscapedPx); Number(EscapedPy);
        foreach (double x in EscapedMatter) Number(x);
        foreach (double x in NucleosynthesisDelta) Number(x);
        mix((ulong)_escapedTransfers.Count);
        foreach (var t in _escapedTransfers)
        {
            Number(t.Year); mix((ulong)t.Source); mix((ulong)t.Generation);
            mix((ulong)t.Cause.Length); foreach (char c in t.Cause) mix(c);
            Number(t.X); Number(t.Y);
            Number(t.Mass); Number(t.Px); Number(t.Py); foreach (double x in t.Matter) Number(x);
        }
    }
}
