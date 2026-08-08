using SolarSim.Engine.Core;

namespace SolarSim.Engine.Models;

/// <summary>
/// Os seis elementos keplerianos que descrevem univocamente uma órbita.
/// Distância em km, ângulos em radianos, anomalia média referida à época J2000.0.
/// </summary>
public readonly record struct OrbitalElements(
    double SemiMajorAxisKm,
    double Eccentricity,
    double InclinationRad,
    double LongitudeOfAscendingNodeRad,
    double ArgumentOfPeriapsisRad,
    double MeanAnomalyAtEpochRad)
{
    public bool IsClosed => Eccentricity < 1.0;

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
