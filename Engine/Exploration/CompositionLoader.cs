using System.Text.Json;
using System.Text.Json.Serialization;
using SolarSim.Engine.Data;

namespace SolarSim.Engine.Exploration;

/// <summary>Lê <c>Data/body_composition_j2000.json</c>.</summary>
public static class CompositionLoader
{
    public const int SupportedSchemaVersion = 1;

    public const string DefaultRelativePath = "Data/body_composition_j2000.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        AllowTrailingCommas = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        PropertyNameCaseInsensitive = true,
    };

    public static IReadOnlyDictionary<string, BodyComposition> FromFile(string path)
        => Parse(File.ReadAllText(path), path);

    public static IReadOnlyDictionary<string, BodyComposition> Parse(string json, string origin = "composition")
    {
        ArgumentNullException.ThrowIfNull(json);

        CompositionDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<CompositionDocument>(json, Options);
        }
        catch (JsonException error)
        {
            throw new SystemDataException($"JSON de composição inválido em '{origin}': {error.Message}", error);
        }

        if (document is null)
        {
            throw new SystemDataException($"O conteúdo de composição em '{origin}' está vazio.");
        }

        if (document.SchemaVersion != SupportedSchemaVersion)
        {
            throw new SystemDataException(
                $"'schemaVersion' de composição é {document.SchemaVersion}, mas este motor lê "
                    + $"{SupportedSchemaVersion}.");
        }

        if (document.Bodies is not { Length: > 0 } bodies)
        {
            throw new SystemDataException("O campo 'bodies' de composição precisa ter ao menos um corpo.");
        }

        var map = new Dictionary<string, BodyComposition>(StringComparer.Ordinal);
        foreach (var entry in bodies)
        {
            if (string.IsNullOrWhiteSpace(entry.BodyId))
            {
                throw new SystemDataException("Corpo de composição sem 'bodyId'.");
            }

            if (!map.TryAdd(entry.BodyId, ToComposition(entry)))
            {
                throw new SystemDataException($"Corpo de composição duplicado: '{entry.BodyId}'.");
            }
        }

        return map;
    }

    private static BodyComposition ToComposition(CompositionBodyEntry entry)
    {
        var deposits = new Dictionary<ResourceKind, ResourceDeposit>();
        Add(deposits, ResourceKind.Regolith, entry.Regolith);
        Add(deposits, ResourceKind.WaterIce, entry.WaterIce);
        Add(deposits, ResourceKind.Metals, entry.Metals);
        Add(deposits, ResourceKind.Organics, entry.Organics);
        return new BodyComposition { BodyId = entry.BodyId, Deposits = deposits };
    }

    private static void Add(
        Dictionary<ResourceKind, ResourceDeposit> deposits,
        ResourceKind kind,
        DepositDto? dto)
    {
        if (dto is null)
        {
            return;
        }

        if (dto.Abundance < 0.0 || dto.Abundance > 1.0)
        {
            throw new SystemDataException($"Abundância de {kind} fora de [0,1].");
        }

        if (dto.ExtractDifficulty <= 0.0)
        {
            throw new SystemDataException($"Dificuldade de {kind} deve ser positiva.");
        }

        deposits[kind] = new ResourceDeposit(dto.Abundance, dto.ExtractDifficulty);
    }

    private sealed class CompositionDocument
    {
        public int SchemaVersion { get; set; }

        public CompositionBodyEntry[]? Bodies { get; set; }
    }

    private sealed class CompositionBodyEntry
    {
        public string BodyId { get; set; } = "";

        public DepositDto? Regolith { get; set; }

        public DepositDto? WaterIce { get; set; }

        public DepositDto? Metals { get; set; }

        public DepositDto? Organics { get; set; }
    }

    private sealed class DepositDto
    {
        public double Abundance { get; set; }

        public double ExtractDifficulty { get; set; } = 1.0;
    }
}
