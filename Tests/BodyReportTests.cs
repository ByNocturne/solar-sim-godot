using SolarSim.Bridge;
using SolarSim.Engine.Core;
using SolarSim.Engine.Models;

namespace SolarSim.Tests;

/// <summary>
/// O retrato que alimenta o inspetor. Ele é a fronteira entre o motor e a tela: se um
/// número aparece errado no painel, ou o erro está aqui, ou está no motor — e o motor
/// já tem os seus próprios testes.
/// </summary>
public sealed class BodyReportTests
{
    [Fact]
    public void RaizNaoTemOrbitaNemPai()
    {
        var sim = SolarSystem.NewEngine();

        var sol = BodyReport.For(sim, "sun", AstroConstants.J2000);

        Assert.True(sol.IsRoot);
        Assert.Null(sol.Elements);
        Assert.Null(sol.ParentName);
        Assert.Equal("Sol", sol.RootName);
        Assert.Equal(0.0, sol.DistanceToRootKm);
        Assert.Equal(0.0, sol.SpeedRelativeToRootKmS);
    }

    [Fact]
    public void TerraOrbitaOSolAUmaUnidadeAstronomica()
    {
        var sim = SolarSystem.NewEngine();

        var terra = BodyReport.For(sim, "earth", AstroConstants.J2000);

        Assert.Equal("Sol", terra.ParentName);
        Assert.InRange(terra.DistanceToRootKm / AstroConstants.AstronomicalUnitKm, 0.98, 1.02);
        Assert.InRange(terra.PeriodDays, 365.0, 365.5);
        Assert.InRange(terra.SpeedRelativeToRootKmS, 29.0, 30.5);
    }

    /// <summary>
    /// A distinção que dá sentido ao painel: a Lua faz 1 km/s em torno da Terra enquanto
    /// faz 30 km/s em torno do Sol. Mostrar só um dos dois números esconde metade do que
    /// está acontecendo.
    /// </summary>
    [Fact]
    public void LuaSeparaAVelocidadeEmTornoDaTerraDaVelocidadeEmTornoDoSol()
    {
        var sim = SolarSystem.NewEngine();

        var lua = BodyReport.For(sim, "moon", AstroConstants.J2000);

        Assert.Equal("Terra", lua.ParentName);
        Assert.InRange(lua.SpeedRelativeToParentKmS, 0.95, 1.10);
        Assert.InRange(lua.SpeedRelativeToRootKmS, 28.0, 32.0);
        Assert.InRange(lua.DistanceToParentKm, 363_000.0, 406_000.0);
        Assert.InRange(lua.DistanceToRootKm / AstroConstants.AstronomicalUnitKm, 0.98, 1.02);
    }

    [Fact]
    public void PeriodoDaLuaEOMesSideral()
    {
        var sim = SolarSystem.NewEngine();

        Assert.Equal(27.32, BodyReport.For(sim, "moon", AstroConstants.J2000).PeriodDays, 0.05);
    }

    /// <summary>
    /// Periápside e apoápside são o que o painel promete como limites da órbita. Se a
    /// distância instantânea saísse dessa faixa, o painel estaria mentindo.
    /// </summary>
    [Fact]
    public void DistanciaAoPaiFicaEntrePeriapsideEApoapside()
    {
        var sim = SolarSystem.NewEngine();

        foreach (var corpo in sim.Bodies.Where(body => body.ParentId is not null))
        {
            for (var dia = 0; dia < 4000; dia += 7)
            {
                var relatorio = BodyReport.For(sim, corpo.Id, AstroConstants.J2000 + dia);

                Assert.InRange(
                    relatorio.DistanceToParentKm,
                    relatorio.PeriapsisKm - 1.0,
                    relatorio.ApoapsisKm + 1.0);
            }
        }
    }

    /// <summary>
    /// O painel mostra a órbita de hoje, não a de J2000: é isso que faz o argumento do
    /// periápside de Mercúrio se mexer conforme o tempo corre, em vez de ficar preso no
    /// valor tabelado.
    /// </summary>
    [Fact]
    public void OInspetorMostraOPeriapsideJaPrecessado()
    {
        var sim = SolarSystem.NewEngine();

        var naEpoca = BodyReport.For(sim, "mercury", AstroConstants.J2000);

        var umSeculoDepois = BodyReport.For(
            sim, "mercury", AstroConstants.J2000 + AstroConstants.DaysPerJulianCentury);

        Assert.NotEqual(
            naEpoca.Elements!.Value.ArgumentOfPeriapsisRad,
            umSeculoDepois.Elements!.Value.ArgumentOfPeriapsisRad);

        // O mesmo número que a linha de precessão do painel exibe, e que o teste do motor
        // fixa em 43 segundos de arco por século.
        Assert.InRange(
            AstroConstants.RadPerSecondToArcsecPerCentury(
                naEpoca.ApsidalPrecessionRadPerSecond),
            42.5,
            43.5);
    }

