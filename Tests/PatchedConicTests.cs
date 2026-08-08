using SolarSim.Engine;
using SolarSim.Engine.Core;
using SolarSim.Engine.Models;

namespace SolarSim.Tests;

/// <summary>
/// Esfera de influência e troca de corpo pai. O que precisa ser verdade na emenda é que
/// nada se mova: a curva passa a ser calculada em torno de outro corpo, mas a posição e
/// a velocidade no referencial global são exatamente as mesmas dos dois lados da costura.
/// </summary>
public sealed class PatchedConicTests
{
    private static CelestialBodyData Sonda(string parentId) => new()
    {
        Id = "sonda",
        Name = "Sonda",
        ParentId = parentId,
        MuKm3S2 = 0.0,
        RadiusKm = 0.0,
    };

    /// <summary>
    /// Raios publicados na literatura, para conferir a fórmula contra números que não
    /// saíram daqui. Terra 924 mil km, Lua 66 mil km, Júpiter 48,2 milhões de km.
    /// </summary>
    [Theory]
    [InlineData("earth", 924_000.0)]
    [InlineData("moon", 66_100.0)]
    [InlineData("jupiter", 48_200_000.0)]
    [InlineData("mars", 577_000.0)]
    public void RaioDaEsferaDeInfluenciaConfereComOsValoresPublicados(
        string bodyId,
        double esperadoKm)
    {
        var sim = SolarSystem.NewEngine();

        var obtido = sim.SphereOfInfluenceKm(bodyId);

        Assert.Equal(esperadoKm, obtido, tolerance: esperadoKm * 0.01);
    }

    [Fact]
    public void ARaizDominaTudoQueNaoEstiverDentroDeOutraEsfera()
    {
        var sim = SolarSystem.NewEngine();

        Assert.True(double.IsPositiveInfinity(sim.SphereOfInfluenceKm("sun")));
    }

    [Fact]
    public void CorpoSemMassaNaoTemEsferaDeInfluencia()
    {
        var sim = SolarSystem.NewEngine();
        sim.AddFromState(Sonda("earth"), Partida(900_000.0, 1.5), sim.Time.JulianDate);

        Assert.Equal(0.0, sim.SphereOfInfluenceKm("sonda"));
    }

    [Fact]
    public void SondaQueSaiDaEsferaDaTerraPassaAOrbitarOSol()
    {
        var sim = SolarSystem.NewEngine();
        sim.AddFromState(Sonda("earth"), Partida(900_000.0, 1.5), sim.Time.JulianDate);

        Correr(sim, dias: 3.0);

        var arcos = sim.TrajectoryOf("sonda");

        Assert.Equal(2, arcos.Count);
        Assert.Equal("earth", arcos[0].ParentId);
        Assert.Equal("sun", arcos[1].ParentId);
        Assert.Equal("sun", sim.BodyOf("sonda").ParentId);

        // A distância à Terra no instante da troca tem de ser a da fronteira.
        var troca = arcos[1].StartJulianDate;
        var distancia = (sim.StateAt("sonda", troca).PositionKm
            - sim.StateAt("earth", troca).PositionKm).Magnitude;

        Assert.Equal(sim.SphereOfInfluenceKm("earth"), distancia, tolerance: 20_000.0);
    }

    /// <summary>
    /// O teste que dá sentido aos outros: na costura, os dois arcos descrevem o mesmo
    /// ponto e a mesma velocidade. Se a conversão de estado para elementos perdesse
    /// alguma coisa, a sonda daria um salto ao trocar de pai.
    /// </summary>
    [Fact]
    public void NaCosturaAPosicaoEAVelocidadeGlobaisNaoDaoSalto()
    {
        var sim = SolarSystem.NewEngine();
        sim.AddFromState(Sonda("earth"), Partida(900_000.0, 1.5), sim.Time.JulianDate);

        Correr(sim, dias: 3.0);

        var arcos = sim.TrajectoryOf("sonda");
        var troca = arcos[1].StartJulianDate;
        var dias = troca - AstroConstants.J2000;

        // Pelo arco antigo: em torno da Terra, com o mu da Terra.
        var pelaTerra = sim.StateAt("earth", troca)
            + KeplerPropagator.StateAt(
                arcos[0].Elements, sim.BodyOf("earth").MuKm3S2, dias);

        // Pelo arco novo, que é o que o motor usa a partir dali.
        var peloSol = sim.StateAt("sonda", troca);

        var desvioPosicao = (peloSol.PositionKm - pelaTerra.PositionKm).Magnitude;
        var desvioVelocidade = (peloSol.VelocityKmS - pelaTerra.VelocityKmS).Magnitude;

        Assert.True(
            desvioPosicao < 1e-6,
            $"A sonda saltou {desvioPosicao:E3} km ao trocar de corpo pai.");

        Assert.True(
            desvioVelocidade < 1e-9,
            $"A velocidade saltou {desvioVelocidade:E3} km/s ao trocar de corpo pai.");
    }

