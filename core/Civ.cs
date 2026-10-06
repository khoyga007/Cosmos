// A civilisation that does things (SPEC 2c). Until now it was two numbers on one planet; here it gets
//   a name and a home, so its story can be told;
//   an appetite: from industry on it turns the planet's metal into waste rock;
//   ships: from the space age on it sends ships to the other solid worlds. A ship is an object like any other
//   (the god can push it, pull it, move it, remove it) with an engine: it steers toward its goal instead of
//   coasting. Where it lands, the people stay: a colony, living under domes where there is no biosphere.
// In a fast-forward there are no ship objects: a trip takes a few years, a chunk of a jump thousands, so the
// rule founds the colony directly. Ships in flight when a jump starts land first.
// Every number is a PLACEHOLDER [P] by Claire (Yang 06/10: build direction 2 + 1), all Consts.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Cosmos.Core;

public sealed partial class Consts
{
    public double CivDome = 0.05;        // population a space-age people can keep where no biosphere feeds them (domes)
    public double CivMetalUse = 5e-8;    // share of the planet's mass turned from metal into rock per year, at full population, from industry on

    public double ShipPop = 0.03;        // a space-age world with at least this population sends ships
    public double ShipsPerWorld = 2;     // ships one world keeps in flight at a time
    public double ShipSpeed = 0.5;       // closing speed on its goal (Earth goes round the Sun at about 1)
    public double ShipThrust = 2;        // velocity change per unit of time its engine can make
    public double ShipLifeYears = 30;    // a ship that has not landed by then is lost
    public double ShipMass = 1e-12;
}

/// Home = the world it rose on, -1 once that world is gone.
public readonly record struct CivInfo(string Name, int Home, double BornYear, Dictionary<string, double>? Stats = null)
{
    public Dictionary<string, double> Stats { get; init; } = Stats ?? new();
}

public sealed partial class World
{
    public readonly List<CivInfo> Civs = new();
    readonly List<int> _civLaunches = new(); // ships sent per civilisation, to tell the first one
    /// What happened to each civilisation, oldest first: the same events as in Events, kept apart so that the
    /// thousands of temperature lines do not push a people's story out. A story, not state: not in the hash.
    public readonly List<(int Civ, RuleEvent Event)> Chronicle = new();
    const int ChronicleCapacity = 4096;
    public int[] Civ = null!;       // civilisation living on this world, or the last one that did; -1 = never any
    public int[] ShipCiv = null!;   // >= 0: this object is a ship of that civilisation
    public int[] ShipTo = null!, ShipFrom = null!;
    public double[] ShipTech = null!, ShipBorn = null!;

    const double ShipRhythmYears = 2; // [P] how often a world may send one
    static readonly string[] NameParts = { "ka", "ren", "tho", "lu", "mi", "sa", "vor", "el", "an", "dra", "qui", "zen", "ta", "no", "ri", "bel", "os", "ya", "ur", "she" };

    void InitCiv(int capacity)
    {
        Civ = new int[capacity]; ShipCiv = new int[capacity]; ShipTo = new int[capacity]; ShipFrom = new int[capacity];
        ShipTech = new double[capacity]; ShipBorn = new double[capacity];
        Array.Fill(Civ, -1); Array.Fill(ShipCiv, -1);
    }

    void ResetCiv(int i) { Civ[i] = ShipCiv[i] = ShipTo[i] = ShipFrom[i] = -1; ShipTech[i] = ShipBorn[i] = 0; }

    public bool IsShip(int i) => ShipCiv[i] >= 0;

    /// Has a surface to stand on: ships land only here.
    public bool IsSolid(int i) => IsWorld(i) && Share(i, ElementRole.Rock) + Share(i, ElementRole.Metal) >= C.LifeSolidMin;

    /// Worlds a civilisation lives on right now.
    public int WorldsOf(int civ)
    {
        int n = 0;
        for (int i = 0; i < N; i++) if (Alive[i] && Civ[i] == civ && Pop[i] > 0) n++;
        return n;
    }

    void CivEvent(int slot, int civ, string change, double a = 0, double b = 0, double c = 0)
    {
        LogEvent(slot, "civ", change, a, b, c);
        if (Chronicle.Count == ChronicleCapacity) Chronicle.RemoveAt(0);
        Chronicle.Add((civ, new RuleEvent(Year, slot, "civ", change, a, b, c)));
    }

    // a people that rose here by itself: gets a name of its own
    int NewCiv(int home)
    {
        int parts = 2 + (int)(Next() * 2);
        string name = "";
        for (int k = 0; k < parts; k++) name += NameParts[(int)(Next() * NameParts.Length)];
        Civs.Add(new CivInfo(char.ToUpperInvariant(name[0]) + name[1..], home, Year));
        _civLaunches.Add(0);
        return Civs.Count - 1;
    }

    // What the population can live on: the biosphere when there is enough of one; in the space age domes otherwise.
    double CivRoom(int i) => Life[i] >= C.CivLifeMin / 5 ? Life[i] : (TechStage(i) < Stages.Count && Stages[TechStage(i)].CanDome ? C.CivDome : 0);

