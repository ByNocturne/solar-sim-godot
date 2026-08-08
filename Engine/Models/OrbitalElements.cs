using SolarSim.Engine.Core;

namespace SolarSim.Engine.Models;

/// <summary>
/// Os seis elementos keplerianos que descrevem univocamente uma órbita.
/// Distância em km, ângulos em radianos, anomalia média referida à época J2000.0.
/// </summary>
/// <remarks>
/// Vale para elipses e hipérboles. Na hipérbole o semi-eixo maior é negativo e a
/// anomalia média não é um ângulo cíclico, e sim uma quantidade que cresce sem limite
/// com o tempo. A parábola exata não é representável aqui; ver
/// <see cref="MinimumEccentricityGap"/>.
/// </remarks>
public readonly record struct OrbitalElements(
    double SemiMajorAxisKm,
    double Eccentricity,
    double InclinationRad,
    double LongitudeOfAscendingNodeRad,
    double ArgumentOfPeriapsisRad,
    double MeanAnomalyAtEpochRad)
{
    /// <summary>
    /// Quanto a excentricidade precisa se afastar de 1 para que a órbita seja
    /// representável por semi-eixo maior. Na parábola o semi-eixo é infinito, e nas
    /// vizinhanças dele o produto <c>a·(1-e²)</c> perde dígitos por cancelamento.
    /// Com esta folga a perda fica na décima casa, o que é irrelevante ao lado do erro
    /// do próprio modelo de dois corpos.
    /// </summary>
    public const double MinimumEccentricityGap = 1e-6;

    public bool IsClosed => Eccentricity < 1.0;

    /// <summary>
    /// Semi-latus rectum: o raio no ponto em que a anomalia verdadeira vale 90 graus.
    /// É o parâmetro de tamanho que continua positivo e finito na hipérbole, e por isso
    /// é ele, e não o semi-eixo maior, que a equação da cônica usa.
    /// </summary>
    public double SemiLatusRectumKm
        => SemiMajorAxisKm * (1.0 - Eccentricity) * (1.0 + Eccentricity);

    /// <summary>Menor distância ao foco. Positiva também na hipérbole, onde a &lt; 0.</summary>
    public double PeriapsisKm => SemiMajorAxisKm * (1.0 - Eccentricity);

    /// <summary>Maior distância ao foco, infinita quando a órbita é aberta.</summary>
    public double ApoapsisKm
        => IsClosed ? SemiMajorAxisKm * (1.0 + Eccentricity) : double.PositiveInfinity;

    /// <summary>
    /// Raio dado o ângulo desde o periápside, pela equação da cônica. Uma fórmula só
    /// para elipse e hipérbole.
    /// </summary>
    public double RadiusAt(double trueAnomalyRad)
        => SemiLatusRectumKm / (1.0 + Eccentricity * Math.Cos(trueAnomalyRad));

    /// <summary>
    /// Recusa a excentricidade que a representação não comporta: negativa, ou perto
    /// demais de 1.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"/>
    public static void RequireRepresentableEccentricity(double eccentricity)
    {
        if (eccentricity < 0.0 || double.IsNaN(eccentricity))
        {
            throw new ArgumentOutOfRangeException(
                nameof(eccentricity),
                eccentricity,
                "A excentricidade não pode ser negativa.");
        }

        if (Math.Abs(eccentricity - 1.0) < MinimumEccentricityGap)
        {
            throw new ArgumentOutOfRangeException(
                nameof(eccentricity),
                eccentricity,
                $"Excentricidade a menos de {MinimumEccentricityGap:G1} de 1: a órbita é "
                    + "parabólica ou quase, e o semi-eixo maior deixa de ser um parâmetro "
                    + "utilizável. Deslocar a excentricidade para fora dessa faixa muda a "
                    + "trajetória menos do que o erro numérico de mantê-la dentro.");
        }
    }

    /// <summary>
    /// Constrói a partir das unidades em que as tabelas de efemérides são publicadas:
    /// semi-eixo em unidades astronômicas e ângulos em graus.
    /// </summary>
    public static OrbitalElements FromAuAndDegrees(
        double semiMajorAxisAu,
        double eccentricity,
        double inclinationDeg,
        double longitudeOfAscendingNodeDeg,
        double argumentOfPeriapsisDeg,
        double meanAnomalyAtEpochDeg)
        => new(
            semiMajorAxisAu * AstroConstants.AstronomicalUnitKm,
            eccentricity,
            AstroConstants.DegreesToRadians(inclinationDeg),
            AstroConstants.DegreesToRadians(longitudeOfAscendingNodeDeg),
            AstroConstants.DegreesToRadians(argumentOfPeriapsisDeg),
            AstroConstants.DegreesToRadians(meanAnomalyAtEpochDeg));
}