    [Fact]
    public void SondaQueEntraNaEsferaDaTerraPassaAOrbitaLa()
    {
        var sim = SolarSystem.NewEngine();

        // Solta em órbita do Sol, logo fora da esfera da Terra e caindo em direção a ela.
        var terra = sim.StateAt("earth", sim.Time.JulianDate);
        var afastamento = new Vector3D(1_000_000.0, 0.0, 0.0);

        var relativo = new StateVector(afastamento, new Vector3D(-1.0, 0.0, 0.0));

        sim.AddFromState(
            Sonda("sun"), terra + relativo, sim.Time.JulianDate);

        Assert.Equal("sun", sim.BodyOf("sonda").ParentId);

        Correr(sim, dias: 3.0);

        Assert.Equal("earth", sim.BodyOf("sonda").ParentId);
        Assert.Equal(2, sim.TrajectoryOf("sonda").Count);
    }

    [Fact]
    public void TempoParaTrasDesfazAEmenda()
    {
        var sim = SolarSystem.NewEngine();
        var partida = sim.Time.JulianDate;

        sim.AddFromState(Sonda("earth"), Partida(900_000.0, 1.5), partida);

        Correr(sim, dias: 3.0);
        Assert.Equal(2, sim.TrajectoryOf("sonda").Count);

        // De volta a antes da troca: o arco que ainda não aconteceu deixa de existir.
        sim.Time.JumpTo(partida);
        sim.Advance(0.0);

        Assert.Single(sim.TrajectoryOf("sonda"));
        Assert.Equal("earth", sim.BodyOf("sonda").ParentId);
    }

    [Fact]
    public void ConsultarUmaDataAnteriorAEmendaUsaOArcoDaquelaEpoca()
    {
        var sim = SolarSystem.NewEngine();
        var partida = sim.Time.JulianDate;

        sim.AddFromState(Sonda("earth"), Partida(900_000.0, 1.5), partida);

        var antes = sim.StateAt("sonda", partida);

        Correr(sim, dias: 3.0);

        // Mesmo com a sonda já orbitando o Sol, a consulta a uma data anterior à costura
        // tem de devolver o que ela era naquele instante.
        var agora = sim.StateAt("sonda", partida);

        Assert.Equal(
            0.0, (agora.PositionKm - antes.PositionKm).Magnitude, tolerance: 1e-6);
    }

    /// <summary>
    /// Estado inicial de uma sonda solta a certa distância da Terra, afastando-se dela.
    /// A velocidade tem componente transversal — pouca, mas alguma — porque uma subida
    /// puramente radial não é uma cônica, e o motor recusa descrevê-la como se fosse.
    /// </summary>
    private static StateVector Partida(double distanciaKm, double velocidadeKmS)
        => new(
            new Vector3D(distanciaKm, 0.0, 0.0),
            new Vector3D(velocidadeKmS * 0.96, velocidadeKmS * 0.26, velocidadeKmS * 0.1));

    /// <summary>
    /// Roda a simulação em passos pequenos, como o laço de quadro faria. A emenda é
    /// descoberta andando, e não resolvendo: é por isso que o passo importa.
    /// </summary>
    private static void Correr(SimEngine sim, double dias, int passos = 600)
    {
        sim.Time.SpeedMultiplier = AstroConstants.SecondsPerDay;

        for (var passo = 0; passo < passos; passo++)
        {
            sim.Advance(dias / passos);
        }
    }
}
