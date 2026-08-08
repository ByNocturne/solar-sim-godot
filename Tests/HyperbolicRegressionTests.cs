using System.Globalization;
using System.Text;
using SolarSim.Engine.Core;
using SolarSim.Engine.Models;

namespace SolarSim.Tests;

/// <summary>
/// A cônica aberta contra um objeto real: 1I/'Oumuamua, o primeiro corpo interestelar
/// observado, com excentricidade 1,1997. Os elementos e as efemérides são do JPL
/// Horizons (solução JPL#16), referencial eclíptico de J2000 centrado no Sol, em km e
/// km/s.
/// </summary>
/// <remarks>
/// Este teste vale por dois. Além de exercitar a equação de Kepler hiperbólica, ele é o
/// único lugar em que a convenção de ângulos da hipérbole é conferida contra alguém de
/// fora: um sinal trocado no argumento do periápside ou na anomalia hiperbólica passa
/// pelos testes de ida e volta, que são simétricos, mas não passa aqui.
///
/// O erro esperado não é zero, e não é do propagador. 'Oumuamua tem aceleração não
/// gravitacional medida, atribuída a desgaseificação, com A1 de 2,79e-7 au/dia² — a
/// solução do JPL a inclui e o modelo de dois corpos não tem como incluir. Por isso as
/// referências ficam concentradas em torno da época dos elementos osculadores, e a
/// tolerância cresce com o afastamento dela.
/// </remarks>
public sealed class HyperbolicRegressionTests
{
    /// <summary>Época dos elementos osculadores: 2017-Set-04, dois dias antes do periélio.</summary>
    private const double ElementsEpochJulianDate = 2458000.5;

    private static readonly Referencia[] Referencias =
    [
        new(2457980.5,
            new Vector3D(-6.254167384576350e7, -8.233565251016735e7, 7.593866006084412e7),
            new Vector3D(1.414243069274839, 3.077549564279813e1, -4.260348308937543e1)),
        new(2458000.5,
            new Vector3D(-4.545606316981158e7, -1.608652271577169e7, -6.683584793812752e6),
            new Vector3D(3.020609236969604e1, 5.047034257439882e1, -5.181636711675863e1)),
        new(2458020.5,
            new Vector3D(5.801184025613478e7, 5.030858012266565e7, -3.359085702703150e7),
            new Vector3D(5.815880373676509e1, 1.926321762905797e1, 1.041004106668126e1)),
        new(2458060.5,
            new Vector3D(2.172243411289662e8, 8.939359393259971e7, 1.420439163419559e7),
            new Vector3D(3.945034647237127e1, 7.908654504403072, 1.435362361695799e1)),
        new(2458200.5,
            new Vector3D(6.199328588232520e8, 1.589122243698032e8, 1.765793252862003e8),
            new Vector3D(3.020117548835242e1, 4.824437472664505, 1.272779247504157e1)),
    ];

    /// <summary>
    /// Limites deliberadamente próximos do erro medido, como nas referências elípticas
    /// do M2: máximos observados de 0,269% na posição e 0,356% na velocidade, ambos na
    /// referência mais distante da época — 200 dias depois, e portanto a mais
    /// contaminada pela aceleração não gravitacional.
    /// </summary>
    private const double PositionToleranceFraction = 0.003;

    private const double SpeedToleranceFraction = 0.004;

    /// <summary>
    /// Na própria época dos elementos não há tempo para a aceleração não gravitacional
    /// agir, e o que sobra é só a conversão de elementos para vetor de estado. É aqui
    /// que a convenção de ângulos da hipérbole é conferida de verdade: qualquer sinal
    /// trocado apareceria como um erro grosseiro, e não como um décimo de por cento.
    /// </summary>
    [Fact]
    public void NaEpocaDosElementosOEstadoBateAtePraticamenteONumeroDeMaquina()
    {
        var referencia = Referencias.Single(
            item => item.JulianDateTdb == ElementsEpochJulianDate);

        var calculado = Estado(ElementsEpochJulianDate);

        var desvioPosicao = (calculado.PositionKm - referencia.PositionKm).Magnitude;
        var desvioVelocidade = (calculado.VelocityKmS - referencia.VelocityKmS).Magnitude;

        // O que resta é o arredondamento dos dígitos publicados pelo Horizons.
        Assert.True(
            desvioPosicao < 1.0,
            $"Posicao desviou {desvioPosicao:N3} km sobre um raio de "
                + $"{referencia.PositionKm.Magnitude:N0} km.");

        Assert.True(
            desvioVelocidade < 1e-6,
            $"Velocidade desviou {desvioVelocidade:E3} km/s.");
    }

