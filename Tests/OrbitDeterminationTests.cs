using SolarSim.Engine.Core;
using SolarSim.Engine.Models;

namespace SolarSim.Tests;

/// <summary>
/// O problema inverso. O teste central é o ida e volta: propagar elementos até um
/// instante, converter o estado de volta e exigir os mesmos elementos. Um erro em
/// qualquer um dos dois caminhos quebra a igualdade, a menos que os dois errem
/// exatamente da mesma forma — o que a comparação contra as efemérides do JPL já
/// descarta para o caminho de ida.
/// </summary>
public sealed class OrbitDeterminationTests
{
    private const double Mu = AstroConstants.SunMuKm3S2;

    public static TheoryData<double, double, double, double, double> Orbitas()
    {
        var dados = new TheoryData<double, double, double, double, double>();

        foreach (var eccentricity in new[] { 0.0, 0.001, 0.2, 0.7, 0.95, 1.2, 3.0 })
        {
            foreach (var inclination in new[] { 0.0, 0.5, 23.4, 90.0, 143.0, 180.0 })
            {
                dados.Add(eccentricity, inclination, 41.0, 217.0, 33.0);
                dados.Add(eccentricity, inclination, 0.0, 0.0, 200.0);
            }
        }

        return dados;
    }

    [Theory]
    [MemberData(nameof(Orbitas))]
    public void EstadoSobreviveAoCaminhoDeIdaEVolta(
        double eccentricity,
        double inclinationDeg,
        double nodeDeg,
        double argumentDeg,
        double meanAnomalyDeg)
    {
        var original = Montar(eccentricity, inclinationDeg, nodeDeg, argumentDeg, meanAnomalyDeg);

        foreach (var dia in new[] { -900.0, -13.0, 0.0, 7.0, 365.0 })
        {
            var estado = KeplerPropagator.StateAt(original, Mu, dia);
            var recuperados = OrbitDetermination.ElementsFrom(estado, Mu, dia);
            var refeito = KeplerPropagator.StateAt(recuperados, Mu, dia);

            var desvioPosicao = (refeito.PositionKm - estado.PositionKm).Magnitude;
            var desvioVelocidade = (refeito.VelocityKmS - estado.VelocityKmS).Magnitude;

            Assert.True(
                desvioPosicao < estado.DistanceKm * 1e-10,
                $"Posicao desviou {desvioPosicao:E3} km no dia {dia}.");

            Assert.True(
                desvioVelocidade < estado.SpeedKmS * 1e-10,
                $"Velocidade desviou {desvioVelocidade:E3} km/s no dia {dia}.");
        }
    }

    [Theory]
    [MemberData(nameof(Orbitas))]
    public void FormaETamanhoDaOrbitaSaoRecuperados(
        double eccentricity,
        double inclinationDeg,
        double nodeDeg,
        double argumentDeg,
        double meanAnomalyDeg)
    {
        var original = Montar(eccentricity, inclinationDeg, nodeDeg, argumentDeg, meanAnomalyDeg);

        var estado = KeplerPropagator.StateAt(original, Mu, 88.0);
        var recuperados = OrbitDetermination.ElementsFrom(estado, Mu, 88.0);

        // Semi-eixo, excentricidade e inclinação são bem determinados em qualquer caso;
        // os três ângulos restantes não são, e por isso ficam de fora aqui.
        Assert.Equal(
            original.SemiMajorAxisKm,
            recuperados.SemiMajorAxisKm,
            tolerance: Math.Abs(original.SemiMajorAxisKm) * 1e-10);

        Assert.Equal(original.Eccentricity, recuperados.Eccentricity, tolerance: 1e-10);
        Assert.Equal(original.InclinationRad, recuperados.InclinationRad, tolerance: 1e-10);
    }

    [Theory]
    [InlineData(0.2, 23.4)]
    [InlineData(0.7, 143.0)]
    [InlineData(1.2, 30.0)]
    public void OsSeisElementosVoltamIguaisQuandoNenhumEDegenerado(
        double eccentricity,
        double inclinationDeg)
    {
        var original = Montar(eccentricity, inclinationDeg, 41.0, 217.0, 33.0);

        var estado = KeplerPropagator.StateAt(original, Mu, 88.0);
        var recuperados = OrbitDetermination.ElementsFrom(estado, Mu, 88.0);

        AssertAnguloIgual(
            original.LongitudeOfAscendingNodeRad,
            recuperados.LongitudeOfAscendingNodeRad,
            1e-10);

        AssertAnguloIgual(
            original.ArgumentOfPeriapsisRad,
            recuperados.ArgumentOfPeriapsisRad,
            1e-10);

        if (original.IsClosed)
        {
            AssertAnguloIgual(
                original.MeanAnomalyAtEpochRad, recuperados.MeanAnomalyAtEpochRad, 1e-9);
        }
        else
        {
            Assert.Equal(
                original.MeanAnomalyAtEpochRad,
                recuperados.MeanAnomalyAtEpochRad,
                tolerance: 1e-9);
        }
    }