    // Industry eats metal: `lived` = population summed over the stretch (people * years). The mass stays (waste rock).
    void UseMetal(int i, double lived)
    {
        int o = i * ElementCount, rock = Elem(ElementRole.Rock);
        if (rock < 0) return;
        double used = Math.Min(Matter(i, ElementRole.Metal), C.CivMetalUse * lived * M[i]);
        if (!(used > 0)) return;
        LoseMatter(i, ElementRole.Metal, used); Comp[o + rock] += used;
        SetRadius(i);
    }

    // The people of a ship step out on world k.
    void Land(int k, int civ, double tech)
    {
        if (!IsSolid(k)) { CivEvent(k, civ, "civ.ship.lost", civ, k, tech); return; } // flew into a star or a gas world
        if (Pop[k] <= 0)
        {
            Pop[k] = C.CivSeed; Tech[k] = tech; Civ[k] = civ; Touched[k] = Year;
            CivEvent(k, civ, "civ.colony", civ, tech, Life[k]);
        }
        else if (Civ[k] == civ) Tech[k] = Math.Max(Tech[k], tech); // supply: what home has learned
        else CivEvent(k, civ, "civ.ship.turned", civ, Civ[k], tech); // another people got here first: nobody steps out
    }

    // a ship touched a pulling object (called from Merge): it lands or is lost; its mass is not worth adding
    void ShipArrives(int ship, int at)
    {
        Land(at, ShipCiv[ship], ShipTech[ship]);
        Kill(ship);
    }

    void LandShips()
    {
        for (int i = 0; i < N; i++)
        {
            if (!Alive[i] || !IsShip(i)) continue;
            if (Ok(ShipTo[i])) Land(ShipTo[i], ShipCiv[i], ShipTech[i]);
            else CivEvent(Ok(ShipFrom[i]) ? ShipFrom[i] : i, ShipCiv[i], "civ.ship.lost", ShipCiv[i], ShipTo[i], ShipTech[i]);
            Kill(i);
        }
    }

    // nearest solid world nobody lives on; when there is none (and ships fly), one of its own worlds, for supply
    int ShipGoal(int from, int civ, bool supply)
    {
        int best = -1; double bestD = double.MaxValue; int own = 0;
        double reach = Math.Max(0.02, C.ShipSpeed) * C.ShipLifeYears * C.YearTime; // further than a ship flies in its life = lost for sure
        for (int j = 0; j < N; j++)
        {
            if (j == from || !Alive[j] || !IsSolid(j)) continue;
            if (Pop[j] > 0) { if (Civ[j] == civ) own++; continue; }
            if (C.CivDome <= 0 && Life[j] < C.CivLifeMin / 5) continue; // nothing to live on there
            double dx = X[j] - X[from], dy = Y[j] - Y[from], d = dx * dx + dy * dy;
            if (d < bestD && d < reach * reach) { bestD = d; best = j; }
        }
        if (best >= 0 || !supply || own == 0) return best;
        int pick = (int)(Next() * own);
        for (int j = 0; j < N; j++)
            if (j != from && Alive[j] && IsSolid(j) && Pop[j] > 0 && Civ[j] == civ && pick-- == 0) return j;
        return -1;
    }

    void UpdateShips()
    {
        bool direct = _riding || RuleYears > 4 * ShipRhythmYears; // years went by in one go: no time to watch a ship fly
        if (!direct)
            for (int i = 0; i < N; i++)
                if (Alive[i] && IsShip(i) && (!Ok(ShipTo[i]) || Year - ShipBorn[i] > C.ShipLifeYears))
                {
                    CivEvent(Ok(ShipFrom[i]) ? ShipFrom[i] : i, ShipCiv[i], "civ.ship.lost", ShipCiv[i], ShipTo[i], ShipTech[i]);
                    Kill(i);
                }

        int n = N; // ships made below are not worlds; no need to look at them
        for (int i = 0; i < n; i++)
        {
            if (!Alive[i] || !IsWorld(i) || (uint)Civ[i] >= (uint)Civs.Count || Pop[i] < C.ShipPop || TechStage(i) >= Stages.Count || !Stages[TechStage(i)].CanLaunchShips) continue;
            int civ = Civ[i];
            if (!direct)
            {
                int flying = 0;
                for (int s = 0; s < N; s++) if (Alive[s] && ShipFrom[s] == i && IsShip(s)) flying++;
                if (flying >= C.ShipsPerWorld) continue;
            }
            int goal = ShipGoal(i, civ, supply: !direct);
            if (goal < 0) continue;
            void sent() { while (_civLaunches.Count < Civs.Count) _civLaunches.Add(0); if (_civLaunches[civ]++ == 0) CivEvent(i, civ, "civ.ship.first", civ, goal, Tech[i]); }
            if (direct) { sent(); Land(goal, civ, Tech[i]); continue; }

            double dx = X[goal] - X[i], dy = Y[goal] - Y[i], d = Math.Sqrt(dx * dx + dy * dy);
            if (!(d > 0)) continue;
            // it leaves already moving the way its engine will hold it (goal's velocity + closing speed), from the
            // side of the planet that this velocity points away from, so that it does not fall back on its own home
            double close = Math.Min(C.ShipSpeed, Math.Sqrt(0.5 * C.ShipThrust * d));
            double vx = Vx[goal] + dx / d * close, vy = Vy[goal] + dy / d * close;
            double ux = vx - Vx[i], uy = vy - Vy[i], u = Math.Sqrt(ux * ux + uy * uy);
            if (!(u > 0)) { ux = dx; uy = dy; u = d; }
            double off = R[i] * 1.5 + 1e-4;
            int metal = Elem(ElementRole.Metal);
            if (metal < 0) continue;
            var shipMix = new double[ElementCount]; shipMix[metal] = 1;
            int ship = Add(X[i] + ux / u * off, Y[i] + uy / u * off, vx, vy, C.ShipMass, shipMix, $"Tàu {Civs[civ].Name}");
            if (ship < 0) continue; // the world is full
            sent();
            ShipCiv[ship] = civ; ShipTo[ship] = goal; ShipFrom[ship] = i; ShipTech[ship] = Tech[i]; ShipBorn[ship] = Year;
        }
    }

