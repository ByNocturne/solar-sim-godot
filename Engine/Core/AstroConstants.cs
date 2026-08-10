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

    /// <summary>
    /// Dias em um século juliano. É a unidade em que as tabelas de efemérides publicam
    /// taxas seculares, e por isso a unidade que aparece na fronteira do JSON.
    /// </summary>
    public const double DaysPerJulianCentury = 36525.0;

    public const double SecondsPerJulianCentury = DaysPerJulianCentury * SecondsPerDay;

    /// <summary>
    /// Dias em um ano juliano. É a unidade em que a literatura publica drifts seculares
    /// de corpos menores (UA por milhão de anos), e por isso a conversão do Yarkovsky.
    /// </summary>
    public const double DaysPerJulianYear = 365.25;

    public const double SecondsPerJulianYear = DaysPerJulianYear * SecondsPerDay;

    /// <summary>Anos em um milhão de anos — a unidade do <c>da/dt</c> do Yarkovsky.</summary>
    public const double YearsPerMillion = 1_000_000.0;

    public const double TwoPi = Math.PI * 2.0;

    /// <summary>Segundos de arco em uma volta completa.</summary>
    public const double ArcsecondsPerTurn = 360.0 * 3600.0;

    /// <summary>
    /// Velocidade da luz no vácuo, em km/s. Entra na precessão relativística do
    /// periápside, que é o único lugar onde a relatividade aparece no motor.
    /// </summary>
    public const double SpeedOfLightKmS = 299_792.458;

    /// <summary>Unidade astronômica em km, conforme definição da IAU de 2012.</summary>
    public const double AstronomicalUnitKm = 149_597_870.7;

    /// <summary>
    /// GM do Sol. O GM dos demais corpos vem do arquivo de dados; este fica aqui por ser
    /// a referência do sistema e por permitir exercitar o propagador sem carregar dados.
    /// </summary>
    public const double SunMuKm3S2 = 1.327_124_400_18e11;

    public static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;

    public static double RadiansToDegrees(double radians) => radians * 180.0 / Math.PI;

    /// <summary>
    /// Converte uma taxa angular interna, em radianos por segundo, para a unidade em que
    /// a literatura publica precessão: segundos de arco por século.
    /// </summary>
    public static double RadPerSecondToArcsecPerCentury(double radPerSecond)
        => radPerSecond * SecondsPerJulianCentury * ArcsecondsPerTurn / TwoPi;

    /// <summary>Normaliza um ângulo para o intervalo [0, 2π).</summary>
    public static double NormalizeAngle(double radians)
    {
        var wrapped = radians % TwoPi;
        return wrapped < 0.0 ? wrapped + TwoPi : wrapped;
    }
}
