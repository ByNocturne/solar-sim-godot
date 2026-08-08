using System.Text.Json;
using System.Text.Json.Serialization;
using SolarSim.Engine.Core;
using SolarSim.Engine.Models;

namespace SolarSim.Engine.Data;

/// <summary>
/// Lê <c>Data/body_environment_j2000.json</c> para unidades internas (radianos, Pa, s).
/// </summary>
public static class EnvironmentLoader
{
    public const int SupportedSchemaVersion = 1;

    public const string DefaultRelativePath = "Data/body_environment_j2000.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        AllowTrailingCommas = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static IReadOnlyDictionary<string, BodyEnvironment> Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        EnvironmentDocument? document;

        try
        {
            document = JsonSerializer.Deserialize<EnvironmentDocument>(json, Options);
        }
        catch (JsonException error)
        {
            throw new SystemDataException($"JSON ambiental inválido: {error.Message}", error);
        }

        if (document is null)
        {
            throw new SystemDataException("O conteúdo ambiental está vazio.");
        }

        if (document.SchemaVersion is not { } version)
        {
            throw new SystemDataException("O campo 'schemaVersion' é obrigatório.");
        }

        if (version != SupportedSchemaVersion)
        {
            throw new SystemDataException(
                $"'schemaVersion' ambiental é {version}, mas este motor lê a versão "
                    + $"{SupportedSchemaVersion}.");
        }

        if (document.Bodies is not { Length: > 0 } bodies)
        {
            throw new SystemDataException(
                "O campo 'bodies' ambiental é obrigatório e precisa ter ao menos um corpo.");
        }

        var map = new Dictionary<string, BodyEnvironment>(StringComparer.Ordinal);

        foreach (var dto in bodies)
        {
            var env = ToEnvironment(dto);
            if (!map.TryAdd(env.BodyId, env))
            {
                throw new SystemDataException(
                    $"Corpo ambiental '{env.BodyId}': id duplicado.");
            }
        }