    // Engines, once per Advance: a ship wants the velocity of its goal plus a closing speed that drops as it gets
    // near, and turns toward that as fast as its thrust allows. Gravity and the god's hand still act on it; it
    // corrects. Seen from the goal the ship comes straight in; seen from the star that is a curve, because the
    // goal is going round. A star too close overrides all that: the ship holds off beside it, sliding toward the
    // goal's side, until the goal comes round.
    void SteerShips(double h)
    {
        int stars = -1;
        for (int i = 0; i < N; i++)
        {
            if (!Alive[i] || ShipCiv[i] < 0) continue;
            int t = ShipTo[i];
            if (!Ok(t))
            {
                CivEvent(Ok(ShipFrom[i]) ? ShipFrom[i] : i, ShipCiv[i], "civ.ship.lost", ShipCiv[i], t, ShipTech[i]);
                Kill(i);
                continue;
            }
            if (stars < 0) { stars = 0; for (int s = 0; s < N; s++) if (Alive[s] && M[s] >= C.StarMass) _starSlots[stars++] = s; }
            double dx = X[t] - X[i], dy = Y[t] - Y[i], d = Math.Sqrt(dx * dx + dy * dy);
            if (!(d > 0)) continue;
            dx /= d; dy /= d;
            double closing = Math.Clamp(Math.Sqrt(0.5 * C.ShipThrust * d), 0.02, Math.Max(0.02, C.ShipSpeed));
            double wx = Vx[t] + dx * closing, wy = Vy[t] + dy * closing;
            for (int k = 0; k < stars; k++)
            {
                int s = _starSlots[k];
                if (s == t) continue;
                double ox = X[i] - X[s], oy = Y[i] - Y[s], o = Math.Sqrt(ox * ox + oy * oy), keep = R[s] * 1.5;
                if (o >= keep || !(o > 0)) continue;
                ox /= o; oy /= o;
                double side = dx * -oy + dy * ox >= 0 ? 1 : -1; // which way round the star the goal lies
                double v = Math.Max(0.02, C.ShipSpeed);
                wx = Vx[s] + (ox + side * -oy) * v; wy = Vy[s] + (oy + side * ox) * v;
            }
            wx -= Vx[i]; wy -= Vy[i];
            double want = Math.Sqrt(wx * wx + wy * wy), can = C.ShipThrust * h;
            if (want > can) { wx *= can / want; wy *= can / want; }
            Vx[i] += wx; Vy[i] += wy;
        }
    }

    void HashCiv(Action<ulong> mix)
    {
        void number(double n) => mix(BitConverter.DoubleToUInt64Bits(n));
        for (int i = 0; i < N; i++)
        {
            if (!Alive[i]) continue;
            mix((ulong)Civ[i]); mix((ulong)ShipCiv[i]);
            if (ShipCiv[i] >= 0) { mix((ulong)ShipTo[i]); mix((ulong)ShipFrom[i]); number(ShipTech[i]); number(ShipBorn[i]); }
        }
        mix((ulong)Civs.Count);
        for (int c = 0; c < Civs.Count; c++)
        {
            foreach (char ch in Civs[c].Name) mix(ch);
            mix((ulong)Civs[c].Home); number(Civs[c].BornYear); mix((ulong)(c < _civLaunches.Count ? _civLaunches[c] : 0));
            if (Civs[c].Stats != null && Civs[c].Stats.Count > 0)
            {
                mix((ulong)Civs[c].Stats.Count);
                foreach (var kv in Civs[c].Stats.OrderBy(k => k.Key, StringComparer.Ordinal))
                {
                    foreach (char ch in kv.Key) mix(ch);
                    number(kv.Value);
                }
            }
        }
    }
}
