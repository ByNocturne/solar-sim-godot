using SolarSim.Engine;
using SolarSim.Engine.Core;
using SolarSim.Engine.Data;
using SolarSim.Engine.Models;

namespace SolarSim.Tests;

/// <summary>
/// Corpos que entram e saem em tempo de execução, fora do arquivo de dados. É o critério
/// de pronto do M7: inserir um corpo a partir de um vetor de estado arbitrário e vê-lo
/// propagar junto com o resto do sistema.
/// </summary>
public sealed class DynamicBodyTests
{
    /// <summary>
    /// Uma sonda: sem massa e sem raio, porque nenhuma das duas coisas influi na
    /// trajetória dela nem na de mais ninguém.
    /// </summary>
    private static CelestialBodyData Sonda(string id = "sonda", string parentId = "earth")
        => new()
        {
            Id = id,
            Name = "Sonda",
            ParentId = parentId,
            MuKm3S2 = 0.0,
            RadiusKm = 0.0,
            ColorRgb = 0xFFFFFF,
        };

    /// <summary>
    /// Órbita circular baixa em torno da Terra, a 400 km de altitude, inclinada.
    /// </summary>
    private static StateVector OrbitaBaixa()
    {
        const double raio = 6_778.0;
        var velocidade = Math.Sqrt(398_600.435436 / raio);

        return new StateVector(
            new Vector3D(raio, 0.0, 0.0),
            new Vector3D(0.0, velocidade * 0.6, velocidade * 0.8));
    }

    [Fact]
    public void CorpoInseridoAPartirDeVetorDeEstadoPropagaJuntoComOResto()
    {
        var sim = SolarSystem.NewEngine();
        var julianDate = sim.Time.JulianDate;
        var inicial = OrbitaBaixa();

        sim.AddFromState(Sonda(), inicial, julianDate);

        // No instante da inserção o estado local tem de ser exatamente o que foi dado:
        // é o que prova que a conversão inversa não distorceu a órbita pedida.
        var local = sim.LocalStateAt("sonda", julianDate);

        Assert.Equal(
            0.0,
            (local.PositionKm - inicial.PositionKm).Magnitude,
            tolerance: 1e-6);

        Assert.Equal(
            0.0,
            (local.VelocityKmS - inicial.VelocityKmS).Magnitude,
            tolerance: 1e-9);

        // E o estado global é o da Terra somado ao local, ou seja, a sonda acompanha a
        // Terra em torno do Sol sem que ninguém tenha precisado dizer isso a ela.
        var global = sim.StateAt("sonda", julianDate);
        var terra = sim.StateAt("earth", julianDate);

        Assert.Equal(
            0.0,
            (global.PositionKm - terra.PositionKm - inicial.PositionKm).Magnitude,
            tolerance: 1e-6);
    }

    [Fact]
    public void OrbitaDaSondaConservaEnergiaAoLongoDeVariasVoltas()
    {
        var sim = SolarSystem.NewEngine();
        sim.AddFromState(Sonda(), OrbitaBaixa(), sim.Time.JulianDate);

        var mu = sim.GravitationalParameterOf("sonda");
        var periodo = KeplerPropagator.OrbitalPeriodDays(
            sim.ElementsOf("sonda")!.Value.SemiMajorAxisKm, mu);

        var energias = Enumerable
            .Range(0, 64)
            .Select(passo => sim.LocalStateAt(
                "sonda", sim.Time.JulianDate + periodo * passo / 8.0))
            .Select(estado => estado.SpecificEnergy(mu))
            .ToArray();

        var variacao = (energias.Max() - energias.Min()) / Math.Abs(energias[0]);

        Assert.True(variacao < 1e-12, $"Energia variou {variacao:E3}.");

        // Oito voltas em oito períodos: o período calculado é o período de verdade.
        Assert.InRange(periodo, 0.06, 0.07);
    }

