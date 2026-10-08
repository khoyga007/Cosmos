using System;
using System.Collections.Generic;
using Cosmos.Core;

namespace Cosmos.Game;

/// <summary>
/// Immutable-per-frame snapshot of simulation and UI state copied at sync points.
/// Main thread reads exclusively from this for rendering, picking, panels, and GodUi
/// while the simulation worker task owns and runs World concurrently.
/// </summary>
public sealed class RenderSnapshot
{
    public int N;
    public int Live;
    public long Merges;
    public double Year;
    public long Step;

    public double[] X = Array.Empty<double>();
    public double[] Y = Array.Empty<double>();
    public double[] Vx = Array.Empty<double>();
    public double[] Vy = Array.Empty<double>();
    public double[] M = Array.Empty<double>();
    public double[] R = Array.Empty<double>();
    public bool[] Alive = Array.Empty<bool>();
    public int[] Gen = Array.Empty<int>();
    public int[] Par = Array.Empty<int>();
    public int[] Grp = Array.Empty<int>();
    public uint[] Col = Array.Empty<uint>();
    public double[] Comp = Array.Empty<double>();
    public string?[] Name = Array.Empty<string?>();

    public bool[] Attracts = Array.Empty<bool>();
    public bool[] IsShip = Array.Empty<bool>();
    public int[] ShipTo = Array.Empty<int>();
    public int[] ShipFrom = Array.Empty<int>();
    public int[] ShipCiv = Array.Empty<int>();
    public double[] ShipBorn = Array.Empty<double>();
    public StarPhase[] StarPhase = Array.Empty<StarPhase>();
    public uint[] StarColour = Array.Empty<uint>();
    public double[] Hill = Array.Empty<double>();
    public Kind[] Kind = Array.Empty<Kind>();

    public double[] Life = Array.Empty<double>();
    public double[] Pop = Array.Empty<double>();
    public double[] Water = Array.Empty<double>();
    public double[] WaterYears = Array.Empty<double>();
    public double[] Temp = Array.Empty<double>();
    public int[] Civ = Array.Empty<int>();
    public double[] Tech = Array.Empty<double>();
    public string?[] CivTag = Array.Empty<string?>();

    public double G;
    public double RadiusScale;
    public double AttractMass;
    public double StarMass;
    public double StarSolarMass;
    public double StarGiantFraction;
    public readonly double[] Density = new double[World.NElem];

    // UI collections published at sync
    public readonly List<RuleEvent> Events = new();
    public readonly List<(string Id, string NameVi, bool Enabled, double RhythmYears)> Rules = new();
    public readonly Dictionary<string, double> Consts = new();
    public readonly List<CivInfo> Civs = new();
    public readonly List<(int Civ, RuleEvent Event)> Chronicle = new();
    public readonly List<string> Groups = new();

    public void EnsureCapacity(int capacity)
    {
        if (X.Length >= capacity) return;
        int cap = capacity;
        X = new double[cap]; Y = new double[cap];
        Vx = new double[cap]; Vy = new double[cap];
        M = new double[cap]; R = new double[cap];
        Alive = new bool[cap]; Gen = new int[cap];
        Par = new int[cap]; Grp = new int[cap];
        Col = new uint[cap]; Name = new string?[cap];
        Comp = new double[cap * World.NElem];
        Attracts = new bool[cap]; IsShip = new bool[cap];
        ShipTo = new int[cap]; ShipFrom = new int[cap];
        ShipCiv = new int[cap]; ShipBorn = new double[cap];
        StarPhase = new StarPhase[cap]; StarColour = new uint[cap];
        Hill = new double[cap]; Kind = new Kind[cap];
        Life = new double[cap]; Pop = new double[cap];
        Water = new double[cap]; WaterYears = new double[cap];
        Temp = new double[cap]; Civ = new int[cap]; Tech = new double[cap];
        CivTag = new string?[cap];
        TechStage = new int[cap];
        PeopleCount = new double[cap];
        PowerWatts = new double[cap];
        KardashevScale = new double[cap];
        CivFootprint = new double[cap];
        StarTemp = new double[cap];
        StarLuminosity = new double[cap];
        StarAge = new double[cap];
        StarFuel = new double[cap];
        StarCoolingAge = new double[cap];
        StarLifetime = new double[cap];
    }

