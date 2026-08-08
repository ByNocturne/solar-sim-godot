using System.Text.Json;
using System.Text.Json.Serialization;
using SolarSim.Engine.Models;

namespace SolarSim.Engine.Data;

/// <summary>
/// Salva e restaura a simulação.
/// </summary>
/// <remarks>
/// O invariante 4 é o que torna isto pequeno. Como o estado é função pura da Data
/// Juliana, não há posição, velocidade nem fase de nada para guardar: salvar o Sistema
/// Solar inteiro é salvar um <c>double</c>. O que sobra são os corpos que não estão no
/// arquivo de dados — os acrescentados em runtime — e, deles, a trajetória em arcos, que
/// é a única coisa no motor que depende do caminho percorrido e não só da data.
/// </remarks>
public static class SaveState
{
    /// <summary>
    /// Muda quando o formato deixa de ser compatível. Um arquivo de versão desconhecida
    /// é recusado, em vez de lido pela metade.
    /// </summary>
    public const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Serialize(SimEngine sim)
    {
        ArgumentNullException.ThrowIfNull(sim);

        var bodies = sim.DynamicBodyIds
            .Select(sim.BodyOf)
            .OrderBy(body => body.Id, StringComparer.Ordinal)
            .Select(body => new BodyDto
            {
                Id = body.Id,
                Name = body.Name,
                MuKm3S2 = body.MuKm3S2,
                RadiusKm = body.RadiusKm,
                ColorRgb = body.ColorRgb,
                Arcs = [.. sim.TrajectoryOf(body.Id).Select(ToDto)],
            })
            .ToArray();

        return JsonSerializer.Serialize(
            new SaveDto
            {
                Version = CurrentVersion,
                JulianDate = sim.Time.JulianDate,
                Bodies = bodies,
            },
            Options);
    }

    /// <summary>
    /// Restaura sobre um motor já carregado com o arquivo de dados: acerta o relógio,
    /// descarta os corpos dinâmicos que houver e recria os salvos.
    /// </summary>
    /// <exception cref="SystemDataException">
    /// Se o documento estiver mal formado, tiver versão desconhecida ou descrever um
    /// corpo que a hierarquia não aceita.
    /// </exception>
    public static void Restore(SimEngine sim, string json)
    {
        ArgumentNullException.ThrowIfNull(sim);

        var save = Read(json);

        if (save.Version != CurrentVersion)
        {
            throw new SystemDataException(
                $"Arquivo salvo na versão {save.Version}, e esta build lê a versão "
                    + $"{CurrentVersion}.");
        }

        foreach (var bodyId in sim.DynamicBodyIds.ToArray())
        {
            sim.Remove(bodyId);
        }

        sim.Time.JumpTo(save.JulianDate);

        foreach (var body in save.Bodies)
        {
            sim.Add(ToBody(body), new Trajectory(body.Arcs.Select(ToArc)));
        }
    }

    private static SaveDto Read(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<SaveDto>(json, Options)
                ?? throw new SystemDataException("O arquivo salvo está vazio.");
        }
        catch (JsonException erro)
        {
            throw new SystemDataException(
                $"O arquivo salvo não é um JSON válido: {erro.Message}", erro);
        }
    }

    private static CelestialBodyData ToBody(BodyDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Id))
        {
            throw new SystemDataException("Corpo salvo sem 'id'.");
        }

        if (dto.Arcs.Length == 0)
        {
            throw new SystemDataException(
                $"Corpo '{dto.Id}': salvo sem nenhum arco de trajetória.");
        }

        var last = dto.Arcs[^1];

        return new CelestialBodyData
        {
            Id = dto.Id,
            Name = dto.Name ?? dto.Id,
            ParentId = last.ParentId,
            MuKm3S2 = dto.MuKm3S2,
            RadiusKm = dto.RadiusKm,
            ColorRgb = dto.ColorRgb,
            Elements = ToArc(last).Elements,
        };
    }

    private static ArcDto ToDto(TrajectoryArc arc) => new()
    {
        StartJulianDate = arc.StartJulianDate,
        ParentId = arc.ParentId,
        SemiMajorAxisKm = arc.Elements.SemiMajorAxisKm,
        Eccentricity = arc.Elements.Eccentricity,
        InclinationRad = arc.Elements.InclinationRad,
        LongitudeOfAscendingNodeRad = arc.Elements.LongitudeOfAscendingNodeRad,
        ArgumentOfPeriapsisRad = arc.Elements.ArgumentOfPeriapsisRad,
        MeanAnomalyAtEpochRad = arc.Elements.MeanAnomalyAtEpochRad,
    };

    /// <remarks>
    /// Os ângulos ficam em radianos, e não em graus como no arquivo de dados: este
    /// documento é escrito pela máquina para a máquina, e converter duas vezes só
    /// arriscaria perder dígitos de algo que não é para ser lido por ninguém.
    /// </remarks>
    private static TrajectoryArc ToArc(ArcDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.ParentId))
        {
            throw new SystemDataException("Arco de trajetória salvo sem 'parentId'.");
        }

        return new TrajectoryArc(
            dto.StartJulianDate,
            dto.ParentId,
            new OrbitalElements(
                dto.SemiMajorAxisKm,
                dto.Eccentricity,
                dto.InclinationRad,
                dto.LongitudeOfAscendingNodeRad,
                dto.ArgumentOfPeriapsisRad,
                dto.MeanAnomalyAtEpochRad));
    }

    private sealed record SaveDto
    {
        [JsonPropertyName("version")]
        public int Version { get; init; }

        [JsonPropertyName("julianDate")]
        public double JulianDate { get; init; }

        [JsonPropertyName("bodies")]
        public BodyDto[] Bodies { get; init; } = [];
    }

    private sealed record BodyDto
    {
        [JsonPropertyName("id")]
        public string? Id { get; init; }

        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("muKm3S2")]
        public double MuKm3S2 { get; init; }

        [JsonPropertyName("radiusKm")]
        public double RadiusKm { get; init; }

        [JsonPropertyName("colorRgb")]
        public uint ColorRgb { get; init; }

        [JsonPropertyName("arcs")]
        public ArcDto[] Arcs { get; init; } = [];
    }

    private sealed record ArcDto
    {
        [JsonPropertyName("startJulianDate")]
        public double StartJulianDate { get; init; }

        [JsonPropertyName("parentId")]
        public string? ParentId { get; init; }

        [JsonPropertyName("semiMajorAxisKm")]
        public double SemiMajorAxisKm { get; init; }

        [JsonPropertyName("eccentricity")]
        public double Eccentricity { get; init; }

        [JsonPropertyName("inclinationRad")]
        public double InclinationRad { get; init; }

        [JsonPropertyName("longitudeOfAscendingNodeRad")]
        public double LongitudeOfAscendingNodeRad { get; init; }

        [JsonPropertyName("argumentOfPeriapsisRad")]
        public double ArgumentOfPeriapsisRad { get; init; }

        [JsonPropertyName("meanAnomalyAtEpochRad")]
        public double MeanAnomalyAtEpochRad { get; init; }
    }
}
