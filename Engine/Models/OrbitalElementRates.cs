using SolarSim.Engine.Core;

namespace SolarSim.Engine.Models;

/// <summary>
/// Quanto cada elemento orbital anda por segundo. É o que transforma a órbita fixa do
/// M2 em uma órbita que gira, encolhe ou se inclina ao longo dos séculos.
/// </summary>
/// <remarks>
/// Não existe taxa para a anomalia média, e a ausência é proposital: o avanço dela ao
/// longo da órbita é o movimento médio, que o motor já calcula do semi-eixo maior.
/// Declarar as duas coisas permitiria que discordassem entre si, e a discordância só
/// apareceria como uma posição errada ao longo da órbita, séculos depois.
/// </remarks>
public readonly record struct OrbitalElementRates(
    double SemiMajorAxisKmPerSecond,
    double EccentricityPerSecond,
    double InclinationRadPerSecond,
    double LongitudeOfAscendingNodeRadPerSecond,
    double ArgumentOfPeriapsisRadPerSecond)
{
    /// <summary>Órbita que não muda: o comportamento do motor até o M14.</summary>
    public static OrbitalElementRates None => default;

    /// <summary>
    /// Verdadeiro quando nenhum elemento anda. O caminho de propagação usa isso para
    /// desviar para o propagador de sempre, sem custo nem diferença de arredondamento.
    /// </summary>
    public bool IsZero
        => SemiMajorAxisKmPerSecond == 0.0
            && EccentricityPerSecond == 0.0
            && InclinationRadPerSecond == 0.0
            && LongitudeOfAscendingNodeRadPerSecond == 0.0
            && ArgumentOfPeriapsisRadPerSecond == 0.0;

    /// <summary>
    /// Soma de contribuições independentes. É assim que a taxa declarada no arquivo e a
    /// calculada pela física — relatividade e achatamento — se combinam em uma só.
    /// </summary>
    public static OrbitalElementRates operator +(
        OrbitalElementRates left,
        OrbitalElementRates right)
        => new(
            left.SemiMajorAxisKmPerSecond + right.SemiMajorAxisKmPerSecond,
            left.EccentricityPerSecond + right.EccentricityPerSecond,
            left.InclinationRadPerSecond + right.InclinationRadPerSecond,
            left.LongitudeOfAscendingNodeRadPerSecond + right.LongitudeOfAscendingNodeRadPerSecond,
            left.ArgumentOfPeriapsisRadPerSecond + right.ArgumentOfPeriapsisRadPerSecond);

    /// <summary>
    /// Constrói a partir das unidades em que as tabelas de efemérides publicam taxas
    /// seculares: por século juliano, com os ângulos em graus.
    /// </summary>
    public static OrbitalElementRates FromPerCentury(
        double semiMajorAxisKmPerCentury,
        double eccentricityPerCentury,
        double inclinationDegPerCentury,
        double longitudeOfAscendingNodeDegPerCentury,
        double argumentOfPeriapsisDegPerCentury)
        => new(
            semiMajorAxisKmPerCentury / AstroConstants.SecondsPerJulianCentury,
            eccentricityPerCentury / AstroConstants.SecondsPerJulianCentury,
            AstroConstants.DegreesToRadians(inclinationDegPerCentury)
                / AstroConstants.SecondsPerJulianCentury,
            AstroConstants.DegreesToRadians(longitudeOfAscendingNodeDegPerCentury)
                / AstroConstants.SecondsPerJulianCentury,
            AstroConstants.DegreesToRadians(argumentOfPeriapsisDegPerCentury)
                / AstroConstants.SecondsPerJulianCentury);
}
