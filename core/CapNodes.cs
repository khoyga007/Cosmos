using System;
using System.Collections.Generic;
using System.Linq;

namespace Cosmos.Core;

public enum CapDomain
{
    Heat,
    Material,
    Knowledge,
    Power,
    Space
}

[Flags]
public enum HabitatMask
{
    None = 0,
    Land = 1,
    Ocean = 2,
    Atmosphere = 4,
    Ice = 8,
    Subsurface = 16,
    All = Land | Ocean | Atmosphere | Ice | Subsurface
}

public static class HabitatExtensions
{
    public static HabitatMask ToMask(this Habitat h) => h switch
    {
        Habitat.Land => HabitatMask.Land,
        Habitat.Ocean => HabitatMask.Ocean,
        Habitat.Atmosphere => HabitatMask.Atmosphere,
        Habitat.Ice => HabitatMask.Ice,
        Habitat.Subsurface => HabitatMask.Subsurface,
        _ => HabitatMask.None
    };
}

public sealed record CapNeed(
    ElementRole Element = ElementRole.None,
    PlanetarySource Planetary = PlanetarySource.None,
    double MinShare = 0,
    bool RequiresOxygen = false,
    bool RequiresVentHeat = false,
    bool RequiresFusionFuel = false,
    bool EscapeVelocityPenalty = false
);

public sealed record CapEffect(
    double WattsPerCapita = 0,
    double MaxPopulationOnEarth = 0,
    double KardashevTerm = 0,
    bool CanLaunchShips = false,
    bool CanDome = false
);

public sealed record CapNode(
    string Id,
    string NameVi,
    CapDomain Domain,
    double TechThreshold,
    IReadOnlyList<string>? Prereqs = null,
    IReadOnlyList<CapNeed>? Needs = null,
    double MinManipulation = 0,
    HabitatMask AllowedHabitats = HabitatMask.All,
    CapEffect? Effect = null,
    int AssociatedStageIndex = -1
)
{
    public IReadOnlyList<string> Prereqs { get; init; } = Prereqs ?? Array.Empty<string>();
    public IReadOnlyList<CapNeed> Needs { get; init; } = Needs ?? Array.Empty<CapNeed>();
    public CapEffect Effect { get; init; } = Effect ?? new CapEffect();
}

