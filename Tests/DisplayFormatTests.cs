using System.Globalization;
using SolarSim.Bridge;
using SolarSim.Engine.Core;

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
