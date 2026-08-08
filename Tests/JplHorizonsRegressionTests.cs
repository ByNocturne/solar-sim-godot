using System.Globalization;
using System.Text;
using SolarSim.Engine;
using SolarSim.Engine.Core;
using SolarSim.Engine.Data;
using SolarSim.Engine.Models;

namespace SolarSim.Tests;

/// <summary>
/// Compara o propagador com efemérides geométricas do JPL Horizons (solução DE441),
/// referencial eclíptico de J2000 centrado no Sol, em km e km/s.
/// </summary>
/// <remarks>
/// As épocas de referência estão em JD TDB. O motor trata a Data Juliana como escala
/// uniforme e ignora a diferença TDB menos UTC, hoje em torno de 69 segundos. A 30 km/s,
/// isso desloca a Terra cerca de 2000 km ao longo da órbita, duas ordens de grandeza
/// abaixo da tolerância adotada aqui, e praticamente nada na distância radial.
///
/// Os alvos são baricentros (Terra-Lua e Marte) porque é a eles que se referem os
/// elementos keplerianos aproximados publicados pelo JPL.
/// </remarks>
public sealed class JplHorizonsRegressionTests
{
    // Limites deliberadamente próximos do erro medido, para que o teste detecte
    // regressão em vez de apenas confirmar a ordem de grandeza. Máximos observados
    // nas seis referências: 0,0132% no raio, 0,0928 grau na direção e 0,0120% na
    // velocidade, todos no caso de Marte em 2026, que é o mais distante da época.
    private const double RadialToleranceFraction = 0.0002;
    private const double AngularToleranceDegrees = 0.15;
    private const double SpeedToleranceFraction = 0.0002;

    private static readonly Referencia[] Referencias =
    [
        new("earth", 2451544.5,
            new Vector3D(-2.521478819505877e7, 1.449249805618538e8, -1.722419584468007e2),
            new Vector3D(-2.983301944620392e1, -5.217353546848384, 4.176555734147769e-5)),
        new("earth", 2456293.5,
            new Vector3D(-2.693468301368217e7, 1.446155461051838e8, -4.383118069991469e3),
            new Vector3D(-2.977017536040555e1, -5.564997957236611, 2.004244423416957e-4)),
        new("earth", 2461041.5,
            new Vector3D(-2.607038480622956e7, 1.447786761728626e8, -8.507044686876237e3),
            new Vector3D(-2.980113416933796e1, -5.391156778582275, 4.785275326151250e-4)),
        new("mars", 2451544.5,
            new Vector3D(2.079950549836587e8, -3.143009713801308e6, -5.178781243501138e6),
            new Vector3D(1.295003526430191, 2.629442068808170e1, 5.190097596655896e-1)),
        new("mars", 2456293.5,
            new Vector3D(1.615753087602733e8, -1.296287132770028e8, -6.683049294624694e6),
            new Vector3D(1.608416639895223e1, 2.096939081625721e1, 4.443553175589088e-2)),
        new("mars", 2461041.5,
            new Vector3D(5.094999446226232e7, -2.074925482421175e8, -5.597537454936221e6),
            new Vector3D(2.444715238735575e1, 7.861165854045014, -4.347434764452198e-1)),
    ];

    [Fact]
    public void DistanciaRadialConfereComAsEfemeridesDoJpl()
    {
        var sim = SolarSystem.NewEngine();
        var relatorio = new StringBuilder();
        var maiorErro = 0.0;

        foreach (var referencia in Referencias)
        {
            var calculado = sim.StateAt(referencia.BodyId, referencia.JulianDateTdb);

            var raioEsperado = referencia.PositionKm.Magnitude;
            var erroRelativo = Math.Abs(calculado.DistanceKm - raioEsperado) / raioEsperado;
            maiorErro = Math.Max(maiorErro, erroRelativo);

            relatorio.AppendLine(CultureInfo.InvariantCulture,
                $"{referencia.BodyId,-6} JD {referencia.JulianDateTdb,12:F1}  " +
                $"esperado {raioEsperado,15:N0} km  " +
                $"obtido {calculado.DistanceKm,15:N0} km  " +
                $"erro {erroRelativo:P4}");
        }

        Assert.True(
            maiorErro < RadialToleranceFraction,
            $"Erro radial maximo de {maiorErro:P4}, acima da tolerancia de "
                + $"{RadialToleranceFraction:P2}.{Environment.NewLine}{relatorio}");
    }

    [Fact]
    public void DirecaoDoVetorPosicaoConfereComAsEfemeridesDoJpl()
    {
        var sim = SolarSystem.NewEngine();
        var relatorio = new StringBuilder();
        var maiorDesvioGraus = 0.0;

        foreach (var referencia in Referencias)
        {
            var calculado = sim.PositionAt(referencia.BodyId, referencia.JulianDateTdb);

            var cosseno = calculado.Normalized().Dot(referencia.PositionKm.Normalized());
            var desvioGraus = AstroConstants.RadiansToDegrees(
                Math.Acos(Math.Clamp(cosseno, -1.0, 1.0)));

            maiorDesvioGraus = Math.Max(maiorDesvioGraus, desvioGraus);

            relatorio.AppendLine(CultureInfo.InvariantCulture,
                $"{referencia.BodyId,-6} JD {referencia.JulianDateTdb,12:F1}  " +
                $"desvio angular {desvioGraus:F4} graus");
        }

        // Elementos fixos, sem taxas seculares, acumulam erro ao longo do tempo: o desvio
        // sai de 0,001 grau em J2000 para 0,09 grau em 2026. Reduzir isso exige os itens
        // de perturbação que estão no backlog, não um ajuste no propagador.
        Assert.True(
            maiorDesvioGraus < AngularToleranceDegrees,
            $"Desvio angular maximo de {maiorDesvioGraus:F4} graus."
                + $"{Environment.NewLine}{relatorio}");
    }

    [Fact]
    public void VelocidadeConfereComAsEfemeridesDoJpl()
    {
        var sim = SolarSystem.NewEngine();
        var relatorio = new StringBuilder();
        var maiorErro = 0.0;

        foreach (var referencia in Referencias)
        {
            var calculado = sim.StateAt(referencia.BodyId, referencia.JulianDateTdb);

            var velocidadeEsperada = referencia.VelocityKmS.Magnitude;
            var erroRelativo =
                Math.Abs(calculado.SpeedKmS - velocidadeEsperada) / velocidadeEsperada;

            maiorErro = Math.Max(maiorErro, erroRelativo);

            relatorio.AppendLine(CultureInfo.InvariantCulture,
                $"{referencia.BodyId,-6} JD {referencia.JulianDateTdb,12:F1}  " +
                $"esperado {velocidadeEsperada,8:F4} km/s  " +
                $"obtido {calculado.SpeedKmS,8:F4} km/s  erro {erroRelativo:P4}");
        }

        Assert.True(
            maiorErro < SpeedToleranceFraction,
            $"Erro maximo de velocidade de {maiorErro:P4}."
                + $"{Environment.NewLine}{relatorio}");
    }

    private sealed record Referencia(
        string BodyId,
        double JulianDateTdb,
        Vector3D PositionKm,
        Vector3D VelocityKmS);
}