    [Theory]
    [InlineData("mercury")]
    [InlineData("mars")]
    [InlineData("neptune")]
    [InlineData("moon")]
    [InlineData("io")]
    public void ElementosTabeladosSaoRecuperadosAPartirDoEstadoDeUmCorpoReal(string bodyId)
    {
        var sim = SolarSystem.NewEngine();
        var tabelados = sim.ElementsOf(bodyId)!.Value;
        var mu = sim.GravitationalParameterOf(bodyId);

        // Longe da época de propósito: é o recuo da anomalia média que fica em xeque.
        const double dias = 4_000.0;
        var estado = sim.LocalStateAt(bodyId, AstroConstants.J2000 + dias);

        // A comparação é contra os elementos daquela data, e não contra os de J2000: com
        // as taxas seculares do M15 os dois deixaram de ser a mesma coisa. Io é o caso
        // extremo — o achatamento de Júpiter faz o nodo dela dar meia volta em 4000 dias.
        var naData = sim.ElementsAt(bodyId, AstroConstants.J2000 + dias)!.Value;

        var recuperados = OrbitDetermination.ElementsFrom(estado, mu, dias);

        Assert.Equal(
            naData.SemiMajorAxisKm,
            recuperados.SemiMajorAxisKm,
            tolerance: naData.SemiMajorAxisKm * 1e-10);

        Assert.Equal(naData.Eccentricity, recuperados.Eccentricity, tolerance: 1e-10);
        Assert.Equal(naData.InclinationRad, recuperados.InclinationRad, tolerance: 1e-10);

        AssertAnguloIgual(
            naData.LongitudeOfAscendingNodeRad,
            recuperados.LongitudeOfAscendingNodeRad,
            1e-9);

        AssertAnguloIgual(
            naData.ArgumentOfPeriapsisRad,
            recuperados.ArgumentOfPeriapsisRad,
            1e-9);

        // A anomalia média volta referida a J2000, e nenhum destes corpos tem semi-eixo
        // andando, então ela precisa bater com a tabelada mesmo depois da precessão.
        AssertAnguloIgual(
            tabelados.MeanAnomalyAtEpochRad, recuperados.MeanAnomalyAtEpochRad, 1e-7);
    }

    /// <summary>
    /// A Terra está tabelada com inclinação negativa, como na tabela de Standish de que
    /// os dados vieram. A conversão inversa não tem como devolver isso: ela mede a
    /// inclinação entre zero e pi. O que ela devolve é a mesma órbita na forma canônica,
    /// com o sinal absorvido por meia volta no nodo e no argumento do periápside — e é
    /// isso que este teste fixa, porque é o tipo de coisa que parece defeito.
    /// </summary>
    [Fact]
    public void InclinacaoNegativaVoltaComoMeiaVoltaNoNodoENoArgumento()
    {
        var sim = SolarSystem.NewEngine();
        var tabelados = sim.ElementsOf("earth")!.Value;
        var mu = sim.GravitationalParameterOf("earth");

        Assert.True(tabelados.InclinationRad < 0.0);

        // Os elementos da data, porque o periélio da Terra avança 3,8 segundos de arco
        // por século pela relatividade, e em 4000 dias isso já é maior que a tolerância.
        var naData = sim.ElementsAt("earth", AstroConstants.J2000 + 4_000.0)!.Value;

        var estado = sim.LocalStateAt("earth", AstroConstants.J2000 + 4_000.0);
        var recuperados = OrbitDetermination.ElementsFrom(estado, mu, 4_000.0);

        Assert.Equal(-naData.InclinationRad, recuperados.InclinationRad, tolerance: 1e-16);

        AssertAnguloIgual(
            naData.LongitudeOfAscendingNodeRad + Math.PI,
            recuperados.LongitudeOfAscendingNodeRad,
            1e-9);

        AssertAnguloIgual(
            naData.ArgumentOfPeriapsisRad + Math.PI,
            recuperados.ArgumentOfPeriapsisRad,
            1e-9);

        // A prova de que as duas formas são a mesma órbita: elas propagam para o mesmo
        // lugar em qualquer data, e não só naquela em que o estado foi medido. A
        // comparação é entre duas órbitas fixas, sem a precessão, que aqui só atrapalharia.
        foreach (var dia in new[] { -20_000.0, 0.0, 4_000.0, 50_000.0 })
        {
            var esperado = KeplerPropagator.StateAt(naData, mu, dia - 4_000.0);
            var obtido = KeplerPropagator.StateAt(recuperados, mu, dia);

            var desvio = (obtido.PositionKm - esperado.PositionKm).Magnitude;

            Assert.True(desvio < 1.0, $"Desvio de {desvio:N3} km no dia {dia}.");
        }
    }

