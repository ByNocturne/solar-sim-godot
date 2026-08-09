using System.Globalization;
using SolarSim.Bridge;
using SolarSim.Engine.Core;
using SolarSim.Engine.Models;

namespace SolarSim.Tests;

/// <summary>
/// A tradução de número para texto na fronteira da tela.
/// </summary>
public sealed class DisplayFormatTests
{
    [Fact]
    public void OrbitaDeSateliteSaiEmQuilometros()
    {
        Assert.Equal("384.748 km", DisplayFormat.Distance(384_748.0));
        Assert.Equal("6.371 km", DisplayFormat.Distance(6371.0));
    }

    [Fact]
    public void OrbitaInterplanetariaSaiEmUnidadesAstronomicas()
    {
        Assert.Equal("1,0000 UA", DisplayFormat.Distance(AstroConstants.AstronomicalUnitKm));
        Assert.Equal(
            "30,0000 UA",
            DisplayFormat.Distance(30.0 * AstroConstants.AstronomicalUnitKm));
    }

    [Fact]
    public void DuracaoEscolheAUnidadePelaOrdemDeGrandeza()
    {
        Assert.Equal("12,00 h", DisplayFormat.Duration(0.5));
        Assert.Equal("27,32 d", DisplayFormat.Duration(27.32));
        Assert.Equal("164,79 anos", DisplayFormat.Duration(164.79 * 365.25));
    }

    [Fact]
    public void AnguloSaiEmGraus()
    {
        Assert.Equal("180,000°", DisplayFormat.Angle(Math.PI));
        Assert.Equal("0,000°", DisplayFormat.Angle(0.0));
    }

    /// <summary>
    /// A precessão sai em segundos de arco por século porque é a única unidade em que o
    /// número é legível: a mesma taxa em graus por segundo teria treze zeros.
    /// </summary>
    [Fact]
    public void PrecessaoSaiEmSegundosDeArcoPorSeculo()
    {
        var umSegundoDeArcoPorSeculo = AstroConstants.TwoPi
            / (360.0 * 3600.0)
            / AstroConstants.SecondsPerJulianCentury;

        Assert.Equal("1,00 ″/século", DisplayFormat.PrecessionRate(umSegundoDeArcoPorSeculo));
        Assert.Equal("-1,00 ″/século", DisplayFormat.PrecessionRate(-umSegundoDeArcoPorSeculo));
    }

    /// <summary>
    /// A unidade da literatura pressupõe a precessão lenta de um planeta. O periápside de
    /// Io dá uma volta a cada quatro anos, e em ″/século isso são trinta milhões: um
    /// número que ocupa a coluna inteira sem dizer nada. O tempo da volta diz.
    /// </summary>
    [Fact]
    public void PrecessaoRapidaSaiComoOTempoDeUmaVolta()
    {
        var quatroAnos = 4.0 * 365.25 * AstroConstants.SecondsPerDay;
        var umaVoltaEmQuatroAnos = AstroConstants.TwoPi / quatroAnos;

        Assert.Equal("1 volta / 4,00 anos", DisplayFormat.PrecessionRate(umaVoltaEmQuatroAnos));
    }

    /// <summary>
    /// Precessão retrógrada é a mesma volta ao contrário, e o sinal fica no número de
    /// voltas: um tempo negativo não existe.
    /// </summary>
    [Fact]
    public void PrecessaoRetrogradaRapidaLevaOSinalNaVolta()
    {
        var quatroAnos = 4.0 * 365.25 * AstroConstants.SecondsPerDay;

        Assert.Equal(
            "−1 volta / 4,00 anos",
            DisplayFormat.PrecessionRate(-AstroConstants.TwoPi / quatroAnos));
    }

    /// <summary>
    /// Órbita que não precessa mostra traço, e não "0,00 ″/século": zero exato ali quer
    /// dizer que o efeito não se aplica, não que ele foi medido e deu zero.
    /// </summary>
    [Fact]
    public void PrecessaoNulaViraTraco()
    {
        Assert.Equal(DisplayFormat.Absent, DisplayFormat.PrecessionRate(0.0));
    }

    [Fact]
    public void TaxaDeTempoDizQuantoAvancaPorSegundoReal()
    {
        Assert.Equal("1,00 d/s", DisplayFormat.TimeRate(AstroConstants.SecondsPerDay));
        Assert.Equal("-1,00 d/s", DisplayFormat.TimeRate(-AstroConstants.SecondsPerDay));
    }

