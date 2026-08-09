using SolarSim.Bridge;
using SolarSim.Engine.Core;
using SolarSim.Engine.Models;

namespace SolarSim.Tests;

/// <summary>
/// O limite de Roche, o destino dos satélites e a zona de anel.
/// </summary>
public sealed class RocheLimitTests
{
    /// <summary>
    /// Fobos é o caso de ouro do marco: orbita entre os dois limites de Marte, e é por isso
    /// que a superfície dele é sulcada e que ele vai virar um anel. Se o modelo o desse
    /// como estável, ou como já desfeito, não estaria descrevendo nada.
    /// </summary>
    [Fact]
    public void FobosEstaEntreOsDoisLimitesDeRoche()
    {
        var sim = SolarSystem.NewEngine();
        var mare = sim.TidesOn("phobos", sim.Time.JulianDate);

        Assert.Equal(SatelliteFate.AtRisk, mare.Fate);
        Assert.InRange(mare.PeriapsisKm, mare.RigidLimitKm, mare.FluidLimitKm);
    }

    /// <summary>
    /// O limite fluido de Marte para Fobos é publicado em torno de 10.600 km, e o rígido em
    /// torno de 5.500 km. São os dois números que sustentam o veredito acima.
    /// </summary>
    [Fact]
    public void OsLimitesDeMarteParaFobosBatemComOsPublicados()
    {
        var mare = SolarSystem.NewEngine().TidesOn("phobos", SolarSystem.J2000);

        Assert.Equal(10_600.0, mare.FluidLimitKm, 300.0);
        Assert.Equal(5_500.0, mare.RigidLimitKm, 200.0);
    }

    /// <summary>
    /// Deimos é o controle. Mesma origem e densidade da mesma ordem que a de Fobos, ao
    /// dobro e meio da distância — e estável. É o que mostra que o destino sai da órbita, e
    /// não do material de que o corpo é feito.
    /// </summary>
    [Fact]
    public void DeimosEstaEstavelPeloDobroDaDistancia()
    {
        var sim = SolarSystem.NewEngine();

        var fobos = sim.TidesOn("phobos", sim.Time.JulianDate);
        var deimos = sim.TidesOn("deimos", sim.Time.JulianDate);

        Assert.Equal(SatelliteFate.Stable, deimos.Fate);
        Assert.True(deimos.MarginOverFluid > 1.0);
        Assert.True(fobos.MarginOverFluid < 1.0);

        // Os dois são pilhas de escombros de densidade parecida — 1,88 e 1,47 g/cm³ —, e
        // mesmo assim um se desmancha e o outro não. O que os separa é a distância.
        Assert.Equal(
            1.88,
            BodyMass.DensityFromMu(
                sim.BodyOf("phobos").MuKm3S2, sim.BodyOf("phobos").RadiusKm),
            0.1);

        Assert.Equal(
            1.47,
            BodyMass.DensityFromMu(
                sim.BodyOf("deimos").MuKm3S2, sim.BodyOf("deimos").RadiusKm),
            0.1);
    }

    [Theory]
    [InlineData("moon")]
    [InlineData("io")]
    [InlineData("europa")]
    [InlineData("ganymede")]
    [InlineData("callisto")]
    [InlineData("titan")]
    [InlineData("deimos")]
    public void AsLuasGrandesEstaoTodasForaDaZonaDeRoche(string bodyId)
    {
        var mare = SolarSystem.NewEngine().TidesOn(bodyId, SolarSystem.J2000);

        Assert.Equal(SatelliteFate.Stable, mare.Fate);
    }

    /// <summary>
    /// A Lua está a vinte vezes o limite de Roche da Terra. O número não é notável por si;
    /// serve para mostrar a escala do que "estável" quer dizer, contra o 0,87 de Fobos.
    /// </summary>
    [Fact]
    public void ALuaEstaVinteVezesAlemDoLimiteDaTerra()
    {
        var mare = SolarSystem.NewEngine().TidesOn("moon", SolarSystem.J2000);

        Assert.Equal(20.0, mare.MarginOverFluid, 2.0);
    }

    /// <summary>
    /// O limite fluido é o maior dos dois, sempre e por construção: o corpo que se deforma
    /// se entrega antes do que mantém a forma. A razão entre eles é 2,455/∛2, e não depende
    /// de corpo nenhum.
    /// </summary>
    [Theory]
    [InlineData("phobos")]
    [InlineData("moon")]
    [InlineData("titan")]
    public void OLimiteFluidoEhSempreMaiorQueORigidoPelaMesmaRazao(string bodyId)
    {
        var mare = SolarSystem.NewEngine().TidesOn(bodyId, SolarSystem.J2000);

        var esperada = RocheLimit.FluidCoefficient / Math.Cbrt(2.0);

        Assert.Equal(esperada, mare.FluidLimitKm / mare.RigidLimitKm, 1e-9);
    }