        return map;
    }

    public static IReadOnlyDictionary<string, BodyEnvironment> FromFile(string path)
    {
        try
        {
            return Parse(File.ReadAllText(path));
        }
        catch (SystemDataException error)
        {
            throw new SystemDataException($"Erro em '{path}'. {error.Message}", error);
        }
    }

    private static BodyEnvironment ToEnvironment(BodyEnvironmentDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.BodyId))
        {
            throw new SystemDataException("Há um perfil ambiental sem o campo 'bodyId'.");
        }

        var id = dto.BodyId;
        var albedo = RequireUnitInterval(dto.BondAlbedo, id, "bondAlbedo");
        var rotation = RequireNonNegative(dto.RotationPeriodHours, id, "rotationPeriodHours")
            * 3600.0;
        var obliquity = AstroConstants.DegreesToRadians(
            RequireFinite(dto.ObliquityDeg, id, "obliquityDeg"));
        var core = RequireUnitInterval(dto.MetallicCoreFraction, id, "metallicCoreFraction");
        var k2 = RequireNonNegative(dto.TidalLoveNumberK2, id, "tidalLoveNumberK2");
        var q = RequirePositive(dto.TidalQualityFactor, id, "tidalQualityFactor");
        var inertia = RequireNonNegative(
            dto.ThermalInertiaJPerM2KSqrtS,
            id,
            "thermalInertiaJPerM2KSqrtS");

        AtmosphereProfile? atmosphere = null;
        if (dto.Atmosphere is { } atm)
        {
            atmosphere = new AtmosphereProfile
            {
                SurfacePressurePa = RequireNonNegative(atm.SurfacePressurePa, id, "atmosphere.surfacePressurePa"),
                MoleFractionH2 = RequireUnitInterval(atm.MoleFractionH2, id, "atmosphere.moleFractionH2"),
                MoleFractionHe = RequireUnitInterval(atm.MoleFractionHe, id, "atmosphere.moleFractionHe"),
                MoleFractionN2 = RequireUnitInterval(atm.MoleFractionN2, id, "atmosphere.moleFractionN2"),
                MoleFractionO2 = RequireUnitInterval(atm.MoleFractionO2, id, "atmosphere.moleFractionO2"),
                MoleFractionCO2 = RequireUnitInterval(atm.MoleFractionCO2, id, "atmosphere.moleFractionCO2"),
                MoleFractionCH4 = RequireUnitInterval(atm.MoleFractionCH4, id, "atmosphere.moleFractionCH4"),
                MoleFractionH2O = RequireUnitInterval(atm.MoleFractionH2O, id, "atmosphere.moleFractionH2O"),
                MoleFractionO3 = RequireUnitInterval(atm.MoleFractionO3, id, "atmosphere.moleFractionO3"),
            };
        }

        StellarProperties? star = null;
        if (dto.Star is { } s)
        {
            star = new StellarProperties
            {
                EffectiveTemperatureK = RequirePositive(s.EffectiveTemperatureK, id, "star.effectiveTemperatureK"),
                RadiusKm = RequirePositive(s.RadiusKm, id, "star.radiusKm"),
            };
        }

        return new BodyEnvironment
        {
            BodyId = id,
            BondAlbedo = albedo,
            RotationPeriodSeconds = rotation,
            ObliquityRad = obliquity,
            MetallicCoreFraction = core,
            TidalLoveNumberK2 = k2,
            TidalQualityFactor = q,
            ThermalInertiaJPerM2KSqrtS = inertia,
            Atmosphere = atmosphere,
            Star = star,
        };
    }

    private static double RequireFinite(double? value, string id, string field)
    {
        if (value is not { } v || !double.IsFinite(v))
        {
            throw new SystemDataException(
                $"Corpo '{id}': o campo '{field}' é obrigatório e precisa ser finito.");
        }

        return v;
    }

    private static double RequireNonNegative(double? value, string id, string field)
    {
        var v = RequireFinite(value, id, field);
        if (v < 0.0)
        {
            throw new SystemDataException($"Corpo '{id}': '{field}' não pode ser negativo.");
        }

        return v;
    }

    private static double RequirePositive(double? value, string id, string field)
    {
        var v = RequireFinite(value, id, field);
        if (v <= 0.0)
        {
            throw new SystemDataException($"Corpo '{id}': '{field}' precisa ser positivo.");
        }

        return v;
    }

    private static double RequireUnitInterval(double? value, string id, string field)
    {
        var v = RequireFinite(value, id, field);
        if (v is < 0.0 or > 1.0)
        {
            throw new SystemDataException(
                $"Corpo '{id}': '{field}' precisa estar em [0, 1].");
        }

        return v;
    }

    private sealed class EnvironmentDocument
    {
        [JsonPropertyName("schemaVersion")]
        public int? SchemaVersion { get; init; }

        [JsonPropertyName("bodies")]
        public BodyEnvironmentDto[]? Bodies { get; init; }
    }

    private sealed class BodyEnvironmentDto
    {
        [JsonPropertyName("bodyId")]
        public string? BodyId { get; init; }

        [JsonPropertyName("bondAlbedo")]
        public double? BondAlbedo { get; init; }

        [JsonPropertyName("rotationPeriodHours")]
        public double? RotationPeriodHours { get; init; }

        [JsonPropertyName("obliquityDeg")]
        public double? ObliquityDeg { get; init; }

        [JsonPropertyName("metallicCoreFraction")]
        public double? MetallicCoreFraction { get; init; }

        [JsonPropertyName("tidalLoveNumberK2")]
        public double? TidalLoveNumberK2 { get; init; }

        [JsonPropertyName("tidalQualityFactor")]
        public double? TidalQualityFactor { get; init; }

        [JsonPropertyName("thermalInertiaJPerM2KSqrtS")]
        public double? ThermalInertiaJPerM2KSqrtS { get; init; }

        [JsonPropertyName("atmosphere")]
        public AtmosphereDto? Atmosphere { get; init; }

        [JsonPropertyName("star")]
        public StarDto? Star { get; init; }
    }

    private sealed class AtmosphereDto
    {
        [JsonPropertyName("surfacePressurePa")]
        public double? SurfacePressurePa { get; init; }

        [JsonPropertyName("moleFractionH2")]
        public double? MoleFractionH2 { get; init; }

        [JsonPropertyName("moleFractionHe")]
        public double? MoleFractionHe { get; init; }

        [JsonPropertyName("moleFractionN2")]
        public double? MoleFractionN2 { get; init; }

        [JsonPropertyName("moleFractionO2")]
        public double? MoleFractionO2 { get; init; }

        [JsonPropertyName("moleFractionCO2")]
        public double? MoleFractionCO2 { get; init; }

        [JsonPropertyName("moleFractionCH4")]
        public double? MoleFractionCH4 { get; init; }

        [JsonPropertyName("moleFractionH2O")]
        public double? MoleFractionH2O { get; init; }

        [JsonPropertyName("moleFractionO3")]
        public double? MoleFractionO3 { get; init; }
    }

    private sealed class StarDto
    {
        [JsonPropertyName("effectiveTemperatureK")]
        public double? EffectiveTemperatureK { get; init; }

        [JsonPropertyName("radiusKm")]
        public double? RadiusKm { get; init; }
    }
}
