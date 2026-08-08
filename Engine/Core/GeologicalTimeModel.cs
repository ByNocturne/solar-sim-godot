using SolarSim.Engine.Models;

namespace SolarSim.Engine.Core;

/// <summary>
/// Evolução em escala geológica como função pura de (JD − época), sem integração.
/// </summary>
/// <remarks>
/// Em torno de J2000 usamos uma reta de ensino: +10% de luminosidade por Gyr no
/// futuro e o simétrico no passado. Basta para migrar a zona habitável sem fingir
/// evolução estelar completa.
/// </remarks>
public static class GeologicalTimeModel
{
    public const double SunAgeAtJ2000Years = 4.6e9;

    public const double YearsPerJulianDay = 1.0 / 365.25;

    /// <summary>Variação relativa de luminosidade por bilhão de anos.</summary>
    public const double LuminosityChangePerGyr = 0.10;

    public static double YearsSinceJ2000(double julianDate)
        => (julianDate - AstroConstants.J2000) * YearsPerJulianDay;

    public static double StellarLuminosityRelative(double julianDate)
    {
        var gyr = YearsSinceJ2000(julianDate) / 1.0e9;
        return Math.Max(0.2, 1.0 + LuminosityChangePerGyr * gyr);
    }

    public static AtmosphereProfile? ErodeAtmosphere(
        AtmosphereProfile? atmosphere,
        double magneticMomentRelativeToEarth,
        double julianDate)
    {
        if (atmosphere is null)
        {
            return null;
        }

        var years = YearsSinceJ2000(julianDate);
        if (Math.Abs(years) < 1.0)
        {
            return atmosphere;
        }

        // Com campo forte, erosão desprezível neste modelo.
        if (magneticMomentRelativeToEarth >= 0.3)
        {
            return atmosphere;
        }

        // Tempo característico ~0,5 Gyr sem escudo (Marte).
        var tauYears = 5.0e8 * (1.0 + 10.0 * Math.Max(0.0, magneticMomentRelativeToEarth));
        var factor = Math.Exp(-Math.Abs(years) / tauYears);
        // No passado (years &lt; 0) a atmosfera era mais densa.
        if (years < 0.0)
        {
            factor = Math.Exp(Math.Abs(years) / tauYears);
        }

        return atmosphere with
        {
            SurfacePressurePa = atmosphere.SurfacePressurePa * factor,
        };
    }
}
