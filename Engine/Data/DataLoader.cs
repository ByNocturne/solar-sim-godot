using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using SolarSim.Engine.Core;
using SolarSim.Engine.Models;

namespace SolarSim.Engine.Data;

/// <summary>
/// Lê a descrição do sistema em JSON e a converte para as unidades internas do motor:
/// quilômetros e radianos. A fronteira do JSON é o único lugar do projeto onde graus e
/// unidades astronômicas existem.
/// </summary>
/// <remarks>
/// Toda inconsistência vira <see cref="SystemDataException"/> aqui, na carga, e não uma
/// posição errada dez marcos depois. As mensagens sempre nomeiam o corpo e o campo.
/// </remarks>
public static class DataLoader
{
    public const int SupportedSchemaVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        AllowTrailingCommas = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        // Campo com nome errado é erro de digitação, e silenciar isso faria o corpo
        // aparecer no lugar errado sem nenhum aviso.
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    /// <summary>
    /// Devolve os corpos já em ordem de avaliação: o pai sempre antes do filho.
    /// </summary>
    public static IReadOnlyList<CelestialBodyData> Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        SystemDocument? document;

        try
        {
            document = JsonSerializer.Deserialize<SystemDocument>(json, Options);
        }
        catch (JsonException error)
        {
            throw new SystemDataException($"JSON inválido: {error.Message}", error);
        }

        if (document is null)
        {
            throw new SystemDataException("O conteúdo está vazio.");
        }

        RequireSupportedSchema(document.SchemaVersion);
        RequireJ2000Epoch(document.Epoch);

        if (document.Bodies is not { Length: > 0 } bodies)
        {
            throw new SystemDataException(
                "O campo 'bodies' é obrigatório e precisa ter ao menos a raiz.");
        }

        var parsed = Array.ConvertAll(bodies, ToBodyData);

