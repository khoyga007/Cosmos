// The only way the outside (window, tests, later a script) changes a world: a Command through World.Do.
// Every command is kept in the Journal with the step it arrived at, so a run can be replayed exactly:
// same scene + same journal = same world. Anything the god can do must be a command here.
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Cosmos.Core;

public enum CmdKind
{
    Create,          // new object at (X, Y) with velocity (Vx, Vy), mass Amount, matter Mix
    CreateOrbiting,  // new object at (X, Y) on a circular orbit around Target, mass Amount, matter Mix
    AddMatter,       // Target gets Amount (may be negative) of element group Index; mass follows; nothing left = removed
    Push,            // Target velocity += (Vx, Vy)
    SetConst,        // constant Name ("G", "Density[2]", ...) = Amount
    Remove,          // Target is taken out of the world
    SetRule,         // rule Name ("temperature", ...) switched on (Amount != 0) or off (0)
    SeedLife,        // Target (a planet or moon) gets life at level Amount (0..1); 0 wipes it. Whether it lasts is up to the rules
    Move,            // Target is put at (X, Y) with velocity (Vx, Vy); what it holds in orbit goes along
    Force,           // the god's hand: every object within radius Vx of (X, Y) gets velocity toward that point, Amount at
                     // the centre fading to 0 at the edge; negative Amount = away. Mass does not matter
    FastForward,     // every object rides its present orbit for Amount years (closed formula: no pull between siblings, no collisions)
}

public record struct Command(CmdKind Kind, int Target = -1, double X = 0, double Y = 0, double Vx = 0, double Vy = 0,
    double Amount = 0, int Index = 0, double[]? Mix = null, string? Name = null, uint Col = 0);

public sealed partial class World
{
    public readonly List<(long Step, Command Cmd)> Journal = new();

    /// Applies the command now (call between Advance calls) and records it. Returns the new slot for the two
    /// Create kinds, the target for the others, -1 when the command could not be applied (and is then not recorded).
    public int Do(in Command c)
    {
        int r = Apply(c);
        if (r >= 0 || r == -2) { Journal.Add((Step, c)); return Math.Max(r, 0); }
        return -1;
    }

    /// Feeds a recorded journal back: call before each Advance; applies the commands that arrived at this step.
    public void Replay(List<(long Step, Command Cmd)> journal, ref int next)
    {
        while (next < journal.Count && journal[next].Step == Step) Do(journal[next++].Cmd);
    }

    bool Ok(int i) => i >= 0 && i < N && Alive[i];

