using SolarSim.Engine.Core;
using SolarSim.Engine.Models;

namespace SolarSim.Tests;

public sealed class KeplerPropagatorTests
{
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.0167)]
    [InlineData(0.2056)]
    [InlineData(0.7)]
    [InlineData(0.94)]
    public void SolverSatisfazAEquacaoDeKepler(double eccentricity)
    {
        for (var passo = 0; passo < 32; passo++)
        {
            var meanAnomaly = AstroConstants.TwoPi * passo / 32.0;

            var eccentricAnomaly =
                KeplerPropagator.SolveEccentricAnomaly(meanAnomaly, eccentricity);

            var reconstruido = eccentricAnomaly - eccentricity * Math.Sin(eccentricAnomaly);

            Assert.Equal(meanAnomaly, reconstruido, precision: 10);
        }
    }

    [Fact]
    public void OrbitaCircularMantemRaioConstante()
    {
        var elements = new OrbitalElements(
            SemiMajorAxisKm: AstroConstants.AstronomicalUnitKm,
            Eccentricity: 0.0,
            InclinationRad: 0.0,
            LongitudeOfAscendingNodeRad: 0.0,
            ArgumentOfPeriapsisRad: 0.0,
            MeanAnomalyAtEpochRad: 0.0);

        for (var dia = 0; dia < 365; dia += 7)
        {
            var posicao = KeplerPropagator.PositionAt(elements, AstroConstants.SunMuKm3S2, dia);

            Assert.Equal(AstroConstants.AstronomicalUnitKm, posicao.Magnitude, precision: 3);
        }
    }

    [Fact]
    public void PeriodoDaTerraFicaProximoDeUmAnoSideral()
    {
        var periodo = KeplerPropagator.OrbitalPeriodDays(
            1.00000261 * AstroConstants.AstronomicalUnitKm,
            AstroConstants.SunMuKm3S2);

        // Ano sideral: 365,256 dias. A folga cobre o fato de o mu usado ser o do Sol
        // isolado, ignorando a massa do sistema Terra-Lua.
        Assert.InRange(periodo, 365.0, 365.5);
    }

    [Fact]
    public void PosicaoRetornaAoPontoDePartidaDepoisDeUmPeriodo()
    {
        var elements = OrbitalElements.FromAuAndDegrees(
            semiMajorAxisAu: 1.00000261,
            eccentricity: 0.01671123,
            inclinationDeg: 0.0,
            longitudeOfAscendingNodeDeg: 0.0,
            argumentOfPeriapsisDeg: 102.93768193,
            meanAnomalyAtEpochDeg: -2.47311027);

        var periodo = KeplerPropagator.OrbitalPeriodDays(
            elements.SemiMajorAxisKm, AstroConstants.SunMuKm3S2);

        var inicio = KeplerPropagator.PositionAt(elements, AstroConstants.SunMuKm3S2, 0.0);
        var depois = KeplerPropagator.PositionAt(elements, AstroConstants.SunMuKm3S2, periodo);

        var desvio = (depois - inicio).Magnitude;

        Assert.True(desvio < 1.0, $"Desvio de {desvio:N3} km apos um periodo completo.");
    }

    [Fact]
    public void DistanciaVariaEntrePerielioEAfelio()
    {
        var elements = OrbitalElements.FromAuAndDegrees(
            semiMajorAxisAu: 1.00000261,
            eccentricity: 0.01671123,
            inclinationDeg: 0.0,
            longitudeOfAscendingNodeDeg: 0.0,
            argumentOfPeriapsisDeg: 102.93768193,
            meanAnomalyAtEpochDeg: -2.47311027);

        var distancias = Enumerable
            .Range(0, 366)
            .Select(dia =>
                KeplerPropagator.PositionAt(elements, AstroConstants.SunMuKm3S2, dia).Magnitude)
            .ToArray();

        var perielioEsperado = elements.SemiMajorAxisKm * (1.0 - elements.Eccentricity);
        var afelioEsperado = elements.SemiMajorAxisKm * (1.0 + elements.Eccentricity);

        Assert.Equal(perielioEsperado, distancias.Min(), tolerance: 50_000.0);
        Assert.Equal(afelioEsperado, distancias.Max(), tolerance: 50_000.0);
    }
}
