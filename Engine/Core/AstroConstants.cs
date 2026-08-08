namespace SolarSim.Engine.Core;

/// <summary>
/// Constantes de referência. Unidades canônicas do projeto: km, segundos, radianos,
/// com parâmetro gravitacional em km³/s².
/// </summary>
public static class AstroConstants
{
    /// <summary>Época J2000.0: meio-dia de 1 de janeiro de 2000 UTC.</summary>
    public const double J2000 = 2451545.0;

    public const double SecondsPerDay = 86400.0;

    public const double TwoPi = Math.PI * 2.0;

    /// <summary>Unidade astronômica em km, conforme definição da IAU de 2012.</summary>
    public const double AstronomicalUnitKm = 149_597_870.7;

    /// <summary>GM do Sol.</summary>
    public const double SunMuKm3S2 = 1.327_124_400_18e11;

    /// <summary>GM da Terra.</summary>
    public const double EarthMuKm3S2 = 3.986_004_418e5;

    public static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;

    public static double RadiansToDegrees(double radians) => radians * 180.0 / Math.PI;

    /// <summary>Normaliza um ângulo para o intervalo [0, 2π).</summary>
    public static double NormalizeAngle(double radians)
    {
        var wrapped = radians % TwoPi;
        return wrapped < 0.0 ? wrapped + TwoPi : wrapped;
    }
}
