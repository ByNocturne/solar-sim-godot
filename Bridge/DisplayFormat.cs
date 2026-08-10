using System.Globalization;
using SolarSim.Engine.Core;
using SolarSim.Engine.Models;

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

    /// <summary>
    /// GM em km³/s². A casa decimal aparece quando o corpo é pequeno o bastante para
    /// precisar dela.
    /// </summary>
    /// <remarks>
    /// Arredondar sempre para inteiro servia enquanto o menor corpo do arquivo era a Lua,
    /// com GM de 4.903. O catálogo de corpos menores trouxe Héctor, cujo GM derivado é
    /// 0,39 — e "0 km³/s²" não é um número arredondado, é a afirmação falsa de que o corpo
    /// não tem massa, logo acima de uma esfera de influência que existe.
    /// </remarks>
    public static string GravitationalParameter(double muKm3S2)
        => Format(muKm3S2, Math.Abs(muKm3S2) < 100.0 ? "N3" : "N0", " km³/s²");

    /// <summary>
    /// Abaixo disso a precessão se lê melhor como o tempo de uma volta inteira. O corte
    /// fica em dez mil anos porque é onde as duas leituras trocam de lado: a de Io leva
    /// quatro anos e sai como volta, a da Lua leva oitenta mil e sai em ″/século.
    /// </summary>
    private const double TurnAsDurationThresholdDays = 10_000.0 * DaysPerJulianYear;

    /// <summary>
    /// Taxa de precessão em segundos de arco por século, que é a unidade em que a
    /// literatura publica o número — e a única em que 43 é um valor legível: em graus por
    /// segundo, a precessão de Mercúrio seria 0,0000000000000038.
    /// </summary>
    /// <remarks>
    /// Só que a unidade da literatura pressupõe a precessão lenta de um planeta. O
    /// periápside de Io, empurrado pelo J₂ de Júpiter, dá uma volta a cada quatro anos:
    /// em ″/século isso são trinta milhões, um número que não se lê. Quando a volta cabe
    /// numa vida humana, o tempo dela é a leitura honesta.
    /// </remarks>
    public static string PrecessionRate(double radPerSecond)
    {
        if (radPerSecond == 0.0 || !double.IsFinite(radPerSecond))
        {
            return Absent;
        }

        var turnDays =
            AstroConstants.TwoPi / Math.Abs(radPerSecond) / AstroConstants.SecondsPerDay;

        if (turnDays > TurnAsDurationThresholdDays)
        {
            return Format(
                AstroConstants.RadPerSecondToArcsecPerCentury(radPerSecond),
                "N2",
                " ″/século");
        }

        // O sinal fica no número de voltas, e não no tempo: uma volta a cada tanto tempo,
        // ou menos uma volta, que é a mesma volta ao contrário.
        var turns = radPerSecond < 0.0 ? "−1 volta / " : "1 volta / ";

        return turns + Duration(turnDays);
    }

    /// <summary>
    /// Drift secular do semi-eixo maior. A unidade é a da literatura do Yarkovsky —
    /// UA por milhão de anos —, porque em km/s o número de Bennu seria −9×10⁻⁹.
    /// </summary>
    public static string SemiMajorAxisDrift(double kmPerSecond)
    {
        if (kmPerSecond == 0.0 || !double.IsFinite(kmPerSecond))
        {
            return Absent;
        }

        var auPerMyr = kmPerSecond
            * AstroConstants.YearsPerMillion
            * AstroConstants.SecondsPerJulianYear
            / AstroConstants.AstronomicalUnitKm;

        return Format(auPerMyr, "N4", " UA/Myr");
    }

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

    public static string Temperature(double kelvin)
        => Format(kelvin, "N1", " K");

    public static string Pressure(double pascals)
    {
        if (!double.IsFinite(pascals))
        {
            return Absent;
        }

        if (Math.Abs(pascals) >= 1.0e4)
        {
            return Format(pascals / 1.0e5, "N2", " bar");
        }

        return Format(pascals, "N0", " Pa");
    }

    public static string HabitabilityIndex(double bhi)
        => Format(Math.Clamp(bhi, 0.0, 1.0), "N2");

    public static string RadiationRelative(double relative)
        => Format(relative, "N2", "× Terra");

    /// <summary>
    /// A classe do corpo em português. A tradução mora aqui, e não no enumerador, porque
    /// o enumerador é domínio e o texto é fronteira: o mesmo motor precisa poder falar
    /// outra língua sem que <see cref="BodyKind"/> mude.
    /// </summary>
    public static string Kind(BodyKind kind) => kind switch
    {
        BodyKind.Star => "Estrela",
        BodyKind.Planet => "Planeta",
        BodyKind.Moon => "Satélite",
        BodyKind.Asteroid => "Asteroide",
        BodyKind.NearEarthAsteroid => "Asteroide próximo da Terra",
        BodyKind.Trojan => "Troiano",
        BodyKind.Centaur => "Centauro",
        BodyKind.Comet => "Cometa",
        BodyKind.TransNeptunian => "Transnetuniano",
        BodyKind.Spacecraft => "Sonda",
        _ => Absent,
    };

    /// <summary>
    /// Classe e família na mesma linha, sem repetir a classe quando a família já a diz.
    /// </summary>
    /// <remarks>
    /// A família é o que o corpo menor tem de particular, e quase sempre acrescenta à
    /// classe: "Asteroide · Cinturão principal" diz duas coisas. Mas "Troiano de Júpiter
    /// (L4)" já começa dizendo troiano, e "Troiano · Troiano de Júpiter (L4)" gasta uma
    /// linha inteira para gaguejar.
    /// </remarks>
    public static string Classification(BodyKind kind, string? family)
    {
        var name = Kind(kind);

        if (family is null || family.Length == 0)
        {
            return name;
        }

        if (name == Absent || family.StartsWith(name, StringComparison.OrdinalIgnoreCase))
        {
            return family;
        }

        return $"{name} · {family}";
    }

    /// <summary>
    /// O destino de um satélite sob a maré do pai, com a folga que o sustenta.
    /// </summary>
    /// <remarks>
    /// O número acompanha a palavra porque sozinha ela mente por omissão nas duas pontas:
    /// "estável" vale tanto para a Lua, a vinte vezes o limite, quanto para um corpo que
    /// passa a um por cento dele. A folga é sobre o limite fluido, que é o que decide
    /// primeiro.
    /// </remarks>
    public static string Fate(in SatelliteTides tides)
    {
        var name = tides.Fate switch
        {
            SatelliteFate.Stable => "estável",
            SatelliteFate.AtRisk => "em risco",
            SatelliteFate.Disrupted => "desfeito",
            _ => Absent,
        };

        if (tides.Fate == SatelliteFate.Unknown)
        {
            return name;
        }

        return $"{name} ({Format(tides.MarginOverFluid, "N2")}× Roche)";
    }

    /// <summary>A faixa de anel possível, em raios do corpo.</summary>
    public static string RingZone(in RingZone zone)
    {
        if (!zone.HasRoom)
        {
            return Absent;
        }

        var outer = zone.OuterRadiusKm / zone.InnerRadiusKm;

        var span = $"até {Format(outer, "N2")} raios";

        return zone.IsPlausible ? span : $"{span} (sem gelo)";
    }

    /// <summary>
    /// Um número que não é finito não vira texto: vira traço. O apoápside de uma
    /// hipérbole é infinito de verdade, e a alternativa seria a coluna exibir "∞" ou, pior,
    /// "NaN".
    /// </summary>
    private static string Format(double value, string format, string unit = "")
        => double.IsFinite(value) ? value.ToString(format, Numbers) + unit : Absent;
}
