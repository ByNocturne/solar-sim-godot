using System.Globalization;
using SolarSim.Engine.Core;
using SolarSim.Engine.Models;

namespace SolarSim.Tests;

/// <summary>
/// A solução analítica contra uma integração numérica da mesma equação de movimento.
/// </summary>
/// <remarks>
/// Os testes de ida e volta são simétricos: um erro que o caminho de ida e o de volta
/// cometam juntos passa por eles. Este não é — Runge-Kutta de quarta ordem não sabe nada
/// de anomalias, de cônicas nem de elementos, e só integra a aceleração
/// <c>-mu·r/|r|³</c>. Concordar com ele em dez casas é a evidência mais forte que existe
/// aqui de que a equação de Kepler hiperbólica está certa: para a elipse há as
/// efemérides do JPL, mas para a hipérbole a única referência externa, 'Oumuamua, vem
/// contaminada por aceleração não gravitacional.
/// </remarks>
public sealed class NumericalCrossCheckTests
{
    /// <summary>Passo de 200 segundos, contra um tempo característico de 13 dias no periápside.</summary>
    private const double StepSeconds = 200.0;

    private const double SpanDays = 60.0;

    [Theory]
    [InlineData(0.2)]
    [InlineData(0.7)]
    [InlineData(0.95)]
    [InlineData(1.2)]
    [InlineData(3.0)]
    [InlineData(10.0)]
    public void PropagadorConcordaComRungeKuttaAoLongoDeUmaPassagemPeloPeriapside(
        double eccentricity)
    {
        var periapsis = 0.5 * AstroConstants.AstronomicalUnitKm;

        var elementos = new OrbitalElements(
            SemiMajorAxisKm: periapsis / (1.0 - eccentricity),
            Eccentricity: eccentricity,
            InclinationRad: AstroConstants.DegreesToRadians(37.0),
            LongitudeOfAscendingNodeRad: AstroConstants.DegreesToRadians(64.0),
            ArgumentOfPeriapsisRad: AstroConstants.DegreesToRadians(151.0),
            MeanAnomalyAtEpochRad: 0.0);

        var inicio = KeplerPropagator.StateAt(elementos, AstroConstants.SunMuKm3S2, -SpanDays);
        var fim = KeplerPropagator.StateAt(elementos, AstroConstants.SunMuKm3S2, SpanDays);

        var integrado = Integrar(
            inicio, AstroConstants.SunMuKm3S2, 2.0 * SpanDays * AstroConstants.SecondsPerDay);

        var desvio = (integrado.PositionKm - fim.PositionKm).Magnitude;
        var erroRelativo = desvio / fim.DistanceKm;

        Assert.True(
            erroRelativo < 1e-10,
            string.Create(
                CultureInfo.InvariantCulture,
                $"Excentricidade {eccentricity}: desvio de {desvio:N6} km sobre "
                    + $"{fim.DistanceKm:N0} km, ou {erroRelativo:E3}."));
    }

    /// <summary>
    /// Runge-Kutta clássico de quarta ordem sobre o sistema de primeira ordem
    /// <c>r' = v</c>, <c>v' = -mu·r/|r|³</c>.
    /// </summary>
    private static StateVector Integrar(StateVector inicial, double mu, double totalSeconds)
    {
        var passos = (int)Math.Ceiling(totalSeconds / StepSeconds);
        var passo = totalSeconds / passos;
        var estado = inicial;

        for (var indice = 0; indice < passos; indice++)
        {
            var k1 = Derivada(estado, mu);
            var k2 = Derivada(Avancar(estado, k1, passo / 2.0), mu);
            var k3 = Derivada(Avancar(estado, k2, passo / 2.0), mu);
            var k4 = Derivada(Avancar(estado, k3, passo), mu);

            var media = new StateVector(
                (k1.PositionKm + 2.0 * k2.PositionKm + 2.0 * k3.PositionKm + k4.PositionKm) / 6.0,
                (k1.VelocityKmS + 2.0 * k2.VelocityKmS + 2.0 * k3.VelocityKmS + k4.VelocityKmS)
                    / 6.0);

            estado = Avancar(estado, media, passo);
        }

        return estado;
    }

    /// <summary>A derivada do estado, guardada no mesmo tipo: velocidade e aceleração.</summary>
    private static StateVector Derivada(StateVector estado, double mu)
    {
        var distancia = estado.DistanceKm;
        var aceleracao = estado.PositionKm * (-mu / (distancia * distancia * distancia));

        return new StateVector(estado.VelocityKmS, aceleracao);
    }

    private static StateVector Avancar(StateVector estado, StateVector derivada, double passo)
        => new(
            estado.PositionKm + derivada.PositionKm * passo,
            estado.VelocityKmS + derivada.VelocityKmS * passo);
}
