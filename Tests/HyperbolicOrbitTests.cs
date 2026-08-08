using SolarSim.Engine.Core;
using SolarSim.Engine.Models;

namespace SolarSim.Tests;

/// <summary>
/// A cônica aberta. Não há período para amostrar nem apoápside a que retornar, então o
/// que se verifica são as grandezas conservadas, a simetria em torno do periápside e o
/// comportamento assintótico — que é justamente o que interessa a um sobrevoo.
/// </summary>
public sealed class HyperbolicOrbitTests
{
    /// <summary>Uma hipérbole heliocêntrica com periélio dentro da órbita da Terra.</summary>
    private static OrbitalElements Sobrevoo(double eccentricity)
    {
        var periapsis = 0.5 * AstroConstants.AstronomicalUnitKm;

        return new OrbitalElements(
            SemiMajorAxisKm: periapsis / (1.0 - eccentricity),
            Eccentricity: eccentricity,
            InclinationRad: AstroConstants.DegreesToRadians(30.0),
            LongitudeOfAscendingNodeRad: AstroConstants.DegreesToRadians(70.0),
            ArgumentOfPeriapsisRad: AstroConstants.DegreesToRadians(110.0),
            MeanAnomalyAtEpochRad: 0.0);
    }

    [Theory]
    [InlineData(1.001)]
    [InlineData(1.05)]
    [InlineData(1.5)]
    [InlineData(3.0)]
    [InlineData(10.0)]
    public void SolverSatisfazAEquacaoDeKeplerHiperbolica(double eccentricity)
    {
        for (var passo = -40; passo <= 40; passo++)
        {
            var meanAnomaly = passo * 2.5;

            var hyperbolic =
                KeplerPropagator.SolveHyperbolicAnomaly(meanAnomaly, eccentricity);

            var reconstruido =
                eccentricity * Math.Sinh(hyperbolic) - hyperbolic;

            Assert.Equal(
                meanAnomaly,
                reconstruido,
                tolerance: 1e-9 * (1.0 + Math.Abs(meanAnomaly)));
        }
    }

    [Theory]
    [InlineData(1.000001)]
    [InlineData(1.001)]
    [InlineData(1.5)]
    [InlineData(10.0)]
    [InlineData(100.0)]
    public void SolverHiperbolicoConvergeEmMenosDeDezIteracoes(double eccentricity)
    {
        var maiorContagem = 0;
        var piorAnomalia = 0.0;

        for (var passo = -500; passo <= 500; passo++)
        {
            var meanAnomaly = passo * 0.5;

            KeplerPropagator.SolveHyperbolicAnomaly(
                meanAnomaly, eccentricity, out var iteracoes);

            if (iteracoes > maiorContagem)
            {
                maiorContagem = iteracoes;
                piorAnomalia = meanAnomaly;
            }
        }

        Assert.True(
            maiorContagem < 10,
            $"Excentricidade {eccentricity}: {maiorContagem} iteracoes em M = {piorAnomalia}.");
    }

    [Theory]
    [InlineData(1.001)]
    [InlineData(1.5)]
    [InlineData(3.0)]
    public void AnomaliaMediaNulaCaiNoPeriapside(double eccentricity)
    {
        var elementos = Sobrevoo(eccentricity);
        var estado = KeplerPropagator.StateAt(elementos, AstroConstants.SunMuKm3S2, 0.0);

        Assert.Equal(
            elementos.PeriapsisKm,
            estado.DistanceKm,
            tolerance: elementos.PeriapsisKm * 1e-12);

        var cosseno = estado.PositionKm.Normalized().Dot(estado.VelocityKmS.Normalized());

        Assert.Equal(0.0, cosseno, tolerance: 1e-12);
    }

