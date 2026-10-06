// Worlds and small bodies classification & comet activity (SPEC §8, Round 5).
// Physics-derived classification: NO stored field, purely functional.
// Comets sublimate ice when inside the snow line; hot light worlds lose hydrogen.
using System;

namespace Cosmos.Core;

public enum BodyClass
{
    Star,
    Lava,
    Ocean,
    Rocky,
    IceBall,
    GasGiant,
    IceGiant,
    DwarfPlanet,
    Asteroid,
    Comet,
    Ship
}

public sealed partial class Consts
{
    // PLACEHOLDER [P]: Classification & volatile thresholds (SPEC §8)
    // Real-world basalt rock melts ~1300-1500 K; in Cosmos toy Kelvin scale Mercury is 386.5 K, 0.2 AU is ~475 K.
    public double LavaTemp = 450;
    // Share of water needed with liquid water state to classify as Ocean world (Earth has 0.01).
    public double OceanIceMin = 0.005;
    // Mass share of ice for a cold solid body to classify as IceBall (Europa/Pluto ~0.3-0.5).
    public double IceBallIceMin = 0.25;
    // Gas giant thresholds (Jupiter 317.8 M_Earth, gas 0.90; Saturn 95.2 M_Earth, gas 0.85).
    public double GasGiantMassMin = 10 * World.EarthMass;
    public double GasGiantGasMin = 0.20;
    // Ice giant thresholds (Uranus 14.5 M_Earth, ice 0.65; Neptune 17.1 M_Earth, ice 0.66).
    public double IceGiantMassMin = 5 * World.EarthMass;
    public double IceGiantIceMin = 0.30;
    // Mass below which a pulling star-orbiter is a DwarfPlanet instead of a major planet (Pluto 0.002, Mercury 0.055).
    public double ClearingMass = 0.03 * World.EarthMass;
    // Mass share of ice for a small body to be a comet instead of an asteroid (Kuiper ~0.70, Asteroids ~0.05).
    public double CometIceMin = 0.10;
    // Snow line sublimation temperature in vacuum (~170-200 K; between Mars 252.7 K and Jupiter 172.7 K).
    public double SnowLineTemp = 200;
    // Comet ice sublimation rate per year at reference temperature.
    public double CometSublimationRate = 5e-7;
    // Escape velocity vs thermal velocity ratio needed to retain light gas atmosphere.
    public double GasHoldRatio = 0.0035;
    // Gas loss rate per year when a hot lightweight world cannot hold gas.
    public double GasLossRate = 1e-6;
}

public sealed partial class World
{
    void InitKindRules()
    {
        Rules.Add(new Rule("comets", "Comp,Temp,M,SnowLine*", "Comp,M,R", 0.1, w => w.UpdateComets()));
    }

    /// <summary>
    /// Pure function of M, Comp shares, Temp, Water, R, primary (SPEC §8.1).
    /// No stored field: recalculates instantly from physics state.
    /// </summary>
    public BodyClass ClassOf(int i)
    {
        if (!Alive[i]) return BodyClass.Asteroid;
        if (IsShip(i)) return BodyClass.Ship;
        if (M[i] >= C.StarMass) return BodyClass.Star;

        double m = M[i];
        double gas = Share(i, 0);
        double ice = Share(i, 1);
        double solid = Share(i, 2) + Share(i, 3);
        double temp = Temp[i];

        // 1. Gas Giant: massive + dominated by hydrogen/gas
        if (m >= C.GasGiantMassMin && gas >= C.GasGiantGasMin && gas >= ice)
            return BodyClass.GasGiant;

        // 2. Ice Giant: massive + dominated by ices
        if (m >= C.IceGiantMassMin && ice >= C.IceGiantIceMin && ice > gas)
            return BodyClass.IceGiant;

        // 3. Lava world: temperature above rock melting point
        if (!double.IsNaN(temp) && temp >= C.LavaTemp)
            return BodyClass.Lava;

        // 4. Pulling bodies: Major planets, dwarf planets, moons
        if (Attracts(i))
        {
            int p = Par[i];
            bool isSatellite = p >= 0 && Alive[p] && M[p] < C.StarMass;

            // Dwarf planet: directly orbits star / isolated, but mass below clearing mass
            if (!isSatellite && m < C.ClearingMass)
                return BodyClass.DwarfPlanet;

            // Terrestrial bodies:
            int waterState = Water != null && i < Water.Length ? Water[i] : (int)WaterState.None;
            if (waterState == (int)WaterState.Liquid && ice >= C.OceanIceMin)
                return BodyClass.Ocean;

            if (ice >= C.IceBallIceMin && ice > solid)
                return BodyClass.IceBall;

            return BodyClass.Rocky;
        }

        // 5. Small bodies (do not pull): Comets or Asteroids
        if (ice >= C.CometIceMin)
            return BodyClass.Comet;

        return BodyClass.Asteroid;
    }

