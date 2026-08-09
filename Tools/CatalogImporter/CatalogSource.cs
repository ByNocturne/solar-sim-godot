using System.Globalization;
using SolarSim.Engine.Core;
using SolarSim.Engine.Models;

namespace SolarSim.CatalogImporter;

/// <summary>
/// Um corpo menor como ele sai da fonte, antes de virar JSON.
/// </summary>
/// <remarks>
/// Não é <see cref="CelestialBodyData"/> porque o que se escreve no arquivo é a entrada
/// do carregador, não a saída dele: aqui a densidade ainda é densidade, e o semi-eixo
/// ainda está em unidades astronômicas.
/// </remarks>
public sealed record CatalogBody
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required BodyKind Kind { get; init; }

    public string? Family { get; init; }

    public required double SemiMajorAxisAu { get; init; }

    public required double Eccentricity { get; init; }

    public required double InclinationDeg { get; init; }

    public required double LongitudeOfAscendingNodeDeg { get; init; }

    public required double ArgumentOfPeriapsisDeg { get; init; }

    /// <summary>Anomalia média já referida a J2000.0.</summary>
    public required double MeanAnomalyAtEpochDeg { get; init; }

    public required double RadiusKm { get; init; }

    /// <summary>GM medido. Exclusivo com <see cref="DensityGCm3"/>.</summary>
    public double? MuKm3S2 { get; init; }

    public double? DensityGCm3 { get; init; }

    public required uint ColorRgb { get; init; }

    public string? Note { get; init; }
}

/// <summary>
/// Lê o CSV curado e devolve corpos com a anomalia média já trazida para J2000.
/// </summary>
/// <remarks>
/// A conversão de época é a razão de o importador existir. As fontes publicam elementos
/// osculadores na época que lhes convém — o MPC usa uma época corrente, o Horizons usa a
/// da solução —, e o motor referencia tudo a J2000.0. Fazer esse deslocamento na carga
/// significaria fazê-lo a cada abertura do jogo; fazê-lo aqui deixa o arquivo pronto.
/// </remarks>
public static class CatalogSource
{
    private static readonly string[] Columns =
    [
        "id",
        "name",
        "kind",
        "family",
        "colorRgb",
        "epochJd",
        "semiMajorAxisAu",
        "eccentricity",
        "inclinationDeg",
        "longitudeOfAscendingNodeDeg",
        "argumentOfPeriapsisDeg",
        "meanAnomalyDeg",
        "radiusKm",
        "muKm3S2",
        "densityGCm3",
        "note",
    ];

    public static IReadOnlyList<CatalogBody> Read(string csv)
    {
        ArgumentNullException.ThrowIfNull(csv);

        var rows = Csv.Parse(csv);

        if (rows.Count == 0)
        {
            throw new CatalogSourceException("O CSV está vazio.");
        }

        RequireHeader(rows[0]);

        var bodies = new List<CatalogBody>(rows.Count - 1);

        for (var line = 1; line < rows.Count; line++)
        {
            bodies.Add(ToBody(rows[line], line + 1));
        }

        if (bodies.Count == 0)
        {
            throw new CatalogSourceException("O CSV tem cabeçalho e nenhum corpo.");
        }

        return bodies;
    }

    /// <summary>
    /// A anomalia média deslocada da época da fonte para J2000, pelo movimento médio.
    /// </summary>
    /// <remarks>
    /// Só a anomalia média anda: os outros cinco elementos são osculadores e, no modelo de
    /// dois corpos, constantes. Deslocá-los exigiria a perturbação que os move, que é
    /// justamente o que o modelo não tem — e é por isso que a fonte preferida é aquela que
    /// já entrega os elementos na data pedida.
    /// </remarks>
    public static double MeanAnomalyAtJ2000Deg(
        double meanAnomalyDeg,
        double epochJd,
        double semiMajorAxisAu,
        double bodyMuKm3S2)
    {
        var elapsedDays = AstroConstants.J2000 - epochJd;

        if (elapsedDays == 0.0)
        {
            return meanAnomalyDeg;
        }

        var semiMajorAxisKm = semiMajorAxisAu * AstroConstants.AstronomicalUnitKm;
        var mu = AstroConstants.SunMuKm3S2 + bodyMuKm3S2;
        var meanMotionRadPerSecond = Math.Sqrt(mu / (semiMajorAxisKm * semiMajorAxisKm * semiMajorAxisKm));

        var advancedRad = meanMotionRadPerSecond * elapsedDays * AstroConstants.SecondsPerDay;

        return AstroConstants.RadiansToDegrees(
            AstroConstants.NormalizeAngle(
                AstroConstants.DegreesToRadians(meanAnomalyDeg) + advancedRad));
    }

