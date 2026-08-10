using System.Globalization;
using System.Text;
using SolarSim.Engine.Core;
using SolarSim.Engine.Data;
using SolarSim.Engine.Models;

namespace SolarSim.CatalogImporter;

/// <summary>
/// Escreve o catálogo no mesmo formato que o carregador do motor lê.
/// </summary>
/// <remarks>
/// O texto é montado à mão, e não por serializador, por dois motivos. A ordem dos campos
/// importa para quem abre o arquivo — id e nome primeiro, órbita por último —, e o
/// arquivo é versionado: uma reordenação ou uma troca de formatação numérica viraria um
/// diff de trezentas linhas sem nenhuma mudança de conteúdo.
/// </remarks>
public static class CatalogWriter
{
    public static string ToJson(
        IReadOnlyList<CatalogBody> bodies,
        IReadOnlyList<string> sources,
        IReadOnlyList<string> notes)
    {
        ArgumentNullException.ThrowIfNull(bodies);
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(notes);

        var text = new StringBuilder();

        text.Append("{\n");
        text.Append($"  \"schemaVersion\": {DataLoader.SupportedSchemaVersion},\n");
        text.Append("  \"frame\": \"Eclíptica e equinócio médios de J2000, centrado no Sol\",\n");
        text.Append("  \"epoch\": {\n");
        text.Append("    \"name\": \"J2000.0\",\n");
        text.Append($"    \"julianDate\": {Number(AstroConstants.J2000)}\n");
        text.Append("  },\n");
        text.Append("  \"units\": {\n");
        text.Append("    \"convention\": \"O sufixo do nome do campo declara a unidade, como no arquivo do sistema.\",\n");
        text.Append("    \"mass\": \"muKm3S2 quando o GM foi medido; densityGCm3 quando só há tamanho e uma densidade típica da classe.\"\n");
        text.Append("  },\n");
        text.Append(StringArray("sources", sources));
        text.Append(StringArray("notes", notes));
        text.Append("  \"bodies\": [\n");

        for (var index = 0; index < bodies.Count; index++)
        {
            text.Append(Body(bodies[index]));
            text.Append(index == bodies.Count - 1 ? "\n" : ",\n");
        }

        text.Append("  ]\n");
        text.Append("}\n");

        return text.ToString();
    }

    private static string StringArray(string name, IReadOnlyList<string> values)
    {
        var text = new StringBuilder();

        text.Append($"  \"{name}\": [\n");

        for (var index = 0; index < values.Count; index++)
        {
            text.Append($"    {Quote(values[index])}");
            text.Append(index == values.Count - 1 ? "\n" : ",\n");
        }

        text.Append("  ],\n");

        return text.ToString();
    }

    private static string Body(CatalogBody body)
    {
        var fields = new List<string>
        {
            $"      \"id\": {Quote(body.Id)}",
            $"      \"name\": {Quote(body.Name)}",
            "      \"parent\": \"sun\"",
            $"      \"kind\": {Quote(BodyKinds.JsonName(body.Kind))}",
        };

        if (body.Family is { } family)
        {
            fields.Add($"      \"family\": {Quote(family)}");
        }

        if (body.MuKm3S2 is { } mu)
        {
            fields.Add($"      \"muKm3S2\": {Number(mu)}");
        }
        else
        {
            fields.Add($"      \"densityGCm3\": {Number(body.DensityGCm3!.Value)}");
        }

        fields.Add($"      \"radiusKm\": {Number(body.RadiusKm)}");
        fields.Add($"      \"colorRgb\": \"#{body.ColorRgb:X6}\"");

        if (body.Note is { } note)
        {
            fields.Add($"      \"note\": {Quote(note)}");
        }

        if (body.YarkovskyDaAuPerMyr is { } || body.RadiationPressureBeta is { })
        {
            var ng = new List<string>();

            if (body.YarkovskyDaAuPerMyr is { } da)
            {
                ng.Add($"        \"yarkovskyDaAuPerMyr\": {Number(da)}");
            }

            if (body.RadiationPressureBeta is { } beta)
            {
                ng.Add($"        \"radiationPressureBeta\": {Number(beta)}");
            }

            fields.Add(
                "      \"nonGravitational\": {\n" + string.Join(",\n", ng) + "\n      }");
        }

        var orbit = new[]
        {
            $"        \"semiMajorAxisAu\": {Number(body.SemiMajorAxisAu)}",
            $"        \"eccentricity\": {Number(body.Eccentricity)}",
            $"        \"inclinationDeg\": {Number(body.InclinationDeg)}",
            $"        \"longitudeOfAscendingNodeDeg\": {Number(body.LongitudeOfAscendingNodeDeg)}",
            $"        \"argumentOfPeriapsisDeg\": {Number(body.ArgumentOfPeriapsisDeg)}",
            $"        \"meanAnomalyAtEpochDeg\": {Number(body.MeanAnomalyAtEpochDeg)}",
        };

        fields.Add("      \"orbit\": {\n" + string.Join(",\n", orbit) + "\n      }");

        return "    {\n" + string.Join(",\n", fields) + "\n    }";
    }

    /// <summary>
    /// Número com precisão de ida e volta e ponto decimal, custe o que custar à cultura
    /// de quem roda a ferramenta. Um catálogo gerado com vírgula decimal não é lido de
    /// volta por ninguém.
    /// </summary>
    private static string Number(double value)
    {
        var text = value.ToString("R", CultureInfo.InvariantCulture);

        return text.Contains('.') || text.Contains('E') || text.Contains('e')
            ? text
            : text + ".0";
    }

    private static string Quote(string value)
    {
        var escaped = value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);

        return $"\"{escaped}\"";
    }
}