    /// <summary>
    /// Corpo sem precessão declara zero, e o painel troca isso por um traço em vez de
    /// exibir uma taxa que não existe.
    /// </summary>
    [Fact]
    public void SondaNaoTemPrecessaoADeclarar()
    {
        var sim = SolarSystem.NewEngine();

        sim.AddFromState(
            new CelestialBodyData
            {
                Id = "probe1",
                Name = "Sonda",
                ParentId = "earth",
                MuKm3S2 = 0.0,
                RadiusKm = 0.0,
            },
            new StateVector(new Vector3D(20_000.0, 0.0, 0.0), new Vector3D(0.0, 4.0, 0.0)),
            AstroConstants.J2000);

        var sonda = BodyReport.For(sim, "probe1", AstroConstants.J2000);

        Assert.Equal(0.0, sonda.ApsidalPrecessionRadPerSecond);
    }

    /// <summary>
    /// A anomalia verdadeira mostrada tem que ser a do ponto onde o corpo está, e não um
    /// ângulo qualquer que cresce com o tempo. A equação da cônica amarra as duas coisas.
    /// </summary>
    [Fact]
    public void AnomaliaVerdadeiraConcordaComAEquacaoDaConica()
    {
        var sim = SolarSystem.NewEngine();

        foreach (var corpo in sim.Bodies.Where(body => body.ParentId is not null))
        {
            for (var dia = 0; dia < 900; dia += 11)
            {
                var relatorio = BodyReport.For(sim, corpo.Id, AstroConstants.J2000 + dia);
                var elementos = relatorio.Elements!.Value;

                var semiLatusRectum = elementos.SemiMajorAxisKm
                    * (1.0 - (elementos.Eccentricity * elementos.Eccentricity));

                var esperado = semiLatusRectum
                    / (1.0 + (elementos.Eccentricity * Math.Cos(relatorio.TrueAnomalyRad)));

                Assert.Equal(esperado, relatorio.DistanceToParentKm, esperado * 1e-9);
            }
        }
    }

    /// <summary>
    /// A esfera de influência da Terra tem cerca de 925 mil km, e é o número que decide
    /// quando uma sonda deixa de orbitar a Terra. Mostrá-lo no painel é o que torna a
    /// emenda de cônicas previsível para quem está pilotando.
    /// </summary>
    [Fact]
    public void EsferaDeInfluenciaAparecoNoRetratoDoPlaneta()
    {
        var sim = SolarSystem.NewEngine();

        var terra = BodyReport.For(sim, "earth", AstroConstants.J2000);

        Assert.Equal(925_000.0, terra.SphereOfInfluenceKm, 15_000.0);
        Assert.False(terra.IsDynamic);
    }

    /// <summary>
    /// Uma órbita aberta não tem volta a completar nem ponto mais distante. O retrato diz
    /// isso com infinito, e cabe à formatação transformá-lo em traço na tela — inventar
    /// um número finito aqui seria mentir com precisão.
    /// </summary>
    [Fact]
    public void SondaEmFugaNaoTemPeriodoNemApoapside()
    {
        var sim = SolarSystem.NewEngine();
        var julianDate = sim.Time.JulianDate;

        const double raioKm = 6_778.0;
        var escapeKmS = Math.Sqrt(2.0 * 398_600.435436 / raioKm);

        sim.AddFromState(
            new CelestialBodyData
            {
                Id = "sonda",
                Name = "Sonda",
                ParentId = "earth",
                MuKm3S2 = 0.0,
                RadiusKm = 0.0,
                ColorRgb = 0xFFFFFF,
            },
            new StateVector(
                new Vector3D(raioKm, 0.0, 0.0),
                new Vector3D(0.0, escapeKmS * 1.2, 0.0)),
            julianDate);

        var sonda = BodyReport.For(sim, "sonda", julianDate);

        Assert.True(sonda.IsDynamic);
        Assert.True(sonda.Elements!.Value.Eccentricity > 1.0);
        Assert.Equal(double.PositiveInfinity, sonda.PeriodDays);
        Assert.Equal(double.PositiveInfinity, sonda.ApoapsisKm);
        Assert.Equal(raioKm, sonda.PeriapsisKm, 1e-6);

        // Sem massa não há esfera: a sonda é atraída, e não atrai.
        Assert.Equal(0.0, sonda.SphereOfInfluenceKm);
    }

    [Fact]
    public void ConsultarNaoMexeNoRelogio()
    {
        var sim = SolarSystem.NewEngine();

        BodyReport.For(sim, "neptune", AstroConstants.J2000 + 50_000.0);

        Assert.Equal(AstroConstants.J2000, sim.Time.JulianDate);
    }

    [Fact]
    public void CorpoDesconhecidoFalhaComMensagemClara()
    {
        var sim = SolarSystem.NewEngine();

        var erro = Assert.Throws<KeyNotFoundException>(
            () => BodyReport.For(sim, "plutao", AstroConstants.J2000));

        Assert.Contains("plutao", erro.Message, StringComparison.Ordinal);
    }
}
