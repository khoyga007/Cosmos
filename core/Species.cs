using System;
using System.Collections.Generic;
using System.Linq;

namespace Cosmos.Core;

public enum Habitat { Land, Ocean, Atmosphere, Ice, Subsurface }
public enum EnergyBasis { Chem, Photo, Thermal }
public enum SenseKind { Vision, Sonar, Electro, Chemical }
public enum SocialStructure { Solitary, Band, Hive, Gestalt }

public sealed record SpeciesParamDef(
    string Id,
    string NameVi,
    Type ValueType,
    object DefaultValue,
    double Min = double.NegativeInfinity,
    double Max = double.PositiveInfinity,
    string Unit = ""
);

public static class SpeciesParamCatalog
{
    public static readonly IReadOnlyList<SpeciesParamDef> Params = Array.AsReadOnly(new SpeciesParamDef[]
    {
        new("habitat", "Môi trường sống", typeof(Habitat), Habitat.Land),
        new("manipulation", "Năng lực thao tác", typeof(double), 1.0, Min: 0.0, Max: 1.0),
        new("energy_basis", "Nguồn năng lượng", typeof(EnergyBasis), EnergyBasis.Chem),
        new("senses", "Giác quan", typeof(SenseKind), SenseKind.Vision),
        new("lifespan", "Tuổi thọ", typeof(double), 80.0, Min: 0.1, Max: 100000.0, Unit: "yr"),
        new("social", "Cấu trúc xã hội", typeof(SocialStructure), SocialStructure.Band),
        new("temp_min", "Nhiệt độ tối thiểu", typeof(double), 250.0, Min: 0.0, Max: 1000.0, Unit: "K"),
        new("temp_max", "Nhiệt độ tối đa", typeof(double), 320.0, Min: 0.0, Max: 1000.0, Unit: "K"),
    });

    public static readonly IReadOnlyDictionary<string, SpeciesParamDef> ById =
        Params.ToDictionary(p => p.Id, StringComparer.Ordinal);
}

public sealed record Species(
    string Id,
    string NameVi,
    Habitat Habitat = Habitat.Land,
    double Manipulation = 1.0,
    EnergyBasis EnergyBasis = EnergyBasis.Chem,
    SenseKind Senses = SenseKind.Vision,
    double Lifespan = 80.0,
    SocialStructure Social = SocialStructure.Band,
    double TempMin = 250.0,
    double TempMax = 320.0,
    IReadOnlyDictionary<string, object>? Extra = null
)
{
    public IReadOnlyDictionary<string, object> Extra { get; init; } = Extra ?? new Dictionary<string, object>();

    public object GetParam(string paramId) => paramId switch
    {
        "habitat" => Habitat,
        "manipulation" => Manipulation,
        "energy_basis" => EnergyBasis,
        "senses" => Senses,
        "lifespan" => Lifespan,
        "social" => Social,
        "temp_min" => TempMin,
        "temp_max" => TempMax,
        _ => Extra.TryGetValue(paramId, out var val) ? val
             : SpeciesParamCatalog.ById.TryGetValue(paramId, out var def) ? def.DefaultValue
             : throw new KeyNotFoundException($"Unknown species param '{paramId}'")
    };

    public Species WithParam(string paramId, object value) => paramId switch
    {
        "habitat" => this with { Habitat = (Habitat)value },
        "manipulation" => this with { Manipulation = Convert.ToDouble(value) },
        "energy_basis" => this with { EnergyBasis = (EnergyBasis)value },
        "senses" => this with { Senses = (SenseKind)value },
        "lifespan" => this with { Lifespan = Convert.ToDouble(value) },
        "social" => this with { Social = (SocialStructure)value },
        "temp_min" => this with { TempMin = Convert.ToDouble(value) },
        "temp_max" => this with { TempMax = Convert.ToDouble(value) },
        _ => this with
        {
            Extra = new Dictionary<string, object>(Extra) { [paramId] = value }
        }
    };
}

public static class SpeciesCatalog
{
    public static readonly Species Human = new(
        Id: "human",
        NameVi: "Con người",
        Habitat: Habitat.Land,
        Manipulation: 1.0,
        EnergyBasis: EnergyBasis.Chem,
        Senses: SenseKind.Vision,
        Lifespan: 80.0,
        Social: SocialStructure.Band,
        TempMin: 250.0,
        TempMax: 320.0
    );

    public static readonly IReadOnlyList<Species> Templates = Array.AsReadOnly(new[]
    {
        Human
    });
}
