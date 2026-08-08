using SolarSim.Bridge;
using SolarSim.Engine;
using SolarSim.Engine.Models;

namespace SolarSim.Tests;

/// <summary>
/// A projeção hierárquica e a câmera. Aqui mora a resposta ao critério do M4: com a
/// câmera ancorada em Netuno, o que chega à camada gráfica são números pequenos.
/// </summary>
public sealed class ProjectionTests
{
    [Fact]
    public void CadaNivelGanhaOEspacoDeTelaDaSuaProfundidade()
    {
        var layout = Layout();

        Assert.Equal(
            ScaleLayout.ScreenRadiusForDepth(1), layout.LevelOf("sun").ScreenRadiusPixels);
        Assert.Equal(
            ScaleLayout.ScreenRadiusForDepth(2), layout.LevelOf("earth").ScreenRadiusPixels);
        Assert.Equal(
            ScaleLayout.ScreenRadiusForDepth(2), layout.LevelOf("jupiter").ScreenRadiusPixels);

        // O sistema inteiro precisa caber na metade da altura da janela padrão.
        Assert.True(ScaleLayout.ScreenRadiusForDepth(1) <= 324.0);
    }

    [Fact]
    public void OLimiteDeCadaNivelEOApoapsisDoFilhoMaisDistante()
    {
        var sim = SolarSystem.NewEngine();
        var layout = new ScaleLayout(sim.Bodies);

        var netuno = sim.ElementsOf("neptune")!.Value;
        var callisto = sim.ElementsOf("callisto")!.Value;

        Assert.Equal(
            netuno.SemiMajorAxisKm * (1.0 + netuno.Eccentricity),
            layout.LevelOf("sun").MaxDistanceKm,
            tolerance: 1.0);

        Assert.Equal(
            callisto.SemiMajorAxisKm * (1.0 + callisto.Eccentricity),
            layout.LevelOf("jupiter").MaxDistanceKm,
            tolerance: 1.0);
    }

    [Fact]
    public void CorpoSemFilhosTemNivelDegenerado()
        => Assert.Equal(default, Layout().LevelOf("titan"));

    [Fact]
    public void RaizFicaNaOrigemDoEspacoProjetado()
    {
        var (projector, _) = Projetar();

        Assert.Equal(Vector3D.Zero, projector.PositionOf("sun"));
    }

    [Fact]
    public void PosicaoProjetadaDaLuaEADaTerraMaisODeslocamentoEscalado()
    {
        var (projector, snapshot) = Projetar();

        var lua = snapshot.Bodies.Single(estado => estado.Id == "moon");
        var esperado = projector.PositionOf("earth")
            + projector.Mapper.ToPixels(lua.LocalPositionKm, projector.Layout.LevelOf("earth"));

        Assert.Equal(esperado, projector.PositionOf("moon"));
    }

    /// <summary>
    /// O ponto da escala hierárquica: comprimida pela mesma curva que faz Netuno caber na
    /// tela, a Lua ficaria a menos de um pixel da Terra e desapareceria dentro dela.
    /// </summary>
    [Theory]
    [InlineData("earth", "moon")]
    [InlineData("jupiter", "io")]
    [InlineData("jupiter", "europa")]
    [InlineData("jupiter", "ganymede")]
    [InlineData("jupiter", "callisto")]
    [InlineData("saturn", "titan")]
    public void SateliteNaoDesaparecePorDentroDoPlaneta(string planetaId, string sateliteId)
    {
        var (projector, _) = Projetar();
        var sim = SolarSystem.NewEngine();

        var separacao =
            (projector.PositionOf(sateliteId) - projector.PositionOf(planetaId)).Magnitude;

        var discos = RaioVisual(sim, projector, planetaId) + RaioVisual(sim, projector, sateliteId);

        Assert.True(
            separacao > discos * 1.5,
            $"'{sateliteId}' fica a {separacao:F1} px de '{planetaId}', que desenha "
                + $"{discos:F1} px de disco somado.");
    }

    [Fact]
    public void SistemaDeSatelitesCabeNoEspacoReservadoAoNivel()
    {
        var (projector, _) = Projetar();
        var limite = ScaleLayout.ScreenRadiusForDepth(2);

        foreach (var (planetaId, sateliteId) in new[]
        {
            ("earth", "moon"), ("jupiter", "callisto"), ("saturn", "titan"),
        })
        {
            var separacao =
                (projector.PositionOf(sateliteId) - projector.PositionOf(planetaId)).Magnitude;

            Assert.InRange(separacao, 0.0, limite);
        }
    }