    [Fact]
    public void SondaEmTrajetoriaDeEscapeVirouOrbitaAberta()
    {
        var sim = SolarSystem.NewEngine();

        const double raio = 6_778.0;
        var escape = Math.Sqrt(2.0 * 398_600.435436 / raio);

        sim.AddFromState(
            Sonda(),
            new StateVector(
                new Vector3D(raio, 0.0, 0.0), new Vector3D(0.0, escape * 1.1, 0.0)),
            sim.Time.JulianDate);

        var elementos = sim.ElementsOf("sonda")!.Value;

        Assert.False(elementos.IsClosed);
        Assert.True(sim.LocalStateAt("sonda", sim.Time.JulianDate + 1.0).DistanceKm > raio);
    }

    [Fact]
    public void CorpoAcrescentadoApareceNaArvoreDepoisDoPai()
    {
        var sim = SolarSystem.NewEngine();
        sim.AddFromState(Sonda(), OrbitaBaixa(), sim.Time.JulianDate);

        var ids = sim.Bodies.Select(body => body.Id).ToList();

        Assert.Contains("sonda", ids);
        Assert.True(ids.IndexOf("earth") < ids.IndexOf("sonda"));
        Assert.True(sim.IsDynamic("sonda"));
        Assert.False(sim.IsDynamic("earth"));
    }

    [Fact]
    public void CorpoRemovidoSaiDaArvoreEDoRegistro()
    {
        var sim = SolarSystem.NewEngine();
        sim.AddFromState(Sonda(), OrbitaBaixa(), sim.Time.JulianDate);

        Assert.True(sim.Remove("sonda"));

        Assert.False(sim.Contains("sonda"));
        Assert.Empty(sim.DynamicBodyIds);
        Assert.False(sim.Remove("sonda"));
    }

    [Fact]
    public void MudancaDeEstruturaEAnunciada()
    {
        var sim = SolarSystem.NewEngine();
        var avisos = 0;
        sim.StructureChanged += () => avisos++;

        sim.AddFromState(Sonda(), OrbitaBaixa(), sim.Time.JulianDate);
        Assert.Equal(1, avisos);

        sim.Remove("sonda");
        Assert.Equal(2, avisos);
    }

    [Fact]
    public void IdentificadorDuplicadoFalhaESistemaContinuaIntacto()
    {
        var sim = SolarSystem.NewEngine();
        var antes = sim.Bodies.Count;

        Assert.Throws<SystemDataException>(
            () => sim.AddFromState(Sonda(id: "earth"), OrbitaBaixa(), sim.Time.JulianDate));

        Assert.Equal(antes, sim.Bodies.Count);
        Assert.Empty(sim.DynamicBodyIds);

        // O sistema tem de continuar utilizável depois da recusa, e não meio montado.
        Assert.True(sim.StateAt("earth", sim.Time.JulianDate).DistanceKm > 0.0);
    }

    [Fact]
    public void PaiInexistenteFalha()
    {
        var sim = SolarSystem.NewEngine();

        var erro = Assert.Throws<SystemDataException>(
            () => sim.AddFromState(
                Sonda(parentId: "kerbin"), OrbitaBaixa(), sim.Time.JulianDate));

        Assert.Contains("kerbin", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CorpoSemPaiFalhaPorqueSeriaUmaSegundaRaiz()
    {
        var sim = SolarSystem.NewEngine();

        var erro = Assert.Throws<SystemDataException>(
            () => sim.Add(new CelestialBodyData { Id = "outra", Name = "Outra" }));

        Assert.Contains("raiz", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RemoverARaizFalha()
    {
        var sim = SolarSystem.NewEngine();

        var erro = Assert.Throws<SystemDataException>(() => sim.Remove("sun"));

        Assert.Contains("raiz", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RemoverUmCorpoComFilhosFalhaDizendoQuaisSao()
    {
        var sim = SolarSystem.NewEngine();

        var erro = Assert.Throws<SystemDataException>(() => sim.Remove("jupiter"));

        Assert.Contains("io", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CorpoDoArquivoNaoTemTrajetoriaEmArcos()
    {
        var sim = SolarSystem.NewEngine();

        var erro = Assert.Throws<KeyNotFoundException>(() => sim.TrajectoryOf("earth"));

        Assert.Contains("arquivo de dados", erro.Message, StringComparison.Ordinal);
    }
}
