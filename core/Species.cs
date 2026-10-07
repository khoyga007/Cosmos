using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

public sealed record Species
{
    private readonly string _id = "";
    private readonly string _nameVi = "";
    private readonly Habitat _habitat = Habitat.Land;
    private readonly double _manipulation = 1.0;
    private readonly EnergyBasis _energyBasis = EnergyBasis.Chem;
    private readonly SenseKind _senses = SenseKind.Vision;
    private readonly double _lifespan = 80.0;
    private readonly SocialStructure _social = SocialStructure.Band;
    private readonly double _tempMin = 250.0;
    private readonly double _tempMax = 320.0;
    private readonly IReadOnlyDictionary<string, object> _extra = EmptyExtra;

    private static readonly IReadOnlyDictionary<string, object> EmptyExtra =
        new ReadOnlyDictionary<string, object>(new Dictionary<string, object>());

    public string Id { get => _id; init => _id = value ?? ""; }
    public string NameVi { get => _nameVi; init => _nameVi = value ?? ""; }

    public Habitat Habitat
    {
        get => _habitat;
        init
        {
            ValidateParam("habitat", value);
            _habitat = value;
        }
    }

    public double Manipulation
    {
        get => _manipulation;
        init
        {
            ValidateParam("manipulation", value);
            _manipulation = value;
        }
    }

    public EnergyBasis EnergyBasis
    {
        get => _energyBasis;
        init
        {
            ValidateParam("energy_basis", value);
            _energyBasis = value;
        }
    }

    public SenseKind Senses
    {
        get => _senses;
        init
        {
            ValidateParam("senses", value);
            _senses = value;
        }
    }

    public double Lifespan
    {
        get => _lifespan;
        init
        {
            ValidateParam("lifespan", value);
            _lifespan = value;
        }
    }

    public SocialStructure Social
    {
        get => _social;
        init
        {
            ValidateParam("social", value);
            _social = value;
        }
    }

    public double TempMin
    {
        get => _tempMin;
        init
        {
            ValidateParam("temp_min", value);
            _tempMin = value;
        }
    }

    public double TempMax
    {
        get => _tempMax;
        init
        {
            ValidateParam("temp_max", value);
            _tempMax = value;
        }
    }

    public IReadOnlyDictionary<string, object> Extra
    {
        get => _extra;
        init => _extra = FreezeExtra(value);
    }

    public static IReadOnlyDictionary<string, object> FreezeExtra(IReadOnlyDictionary<string, object>? dict)
    {
        if (dict == null || dict.Count == 0) return EmptyExtra;
        var copy = new Dictionary<string, object>(dict.Count);
        foreach (var kv in dict) copy[kv.Key] = kv.Value;
        return new ReadOnlyDictionary<string, object>(copy);
    }

    public Species(
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
        ValidateParam("habitat", Habitat);
        ValidateParam("manipulation", Manipulation);
        ValidateParam("energy_basis", EnergyBasis);
        ValidateParam("senses", Senses);
        ValidateParam("lifespan", Lifespan);
        ValidateParam("social", Social);
        ValidateParam("temp_min", TempMin);
        ValidateParam("temp_max", TempMax);

        this._id = Id ?? "";
        this._nameVi = NameVi ?? "";
        this._habitat = Habitat;
        this._manipulation = Manipulation;
        this._energyBasis = EnergyBasis;
        this._senses = Senses;
        this._lifespan = Lifespan;
        this._social = Social;
        this._tempMin = TempMin;
        this._tempMax = TempMax;
        this._extra = FreezeExtra(Extra);
    }

    public static void ValidateParam(string paramId, object value)
    {
        if (SpeciesParamCatalog.ById.TryGetValue(paramId, out var def))
        {
            if (def.ValueType == typeof(double))
            {
                double d = Convert.ToDouble(value);
                if (double.IsNaN(d) || double.IsInfinity(d))
                    throw new ArgumentOutOfRangeException(paramId, $"Param '{paramId}' cannot be NaN or Infinity");
                if (d < def.Min || d > def.Max)
                    throw new ArgumentOutOfRangeException(paramId, $"Param '{paramId}' value {d} out of range [{def.Min}..{def.Max}]");
            }
            else if (def.ValueType.IsEnum)
            {
                if (!Enum.IsDefined(def.ValueType, value))
                    throw new ArgumentOutOfRangeException(paramId, $"Param '{paramId}' invalid enum value {value}");
            }
        }
    }

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

    public Species WithParam(string paramId, object value)
    {
        ValidateParam(paramId, value);
        return paramId switch
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
                Extra = UpdateExtra(Extra, paramId, value)
            }
        };
    }

    private static IReadOnlyDictionary<string, object> UpdateExtra(IReadOnlyDictionary<string, object> current, string key, object value)
    {
        var dict = new Dictionary<string, object>(current);
        dict[key] = value;
        return new ReadOnlyDictionary<string, object>(dict);
    }
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
