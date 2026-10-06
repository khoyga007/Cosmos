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
        return w.Do(cmd) != -1; // -2 = applied, no object involved
    }

    /// <summary>
    /// Round 2: Turn rule on or off.
    /// </summary>
    public static bool SetRule(World w, string ruleId, bool enabled)
    {
        var cmd = new Command(CmdKind.SetRule, Name: ruleId, Amount: enabled ? 1 : 0);
        return w.Do(cmd) != -1; // -2 = applied, no object involved
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
        return w.Do(cmd) != -1; // -2 = applied, no object involved
    }

    /// <summary>
    /// The object a thing of this mass at (x, y) would ride around: the lightest pulling object, heavier than the
    /// thing itself, whose zone (Hill radius) holds the point. -1 when nothing heavier pulls there.
    /// </summary>
    public static int PrimaryAt(World w, double x, double y, double mass, int skip = -1)
    {
        int best = -1;
        for (int j = 0; j < w.N; j++)
        {
            if (j == skip || !w.Alive[j] || !w.Attracts(j) || w.M[j] <= mass) continue;
            if (best >= 0 && w.M[j] >= w.M[best]) continue;
            double dx = x - w.X[j], dy = y - w.Y[j], h = w.Hill(j);
            if (h == double.MaxValue || dx * dx + dy * dy < h * h) best = j;
        }
        return best;
    }

    /// <summary>
    /// Speed of a circular orbit at (x, y) around primary; 1 when there is no primary. The mouse scale for
    /// launch and push: a drag reads as a share of this speed, so it feels the same next to a moon or a star.
    /// </summary>
    public static double OrbitSpeed(World w, int primary, double x, double y)
    {
        if (primary < 0) return 1;
        double dx = x - w.X[primary], dy = y - w.Y[primary], d = Math.Sqrt(dx * dx + dy * dy);
        return d > 0 ? Math.Sqrt(w.C.G * w.M[primary] / d) : 1;
    }

    /// <summary>
    /// Create an object at (x, y) with the given velocity.
    /// </summary>
    public static int Launch(World w, double x, double y, double vx, double vy, double mass, double[] mix, string? name = null)
    {
        if (mass <= 0) return -1;
        return w.Do(new Command(CmdKind.Create, X: x, Y: y, Vx: vx, Vy: vy, Amount: mass, Mix: NormalizeMix(mix), Name: name));
    }

    /// <summary>
    /// Push the target onto a circular orbit around the object it rides around, keeping its direction of turn.
    /// </summary>
    public static int Circularize(World w, int target)
    {
        if (target < 0 || target >= w.N || !w.Alive[target]) return -1;
        int p = PrimaryAt(w, w.X[target], w.Y[target], w.M[target], target);
        if (p < 0) return -1;
        double dx = w.X[target] - w.X[p], dy = w.Y[target] - w.Y[p], d = Math.Sqrt(dx * dx + dy * dy);
        if (!(d > 0)) return -1;
        double rvx = w.Vx[target] - w.Vx[p], rvy = w.Vy[target] - w.Vy[p];
        double v = Math.Sqrt(w.C.G * (w.M[p] + w.M[target]) / d) * (dx * rvy - dy * rvx < 0 ? -1 : 1);
        return Push(w, target, -dy / d * v - rvx, dx / d * v - rvy);
    }

    public static int Remove(World w, int target)
    {
        if (target < 0 || target >= w.N || !w.Alive[target]) return -1;
        return w.Do(new Command(CmdKind.Remove, Target: target));
    }

    /// <summary>
    /// Path a thing would take around primary from (rx, ry) with velocity (rvx, rvy), both seen from the primary.
    /// Draw aid only (two bodies, nothing else pulls). Fills path with x, y pairs; returns the number of points.
    /// hits = the path ends inside the primary.
    /// </summary>
    public static int Predict(World w, int primary, double mass, double rx, double ry, double rvx, double rvy, double[] path, out bool hits)
    {
        hits = false;
        int max = path.Length / 2;
        if (primary < 0 || max < 2) return 0;
        double mu = w.C.G * (w.M[primary] + mass), r = Math.Sqrt(rx * rx + ry * ry);
        if (!(r > 0) || !(mu > 0)) return 0;
        double v2 = rvx * rvx + rvy * rvy, inva = 2 / r - v2 / mu;
        // one whole turn when it is held; otherwise far enough to show where it leaves to
        double total = inva > 1e-9 ? Math.Tau / Math.Sqrt(mu * inva * inva * inva) : 8 * r / Math.Sqrt(Math.Max(v2, 1e-12));
        int sub = 8; double dt = total / ((max - 1) * sub);
        int n = 0;
        path[0] = rx; path[1] = ry; n = 1;
        for (int k = 1; k < max; k++)
        {
            for (int s = 0; s < sub; s++)
            {
                double r3 = r * r * r;
                rvx -= mu * rx / r3 * dt * 0.5; rvy -= mu * ry / r3 * dt * 0.5;
                rx += rvx * dt; ry += rvy * dt;
                r = Math.Sqrt(rx * rx + ry * ry); r3 = r * r * r;
                rvx -= mu * rx / r3 * dt * 0.5; rvy -= mu * ry / r3 * dt * 0.5;
                if (r < w.R[primary]) { hits = true; break; }
            }
            if (!double.IsFinite(rx) || !double.IsFinite(ry)) break;
            path[n * 2] = rx; path[n * 2 + 1] = ry; n++;
            if (hits) break;
        }
        return n;
    }

    /// <summary>
    /// Put the target at (x, y). circular = it lands on a circular orbit around whatever rules that spot (at rest
    /// when nothing heavier is there); otherwise it keeps the velocity it had. alone = what it holds in orbit stays behind.
    /// </summary>
    public static int Move(World w, int target, double x, double y, bool circular, bool alone = false)
    {
        if (target < 0 || target >= w.N || !w.Alive[target]) return -1;
        double vx = w.Vx[target], vy = w.Vy[target];
        if (circular)
        {
            int p = PrimaryAt(w, x, y, w.M[target], target);
            vx = vy = 0;
            if (p >= 0)
            {
                double dx = x - w.X[p], dy = y - w.Y[p], d = Math.Sqrt(dx * dx + dy * dy);
                if (!(d > 0)) return -1;
                double v = Math.Sqrt(w.C.G * (w.M[p] + w.M[target]) / d);
                vx = w.Vx[p] - dy / d * v; vy = w.Vy[p] + dx / d * v;
            }
        }
        return w.Do(new Command(CmdKind.Move, Target: target, X: x, Y: y, Vx: vx, Vy: vy, Index: alone ? 1 : 0));
    }

    /// <summary>
    /// The hand: pull (amount > 0) or shove away (amount < 0) everything within radius of (x, y).
    /// </summary>
    public static bool Force(World w, double x, double y, double radius, double amount)
    {
        return w.Do(new Command(CmdKind.Force, X: x, Y: y, Vx: radius, Amount: amount)) != -1;
    }
}