public static class CapNodeCatalog
{
    public static readonly IReadOnlyList<CapNode> Nodes = Array.AsReadOnly(new CapNode[]
    {
        // --- Heat Domain ---
        new("fire", "Lửa", CapDomain.Heat, 0.5,
            AllowedHabitats: HabitatMask.All & ~HabitatMask.Ocean,
            Needs: new[] { new CapNeed(Element: ElementRole.Carbon, MinShare: 0.001, RequiresOxygen: true) },
            Effect: new CapEffect(WattsPerCapita: 150),
            AssociatedStageIndex: 1),

        new("geothermal", "Địa nhiệt", CapDomain.Heat, 1.0,
            Needs: new[] { new CapNeed(RequiresVentHeat: true) },
            Effect: new CapEffect(WattsPerCapita: 250),
            AssociatedStageIndex: 2),

        new("solar_furnace", "Lò nung Mặt trời", CapDomain.Heat, 2.0,
            Needs: new[] { new CapNeed(Planetary: PlanetarySource.Starlight) },
            Effect: new CapEffect(WattsPerCapita: 500),
            AssociatedStageIndex: 4),

        // --- Material Domain ---
        new("stonework", "Chế tác Đá", CapDomain.Material, 0.0,
            MinManipulation: 0.2,
            Needs: new[] { new CapNeed(Element: ElementRole.Rock, MinShare: 0.05) },
            Effect: new CapEffect(WattsPerCapita: 100),
            AssociatedStageIndex: 0),

        new("smelting", "Luyện kim", CapDomain.Material, 1.0,
            MinManipulation: 0.5,
            Prereqs: new[] { "fire|geothermal|solar_furnace" },
            Needs: new[] { new CapNeed(Element: ElementRole.Metal, MinShare: 0.02) },
            Effect: new CapEffect(WattsPerCapita: 250),
            AssociatedStageIndex: 2),

        new("alloys", "Hợp kim", CapDomain.Material, 2.0,
            Prereqs: new[] { "smelting" },
            Needs: new[] { new CapNeed(Element: ElementRole.Metal, MinShare: 0.08), new CapNeed(Element: ElementRole.Carbon, MinShare: 0.002) },
            Effect: new CapEffect(WattsPerCapita: 800),
            AssociatedStageIndex: 5),

        // --- Knowledge Domain ---
        new("memory", "Ký ức & Truyền khẩu", CapDomain.Knowledge, 0.0,
            Effect: new CapEffect(WattsPerCapita: 100),
            AssociatedStageIndex: 0),

        new("writing", "Chữ viết", CapDomain.Knowledge, 0.5,
            MinManipulation: 0.4,
            Effect: new CapEffect(WattsPerCapita: 150),
            AssociatedStageIndex: 1),

        new("printing", "In ấn", CapDomain.Knowledge, 1.5,
            Prereqs: new[] { "writing" },
            Effect: new CapEffect(WattsPerCapita: 350),
            AssociatedStageIndex: 3),

        new("computing", "Điện toán", CapDomain.Knowledge, 2.5,
            Prereqs: new[] { "printing", "electricity" },
            Effect: new CapEffect(WattsPerCapita: 2400),
            AssociatedStageIndex: 7),

        // --- Power Domain ---
        new("electricity", "Điện lực", CapDomain.Power, 2.3,
            Prereqs: new[] { "alloys" },
            Needs: new[] { new CapNeed(Element: ElementRole.Metal, MinShare: 0.10) },
            Effect: new CapEffect(WattsPerCapita: 1500),
            AssociatedStageIndex: 6),

        new("fission", "Năng lượng Phân hạch", CapDomain.Power, 2.85,
            Prereqs: new[] { "electricity" },
            Needs: new[] { new CapNeed(Element: ElementRole.Radio, MinShare: 0.0005) },
            Effect: new CapEffect(WattsPerCapita: 2400, KardashevTerm: 0.73),
            AssociatedStageIndex: 7),

        new("fusion", "Năng lượng Nhiệt hạch", CapDomain.Power, 3.5,
            Prereqs: new[] { "electricity" },
            Needs: new[] { new CapNeed(RequiresFusionFuel: true) },
            Effect: new CapEffect(WattsPerCapita: 50000, KardashevTerm: 0.94),
            AssociatedStageIndex: 9),

        // --- Space Domain ---
        new("orbit", "Quỹ đạo Vệ tinh", CapDomain.Space, 3.0,
            Prereqs: new[] { "electricity", "alloys" },
            Needs: new[] { new CapNeed(Element: ElementRole.Metal, MinShare: 0.10), new CapNeed(Element: ElementRole.Ice, MinShare: 0.001), new CapNeed(EscapeVelocityPenalty: true) },
            Effect: new CapEffect(WattsPerCapita: 8000, CanLaunchShips: true, CanDome: true),
            AssociatedStageIndex: 8),

        new("interplanetary", "Du hành Liên hành tinh", CapDomain.Space, 3.5,
            Prereqs: new[] { "orbit", "fusion" },
            Needs: new[] { new CapNeed(Element: ElementRole.Radio, MinShare: 0.001), new CapNeed(EscapeVelocityPenalty: true) },
            Effect: new CapEffect(WattsPerCapita: 50000, CanLaunchShips: true, CanDome: true),
            AssociatedStageIndex: 9)
    });

    public static readonly IReadOnlyDictionary<string, CapNode> ById =
        Nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);
}

public static class CapGraph
{
    public static readonly IReadOnlyList<CapNode> Nodes = CapNodeCatalog.Nodes;

    public static double EscapeVelocity(World w, int slot)
    {
        if (slot < 0 || slot >= w.N || !w.Alive[slot] || !(w.R[slot] > 0)) return 0;
        return Math.Sqrt(2.0 * w.C.G * w.M[slot] / w.R[slot]);
    }

