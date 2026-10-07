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
    public double CivDomeTemp = 288;     // K: where a dome is cheapest to keep [P]
    public double CivDomeTempRange = 250; // K away from that at which no dome holds; in between its room shrinks in line [P]

    public double ShipPop = 0.03;        // a space-age world with at least this population sends ships
    public double ShipsPerWorld = 2;     // ships one world keeps in flight at a time
    public double ShipSpeed = 0.5;       // closing speed on its goal (Earth goes round the Sun at about 1)
    public double ShipThrust = 2;        // velocity change per unit of time its engine can make
    public double ShipLifeYears = 30;    // a ship that has not landed by then is lost
    public double ShipMass = 1e-12;
    public double CivLifespanRef = 80.0;
    public double CivLifespanExp = 0.5;
}

/// Home = the world it rose on, -1 once that world is gone.
public readonly record struct CivInfo(string Name, int Home, double BornYear, Dictionary<string, double>? Stats = null, Species? Species = null)
{
    public Dictionary<string, double> Stats { get; init; } = Stats ?? new();
    public Species Species { get; init; } = Species ?? SpeciesCatalog.Human;
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
    double[] _launchYear = null!;   // year a world reached a ship-launching stage inside the stretch the civ rule just ran (NaN = not in that run); for dating only, not state

    const double ShipRhythmYears = 2; // [P] how often a world may send one
    static readonly string[] NameParts = { "ka", "ren", "tho", "lu", "mi", "sa", "vor", "el", "an", "dra", "qui", "zen", "ta", "no", "ri", "bel", "os", "ya", "ur", "she" };

    void InitCiv(int capacity)
    {
        Civ = new int[capacity]; ShipCiv = new int[capacity]; ShipTo = new int[capacity]; ShipFrom = new int[capacity];
        ShipTech = new double[capacity]; ShipBorn = new double[capacity]; _launchYear = new double[capacity];
        Array.Fill(_launchYear, double.NaN);
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

    void CivEvent(int slot, int civ, string change, double a = 0, double b = 0, double c = 0, double year = double.NaN)
    {
        LogEvent(slot, "civ", change, a, b, c, year);
        if (Chronicle.Count == ChronicleCapacity) Chronicle.RemoveAt(0);
        Chronicle.Add((civ, new RuleEvent(double.IsNaN(year) ? Year : year, slot, "civ", change, a, b, c)));
    }

    // a people that rose here by itself: gets a name of its own
    int NewCiv(int home, Species? species = null)
    {
        int parts = 2 + (int)(Next() * 2);
        string name = "";
        for (int k = 0; k < parts; k++) name += NameParts[(int)(Next() * NameParts.Length)];
        Civs.Add(new CivInfo(char.ToUpperInvariant(name[0]) + name[1..], home, Year, Species: species ?? SpeciesCatalog.Human));
        _civLaunches.Add(0);
        return Civs.Count - 1;
    }

    // What the population can live on: the biosphere when there is enough of one; in the space age domes otherwise.
    double CivRoom(int i) => Life[i] >= C.CivLifeMin / 5 ? Life[i]
        : TechStage(i) < Stages.Count && Stages[TechStage(i)].CanDome ? DomeRoom(i, Civ[i]) : 0;

    /// What a dome on world i can hold for this people. Three things decide it [P]:
    ///   heat: full room at CivDomeTemp, none CivDomeTempRange away from it (Mercury's day side, a moon of Neptune);
    ///   water: ice of its own (WaterIceMin) lets it stand alone;
    ///   supply: without ice it lives on what ships bring, so only while the people still has a world whose
    ///           biosphere feeds it. The home world gone = the dry colonies starve.
    public double DomeRoom(int i, int civ)
    {
        if (C.CivDome <= 0 || double.IsNaN(Temp[i]) || !IsSolid(i)) return 0;
        double heat = C.CivDomeTempRange > 0 ? 1 - Math.Abs(Temp[i] - C.CivDomeTemp) / C.CivDomeTempRange : 1;
        if (!(heat > 0)) return 0;
        double scale = 1.0;
        if (Share(i, ElementRole.Ice) < C.WaterIceMin)
        {
            if ((uint)civ >= (uint)Civs.Count) return 0;
            int home = Civs[civ].Home;
            if (home < 0 || !Alive[home] || Civ[home] != civ || Pop[home] <= 0 || !Fed(civ)) return 0;
            // Dry dome depends on supply from home world; capacity scales with home world population
            scale = Math.Clamp(Pop[home], 0, 1);
        }
        return C.CivDome * Math.Min(1, heat) * scale;
    }

    // does this people have a world that feeds itself? Looked up at the head of each run of the civ and ship rules
    readonly List<bool> _fed = new();
    bool Fed(int civ) => (uint)civ < (uint)_fed.Count && _fed[civ];
    void FindFed()
    {
        _fed.Clear(); for (int c = 0; c < Civs.Count; c++) _fed.Add(false);
        for (int j = 0; j < N; j++)
            if (Alive[j] && Pop[j] > 0 && (uint)Civ[j] < (uint)_fed.Count && Life[j] >= C.CivLifeMin / 5) _fed[Civ[j]] = true;
    }

    void ConsumeResources(int i, double lived, int stage)
    {
        if (stage < 0 || stage >= Stages.Count || !(lived > 0)) return;
        var needs = Stages[stage].Needs;
        int rock = Elem(ElementRole.Rock);
        if (rock < 0 || needs == null) return;
        int o = i * ElementCount;
        double totalUsed = 0;
        bool massChanged = false;

        for (int k = 0; k < needs.Count; k++)
        {
            var need = needs[k];
            if (!(need.ConsumeRate > 0) || need.Element == ElementRole.None) continue;
            double avail = Matter(i, need.Element);
            double want = need.ConsumeRate * lived * M[i];
            double used = Math.Min(avail, want);
            if (used > 0)
            {
                // Physical element transformation driven by BecomesWaste flag:
                // Only elements flagged with BecomesWaste turn into waste rock (slag, fission products).
                if (need.BecomesWaste)
                {
                    LoseMatter(i, need.Element, used);
                    Comp[o + rock] += used;
                    massChanged = true;
                }
                totalUsed += used;
            }
        }

        if (totalUsed > 0)
        {
            if (massChanged) SetRadius(i);
            ConsumedMatter[i] += totalUsed;
        }
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
            if (Life[j] < C.CivLifeMin / 5 && !(DomeRoom(j, civ) > 0)) continue; // nothing to live on there
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
        FindFed();
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
            // in a jump the first ship leaves when its world reached the space age, not at the end of the chunk.
            // That year is kept across runs and can be older than this run's window, so it is not weighed against
            // RuleYears: it stays valid until a ship actually leaves, and Civ.cs clears it only when the run gave
            // a world no new stage.
            double leftYear = direct && !double.IsNaN(_launchYear[i]) && _launchYear[i] <= Year ? _launchYear[i] : Year;
            void sent() { while (_civLaunches.Count < Civs.Count) _civLaunches.Add(0); if (_civLaunches[civ]++ == 0) CivEvent(i, civ, "civ.ship.first", civ, goal, Tech[i], leftYear); }
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

    public static bool IsHuman(Species? s)
    {
        if (s == null) return true;
        if (ReferenceEquals(s, SpeciesCatalog.Human)) return true;
        if (s.Id != SpeciesCatalog.Human.Id || s.NameVi != SpeciesCatalog.Human.NameVi) return false;
        if (s.Habitat != SpeciesCatalog.Human.Habitat) return false;
        if (BitConverter.DoubleToUInt64Bits(s.Manipulation) != BitConverter.DoubleToUInt64Bits(SpeciesCatalog.Human.Manipulation)) return false;
        if (s.EnergyBasis != SpeciesCatalog.Human.EnergyBasis) return false;
        if (s.Senses != SpeciesCatalog.Human.Senses) return false;
        if (BitConverter.DoubleToUInt64Bits(s.Lifespan) != BitConverter.DoubleToUInt64Bits(SpeciesCatalog.Human.Lifespan)) return false;
        if (s.Social != SpeciesCatalog.Human.Social) return false;
        if (BitConverter.DoubleToUInt64Bits(s.TempMin) != BitConverter.DoubleToUInt64Bits(SpeciesCatalog.Human.TempMin)) return false;
        if (BitConverter.DoubleToUInt64Bits(s.TempMax) != BitConverter.DoubleToUInt64Bits(SpeciesCatalog.Human.TempMax)) return false;
        if (s.Extra != null && s.Extra.Count > 0) return false;
        return true;
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
            if (Civs[c].Species is { } sp && !IsHuman(sp))
            {
                mix(1UL);
                mix((ulong)sp.Id.Length);
                foreach (char ch in sp.Id) mix(ch);
                mix((ulong)sp.NameVi.Length);
                foreach (char ch in sp.NameVi) mix(ch);
                mix((ulong)sp.Habitat);
                number(sp.Manipulation);
                mix((ulong)sp.EnergyBasis);
                mix((ulong)sp.Senses);
                number(sp.Lifespan);
                mix((ulong)sp.Social);
                number(sp.TempMin);
                number(sp.TempMax);
                if (sp.Extra != null && sp.Extra.Count > 0)
                {
                    mix((ulong)sp.Extra.Count);
                    foreach (var kv in sp.Extra.OrderBy(k => k.Key, StringComparer.Ordinal))
                    {
                        foreach (char ch in kv.Key) mix(ch);
                        if (kv.Value is double dv) number(dv);
                        else if (kv.Value is bool bv) mix(bv ? 1UL : 0UL);
                        else if (kv.Value is int iv) mix((ulong)iv);
                        else foreach (char ch in kv.Value?.ToString() ?? "") mix(ch);
                    }
                }
            }
        }
    }
}