    [Fact]
    public void PosicaoConfereComAEfemerideDeOumuamua()
    {
        var relatorio = new StringBuilder();
        var maiorErro = 0.0;

        foreach (var referencia in Referencias)
        {
            var calculado = Estado(referencia.JulianDateTdb);

            var desvio = (calculado.PositionKm - referencia.PositionKm).Magnitude;
            var erroRelativo = desvio / referencia.PositionKm.Magnitude;
            maiorErro = Math.Max(maiorErro, erroRelativo);

            relatorio.AppendLine(CultureInfo.InvariantCulture,
                $"JD {referencia.JulianDateTdb,12:F1}  " +
                $"raio {referencia.PositionKm.Magnitude,14:N0} km  " +
                $"desvio {desvio,12:N0} km  erro {erroRelativo:P4}");
        }

        Assert.True(
            maiorErro < PositionToleranceFraction,
            $"Erro maximo de posicao de {maiorErro:P4}."
                + $"{Environment.NewLine}{relatorio}");
    }

    [Fact]
    public void VelocidadeConfereComAEfemerideDeOumuamua()
    {
        var relatorio = new StringBuilder();
        var maiorErro = 0.0;

        foreach (var referencia in Referencias)
        {
            var calculado = Estado(referencia.JulianDateTdb);

            var desvio = (calculado.VelocityKmS - referencia.VelocityKmS).Magnitude;
            var erroRelativo = desvio / referencia.VelocityKmS.Magnitude;
            maiorErro = Math.Max(maiorErro, erroRelativo);

            relatorio.AppendLine(CultureInfo.InvariantCulture,
                $"JD {referencia.JulianDateTdb,12:F1}  " +
                $"velocidade {referencia.VelocityKmS.Magnitude,8:F4} km/s  " +
                $"erro {erroRelativo:P4}");
        }

        Assert.True(
            maiorErro < SpeedToleranceFraction,
            $"Erro maximo de velocidade de {maiorErro:P4}."
                + $"{Environment.NewLine}{relatorio}");
    }

    /// <summary>
    /// O caminho inverso sobre um estado real: os elementos que o JPL publica saem de um
    /// vetor de estado dele, e devem sair também do nosso.
    /// </summary>
    [Fact]
    public void ElementosDoJplSaemDoVetorDeEstadoDoJpl()
    {
        var referencia = Referencias.Single(
            item => item.JulianDateTdb == ElementsEpochJulianDate);

        var recuperados = OrbitDetermination.ElementsFrom(
            new StateVector(referencia.PositionKm, referencia.VelocityKmS),
            AstroConstants.SunMuKm3S2,
            ElementsEpochJulianDate - AstroConstants.J2000);

        var publicados = Oumuamua();

        Assert.Equal(
            publicados.Eccentricity, recuperados.Eccentricity, tolerance: 1e-8);

        Assert.Equal(
            publicados.SemiMajorAxisKm,
            recuperados.SemiMajorAxisKm,
            tolerance: Math.Abs(publicados.SemiMajorAxisKm) * 1e-7);

        Assert.Equal(
            AstroConstants.RadiansToDegrees(publicados.InclinationRad),
            AstroConstants.RadiansToDegrees(recuperados.InclinationRad),
            tolerance: 1e-6);

        Assert.Equal(
            AstroConstants.RadiansToDegrees(publicados.LongitudeOfAscendingNodeRad),
            AstroConstants.RadiansToDegrees(recuperados.LongitudeOfAscendingNodeRad),
            tolerance: 1e-6);

        Assert.Equal(
            AstroConstants.RadiansToDegrees(publicados.ArgumentOfPeriapsisRad),
            AstroConstants.RadiansToDegrees(recuperados.ArgumentOfPeriapsisRad),
            tolerance: 1e-6);
    }

    private static StateVector Estado(double julianDate)
        => KeplerPropagator.StateAt(
            Oumuamua(), AstroConstants.SunMuKm3S2, julianDate - AstroConstants.J2000);

    /// <summary>
    /// Elementos osculadores publicados pelo Horizons para 2017-Set-04, com a anomalia
    /// média recuada até J2000.0, que é a época em que o motor guarda os elementos.
    /// </summary>
    private static OrbitalElements Oumuamua()
    {
        const double semiMajorAxisKm = -1.917508420335425e8;
        const double eccentricity = 1.199726944078625;
        const double meanAnomalyAtElementsEpochDeg = -3.733728003189685;

        var meanMotion = KeplerPropagator.MeanMotionRadPerSecond(
            semiMajorAxisKm, AstroConstants.SunMuKm3S2);

        var secondsFromEpoch =
            (ElementsEpochJulianDate - AstroConstants.J2000) * AstroConstants.SecondsPerDay;

        return new OrbitalElements(
            SemiMajorAxisKm: semiMajorAxisKm,
            Eccentricity: eccentricity,
            InclinationRad: AstroConstants.DegreesToRadians(122.7381084274810),
            LongitudeOfAscendingNodeRad: AstroConstants.DegreesToRadians(24.60122487353196),
            ArgumentOfPeriapsisRad: AstroConstants.DegreesToRadians(241.8791434848963),
            MeanAnomalyAtEpochRad:
                AstroConstants.DegreesToRadians(meanAnomalyAtElementsEpochDeg)
                    - meanMotion * secondsFromEpoch);
    }

    private sealed record Referencia(
        double JulianDateTdb,
        Vector3D PositionKm,
        Vector3D VelocityKmS);
}
