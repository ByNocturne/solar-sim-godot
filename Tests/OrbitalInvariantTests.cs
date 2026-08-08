using SolarSim.Engine;
using SolarSim.Engine.Core;
using SolarSim.Engine.Data;
using SolarSim.Engine.Models;

namespace SolarSim.Tests;

/// <summary>
/// Grandezas que a mecânica kepleriana obriga a permanecer constantes. São o melhor
/// detector de erro no propagador: não dependem de efeméride nenhuma e falham diante de
/// qualquer inconsistência entre posição e velocidade.
/// </summary>
/// <remarks>
/// As amostras são do estado local, relativo ao pai, que é o referencial em que a órbita
/// é kepleriana. No referencial global a órbita da Lua não conserva nada, porque a Terra
/// está acelerando embaixo dela.
/// </remarks>
public sealed class OrbitalInvariantTests
{
    [Theory]
    [InlineData("earth")]
    [InlineData("mars")]
    [InlineData("moon")]
    [InlineData("io")]
    public void EnergiaOrbitalEspecificaSeMantemConstante(string bodyId)
    {
        var sim = SolarSystem.NewEngine();
        var mu = sim.GravitationalParameterOf(bodyId);

        var energias = Amostrar(sim, bodyId)
            .Select(estado => estado.SpecificEnergy(mu))
            .ToArray();

        var variacao = (energias.Max() - energias.Min()) / Math.Abs(energias[0]);

        Assert.True(variacao < 1e-12, $"Energia variou {variacao:E3} ao longo da orbita.");
    }

    [Theory]
    [InlineData("earth")]
    [InlineData("mars")]
    [InlineData("moon")]
    [InlineData("io")]
    public void MomentoAngularEspecificoSeMantemConstante(string bodyId)
    {
        var sim = SolarSystem.NewEngine();

        var momentos = Amostrar(sim, bodyId)
            .Select(estado => estado.SpecificAngularMomentum.Magnitude)
            .ToArray();

        var variacao = (momentos.Max() - momentos.Min()) / momentos[0];

        Assert.True(variacao < 1e-12, $"Momento angular variou {variacao:E3}.");
    }

    [Theory]
    [InlineData("earth")]
    [InlineData("mars")]
    [InlineData("moon")]
    [InlineData("io")]
    public void EnergiaCorrespondeAoSemiEixoMaior(string bodyId)
    {
        var sim = SolarSystem.NewEngine();
        var mu = sim.GravitationalParameterOf(bodyId);
        var elementos = sim.ElementsOf(bodyId)!.Value;

        var estado = sim.LocalStateAt(bodyId, AstroConstants.J2000 + 137.0);

        // Para uma órbita fechada, a energia específica vale -mu / (2a).
        var esperado = -mu / (2.0 * elementos.SemiMajorAxisKm);
        var obtido = estado.SpecificEnergy(mu);

        Assert.Equal(esperado, obtido, tolerance: Math.Abs(esperado) * 1e-12);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.0167)]
    [InlineData(0.0934)]
    [InlineData(0.2056)]
    [InlineData(0.5)]
    [InlineData(0.8)]
    [InlineData(0.9)]
    [InlineData(0.94)]
    public void SolverConvergeEmMenosDeDezIteracoes(double eccentricity)
    {
        var maiorContagem = 0;
        var piorAnomalia = 0.0;

        for (var passo = 0; passo < 720; passo++)
        {
            var meanAnomaly = AstroConstants.TwoPi * passo / 720.0;

            KeplerPropagator.SolveEccentricAnomaly(meanAnomaly, eccentricity, out var iteracoes);

            if (iteracoes > maiorContagem)
            {
                maiorContagem = iteracoes;
                piorAnomalia = meanAnomaly;
            }
        }

        Assert.True(
            maiorContagem < 10,
            $"Excentricidade {eccentricity}: {maiorContagem} iteracoes em M = "
                + $"{AstroConstants.RadiansToDegrees(piorAnomalia):F1} graus.");
    }

    [Fact]
    public void VelocidadeEPerpendicularAoRaioNosApsides()
    {
        var sim = SolarSystem.NewEngine();
        var elementos = sim.ElementsOf("mars")!.Value;
        var parentMu = sim.GravitationalParameterOf("mars");

        var periodo = KeplerPropagator.OrbitalPeriodDays(elementos.SemiMajorAxisKm, parentMu);

        // A anomalia média na época dá o quanto falta para o periélio.
        var diasAtePerielio =
            -elementos.MeanAnomalyAtEpochRad / AstroConstants.TwoPi * periodo;

        foreach (var dia in new[] { diasAtePerielio, diasAtePerielio + periodo / 2.0 })
        {
            var estado = KeplerPropagator.StateAt(elementos, parentMu, dia);

            var cosseno = estado.PositionKm.Normalized().Dot(estado.VelocityKmS.Normalized());

            Assert.Equal(0.0, cosseno, tolerance: 1e-9);
        }
    }

    private static IEnumerable<StateVector> Amostrar(SimEngine sim, string bodyId)
    {
        var elementos = sim.ElementsOf(bodyId)!.Value;
        var periodo = KeplerPropagator.OrbitalPeriodDays(
            elementos.SemiMajorAxisKm, sim.GravitationalParameterOf(bodyId));

        for (var passo = 0; passo < 64; passo++)
        {
            yield return sim.LocalStateAt(bodyId, AstroConstants.J2000 + periodo * passo / 64.0);
        }
    }
}