    public int[] TechStage = Array.Empty<int>();
    public double[] PeopleCount = Array.Empty<double>();
    public double[] PowerWatts = Array.Empty<double>();
    public double[] KardashevScale = Array.Empty<double>();
    public double[] CivFootprint = Array.Empty<double>();
    public int[] CivWorldsCount = Array.Empty<int>();
    public double[] StarTemp = Array.Empty<double>();
    public double[] StarLuminosity = Array.Empty<double>();
    public double[] StarAge = Array.Empty<double>();
    public double[] StarFuel = Array.Empty<double>();
    public double[] StarCoolingAge = Array.Empty<double>();
    public double[] StarLifetime = Array.Empty<double>();
    public int ElemRock;

    public void CopyFrom(World w)
    {
        N = w.N;
        Live = w.Live;
        Merges = w.Merges;
        Year = w.Year;
        Step = w.Step;
        EnsureCapacity(w.X.Length);

        G = w.C.G;
        RadiusScale = w.C.RadiusScale;
        AttractMass = w.C.AttractMass;
        StarMass = w.C.StarMass;
        StarSolarMass = w.C.StarSolarMass;
        StarGiantFraction = w.C.StarGiantFraction;
        Array.Copy(w.C.Density, Density, World.NElem);

        Array.Copy(w.X, X, N);
        Array.Copy(w.Y, Y, N);
        Array.Copy(w.Vx, Vx, N);
        Array.Copy(w.Vy, Vy, N);
        Array.Copy(w.M, M, N);
        Array.Copy(w.R, R, N);
        Array.Copy(w.Alive, Alive, N);
        Array.Copy(w.Gen, Gen, N);
        Array.Copy(w.Par, Par, N);
        Array.Copy(w.Grp, Grp, N);
        Array.Copy(w.Col, Col, N);
        Array.Copy(w.Comp, Comp, N * World.NElem);
        Array.Copy(w.Name, Name, N);
        Array.Copy(w.Life, Life, N);
        Array.Copy(w.Pop, Pop, N);
        Array.Copy(w.Water, Water, N);
        Array.Copy(w.WaterYears, WaterYears, N);
        Array.Copy(w.Temp, Temp, N);
        Array.Copy(w.Civ, Civ, N);
        Array.Copy(w.Tech, Tech, N);

        // Build pulling hierarchy ONCE and bulk-read Hill + Kind to eliminate O(P*N) repeated passes
        w.BulkReadPullingDerived(Hill, Kind);

        for (int i = 0; i < N; i++)
        {
            if (!Alive[i]) { CivTag[i] = null; continue; }
            bool attracts = w.Attracts(i);
            Attracts[i] = attracts;
            if (!attracts)
            {
                bool ship = w.IsShip(i);
                IsShip[i] = ship;
                if (ship)
                {
                    ShipTo[i] = w.ShipTo[i];
                    ShipFrom[i] = w.ShipFrom[i];
                    ShipCiv[i] = w.ShipCiv[i];
                    ShipBorn[i] = w.ShipBorn[i];
                }
                StarPhase[i] = Cosmos.Core.StarPhase.None;
                StarColour[i] = 0;
                CivTag[i] = null;
                continue;
            }

            IsShip[i] = false;
            StarPhase phase = w.StarPhaseOf(i);
            StarPhase[i] = phase;
            StarColour[i] = w.StarColour(i);
            if (phase != Cosmos.Core.StarPhase.None)
            {
                StarTemp[i] = w.StarSurfaceTemperature(i);
                StarLuminosity[i] = w.StarLuminosity(i);
                StarAge[i] = w.StarAge[i];
                StarFuel[i] = w.StarFuel[i];
                StarCoolingAge[i] = w.StarCoolingAge[i];
                StarLifetime[i] = w.StarLifetime(w.M[i]);
            }

            if (M[i] < StarMass && phase == Cosmos.Core.StarPhase.None)
            {
                if (Pop[i] > 0)
                {
                    int civ = Civ[i];
                    CivTag[i] = civ < 0 ? "văn minh" : (civ < w.Civs.Count && w.Civs[civ].Home == i ? w.Civs[civ].Name : $"thuộc địa {(civ < w.Civs.Count ? w.Civs[civ].Name : "")}");
                    TechStage[i] = w.TechStage(i);
                    PeopleCount[i] = w.PeopleCount(i);
                    PowerWatts[i] = w.PowerWatts(i);
                    KardashevScale[i] = w.KardashevScale(i);
                    CivFootprint[i] = w.CivFootprint(i);
                }
                else
                {
                    CivTag[i] = null;
                    TechStage[i] = 0;
                    PeopleCount[i] = 0;
                    PowerWatts[i] = 0;
                    KardashevScale[i] = 0;
                    CivFootprint[i] = 0;
                }
            }
            else CivTag[i] = null;
        }

        ElemRock = w.Elem(ElementRole.Rock);

        // Publish collections for UI
        Events.Clear();
        for (int e = 0; e < w.Events.Count; e++) Events.Add(w.Events[e]);

        Rules.Clear();
        foreach (var r in w.Rules)
            Rules.Add((r.Id, GodUi.RuleVi.TryGetValue(r.Id, out string? v) ? v : r.Id, r.Enabled, r.RhythmYears));

        Consts.Clear();
        foreach (var (k, v) in w.C.All()) Consts[k] = v;

        Civs.Clear();
        for (int c = 0; c < w.Civs.Count; c++) Civs.Add(w.Civs[c]);

        if (CivWorldsCount.Length < w.Civs.Count) CivWorldsCount = new int[w.Civs.Count];
        for (int c = 0; c < w.Civs.Count; c++) CivWorldsCount[c] = w.WorldsOf(c);

        Chronicle.Clear();
        for (int c = 0; c < w.Chronicle.Count; c++) Chronicle.Add(w.Chronicle[c]);

        Groups.Clear();
        for (int g = 0; g < w.Groups.Count; g++) Groups.Add(w.Groups[g]);
    }

