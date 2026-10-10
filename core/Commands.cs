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
    Move,            // Target is put at (X, Y) with velocity (Vx, Vy); what it holds in orbit goes along, unless Index is 1 (alone)
    Force,           // the god's hand: every object within radius Vx of (X, Y) gets velocity toward that point, Amount at
                     // the centre fading to 0 at the edge; negative Amount = away. Mass does not matter
    FastForward,     // every object rides its present orbit for Amount years (closed formula: no pull between siblings, no collisions)
    SetStarState,    // replace Target's numeric stellar snapshot; future evolution starts at the current world year
}

public record struct Command(CmdKind Kind, int Target = -1, double X = 0, double Y = 0, double Vx = 0, double Vy = 0,
    double Amount = 0, int Index = 0, double[]? Mix = null, string? Name = null, uint Col = 0, StellarState? StarState = null);

public sealed partial class World
{
    public readonly List<(long Step, Command Cmd)> Journal = new();

    /// Applies the command now (call between Advance calls) and records it. Returns the new slot for the two
    /// Create kinds, the target for the others, -1 when the command could not be applied (and is then not recorded).
    public int Do(in Command c)
    {
        int r = Apply(c);
        if (r >= 0 || r == -2) { Journal.Add((Step, c)); return r; }
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
                return c.Mix != null ? Add(c.X, c.Y, c.Vx, c.Vy, c.Amount, c.Mix, c.Name, c.Col) : -1;
            case CmdKind.CreateOrbiting:
                if (!Ok(c.Target) || c.Mix == null || c.Amount <= 0) return -1;
                if (c.X == X[c.Target] && c.Y == Y[c.Target]) return -1;
                return AddOrbiting(c.Target, c.X, c.Y, c.Amount, c.Mix, c.Name, c.Col);
            case CmdKind.AddMatter:
            {
                int elem = c.Name == null ? c.Index : Elem(c.Name);
                if (!Ok(c.Target) || elem < 0 || elem >= ElementCount) return -1;
                int i = c.Target, o = i * ElementCount;
                Comp[o + elem] = Math.Max(0, Comp[o + elem] + c.Amount);
                double m = 0;
                for (int e = 0; e < ElementCount; e++) m += Comp[o + e];
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
                if (c.Name == "RocheRhythmYears")
                {
                    _rocheRule.RhythmYears = C.RocheRhythmYears;
                    _rocheRule.NextYear = Math.Min(_rocheRule.NextYear, Year + C.RocheRhythmYears);
                }
                RecalcRadii();
                for (int i = 0; i < N; i++)
                {
                    if (Alive[i] && (!double.IsFinite(R[i]) || R[i] <= 0 || !double.IsFinite(M[i]) || M[i] <= 0))
                        return -1;
                }
                return -2; // applied, no object involved
            case CmdKind.SetRule:
            {
                string? id = c.Name; Rule? rule = Rules.Find(x => x.Id == id);
                if (rule == null) return -1;
                bool enable = c.Amount != 0;
                if (enable && !rule.Enabled)
                {
                    rule.LastYear = Year;
                    rule.NextYear = (Math.Floor(Year / rule.RhythmYears) + 1) * rule.RhythmYears;
                    if (ReferenceEquals(rule, _starRule)) rule.NextYear = Math.Min(rule.NextYear, NextStarBoundary());
                }
                rule.Enabled = enable;
                return -2;
            }
            case CmdKind.SeedLife:
                if (!Ok(c.Target) || !IsWorld(c.Target) || !(c.Amount >= 0 && c.Amount <= 1)) return -1;
                Life[c.Target] = c.Amount; Touched[c.Target] = Year;
                if (c.Amount < C.CivLifeMin) RichYears[c.Target] = 0;
                return c.Target;
            case CmdKind.SetStarState:
                return Ok(c.Target) && c.StarState is StellarState state && SetStellarState(c.Target, state) ? c.Target : -1;
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
                double mx = c.X - X[t], my = c.Y - Y[t], mvx = c.Vx - Vx[t], mvy = c.Vy - Vy[t], zone = Attracts(t) && c.Index != 1 ? Hill(t) : 0;
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
                if (c.Index == 1)
                {
                    // The hand as a displacement, not a kick: it outranks every force in the world, so what it moves
                    // is moved now, paused or not. Amount is the share of the way covered by this call: +f gathers
                    // toward the centre, -f clears out to the edge of the ring. Velocities are left alone.
                    double f = Math.Abs(c.Amount);
                    if (f > 1) return -1;
                    // Small matter does not collide with small matter in Advance (too many pairs), so a gathered
                    // handful would only overlap and drift apart again. Pressed into the grip at the centre it
                    // sticks: every small object there joins the heaviest object there by the ordinary Merge, which
                    // sums mass, matter and momentum. Past AttractMass the lump is a world and contact takes over.
                    double grip = rad * HandGrip; int seed = -1;
                    for (int i = 0; i < N; i++)
                    {
                        if (!Alive[i]) continue;
                        double dx = X[i] - c.X, dy = Y[i] - c.Y, d2 = dx * dx + dy * dy;
                        if (d2 >= rad * rad) continue;
                        double d = Math.Sqrt(d2), to = c.Amount > 0 ? d * (1 - f) : d + (rad - d) * f;
                        if (d > 0) { X[i] = c.X + dx / d * to; Y[i] = c.Y + dy / d * to; } // dead on the centre: no side to clear it to
                        if (c.Amount > 0 && to <= grip && !IsShip(i) && (seed < 0 || M[i] > M[seed])) seed = i;
                    }
                    for (int i = 0; seed >= 0 && i < N; i++)
                    {
                        if (!Alive[i] || i == seed || Attracts(i) || IsShip(i)) continue;
                        double dx = X[i] - c.X, dy = Y[i] - c.Y;
                        if (dx * dx + dy * dy <= grip * grip) Merge(seed, i);
                    }
                    return -2;
                }
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
        Gone(d, -1);
    }

    // Object d is no more: whatever pointed at it points at `heir` (what swallowed it) or at nothing.
    // Without this a later Add reusing the slot would inherit moons, ships on their way and a people's home.
    void Gone(int d, int heir)
    {
        for (int i = 0; i < N; i++)
        {
            if (Par[i] == d) Par[i] = heir;
            if (ShipCiv[i] < 0) continue;
            if (ShipTo[i] == d) ShipTo[i] = heir;
            if (ShipFrom[i] == d) ShipFrom[i] = heir;
        }
        for (int k = 0; k < Civs.Count; k++) if (Civs[k].Home == d) Civs[k] = Civs[k] with { Home = -1 };
        DiscsGone(d, heir);
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

    /// False when there is no such constant, value is not finite, or fails physical range checks.
    public bool Set(string name, double value)
    {
        if (!double.IsFinite(value)) return false;
        int b = name.IndexOf('[');
        string field = b < 0 ? name : name[..b];
        FieldInfo? f = typeof(Consts).GetField(field, BindingFlags.Public | BindingFlags.Instance);
        if (f == null) return false;
        if (b < 0)
        {
            if (f.FieldType != typeof(double)) return false;
            if (!Validate(field, value)) return false;
            f.SetValue(this, value);
            return true;
        }
        if (f.GetValue(this) is not double[] a || !int.TryParse(name[(b + 1)..^1], out int i) || i < 0 || i >= a.Length) return false;
        if (!ValidateArray(field, i, value)) return false;
        a[i] = value;
        return true;
    }

    bool Validate(string name, double v)
    {
        if (v < 0) return false;
        if (name is "RochePhysicalG" or "RocheSolarMassKg" or "RocheSolarRadiusKm" or "RocheDensityKgM3" or "RocheRhythmYears") return v > 0;
        if (name is "RocheEnergySpread" or "RocheStreamWidth") return v <= 1;
        if (name is "RocheFragments" or "RocheRockBudget" or "RocheChangesPerStep") return v == Math.Floor(v) && v <= int.MaxValue && (name != "RocheChangesPerStep" || v > 0);
        if (name == "HypernovaChance") return v <= 1;
        if (name == "GrbHalfAngleDeg") return v > 0 && v <= 90;
        if (name is "GrbFluenceJm2" or "NeutronPhysicalRadiusKm" or "NeutronTovSolar") return v > 0;
        if (name.StartsWith("Star"))
        {
            if (name == "StarWhiteSlope") return v >= 0 && (v > 0 || StarWhiteIntercept > 0);
            if (name == "StarWhiteIntercept") return v > 0;
            return v > 0;
        }
        switch (name)
        {
            case "YearTime":
            case "RadiusScale":
                return v > 0;

            case "AttractMass":
                return v > 0 && v > ShipMass;

            case "ShipMass":
                return v > 0 && v < AttractMass;

            case "JumpSamples":
                return v >= 1 && v <= 10_000;

            case "CivLifespanRef":
                return v > 0 && double.IsFinite(v);

            case "CivLifespanExp":
                return double.IsFinite(v) && v >= 0;

            default:
                return true;
        }
    }

    bool ValidateArray(string name, int idx, double v)
    {
        if (name == "Density") return v > 0;
        return v >= 0;
    }
}