    [Fact]
    public void PlanetasSaemNaOrdemCertaDoSolParaFora()
    {
        var (projector, _) = Projetar();

        var raios = new[] { "mercury", "venus", "earth", "mars", "jupiter", "saturn", "uranus" }
            .Select(id => projector.PositionOf(id).Magnitude)
            .ToArray();

        // Compara cada planeta com o seguinte. Vale em J2000 porque nenhuma dessas
        // órbitas se cruza.
        for (var index = 1; index < raios.Length; index++)
        {
            Assert.True(raios[index] > raios[index - 1], $"O planeta {index} saiu fora de ordem.");
        }
    }

    [Fact]
    public void CameraAncoradaColocaOCorpoExatamenteNoCentro()
    {
        var (projector, _) = Projetar();
        var rig = new CameraRig();

        rig.AnchorTo("neptune");

        // Dois quadros: o primeiro arma a transição, e depois ela precisa terminar.
        rig.Advance(0.0, projector.PositionOf("neptune"));
        rig.Advance(CameraRig.TransitionSeconds, projector.PositionOf("neptune"));

        var relativo = projector.PositionOf("neptune") - rig.FocusPixels;

        Assert.Equal(0.0, relativo.Magnitude);
    }

    /// <summary>
    /// O invariante 2 em forma de teste: a subtração acontece em <c>double</c> e o
    /// resultado é pequeno, então o <c>float</c> que chega ao Godot ainda distingue
    /// frações de pixel mesmo com Netuno como referência.
    /// </summary>
    [Fact]
    public void DeslocamentoMinusculoSobreviveAConversaoParaFloat()
    {
        var (projector, _) = Projetar();
        var netuno = projector.PositionOf("neptune");

        var deslocado = netuno + new Vector3D(0.01, 0.0, 0.0);

        var relativoA = (float)(netuno.X - netuno.X);
        var relativoB = (float)(deslocado.X - netuno.X);

        Assert.NotEqual(relativoA, relativoB);
        Assert.Equal(0.01f, relativoB, tolerance: 1e-6f);
    }

    [Fact]
    public void TransicaoEntreAncorasEContinuaETerminaNoAlvo()
    {
        var (projector, _) = Projetar();
        var rig = new CameraRig();
        var terra = projector.PositionOf("earth");
        var netuno = projector.PositionOf("neptune");

        rig.AnchorTo("earth");
        rig.Advance(0.0, terra);
        rig.Advance(CameraRig.TransitionSeconds, terra);

        rig.AnchorTo("neptune");

        var anterior = rig.FocusPixels;
        var maiorSalto = 0.0;

        for (var passo = 0; passo < 60; passo++)
        {
            rig.Advance(CameraRig.TransitionSeconds / 30.0, netuno);

            maiorSalto = Math.Max(maiorSalto, (rig.FocusPixels - anterior).Magnitude);
            anterior = rig.FocusPixels;
        }

        var distancia = (netuno - terra).Magnitude;

        Assert.False(rig.IsTransitioning);
        Assert.Equal(netuno, rig.FocusPixels);
        Assert.True(
            maiorSalto < distancia / 10.0,
            $"A camera pulou {maiorSalto:F1} px de um quadro para o outro.");
    }

    [Fact]
    public void TrocarDeAncoraDescartaODeslocamentoManual()
    {
        var rig = new CameraRig();

        rig.AnchorTo("earth");
        rig.Pan(new Vector3D(100.0, 50.0, 0.0));

        Assert.Equal(new Vector3D(100.0, 50.0, 0.0), rig.PanPixels);

        rig.AnchorTo("mars");

        Assert.Equal(Vector3D.Zero, rig.PanPixels);
    }

    [Fact]
    public void DeslocamentoManualEntraNoFocoDaCamera()
    {
        var rig = new CameraRig();
        var alvo = new Vector3D(300.0, -200.0, 0.0);

        rig.Advance(1.0, alvo);
        rig.Pan(new Vector3D(40.0, 0.0, 0.0));
        rig.Advance(1.0, alvo);

        Assert.Equal(alvo + new Vector3D(40.0, 0.0, 0.0), rig.FocusPixels);
    }

    private static double RaioVisual(SimEngine sim, SystemProjector projector, string bodyId)
        => projector.Mapper.BodyRadiusPixels(
            sim.Bodies.Single(body => body.Id == bodyId).RadiusKm);

    private static ScaleLayout Layout() => new(SolarSystem.NewEngine().Bodies);

    private static (SystemProjector Projector, SystemStateSnapshot Snapshot) Projetar()
    {
        var sim = SolarSystem.NewEngine();
        var projector = new SystemProjector(new ScaleLayout(sim.Bodies), new ScaleMapper());

        var parents = sim.Bodies.ToDictionary(
            body => body.Id, body => body.ParentId, StringComparer.Ordinal);

        SystemStateSnapshot? capturado = null;
        sim.SystemUpdated += snapshot => capturado = snapshot;
        sim.Advance(0.0);

        projector.Project(capturado!.Value, parents);

        return (projector, capturado.Value);
    }
}