    public static double EscapeVelocityRef(World w)
    {
        double rRef = Math.Max(1e-9, w.EarthRadiusRef);
        return Math.Sqrt(2.0 * w.C.G * World.EarthMass / rRef);
    }

    public static double EscapeVelocityRatio(World w, int slot)
    {
        double vEsc = EscapeVelocity(w, slot);
        double vRef = EscapeVelocityRef(w);
        return vRef > 0 ? (vEsc / vRef) : 1.0;
    }

    public static double KnowledgeRateMultiplier(World w, Species species)
    {
        double span = Math.Max(0.1, species.Lifespan);
        return Math.Pow(span / w.C.CivLifespanRef, w.C.CivLifespanExp);
    }

    public static bool CanUnlock(CapNode node, World w, int slot, Species species, IReadOnlySet<string> opened)
    {
        if ((node.AllowedHabitats & species.Habitat.ToMask()) == 0) return false;
        if (species.Manipulation < node.MinManipulation) return false;

        foreach (var p in node.Prereqs)
        {
            if (p.Contains('|'))
            {
                var options = p.Split('|');
                if (!options.Any(opt => opened.Contains(opt))) return false;
            }
            else
            {
                if (!opened.Contains(p)) return false;
            }
        }

        foreach (var need in node.Needs)
        {
            if (need.Element != ElementRole.None)
            {
                if (w.Share(slot, need.Element) < need.MinShare) return false;
            }
            if (need.Planetary == PlanetarySource.LiquidWater && w.Water[slot] != (int)WaterState.Liquid) return false;
            if (need.Planetary == PlanetarySource.Biosphere && w.Life[slot] < need.MinShare) return false;
            if (need.Planetary == PlanetarySource.Starlight)
            {
                if (double.IsNaN(w.Temp[slot]) || w.Temp[slot] < w.C.StarlightTempMin) return false;
            }
            if (need.RequiresOxygen)
            {
                // Terrestrial oxidizer / atmospheric gas or photosynthetic biosphere
                bool hasOxygen = w.Share(slot, ElementRole.Gas) >= 0.01
                              || (slot < w.Life.Length && w.Life[slot] > 0);
                if (!hasOxygen) return false;
            }
            if (need.RequiresFusionFuel)
            {
                // Fusion fuel: light nuclei from gas (H/He) or ice (water deuterium)
                bool hasFuel = w.Share(slot, ElementRole.Gas) >= 0.001 || w.Share(slot, ElementRole.Ice) >= 0.001;
                if (!hasFuel) return false;
            }
            if (need.RequiresVentHeat)
            {
                // Vent heat requires sufficient terrestrial rock and active thermal source (valid non-freezing temperature)
                if (w.Share(slot, ElementRole.Rock) < 0.20) return false;
                if (double.IsNaN(w.Temp[slot]) || w.Temp[slot] < 100.0) return false;
            }
        }

        return true;
    }

    public static double EffectiveThreshold(CapNode node, World w, int slot)
    {
        double baseThreshold = node.TechThreshold;
        if (node.Needs.Any(n => n.EscapeVelocityPenalty))
        {
            double ratio = EscapeVelocityRatio(w, slot);
            if (ratio > 1.0) baseThreshold *= ratio;
        }
        return baseThreshold;
    }

    public static HashSet<string> EvaluateOpened(World w, int slot, Species species, double currentTech)
    {
        var opened = new HashSet<string>(StringComparer.Ordinal);
        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (var node in Nodes)
            {
                if (opened.Contains(node.Id)) continue;
                double reqTech = EffectiveThreshold(node, w, slot);
                if (currentTech >= reqTech && CanUnlock(node, w, slot, species, opened))
                {
                    opened.Add(node.Id);
                    changed = true;
                }
            }
        }
        return opened;
    }

    public static string DeriveEraLabel(Species species, IReadOnlySet<string> opened, double tech, World w, int slot)
    {
        if (World.IsHuman(species))
        {
            int stage = 0;
            for (int k = w.Stages.Count - 1; k >= 0; k--)
                if (tech >= w.Stages[k].TechThreshold) { stage = k; break; }
            return w.Stages[stage].NameVi;
        }
        return $"Kỷ nguyên {opened.Count}";
    }
}
