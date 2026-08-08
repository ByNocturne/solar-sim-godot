namespace SolarSim.Engine.Core;

/// <summary>
/// Relógio da simulação. Mantém a Data Juliana corrente e avança independentemente
/// de frame-rate: o único estado acumulado no motor é o próprio instante.
/// </summary>
public sealed class TimeEngine
{
    private static readonly DateTime J2000Epoch =
        new(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    public TimeEngine(double startJulianDate = AstroConstants.J2000)
        => JulianDate = startJulianDate;

    public double JulianDate { get; private set; }

    public bool IsPaused { get; set; }

    /// <summary>
    /// Quantos segundos simulados passam por segundo real. Aceita valores negativos,
    /// que fazem o tempo correr para trás.
    /// </summary>
    public double SpeedMultiplier { get; set; } = 1.0;

    /// <summary>Dias julianos decorridos desde a época J2000.0.</summary>
    public double DaysSinceEpoch => JulianDate - AstroConstants.J2000;

    public DateTime UtcDateTime => ToUtc(JulianDate);

    public void Advance(double realSecondsElapsed)
    {
        if (IsPaused || realSecondsElapsed == 0.0)
        {
            return;
        }

        JulianDate += realSecondsElapsed * SpeedMultiplier / AstroConstants.SecondsPerDay;
    }

    public void JumpTo(double julianDate) => JulianDate = julianDate;

    public void JumpTo(DateTime utc) => JulianDate = ToJulianDate(utc);

    public void ResetToEpoch() => JulianDate = AstroConstants.J2000;

    /// <summary>
    /// Conversão ancorada na época J2000.0 em vez do algoritmo clássico de calendário:
    /// evita as armadilhas de borda da reforma gregoriana, que são irrelevantes aqui.
    /// Ignora segundos intercalares, ou seja, trata UTC como escala uniforme.
    /// </summary>
    public static double ToJulianDate(DateTime utc)
    {
        var asUtc = utc.Kind == DateTimeKind.Utc ? utc : utc.ToUniversalTime();
        return AstroConstants.J2000 + (asUtc - J2000Epoch).TotalDays;
    }

    public static DateTime ToUtc(double julianDate)
        => J2000Epoch.AddDays(julianDate - AstroConstants.J2000);
}