    [Fact]
    public void UmaOrbitaMontadaAPartirDeVelocidadeCircularSaiCircular()
    {
        var raio = 7_000.0;
        var mu = 398_600.0;

        // Velocidade circular perpendicular ao raio, no plano da eclíptica.
        var estado = new StateVector(
            new Vector3D(raio, 0.0, 0.0),
            new Vector3D(0.0, Math.Sqrt(mu / raio), 0.0));

        var elementos = OrbitDetermination.ElementsFrom(estado, mu, 0.0);

        Assert.Equal(raio, elementos.SemiMajorAxisKm, tolerance: 1e-9);
        Assert.Equal(0.0, elementos.Eccentricity, tolerance: 1e-15);
        Assert.Equal(0.0, elementos.InclinationRad, tolerance: 1e-15);
    }

    [Fact]
    public void ExcessoDeVelocidadeProduzOrbitaAberta()
    {
        var raio = 7_000.0;
        var mu = 398_600.0;
        var escape = Math.Sqrt(2.0 * mu / raio);

        var estado = new StateVector(
            new Vector3D(raio, 0.0, 0.0),
            new Vector3D(0.0, escape * 1.2, 0.0));

        var elementos = OrbitDetermination.ElementsFrom(estado, mu, 0.0);

        Assert.False(elementos.IsClosed);
        Assert.True(elementos.SemiMajorAxisKm < 0.0);
        Assert.True(elementos.PeriapsisKm > 0.0);
        Assert.True(double.IsPositiveInfinity(elementos.ApoapsisKm));
    }

    [Fact]
    public void QuedaRadialERecusada()
    {
        var estado = new StateVector(
            new Vector3D(7_000.0, 0.0, 0.0),
            new Vector3D(-3.0, 0.0, 0.0));

        var erro = Assert.Throws<ArgumentOutOfRangeException>(
            () => OrbitDetermination.ElementsFrom(estado, 398_600.0, 0.0));

        Assert.Contains("radial", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void VelocidadeDeEscapeExataERecusadaPorSerParabolica()
    {
        var raio = 7_000.0;
        var mu = 398_600.0;

        var estado = new StateVector(
            new Vector3D(raio, 0.0, 0.0),
            new Vector3D(0.0, Math.Sqrt(2.0 * mu / raio), 0.0));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => OrbitDetermination.ElementsFrom(estado, mu, 0.0));
    }

    /// <summary>
    /// Compara dois ângulos pela diferença dada a volta completa: 359 graus e -1 grau
    /// são o mesmo ângulo, e o arquivo de dados usa as duas grafias.
    /// </summary>
    private static void AssertAnguloIgual(double esperado, double obtido, double tolerancia)
    {
        var diferenca = AstroConstants.NormalizeAngle(obtido - esperado + Math.PI) - Math.PI;

        Assert.True(
            Math.Abs(diferenca) < tolerancia,
            $"Angulos diferem em {diferenca:E3} rad: esperado {esperado}, obtido {obtido}.");
    }

    private static OrbitalElements Montar(
        double eccentricity,
        double inclinationDeg,
        double nodeDeg,
        double argumentDeg,
        double meanAnomalyDeg)
    {
        // O periápside fica fixo em meia unidade astronômica para que elipse e hipérbole
        // tenham a mesma escala, e o semi-eixo maior saia negativo quando e passa de 1.
        var periapsis = 0.5 * AstroConstants.AstronomicalUnitKm;

        return new OrbitalElements(
            SemiMajorAxisKm: periapsis / (1.0 - eccentricity),
            Eccentricity: eccentricity,
            InclinationRad: AstroConstants.DegreesToRadians(inclinationDeg),
            LongitudeOfAscendingNodeRad: AstroConstants.DegreesToRadians(nodeDeg),
            ArgumentOfPeriapsisRad: AstroConstants.DegreesToRadians(argumentDeg),
            MeanAnomalyAtEpochRad: eccentricity < 1.0
                ? AstroConstants.DegreesToRadians(meanAnomalyDeg)
                : AstroConstants.DegreesToRadians(meanAnomalyDeg) - Math.PI);
    }
}