    [Theory]
    [InlineData(1.001)]
    [InlineData(1.5)]
    [InlineData(3.0)]
    public void EnergiaEMomentoAngularSeMantemAoLongoDoSobrevoo(double eccentricity)
    {
        var elementos = Sobrevoo(eccentricity);

        var estados = Enumerable
            .Range(-90, 181)
            .Select(dia => KeplerPropagator.StateAt(elementos, AstroConstants.SunMuKm3S2, dia))
            .ToArray();

        var energias = estados
            .Select(estado => estado.SpecificEnergy(AstroConstants.SunMuKm3S2))
            .ToArray();

        var momentos = estados
            .Select(estado => estado.SpecificAngularMomentum.Magnitude)
            .ToArray();

        // Energia positiva é a definição de trajetória de escape, e vale -mu/(2a) com
        // a negativo, exatamente como na elipse.
        var esperada = -AstroConstants.SunMuKm3S2 / (2.0 * elementos.SemiMajorAxisKm);

        Assert.True(esperada > 0.0);
        Assert.Equal(esperada, energias[0], tolerance: esperada * 1e-10);

        Assert.True(
            (energias.Max() - energias.Min()) / esperada < 1e-10,
            $"Energia variou {(energias.Max() - energias.Min()) / esperada:E3}.");

        Assert.True(
            (momentos.Max() - momentos.Min()) / momentos[0] < 1e-10,
            $"Momento angular variou {(momentos.Max() - momentos.Min()) / momentos[0]:E3}.");
    }

    [Theory]
    [InlineData(1.001)]
    [InlineData(1.5)]
    [InlineData(3.0)]
    public void TrajetoriaESimetricaEmTornoDoPeriapside(double eccentricity)
    {
        var elementos = Sobrevoo(eccentricity);

        for (var dia = 1.0; dia <= 200.0; dia *= 2.0)
        {
            var antes = KeplerPropagator.StateAt(elementos, AstroConstants.SunMuKm3S2, -dia);
            var depois = KeplerPropagator.StateAt(elementos, AstroConstants.SunMuKm3S2, dia);

            Assert.Equal(antes.DistanceKm, depois.DistanceKm, tolerance: antes.DistanceKm * 1e-10);
            Assert.Equal(antes.SpeedKmS, depois.SpeedKmS, tolerance: antes.SpeedKmS * 1e-10);
        }
    }

    [Theory]
    [InlineData(1.05)]
    [InlineData(1.5)]
    [InlineData(3.0)]
    public void VelocidadeTendeAoExcessoHiperbolico(double eccentricity)
    {
        var elementos = Sobrevoo(eccentricity);

        // v_infinito = raiz(mu / |a|): o que sobra da velocidade depois de vencer o poço
        // gravitacional. É a grandeza que dimensiona uma janela de transferência.
        var excesso = Math.Sqrt(AstroConstants.SunMuKm3S2 / Math.Abs(elementos.SemiMajorAxisKm));

        var velocidades = new[] { 1e4, 1e5, 1e6, 1e7 }
            .Select(dia => KeplerPropagator.StateAt(elementos, AstroConstants.SunMuKm3S2, dia))
            .Select(estado => estado.SpeedKmS)
            .ToArray();

        // Monotonicamente decrescente e sempre acima do limite, que é o que caracteriza
        // a aproximação por cima da assíntota.
        for (var indice = 1; indice < velocidades.Length; indice++)
        {
            Assert.True(velocidades[indice] < velocidades[indice - 1]);
            Assert.True(velocidades[indice] > excesso);
        }

        Assert.Equal(excesso, velocidades[^1], tolerance: excesso * 1e-3);
    }

    [Theory]
    [InlineData(1.001)]
    [InlineData(1.5)]
    [InlineData(3.0)]
    public void AnomaliaVerdadeiraNaoUltrapassaAAssintota(double eccentricity)
    {
        var elementos = Sobrevoo(eccentricity);
        var limite = Math.Acos(-1.0 / eccentricity);

        foreach (var dia in new[] { -1e7, -1e5, -10.0, 0.0, 10.0, 1e5, 1e7 })
        {
            var anomalia = KeplerPropagator.TrueAnomalyAt(
                elementos, AstroConstants.SunMuKm3S2, dia);

            Assert.InRange(Math.Abs(anomalia), 0.0, limite);
        }
    }

    [Fact]
    public void ParabolaExataERecusadaComMensagemQueExplica()
    {
        var erro = Assert.Throws<ArgumentOutOfRangeException>(
            () => OrbitalElements.RequireRepresentableEccentricity(1.0));

        Assert.Contains("parabólica", erro.Message, StringComparison.Ordinal);
    }
}