    public int WorldsOf(int civ) => (civ >= 0 && civ < CivWorldsCount.Length) ? CivWorldsCount[civ] : 0;
    public string NameOf(int i) => Name[i] ?? ((i >= 0 && i < N && Grp[i] > 0 && Grp[i] < Groups.Count) ? $"{Groups[Grp[i]]} #{i}" : $"Vật thể #{i}");
    public StarPhase StarPhaseOf(int i) => (i >= 0 && i < N) ? StarPhase[i] : Cosmos.Core.StarPhase.None;
    public uint StarColourOf(int i) => (i >= 0 && i < N) ? StarColour[i] : 0;
    public Kind KindOf(int i) => (i >= 0 && i < N) ? Kind[i] : Cosmos.Core.Kind.Rock;
    public bool AttractsOf(int i) => (i >= 0 && i < N) && Attracts[i];
    public bool IsShipOf(int i) => (i >= 0 && i < N) && IsShip[i];
    public double HillOf(int i) => (i >= 0 && i < N) ? Hill[i] : 0;

    public bool IsAlive(int i) => i >= 0 && i < N && Alive[i];
    public bool IsWorld(int i) => i >= 0 && i < N && Alive[i] && M[i] >= AttractMass && M[i] < StarMass && !IsShip[i] && StarPhase[i] == Cosmos.Core.StarPhase.None;

    public int PrimaryAt(double x, double y, double mass, int skip = -1)
    {
        int best = -1;
        for (int j = 0; j < N; j++)
        {
            if (j == skip || !Alive[j] || !Attracts[j] || M[j] <= mass) continue;
            if (best >= 0 && M[j] >= M[best]) continue;
            double dx = x - X[j], dy = y - Y[j], h = Hill[j];
            if (h == double.MaxValue || dx * dx + dy * dy < h * h) best = j;
        }
        return best;
    }

    public double OrbitSpeed(int primary, double x, double y)
    {
        if (primary < 0 || primary >= N || !Alive[primary]) return 1;
        double dx = x - X[primary], dy = y - Y[primary], d = Math.Sqrt(dx * dx + dy * dy);
        return d > 0 ? Math.Sqrt(G * M[primary] / d) : 1;
    }

    public int Heaviest()
    {
        int k = -1;
        for (int i = 0; i < N; i++) if (Alive[i] && (k < 0 || M[i] > M[k])) k = i;
        return k;
    }

    public double BandOf(double tempK) => double.IsNaN(tempK) ? 0 : tempK < 273.15 ? 0 : tempK <= 373.15 ? 1 : 2;

    public int LifeStage(int i)
    {
        if (i < 0 || i >= N || !Alive[i]) return 0;
        double l = Life[i];
        if (l >= 0.5) return 3;
        if (l >= 0.1) return 2;
        if (l >= 0.01) return 1;
        return 0;
    }
}
