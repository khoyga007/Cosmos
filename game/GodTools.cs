using System;
using Cosmos.Core;

namespace Cosmos.Game;

/// <summary>
/// Core god tool actions in the window (SPEC 6 P2 & SPEC 7 Round 2).
/// Every tool creates a Command and sends it through World.Do.
/// No direct writes to world arrays.
/// </summary>
public static class GodTools
{
    /// <summary>
    /// Computes derived preview of Kind and Radius before placing an object.
    /// </summary>
    public static (Kind Kind, double Radius) Preview(World w, double mass, double[] mix, int parent = -1)
    {
        double sum = 0;
        for (int i = 0; i < World.NElem && i < mix.Length; i++) sum += Math.Max(0, mix[i]);
        if (sum <= 0) sum = 1.0;

        double vol = 0;
        for (int e = 0; e < World.NElem; e++)
        {
            double share = (e < mix.Length ? Math.Max(0, mix[e]) : 0) / sum;
            vol += (mass * share) / w.C.Density[e];
        }
        double radius = w.C.RadiusScale * Math.Cbrt(vol);

        Kind kind;
        if (mass >= w.C.StarMass) kind = Kind.Star;
        else if (mass < w.C.AttractMass) kind = Kind.Rock;
        else if (parent >= 0 && parent < w.N && w.Alive[parent] && w.M[parent] < w.C.StarMass) kind = Kind.Moon;
        else kind = Kind.Planet;

        return (kind, radius);
    }

    /// <summary>
    /// Normalizes mix array so sum is 1.0. Returns double[6].
    /// </summary>
    public static double[] NormalizeMix(double[] mix)
    {
        var norm = new double[World.NElem];
        double sum = 0;
        for (int i = 0; i < World.NElem && i < mix.Length; i++)
        {
            norm[i] = Math.Max(0, mix[i]);
            sum += norm[i];
        }
        if (sum <= 0)
        {
            norm[2] = 1.0; // default to rock
        }
        else
        {
            for (int i = 0; i < World.NElem; i++) norm[i] /= sum;
        }
        return norm;
    }

    /// <summary>
    /// Tool 1: Create object at (x, y).
    /// If parent >= 0 and alive, creates in circular orbit around parent (CreateOrbiting).
    /// Otherwise creates at rest with Vx=0, Vy=0 (Create).
    /// </summary>
    public static int Create(World w, double x, double y, double mass, double[] mix, int parent = -1, string? name = null, uint col = 0)
    {
        if (mass <= 0) return -1;
        double[] normMix = NormalizeMix(mix);

        Command cmd;
        if (parent >= 0 && parent < w.N && w.Alive[parent])
        {
            if (x == w.X[parent] && y == w.Y[parent]) x += w.R[parent] + 1.0;
            cmd = new Command(CmdKind.CreateOrbiting, Target: parent, X: x, Y: y, Amount: mass, Mix: normMix, Name: name, Col: col);
        }
        else
        {
            cmd = new Command(CmdKind.Create, X: x, Y: y, Vx: 0, Vy: 0, Amount: mass, Mix: normMix, Name: name, Col: col);
        }
        return w.Do(cmd);
    }

    /// <summary>
    /// Tool 2: Edit matter on target object.
    /// Target gets amount (may be negative) of element group index. Mass follows.
    /// </summary>
    public static int AddMatter(World w, int target, int elemIndex, double amount)
    {
        if (target < 0 || target >= w.N || !w.Alive[target]) return -1;
        if (elemIndex < 0 || elemIndex >= World.NElem) return -1;
        var cmd = new Command(CmdKind.AddMatter, Target: target, Amount: amount, Index: elemIndex);
        return w.Do(cmd);
    }

    /// <summary>
    /// Tool 3: Push target object.
    /// Target velocity += (vx, vy).
    /// </summary>
    public static int Push(World w, int target, double vx, double vy)
    {
        if (target < 0 || target >= w.N || !w.Alive[target]) return -1;
        var cmd = new Command(CmdKind.Push, Target: target, Vx: vx, Vy: vy);
        return w.Do(cmd);
    }

    /// <summary>
    /// Tool 5: Set constant by name (e.g. "G", "Density[2]").
    /// </summary>
    public static bool SetConst(World w, string name, double value)
    {
        var cmd = new Command(CmdKind.SetConst, Name: name, Amount: value);
        int r = w.Do(cmd);
        return r >= 0;
    }

    /// <summary>
    /// Round 2: Turn rule on or off.
    /// </summary>
    public static bool SetRule(World w, string ruleId, bool enabled)
    {
        var cmd = new Command(CmdKind.SetRule, Name: ruleId, Amount: enabled ? 1 : 0);
        int r = w.Do(cmd);
        return r >= 0;
    }

    /// <summary>
    /// Round 2: Seed life at level 0..1 on a planet or moon (0 wipes it).
    /// </summary>
    public static int SeedLife(World w, int target, double level)
    {
        if (target < 0 || target >= w.N || !w.Alive[target] || !w.IsWorld(target)) return -1;
        var cmd = new Command(CmdKind.SeedLife, Target: target, Amount: level);
        return w.Do(cmd);
    }

    /// <summary>
    /// Round 2: Fast forward simulation by years (two-body closed formula).
    /// </summary>
    public static bool FastForward(World w, double years)
    {
        if (!double.IsFinite(years) || years <= 0) return false;
        var cmd = new Command(CmdKind.FastForward, Amount: years);
        int r = w.Do(cmd);
        return r >= 0;
    }
}
