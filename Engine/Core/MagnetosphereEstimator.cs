namespace SolarSim.Engine.Core;

/// <summary>
/// Estimativa heurística de momento magnético e dose ionizante relativa na superfície.
/// </summary>
/// <remarks>
/// O momento relativo à Terra escala com fração de núcleo metálico × taxa de rotação ×
/// massa (via μ). A dose relativa é o fluxo de vento estelar (∝ 1/d²) dividido por
/// (1 + proteção magnética). Modelo de ensino, não MHD.
/// </remarks>
public static class MagnetosphereEstimator
{
    /// <summary>μ da Terra, para normalizar o momento.</summary>
    public const double EarthMuKm3S2 = 3.986_004_418e5;

    public const double EarthRotationPeriodSeconds = 86_164.0;

    public const double EarthMetallicCoreFraction = 0.325;

    public const double EarthHeliocentricKm = 1.495_978_707e8;

    public static double MagneticMomentRelativeToEarth(
        double bodyMuKm3S2,
        double metallicCoreFraction,
        double rotationPeriodSeconds)
    {
        if (bodyMuKm3S2 <= 0.0
            || metallicCoreFraction <= 0.0
            || rotationPeriodSeconds <= 0.0)
        {
            return 0.0;
        }

        var earthSpin = 1.0 / EarthRotationPeriodSeconds;
        var bodySpin = 1.0 / rotationPeriodSeconds;
        var earthProxy = EarthMetallicCoreFraction * earthSpin * EarthMuKm3S2;
        var bodyProxy = metallicCoreFraction * bodySpin * bodyMuKm3S2;
        return bodyProxy / earthProxy;
    }

    /// <summary>
    /// Dose ionizante relativa à Terra em 1 UA com campo terrestre (= 1,0 por definição).
    /// Valores &gt; 1 significam mais hostil.
    /// </summary>
    public static double RelativeIonizingRadiation(
        double heliocentricDistanceKm,
        double magneticMomentRelativeToEarth,
        double stellarLuminosityRelative = 1.0)
    {
        if (heliocentricDistanceKm <= 0.0)
        {
            return double.PositiveInfinity;
        }

        var wind =
            stellarLuminosityRelative
            * (EarthHeliocentricKm / heliocentricDistanceKm)
            * (EarthHeliocentricKm / heliocentricDistanceKm);

        var shield = 1.0 + Math.Max(0.0, magneticMomentRelativeToEarth) * 20.0;
        var absolute = wind / shield;

        // Normaliza pelo valor absoluto da Terra de referência (1 UA, momento = 1).
        var earthReference = 1.0 / (1.0 + 20.0);
        return absolute / earthReference;
    }
}