    /// <summary>
    /// A órbita aberta não tem apoápside nem período, e o retrato diz isso com infinito.
    /// Formatado como número, ele viraria "∞ UA" ou pior, "NaN": o traço é a forma de a
    /// tela admitir que ali não há valor a mostrar.
    /// </summary>
    [Fact]
    public void ValorQueNaoEFinitoSaiComoTraco()
    {
        Assert.Equal("—", DisplayFormat.Distance(double.PositiveInfinity));
        Assert.Equal("—", DisplayFormat.Duration(double.PositiveInfinity));
        Assert.Equal("—", DisplayFormat.Speed(double.NaN));
        Assert.Equal("—", DisplayFormat.Angle(double.NaN));
        Assert.Equal("—", DisplayFormat.Ratio(double.PositiveInfinity));
        Assert.Equal("—", DisplayFormat.Temperature(double.NaN));
        Assert.Equal("—", DisplayFormat.Pressure(double.PositiveInfinity));
    }

    [Fact]
    public void Temperatura_pressao_e_bhi()
    {
        Assert.Equal("288,0 K", DisplayFormat.Temperature(288.0));
        Assert.Equal("1,01 bar", DisplayFormat.Pressure(101_325.0));
        Assert.Equal("610 Pa", DisplayFormat.Pressure(610.0));
        Assert.Equal("0,92", DisplayFormat.HabitabilityIndex(0.92));
    }

    [Fact]
    public void TeachingExplain_covers_earth_flags()
    {
        var (sim, env) = SolarSystem.NewEnvironment();
        var report = env.ReportFor(sim, "earth");
        var lines = TeachingExplain.LinesFor(report);
        Assert.Contains(lines, l => l.Contains("BHI", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains("água", StringComparison.Ordinal));
    }

    [Fact]
    public void ClassificacaoJuntaClasseEFamiliaQuandoElasDizemCoisasDiferentes()
        => Assert.Equal(
            "Asteroide · Cinturão principal",
            DisplayFormat.Classification(BodyKind.Asteroid, "Cinturão principal"));

    /// <summary>
    /// "Troiano · Troiano de Júpiter (L4)" gastaria duas linhas da ficha para gaguejar.
    /// </summary>
    [Fact]
    public void ClassificacaoNaoRepeteAClasseQueAFamiliaJaDiz()
    {
        Assert.Equal(
            "Troiano de Júpiter (L4)",
            DisplayFormat.Classification(BodyKind.Trojan, "Troiano de Júpiter (L4)"));

        Assert.Equal("Centauro", DisplayFormat.Classification(BodyKind.Centaur, "Centauro"));
    }

    [Fact]
    public void ClassificacaoSemFamiliaEhSoAClasse()
        => Assert.Equal("Cometa", DisplayFormat.Classification(BodyKind.Comet, null));

    /// <summary>
    /// Corpo menor tem GM abaixo de um, e arredondá-lo para inteiro seria escrever que o
    /// corpo não tem massa logo acima de uma esfera de influência que existe.
    /// </summary>
    [Fact]
    public void GmDeCorpoPequenoNaoEhArredondadoParaZero()
    {
        Assert.Equal("0,398 km³/s²", DisplayFormat.GravitationalParameter(0.3984));
        Assert.Equal("62,628 km³/s²", DisplayFormat.GravitationalParameter(62.6284));
    }

    [Fact]
    public void GmDeCorpoGrandeContinuaInteiro()
    {
        Assert.Equal("398.600 km³/s²", DisplayFormat.GravitationalParameter(398_600.4));
        Assert.Equal(
            "132.712.440.018 km³/s²",
            DisplayFormat.GravitationalParameter(132_712_440_018.0));
    }

    /// <summary>
    /// O painel precisa mostrar a mesma coisa na máquina de quem desenvolve e na de quem
    /// joga. Sem separadores próprios, um computador configurado em inglês trocaria a
    /// vírgula decimal por ponto e o número mudaria de significado por três ordens de
    /// grandeza.
    /// </summary>
    [Theory]
    [InlineData("en-US")]
    [InlineData("de-DE")]
    [InlineData("pt-BR")]
    [InlineData("")]
    public void FormatacaoNaoDependeDaCulturaDoSistema(string cultura)
    {
        var anterior = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultura);

            Assert.Equal("1.234.567 km", DisplayFormat.Distance(1_234_567.0));
            Assert.Equal("29,780 km/s", DisplayFormat.Speed(29.78));
            Assert.Equal("0,0167", DisplayFormat.Ratio(0.01671123));
            Assert.Equal("2.000.000x", DisplayFormat.Multiplier(2.0e6));
        }
        finally
        {
            CultureInfo.CurrentCulture = anterior;
        }
    }
}