    private static void RequireHeader(IReadOnlyList<string> header)
    {
        if (header.Count != Columns.Length
            || !header.Select(field => field.Trim()).SequenceEqual(Columns, StringComparer.Ordinal))
        {
            throw new CatalogSourceException(
                "O cabeçalho do CSV precisa ser exatamente: " + string.Join(",", Columns));
        }
    }

    private static CatalogBody ToBody(IReadOnlyList<string> row, int lineNumber)
    {
        if (row.Count != Columns.Length)
        {
            throw new CatalogSourceException(
                $"Linha {lineNumber}: tem {row.Count} campos, e o cabeçalho tem {Columns.Length}.");
        }

        string Text(string column)
        {
            var value = row[Array.IndexOf(Columns, column)].Trim();

            return value;
        }

        string Required(string column)
        {
            var value = Text(column);

            if (value.Length == 0)
            {
                throw new CatalogSourceException(
                    $"Linha {lineNumber}: o campo '{column}' é obrigatório.");
            }

            return value;
        }

        string? Optional(string column)
        {
            var value = Text(column);

            return value.Length == 0 ? null : value;
        }

        double Number(string column)
        {
            var value = Required(column);

            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                || !double.IsFinite(number))
            {
                throw new CatalogSourceException(
                    $"Linha {lineNumber}: o campo '{column}' vale '{value}', que não é um número.");
            }

            return number;
        }

        double? OptionalNumber(string column)
            => Optional(column) is null ? null : Number(column);

        var id = Required("id");
        var kindName = Required("kind");

        if (!BodyKinds.TryParse(kindName, out var kind))
        {
            throw new CatalogSourceException(
                $"Linha {lineNumber}: 'kind' vale '{kindName}'. As classes são: "
                    + $"{BodyKinds.JsonNames()}.");
        }

        if (!BodyKinds.IsMinor(kind))
        {
            throw new CatalogSourceException(
                $"Linha {lineNumber}: '{kindName}' não é classe de corpo menor.");
        }

        var radiusKm = Number("radiusKm");

        if (radiusKm <= 0.0)
        {
            throw new CatalogSourceException(
                $"Linha {lineNumber}: 'radiusKm' precisa ser positivo.");
        }

        var mu = OptionalNumber("muKm3S2");
        var density = OptionalNumber("densityGCm3");

        if ((mu is null) == (density is null))
        {
            throw new CatalogSourceException(
                $"Linha {lineNumber}: informe 'muKm3S2' ou 'densityGCm3', e apenas um dos dois. "
                    + "O GM medido dispensa a estimativa por densidade.");
        }

        var semiMajorAxisAu = Number("semiMajorAxisAu");
        var eccentricity = Number("eccentricity");
        var epochJd = Number("epochJd");

        if (eccentricity >= 1.0 && epochJd != AstroConstants.J2000)
        {
            throw new CatalogSourceException(
                $"Linha {lineNumber}: excentricidade {eccentricity.ToString(CultureInfo.InvariantCulture)} "
                    + "descreve órbita aberta, e a anomalia média de uma órbita aberta não é "
                    + "deslocável por movimento médio. Peça à fonte os elementos já em J2000.");
        }

        var muForMeanMotion = mu ?? BodyMass.MuFromDensity(density!.Value, radiusKm);

        return new CatalogBody
        {
            Id = id,
            Name = Required("name"),
            Kind = kind,
            Family = Optional("family"),
            SemiMajorAxisAu = semiMajorAxisAu,
            Eccentricity = eccentricity,
            InclinationDeg = Number("inclinationDeg"),
            LongitudeOfAscendingNodeDeg = Number("longitudeOfAscendingNodeDeg"),
            ArgumentOfPeriapsisDeg = Number("argumentOfPeriapsisDeg"),
            MeanAnomalyAtEpochDeg = MeanAnomalyAtJ2000Deg(
                Number("meanAnomalyDeg"), epochJd, semiMajorAxisAu, muForMeanMotion),
            RadiusKm = radiusKm,
            MuKm3S2 = mu,
            DensityGCm3 = density,
            ColorRgb = Palette.Parse(Optional("colorRgb"), kind, lineNumber),
            Note = Optional("note"),
        };
    }
}

public sealed class CatalogSourceException(string message) : Exception(message);
