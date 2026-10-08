using System;
using Cosmos.Core;

namespace Cosmos.Game;

/// <summary>
/// Immutable-per-frame snapshot of simulation state copied at sync points.
/// Main thread reads exclusively from this for rendering, picking, and tool overlays
/// while the simulation worker task runs Advance concurrently.
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
    public string?[] CivTag = Array.Empty<string?>();

    public double G;
    public double RadiusScale;
    public double AttractMass;
    public double StarMass;
    public double StarSolarMass;
    public double StarGiantFraction;
    public readonly double[] Density = new double[World.NElem];

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
        CivTag = new string?[cap];
    }

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
                    Kind[i] = Cosmos.Core.Kind.Rock;
                }
                else
                {
                    Kind[i] = Cosmos.Core.Kind.Rock;
                }
                StarPhase[i] = Cosmos.Core.StarPhase.None;
                StarColour[i] = 0;
                Hill[i] = 0;
                CivTag[i] = null;
                continue;
            }

            IsShip[i] = false;
            StarPhase[i] = w.StarPhaseOf(i);
            StarColour[i] = w.StarColour(i);
            Hill[i] = w.Hill(i);
            Kind[i] = w.KindOf(i);

            if (M[i] < StarMass && StarPhase[i] == Cosmos.Core.StarPhase.None)
            {
                if (Pop[i] > 0)
                {
                    int civ = w.Civ[i];
                    CivTag[i] = civ < 0 ? "văn minh" : (civ < w.Civs.Count && w.Civs[civ].Home == i ? w.Civs[civ].Name : $"thuộc địa {(civ < w.Civs.Count ? w.Civs[civ].Name : "")}");
                }
                else CivTag[i] = null;
            }
            else CivTag[i] = null;
        }
    }

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
        if (primary < 0) return 1;
        double dx = x - X[primary], dy = y - Y[primary], d = Math.Sqrt(dx * dx + dy * dy);
        return d > 0 ? Math.Sqrt(G * M[primary] / d) : 1;
    }

    public int Heaviest()
    {
        int best = -1;
        for (int i = 0; i < N; i++)
        {
            if (!Alive[i]) continue;
            if (best < 0 || M[i] > M[best]) best = i;
        }
        return best;
    }
}