        return BodyHierarchy.Create(parsed).InEvaluationOrder;
    }

    private static void RequireSupportedSchema(int? schemaVersion)
    {
        if (schemaVersion is not { } version)
        {
            throw new SystemDataException("O campo 'schemaVersion' é obrigatório.");
        }

        if (version != SupportedSchemaVersion)
        {
            throw new SystemDataException(
                $"'schemaVersion' é {version}, mas este motor lê a versão "
                    + $"{SupportedSchemaVersion}.");
        }
    }

    /// <summary>
    /// A anomalia média dos elementos é referida a uma época, e o propagador assume
    /// J2000.0. Um arquivo com outra época produziria posições silenciosamente
    /// deslocadas ao longo da órbita.
    /// </summary>
    private static void RequireJ2000Epoch(EpochDto? epoch)
    {
        if (epoch?.JulianDate is not { } julianDate)
        {
            throw new SystemDataException("O campo 'epoch.julianDate' é obrigatório.");
        }

        if (julianDate != AstroConstants.J2000)
        {
            throw new SystemDataException(
                $"A época declarada é JD {julianDate.ToString("F1", CultureInfo.InvariantCulture)}, "
                    + $"mas o motor referencia os elementos a J2000.0 (JD {AstroConstants.J2000}).");
        }
    }

    private static CelestialBodyData ToBodyData(BodyDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Id))
        {
            throw new SystemDataException("Há um corpo sem o campo 'id'.");
        }

        var id = dto.Id;

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            throw new SystemDataException($"Corpo '{id}': o campo 'name' é obrigatório.");
        }

        var mu = RequireFinite(dto.MuKm3S2, id, "muKm3S2");
        if (mu < 0.0)
        {
            throw new SystemDataException($"Corpo '{id}': 'muKm3S2' não pode ser negativo.");
        }

        var radius = RequireFinite(dto.RadiusKm, id, "radiusKm");
        if (radius <= 0.0)
        {
            throw new SystemDataException($"Corpo '{id}': 'radiusKm' precisa ser positivo.");
        }

        var elements = ToElements(dto, id);

        return new CelestialBodyData
        {
            Id = id,
            Name = dto.Name,
            ParentId = dto.Parent,
            MuKm3S2 = mu,
            RadiusKm = radius,
            J2 = Oblateness(dto, id),
            J2ReferenceRadiusKm = EquatorialRadiusKm(dto, id, radius),
            ColorRgb = ParseColor(dto.ColorRgb, id),
            Elements = elements,
            Rates = ToRates(dto.Orbit?.Rates, elements, id),
        };
    }

    private static double Oblateness(BodyDto dto, string id)
    {
        if (dto.J2 is not { } j2)
        {
            return 0.0;
        }

        if (!double.IsFinite(j2) || j2 < 0.0)
        {
            throw new SystemDataException(
                $"Corpo '{id}': 'j2' precisa ser um número não negativo. O achatamento é "
                    + "adimensional e da ordem de 10⁻³ para a Terra.");
        }

        return j2;
    }

    /// <summary>
    /// O raio de referência do J₂. Ausente, vale o raio do corpo — o que é uma
    /// aproximação, já que o achatamento é publicado contra o raio equatorial e
    /// <c>radiusKm</c> costuma ser o médio.
    /// </summary>
    private static double EquatorialRadiusKm(BodyDto dto, string id, double radiusKm)
    {
        if (dto.EquatorialRadiusKm is not { } equatorial)
        {
            return radiusKm;
        }

        if (!double.IsFinite(equatorial) || equatorial <= 0.0)
        {
            throw new SystemDataException(
                $"Corpo '{id}': 'equatorialRadiusKm' precisa ser positivo.");
        }

        return equatorial;
    }

    /// <summary>
    /// As taxas seculares declaradas. Só fazem sentido na órbita fechada: na aberta o
    /// corpo passa uma vez, e uma taxa por século não descreve nada.
    /// </summary>
    private static OrbitalElementRates ToRates(
        RatesDto? rates,
        OrbitalElements? elements,
        string id)
    {
        if (rates is null)
        {
            return OrbitalElementRates.None;
        }

        if (elements is not { IsClosed: true })
        {
            throw new SystemDataException(
                $"Corpo '{id}': 'orbit.rates' só vale para órbita fechada.");
        }

        return OrbitalElementRates.FromPerCentury(
            OptionalFinite(rates.SemiMajorAxisKmPerCentury, id, "orbit.rates.semiMajorAxisKmPerCentury"),
            OptionalFinite(rates.EccentricityPerCentury, id, "orbit.rates.eccentricityPerCentury"),
            OptionalFinite(rates.InclinationDegPerCentury, id, "orbit.rates.inclinationDegPerCentury"),
            OptionalFinite(
                rates.LongitudeOfAscendingNodeDegPerCentury,
                id,
                "orbit.rates.longitudeOfAscendingNodeDegPerCentury"),
            OptionalFinite(
                rates.ArgumentOfPeriapsisDegPerCentury,
                id,
                "orbit.rates.argumentOfPeriapsisDegPerCentury"));
    }

    private static OrbitalElements? ToElements(BodyDto dto, string id)
    {
        if (dto.Parent is null)
        {
            if (dto.Orbit is not null)
            {
                throw new SystemDataException(
                    $"Corpo '{id}': é a raiz, porque não tem 'parent', e a raiz fica na "
                        + "origem. Remova o campo 'orbit' ou declare o 'parent'.");
            }

            return null;
        }

        if (dto.Orbit is not { } orbit)
        {
            throw new SystemDataException(
                $"Corpo '{id}': orbita '{dto.Parent}', então o campo 'orbit' é obrigatório.");
        }

        var semiMajorAxisKm = SemiMajorAxisKm(orbit, id);

        var eccentricity = RequireFinite(orbit.Eccentricity, id, "orbit.eccentricity");
        if (eccentricity < 0.0)
        {
            throw new SystemDataException(
                $"Corpo '{id}': 'orbit.eccentricity' não pode ser negativa.");
        }

        RequireConicIsRepresentable(eccentricity, semiMajorAxisKm, id);

        return new OrbitalElements(
            semiMajorAxisKm,
            eccentricity,
            AstroConstants.DegreesToRadians(
                RequireFinite(orbit.InclinationDeg, id, "orbit.inclinationDeg")),
            AstroConstants.DegreesToRadians(
                RequireFinite(
                    orbit.LongitudeOfAscendingNodeDeg, id, "orbit.longitudeOfAscendingNodeDeg")),
            AstroConstants.DegreesToRadians(
                RequireFinite(orbit.ArgumentOfPeriapsisDeg, id, "orbit.argumentOfPeriapsisDeg")),
            AstroConstants.DegreesToRadians(
                RequireFinite(orbit.MeanAnomalyAtEpochDeg, id, "orbit.meanAnomalyAtEpochDeg")));
    }

    /// <summary>
    /// O semi-eixo aceita duas unidades porque as fontes as usam para escalas diferentes:
    /// planetas vêm em unidades astronômicas, satélites em quilômetros. Exigir
    /// exatamente uma das duas evita o campo ambíguo.
    /// </summary>
    private static double SemiMajorAxisKm(OrbitDto orbit, string id)
    {
        var semiMajorAxisKm = (orbit.SemiMajorAxisKm, orbit.SemiMajorAxisAu) switch
        {
            ({ } km, null) => km,
            (null, { } au) => au * AstroConstants.AstronomicalUnitKm,
            (null, null) => throw new SystemDataException(
                $"Corpo '{id}': informe 'orbit.semiMajorAxisKm' ou 'orbit.semiMajorAxisAu'."),
            _ => throw new SystemDataException(
                $"Corpo '{id}': 'orbit.semiMajorAxisKm' e 'orbit.semiMajorAxisAu' estão "
                    + "ambos preenchidos. Escolha uma unidade."),
        };

        if (!double.IsFinite(semiMajorAxisKm) || semiMajorAxisKm == 0.0)
        {
            throw new SystemDataException(
                $"Corpo '{id}': o semi-eixo maior precisa ser um número diferente de zero.");
        }

        return semiMajorAxisKm;
    }

    /// <summary>
    /// O sinal do semi-eixo maior e o valor da excentricidade contam a mesma coisa, o
    /// tipo de cônica, e precisam contar a mesma. A hipérbole tem semi-eixo negativo por
    /// definição, e a parábola não tem semi-eixo nenhum.
    /// </summary>
    private static void RequireConicIsRepresentable(
        double eccentricity,
        double semiMajorAxisKm,
        string id)
    {
        try
        {
            OrbitalElements.RequireRepresentableEccentricity(eccentricity);
        }
        catch (ArgumentOutOfRangeException erro)
        {
            throw new SystemDataException($"Corpo '{id}': {erro.Message}", erro);
        }

        var closed = eccentricity < 1.0;

        if (closed != semiMajorAxisKm > 0.0)
        {
            var esperado = closed ? "positivo" : "negativo";

            throw new SystemDataException(
                $"Corpo '{id}': excentricidade "
                    + $"{eccentricity.ToString(CultureInfo.InvariantCulture)} pede semi-eixo "
                    + $"maior {esperado}, e o declarado é "
                    + $"{semiMajorAxisKm.ToString(CultureInfo.InvariantCulture)} km. Na "
                    + "hipérbole o semi-eixo é negativo, e é o sinal dele que diz de que "
                    + "lado do foco fica o centro da cônica.");
        }
    }

    /// <summary>Campo opcional que, se vier, precisa ser número: ausente vale zero.</summary>
    private static double OptionalFinite(double? value, string id, string field)
    {
        if (value is not { } number)
        {
            return 0.0;
        }

        if (!double.IsFinite(number))
        {
            throw new SystemDataException($"Corpo '{id}': o campo '{field}' não é um número.");
        }

        return number;
    }

    private static double RequireFinite(double? value, string id, string field)
    {
        if (value is not { } number)
        {
            throw new SystemDataException($"Corpo '{id}': o campo '{field}' é obrigatório.");
        }

        if (!double.IsFinite(number))
        {
            throw new SystemDataException($"Corpo '{id}': o campo '{field}' não é um número.");
        }

        return number;
    }

    private static uint ParseColor(string? color, string id)
    {
        if (color is null)
        {
            return 0xFFFFFF;
        }

        var digits = color.StartsWith('#') ? color[1..] : color;

        if (digits.Length != 6
            || !uint.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
        {
            throw new SystemDataException(
                $"Corpo '{id}': 'colorRgb' vale '{color}', que não é uma cor no formato "
                    + "'#RRGGBB'.");
        }

        return rgb;
    }

    // Os campos de documentação — 'frame', 'units', 'sources', 'notes' e 'note' — são
    // declarados para que o desserializador os aceite, mas não viram estado do motor:
    // existem para quem abre o arquivo, não para quem o executa.
    private sealed record SystemDocument
    {
        [JsonPropertyName("schemaVersion")]
        public int? SchemaVersion { get; init; }

        [JsonPropertyName("frame")]
        public string? Frame { get; init; }

        [JsonPropertyName("epoch")]
        public EpochDto? Epoch { get; init; }

        [JsonPropertyName("units")]
        public JsonElement Units { get; init; }

        [JsonPropertyName("sources")]
        public string[]? Sources { get; init; }

        [JsonPropertyName("notes")]
        public string[]? Notes { get; init; }

        [JsonPropertyName("bodies")]
        public BodyDto[]? Bodies { get; init; }
    }

    private sealed record EpochDto
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("julianDate")]
        public double? JulianDate { get; init; }
    }

    private sealed record BodyDto
    {
        [JsonPropertyName("id")]
        public string? Id { get; init; }

        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("parent")]
        public string? Parent { get; init; }

        [JsonPropertyName("muKm3S2")]
        public double? MuKm3S2 { get; init; }

        [JsonPropertyName("radiusKm")]
        public double? RadiusKm { get; init; }

        [JsonPropertyName("j2")]
        public double? J2 { get; init; }

        [JsonPropertyName("equatorialRadiusKm")]
        public double? EquatorialRadiusKm { get; init; }

        [JsonPropertyName("colorRgb")]
        public string? ColorRgb { get; init; }

        [JsonPropertyName("note")]
        public string? Note { get; init; }

        [JsonPropertyName("orbit")]
        public OrbitDto? Orbit { get; init; }
    }

    private sealed record OrbitDto
    {
        [JsonPropertyName("semiMajorAxisKm")]
        public double? SemiMajorAxisKm { get; init; }

        [JsonPropertyName("semiMajorAxisAu")]
        public double? SemiMajorAxisAu { get; init; }

        [JsonPropertyName("eccentricity")]
        public double? Eccentricity { get; init; }

        [JsonPropertyName("inclinationDeg")]
        public double? InclinationDeg { get; init; }

        [JsonPropertyName("longitudeOfAscendingNodeDeg")]
        public double? LongitudeOfAscendingNodeDeg { get; init; }

        [JsonPropertyName("argumentOfPeriapsisDeg")]
        public double? ArgumentOfPeriapsisDeg { get; init; }

        [JsonPropertyName("meanAnomalyAtEpochDeg")]
        public double? MeanAnomalyAtEpochDeg { get; init; }

        [JsonPropertyName("rates")]
        public RatesDto? Rates { get; init; }
    }

    // Taxas seculares residuais: o que sobra depois da relatividade e do achatamento, que
    // o motor calcula sozinho. Todos os campos são opcionais e valem zero quando ausentes.
    private sealed record RatesDto
    {
        [JsonPropertyName("semiMajorAxisKmPerCentury")]
        public double? SemiMajorAxisKmPerCentury { get; init; }

        [JsonPropertyName("eccentricityPerCentury")]
        public double? EccentricityPerCentury { get; init; }

        [JsonPropertyName("inclinationDegPerCentury")]
        public double? InclinationDegPerCentury { get; init; }

        [JsonPropertyName("longitudeOfAscendingNodeDegPerCentury")]
        public double? LongitudeOfAscendingNodeDegPerCentury { get; init; }

        [JsonPropertyName("argumentOfPeriapsisDegPerCentury")]
        public double? ArgumentOfPeriapsisDegPerCentury { get; init; }

        [JsonPropertyName("note")]
        public string? Note { get; init; }
    }
}