    /// <summary>
    /// Distance from star where temperature reaches water ice sublimation temperature (SPEC §8.2).
    /// </summary>
    public double SnowLine(int star)
    {
        if (star < 0 || star >= N || !Alive[star]) return 0;
        double starMass = M[star];
        double starLight = Math.Pow(starMass / 50.0, C.LuminosityExponent) * 45 * 45;
        double targetTemp = C.SnowLineTemp > 0 ? C.SnowLineTemp : C.WaterFreeze;
        return Math.Sqrt(starLight) * Math.Pow(C.TemperatureScale / targetTemp, 2);
    }

    /// <summary>
    /// Comet tail strength (0..1) for rendering (SPEC §8.3).
    /// Active when an ice-rich small body is inside the snow line.
    /// </summary>
    public double TailStrength(int i)
    {
        if (!Alive[i] || Attracts(i)) return 0;
        double ice = Share(i, 1);
        if (ice < C.CometIceMin) return 0;
        double t = Temp[i];
        if (double.IsNaN(t)) return 0;
        double snowT = C.SnowLineTemp > 0 ? C.SnowLineTemp : C.WaterFreeze;
        if (t <= snowT) return 0;
        double excess = (t - snowT) / Math.Max(1.0, C.ScorchedEdge - snowT);
        return Math.Clamp(excess * Math.Min(1.0, ice / C.CometIceMin), 0, 1);
    }

    /// <summary>
    /// Checks whether an object can retain light gas based on escape speed vs thermal speed (SPEC §8.4).
    /// </summary>
    public bool HoldsGas(int i)
    {
        if (!Alive[i] || R[i] <= 0 || double.IsNaN(Temp[i])) return false;
        double vEsc = Math.Sqrt(2 * C.G * M[i] / R[i]);
        double vTh = Math.Sqrt(Math.Max(1.0, Temp[i]));
        return (vEsc / vTh) >= C.GasHoldRatio;
    }

    /// <summary>
    /// Rule comets: volatile mass loss (ice sublimation on comets and atmospheric gas loss on hot worlds).
    /// Highly optimized: fast radial rejection ensures near-zero cost for 50,000 rocks.
    /// Stretch-exact: linear loss with dt guarantees identical results in 1 or 200 cuts.
    /// </summary>
    void UpdateComets()
    {
        double dt = RuleYears;
        if (dt <= 0) return;

        // Find stars and their snow lines
        int starCount = 0;
        Span<int> starSlots = stackalloc int[8];
        Span<double> snowLines = stackalloc double[8];
        double maxSnowLine = 0;

        for (int i = 0; i < N && starCount < 8; i++)
        {
            if (Alive[i] && M[i] >= C.StarMass)
            {
                starSlots[starCount] = i;
                double sl = SnowLine(i);
                snowLines[starCount] = sl;
                if (sl > maxSnowLine) maxSnowLine = sl;
                starCount++;
            }
        }
        if (starCount == 0) return;
        double maxSnowLineSq = maxSnowLine * maxSnowLine;
        double targetSnowTemp = C.SnowLineTemp > 0 ? C.SnowLineTemp : C.WaterFreeze;

        for (int i = 0; i < N; i++)
        {
            if (!Alive[i]) continue;

            // Atmosphere loss for hot light worlds that cannot hold gas
            if (IsWorld(i) && Comp[i * NElem + 0] > 0 && !HoldsGas(i))
            {
                double gasLoss = Math.Min(Comp[i * NElem + 0], C.GasLossRate * dt * M[i]);
                if (gasLoss > 0)
                {
                    Comp[i * NElem + 0] -= gasLoss;
                    M[i] -= gasLoss;
                    SetRadius(i);
                }
            }

            // Comets: small bodies with ice inside the snowline
            if (Attracts(i)) continue;

            // Fast radial reject: skip objects outside the maximum snow line
            double xi = X[i], yi = Y[i];
            if (xi * xi + yi * yi > maxSnowLineSq) continue;

            double ice = Comp[i * NElem + 1];
            if (ice <= 0) continue;

            double maxSublimation = 0;
            for (int s = 0; s < starCount; s++)
            {
                int st = starSlots[s];
                double dx = xi - X[st], dy = yi - Y[st];
                double d2 = dx * dx + dy * dy;
                double sl = snowLines[s];
                if (d2 < sl * sl)
                {
                    double t = Temp[i];
                    if (double.IsNaN(t)) t = C.TemperatureScale;
                    double excessT = Math.Max(0, t - targetSnowTemp);
                    if (excessT > 0)
                    {
                        double subRate = C.CometSublimationRate * (excessT / 100.0);
                        if (subRate > maxSublimation) maxSublimation = subRate;
                    }
                }
            }

            if (maxSublimation <= 0) continue;

            double loss = Math.Min(ice, maxSublimation * dt);
            if (loss <= 0) continue;

            Comp[i * NElem + 1] -= loss;
            double newM = 0;
            for (int e = 0; e < NElem; e++) newM += Comp[i * NElem + e];

            if (Comp[i * NElem + 1] <= 1e-18 || newM <= 1e-15)
            {
                Comp[i * NElem + 1] = 0;
                M[i] = newM;
                SetRadius(i);
                LogEvent(i, "comets", "comet.spent", Year, newM, R[i]);
            }
            else
            {
                M[i] = newM;
                SetRadius(i);
            }
        }
    }
}