    /// <summary>
    /// Escrito em densidades, o limite pede o raio do pai; escrito em massas, ele se
    /// cancela. As duas formas têm de dar o mesmo número, e é o que prova que a álgebra da
    /// simplificação está certa.
    /// </summary>
    [Fact]
    public void AFormaEmMassasConcordaComAFormaEmDensidades()
    {
        var sim = SolarSystem.NewEngine();
        var marte = sim.BodyOf("mars");
        var fobos = sim.BodyOf("phobos");

        var densidadeMarte = BodyMass.DensityFromMu(marte.MuKm3S2, marte.RadiusKm);
        var densidadeFobos = BodyMass.DensityFromMu(fobos.MuKm3S2, fobos.RadiusKm);

        var emDensidades = RocheLimit.FluidCoefficient
            * marte.RadiusKm
            * Math.Cbrt(densidadeMarte / densidadeFobos);

        var emMassas = RocheLimit.FluidKm(fobos.RadiusKm, fobos.MuKm3S2, marte.MuKm3S2);

        Assert.Equal(emDensidades, emMassas, emMassas * 1e-9);
    }

    /// <summary>
    /// Corpo sem massa ou sem raio não tem limite, e a resposta é "não sei" e não zero: uma
    /// sonda não é um satélite prestes a se partir.
    /// </summary>
    [Fact]
    public void SondaNaoTemDestinoDeMare()
    {
        var sim = SolarSystem.NewEngine();

        sim.AddFromState(
            new CelestialBodyData
            {
                Id = "sonda",
                Name = "Sonda",
                ParentId = "mars",
                Kind = BodyKind.Spacecraft,
                RadiusKm = 0.0,
            },
            new StateVector(new Vector3D(6000.0, 0.0, 0.0), new Vector3D(0.0, 2.5, 0.0)),
            sim.Time.JulianDate);

        Assert.Equal(SatelliteFate.Unknown, sim.TidesOn("sonda", sim.Time.JulianDate).Fate);
    }

    /// <summary>
    /// A palavra sozinha mente por omissão nas duas pontas: "estável" vale tanto para a Lua,
    /// a vinte vezes o limite, quanto para um corpo que passa a um por cento dele. Por isso
    /// a folga acompanha.
    /// </summary>
    [Fact]
    public void OTextoDoDestinoTrazAFolgaJuntoDaPalavra()
    {
        var sim = SolarSystem.NewEngine();

        var fobos = DisplayFormat.Fate(sim.TidesOn("phobos", SolarSystem.J2000));
        var lua = DisplayFormat.Fate(sim.TidesOn("moon", SolarSystem.J2000));

        Assert.Contains("em risco", fobos, StringComparison.Ordinal);
        Assert.Contains("0,87", fobos, StringComparison.Ordinal);

        Assert.Contains("estável", lua, StringComparison.Ordinal);
    }

    /// <summary>
    /// O ensino tem de dizer o que está acontecendo com Fobos, e é o flag que leva isso ao
    /// HUD.
    /// </summary>
    [Fact]
    public void OEnsinoAnunciaQueFobosEstaDentroDoLimiteFluido()
    {
        var (sim, ambiente) = SolarSystem.NewEnvironment();

        var relatorio = ambiente.ReportFor(sim, "phobos");

        Assert.Contains("inside_fluid_roche", relatorio.ExplanationFlags);
        Assert.Contains(
            TeachingExplain.LinesFor(relatorio),
            line => line.Contains("Roche fluido", StringComparison.Ordinal));
    }

    [Fact]
    public void OEnsinoAnunciaAZonaDeAnelDeSaturno()
    {
        var (sim, ambiente) = SolarSystem.NewEnvironment();

        Assert.Contains("ring_zone", ambiente.ReportFor(sim, "saturn").ExplanationFlags);
        Assert.DoesNotContain("ring_zone", ambiente.ReportFor(sim, "earth").ExplanationFlags);
    }

    [Fact]
    public void ARaizNaoTemLimiteDeRoche()
    {
        var mare = SolarSystem.NewEngine().TidesOn("sun", SolarSystem.J2000);

        Assert.Equal(SatelliteFate.Unknown, mare.Fate);
        Assert.Equal(0.0, mare.FluidLimitKm);
    }

    /// <summary>
    /// O veredito é sobre o periápside, e não sobre o semi-eixo: um corpo em órbita
    /// excêntrica se parte no mergulho, mesmo que passe a maior parte do tempo em
    /// segurança. É o que aconteceu com o Shoemaker-Levy 9.
    /// </summary>
    [Fact]
    public void OrbitaExcentricaEhJulgadaPeloMergulhoENaoPelaMedia()
    {
        var sim = SolarSystem.NewEngine();
        var fluido = sim.TidesOn("deimos", SolarSystem.J2000).FluidLimitKm;

        // Semi-eixo com folga; periápside dentro do limite rígido.
        var rigido = sim.TidesOn("deimos", SolarSystem.J2000).RigidLimitKm;

        Assert.Equal(
            SatelliteFate.Disrupted,
            RocheLimit.FateAt(rigido * 0.5, rigido, fluido));

        Assert.Equal(
            SatelliteFate.AtRisk,
            RocheLimit.FateAt((rigido + fluido) / 2.0, rigido, fluido));

        Assert.Equal(
            SatelliteFate.Stable,
            RocheLimit.FateAt(fluido * 1.01, rigido, fluido));
    }
}
