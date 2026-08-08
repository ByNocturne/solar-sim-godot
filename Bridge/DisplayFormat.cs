using System.Globalization;
using SolarSim.Engine.Core;

namespace SolarSim.Bridge;

/// <summary>
/// Números do domínio em texto para a tela. Escolhe a unidade que deixa o valor legível
/// e escreve o símbolo dela junto.
/// </summary>
/// <remarks>
/// Fica na Bridge pelo mesmo motivo que a escala: é conversão de unidade na fronteira. O
/// motor trabalha só em km, segundos e radianos, e não deve saber que existe uma tela —
/// nem em que idioma ela está.
/// </remarks>
public static class DisplayFormat
{
    /// <summary>
    /// O que aparece no lugar de um número que não existe. É o caso do apoápside de uma
    /// órbita aberta e do período de quem nunca volta: escrever "infinito" seria correto
    /// e ilegível numa coluna de números.
    /// </summary>
    public const string Absent = "—";

    /// <summary>Um ano juliano, que é a unidade em que períodos longos se leem melhor.</summary>
    private const double DaysPerJulianYear = 365.25;

    /// <summary>Acima disso, a distância se lê em unidades astronômicas.</summary>
    private const double AstronomicalUnitThresholdKm = 0.01 * AstroConstants.AstronomicalUnitKm;

    /// <summary>
    /// Separadores fixos em vez dos da cultura do sistema. A saída precisa ser a mesma na
    /// máquina de quem desenvolve e na de quem joga, e os testes precisam poder afirmar
    /// qual é ela.
    /// </summary>
    private static readonly NumberFormatInfo Numbers = new()
    {
        NumberDecimalSeparator = ",",
        NumberGroupSeparator = ".",
        NumberGroupSizes = [3],
    };

    /// <summary>
    /// Distância em km ou em unidades astronômicas. O corte fica em 0,01 UA porque é
    /// onde as órbitas de satélites acabam e as interplanetárias começam: a da Lua sai
    /// em quilômetros, a da Terra em UA.
    /// </summary>
    public static string Distance(double km)
        => Math.Abs(km) >= AstronomicalUnitThresholdKm
            ? Format(km / AstroConstants.AstronomicalUnitKm, "N4", " UA")
            : Format(km, "N0", " km");

    public static string Speed(double kmPerSecond) => Format(kmPerSecond, "N3", " km/s");

    /// <summary>Duração em horas, dias ou anos, conforme a ordem de grandeza.</summary>
    public static string Duration(double days)
        => Math.Abs(days) switch
        {
            < 1.0 => Format(days * 24.0, "N2", " h"),
            < 1000.0 => Format(days, "N2", " d"),
            _ => Format(days / DaysPerJulianYear, "N2", " anos"),
        };

    /// <summary>Ângulo em graus, que é como todo livro de astrodinâmica publica.</summary>
    public static string Angle(double radians)
        => Format(AstroConstants.RadiansToDegrees(radians), "N3", "°");

    public static string GravitationalParameter(double muKm3S2)
        => Format(muKm3S2, "N0", " km³/s²");

    /// <summary>Grandeza adimensional, como a excentricidade.</summary>
    public static string Ratio(double value) => Format(value, "N4");

    public static string JulianDate(double julianDate) => "JD " + Format(julianDate, "N3");

    /// <summary>Quantas vezes o tempo simulado corre mais rápido que o real.</summary>
    public static string Multiplier(double multiplier) => Format(multiplier, "N0", "x");

    /// <summary>
    /// Quanto tempo simulado passa por segundo real. É o número que diz alguma coisa: um
    /// multiplicador de dois milhões não informa nada, "23 d/s" informa.
    /// </summary>
    public static string TimeRate(double multiplier)
        => Duration(multiplier / AstroConstants.SecondsPerDay) + "/s";

    /// <summary>
    /// Um número que não é finito não vira texto: vira traço. O apoápside de uma
    /// hipérbole é infinito de verdade, e a alternativa seria a coluna exibir "∞" ou, pior,
    /// "NaN".
    /// </summary>
    private static string Format(double value, string format, string unit = "")
        => double.IsFinite(value) ? value.ToString(format, Numbers) + unit : Absent;
}