    int Apply(in Command c)
    {
        switch (c.Kind)
        {
            case CmdKind.Create:
                return c.Mix is { Length: NElem } && c.Amount > 0 ? Add(c.X, c.Y, c.Vx, c.Vy, c.Amount, c.Mix, c.Name, c.Col) : -1;
            case CmdKind.CreateOrbiting:
                if (!Ok(c.Target) || c.Mix is not { Length: NElem } || c.Amount <= 0) return -1;
                if (c.X == X[c.Target] && c.Y == Y[c.Target]) return -1;
                return AddOrbiting(c.Target, c.X, c.Y, c.Amount, c.Mix, c.Name, c.Col);
            case CmdKind.AddMatter:
            {
                if (!Ok(c.Target) || c.Index < 0 || c.Index >= NElem) return -1;
                int i = c.Target, o = i * NElem;
                Comp[o + c.Index] = Math.Max(0, Comp[o + c.Index] + c.Amount);
                double m = 0;
                for (int e = 0; e < NElem; e++) m += Comp[o + e];
                if (m <= 0) { Kill(i); return i; }
                M[i] = m; SetRadius(i);
                return i;
            }
            case CmdKind.Push:
                if (!Ok(c.Target)) return -1;
                Vx[c.Target] += c.Vx; Vy[c.Target] += c.Vy;
                return c.Target;
            case CmdKind.SetConst:
                if (c.Name == null || !C.Set(c.Name, c.Amount)) return -1;
                RecalcRadii();
                return -2; // applied, no object involved
            case CmdKind.SetRule:
            {
                string? id = c.Name; Rule? rule = Rules.Find(x => x.Id == id);
                if (rule == null) return -1;
                rule.Enabled = c.Amount != 0;
                return -2;
            }
            case CmdKind.SeedLife:
                if (!Ok(c.Target) || !IsWorld(c.Target) || !(c.Amount >= 0 && c.Amount <= 1)) return -1;
                Life[c.Target] = c.Amount; Touched[c.Target] = Year;
                if (c.Amount < C.CivLifeMin) RichYears[c.Target] = 0;
                return c.Target;
            case CmdKind.FastForward:
                if (!double.IsFinite(c.Amount) || c.Amount <= 0) return -1;
                Jump(c.Amount * C.YearTime);
                return -2;
            case CmdKind.Move:
            {
                int t = c.Target;
                if (!Ok(t) || !double.IsFinite(c.X + c.Y + c.Vx + c.Vy)) return -1;
                // its moons (anything lighter, inside its zone and held by it) keep their place around it;
                // moving the heaviest object moves all it holds
                double mx = c.X - X[t], my = c.Y - Y[t], mvx = c.Vx - Vx[t], mvy = c.Vy - Vy[t], zone = Attracts(t) ? Hill(t) : 0;
                for (int i = 0; i < N; i++)
                {
                    if (!Alive[i] || i == t || M[i] >= M[t]) continue;
                    double dx = X[i] - X[t], dy = Y[i] - Y[t], d2 = dx * dx + dy * dy, ux = Vx[i] - Vx[t], uy = Vy[i] - Vy[t];
                    if (!(d2 < zone * zone) || !(ux * ux + uy * uy < 2 * C.G * (M[t] + M[i]) / Math.Sqrt(d2))) continue;
                    X[i] += mx; Y[i] += my; Vx[i] += mvx; Vy[i] += mvy;
                }
                X[t] = c.X; Y[t] = c.Y; Vx[t] = c.Vx; Vy[t] = c.Vy;
                return t;
            }
            case CmdKind.Force:
            {
                double rad = c.Vx;
                if (!double.IsFinite(c.X + c.Y + c.Amount) || !(rad > 0) || !double.IsFinite(rad)) return -1;
                for (int i = 0; i < N; i++)
                {
                    if (!Alive[i]) continue;
                    double dx = c.X - X[i], dy = c.Y - Y[i], d2 = dx * dx + dy * dy;
                    if (d2 >= rad * rad || d2 == 0) continue; // dead on the centre: nowhere to pull to, no side to shove to
                    double d = Math.Sqrt(d2), k = c.Amount * (1 - d / rad) / d;
                    Vx[i] += dx * k; Vy[i] += dy * k;
                }
                return -2;
            }
            case CmdKind.Remove:
                if (!Ok(c.Target)) return -1;
                Kill(c.Target);
                return c.Target;
        }
        return -1;
    }

    void Kill(int d)
    {
        Alive[d] = false; M[d] = 0; _free.Push(d); Live--;
        for (int i = 0; i < N; i++) if (Par[i] == d) Par[i] = -1;
    }
}

public sealed partial class Consts
{
    /// Every constant by name, for panels and files: plain fields as "G", array cells as "Density[2]".
    public IEnumerable<(string Name, double Value)> All()
    {
        foreach (FieldInfo f in typeof(Consts).GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (f.FieldType == typeof(double)) yield return (f.Name, (double)f.GetValue(this)!);
            else if (f.GetValue(this) is double[] a) for (int i = 0; i < a.Length; i++) yield return ($"{f.Name}[{i}]", a[i]);
        }
    }

    /// False when there is no such constant or the value is not a finite number.
    public bool Set(string name, double value)
    {
        if (!double.IsFinite(value)) return false;
        int b = name.IndexOf('[');
        FieldInfo? f = typeof(Consts).GetField(b < 0 ? name : name[..b], BindingFlags.Public | BindingFlags.Instance);
        if (f == null) return false;
        if (b < 0) { if (f.FieldType != typeof(double)) return false; f.SetValue(this, value); return true; }
        if (f.GetValue(this) is not double[] a || !int.TryParse(name[(b + 1)..^1], out int i) || i < 0 || i >= a.Length) return false;
        a[i] = value; return true;
    }
}
