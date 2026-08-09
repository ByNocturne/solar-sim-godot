using SolarSim.Bridge;
using SolarSim.Engine.Data;
using SolarSim.Engine.Models;

namespace SolarSim.Tests;

/// <summary>
/// O filtro de classes e a promessa que ele acompanha: o catálogo não mexe na escala do
/// Sistema Solar.
/// </summary>
public sealed class BodyFilterTests
{
    [Fact]
    public void OferecceApenasAsClassesMenoresQueOsDadosContem()
    {
        var filtro = new BodyFilter(SolarSystem.NewEngineWithCatalog().Bodies);

        Assert.All(filtro.Available, kind => Assert.True(BodyKinds.IsMinor(kind)));
        Assert.Contains(BodyKind.Comet, filtro.Available);
        Assert.DoesNotContain(BodyKind.Planet, filtro.Available);
    }

    [Fact]
    public void SemCatalogoNaoHaNadaAFiltrar()
    {
        var filtro = new BodyFilter(SolarSystem.NewEngine().Bodies);

        Assert.Empty(filtro.Available);
    }

    [Fact]
    public void TudoComecaVisivel()
    {
        var corpos = SolarSystem.NewEngineWithCatalog().Bodies;
        var filtro = new BodyFilter(corpos);

        Assert.Equal(corpos.Count, filtro.Apply(corpos).Count());
    }

    [Fact]
    public void EsconderUmaClasseTiraSoOsCorposDela()
    {
        var corpos = SolarSystem.NewEngineWithCatalog().Bodies;
        var filtro = new BodyFilter(corpos);

        filtro.SetVisible(BodyKind.Comet, false);

        var visiveis = filtro.Apply(corpos).Select(body => body.Id).ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("halley", visiveis);
        Assert.Contains("ceres", visiveis);
        Assert.Contains("earth", visiveis);
    }

    /// <summary>
    /// O Sistema Solar não é filtrável: pedir para esconder planetas não faz nada, em vez
    /// de deixar a tela vazia sem um botão para desfazer.
    /// </summary>
    [Fact]
    public void ClasseQueNaoEhDeCorpoMenorNaoSeEsconde()
    {
        var corpos = SolarSystem.NewEngineWithCatalog().Bodies;
        var filtro = new BodyFilter(corpos);

        filtro.SetVisible(BodyKind.Planet, false);
        filtro.SetVisible(BodyKind.Spacecraft, false);

        Assert.True(filtro.IsVisible(BodyKind.Planet));
        Assert.True(filtro.IsVisible(BodyKind.Spacecraft));
    }

    [Fact]
    public void MudarAVisibilidadeAvisaUmaVezPorMudancaDeFato()
    {
        var filtro = new BodyFilter(SolarSystem.NewEngineWithCatalog().Bodies);
        var avisos = 0;

        filtro.Changed += () => avisos++;

        filtro.SetVisible(BodyKind.Trojan, false);
        filtro.SetVisible(BodyKind.Trojan, false);
        filtro.SetVisible(BodyKind.Trojan, true);

        Assert.Equal(2, avisos);
    }

    /// <summary>
    /// O teste que fixa a decisão de escala do M16: o catálogo entra e a órbita da Terra
    /// não anda um milésimo de pixel.
    /// </summary>
    /// <remarks>
    /// A curva perceptual é normalizada pela maior órbita do nível. Com Sedna dentro do
    /// cálculo, cujo afélio passa de mil unidades astronômicas, o Sistema Solar inteiro
    /// encolheria para acomodar um ponto que passa onze mil anos longe demais para ser
    /// visto. Como corpo menor não dimensiona nível, isso não acontece — e é uma
    /// propriedade que precisa de guardião, porque a alternativa não quebra nada: apenas
    /// deixa a tela pior sem explicação.
    /// </remarks>
    [Fact]
    public void AcrescentarOCatalogoNaoMexeNaEscalaDoSistemaSolar()
    {
        const double alturaJanela = 1080.0;

        var semCatalogo = new ScaleLayout(SolarSystem.NewEngine().Bodies, alturaJanela);
        var comCatalogo = new ScaleLayout(SolarSystem.NewEngineWithCatalog().Bodies, alturaJanela);

        Assert.Equal(semCatalogo.LevelOf("sun"), comCatalogo.LevelOf("sun"));
        Assert.Equal(semCatalogo.LevelOf("earth"), comCatalogo.LevelOf("earth"));
        Assert.Equal(semCatalogo.LevelOf("jupiter"), comCatalogo.LevelOf("jupiter"));
    }

    /// <summary>
    /// O nível do Sol continua sendo o de Netuno, e não o de Sedna. É a mesma decisão
    /// vista pelo número em vez de pela comparação.
    /// </summary>
    [Fact]
    public void ONivelDoSolContinuaDimensionadoPorNetuno()
    {
        var sim = SolarSystem.NewEngineWithCatalog();
        var netuno = sim.ElementsOf("neptune")!.Value;
        var apoapsisDeNetuno = netuno.SemiMajorAxisKm * (1.0 + netuno.Eccentricity);

        var nivel = new ScaleLayout(sim.Bodies, 1080.0).LevelOf("sun");

        Assert.Equal(apoapsisDeNetuno, nivel.MaxDistanceKm, apoapsisDeNetuno * 1e-12);
    }

    /// <summary>
    /// Fora do dimensionamento não é fora da tela: o corpo distante continua sendo
    /// projetado, apenas além do raio nominal do nível.
    /// </summary>
    [Fact]
    public void CorpoDistanteContinuaSendoDesenhadoAlemDoRaioDoNivel()
    {
        var sim = SolarSystem.NewEngineWithCatalog();
        var nivel = new ScaleLayout(sim.Bodies, 1080.0).LevelOf("sun");
        var mapa = new ScaleMapper();

        var sedna = sim.ElementsOf("sedna")!.Value;
        var raio = mapa.OrbitRadiusPixels(sedna.ApoapsisKm, nivel);

        Assert.True(raio > nivel.ScreenRadiusPixels);
        Assert.True(double.IsFinite(raio));
    }

    [Fact]
    public void SondaEntraComoSondaESobreviveAoSaveLoad()
    {
        var sim = SolarSystem.NewEngineWithCatalog();

        var estado = new StateVector(
            new Vector3D(10_000.0, 0.0, 0.0), new Vector3D(0.0, 6.0, 1.0));

        sim.AddFromState(
            new CelestialBodyData
            {
                Id = "sonda",
                Name = "Sonda",
                ParentId = "earth",
                Kind = BodyKind.Spacecraft,
                RadiusKm = 0.0,
            },
            estado,
            sim.Time.JulianDate);

        Assert.Equal(BodyKind.Spacecraft, sim.BodyOf("sonda").Kind);

        var recarregado = SolarSystem.NewEngineWithCatalog();
        SaveState.Restore(recarregado, SaveState.Serialize(sim));

        Assert.Equal(BodyKind.Spacecraft, recarregado.BodyOf("sonda").Kind);
    }
}
