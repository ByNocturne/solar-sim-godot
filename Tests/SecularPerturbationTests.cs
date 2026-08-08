using SolarSim.Engine.Core;
using SolarSim.Engine.Models;

namespace SolarSim.Tests;

/// <summary>
/// O M15 tira a órbita da imobilidade: os elementos passam a andar. O teste de ouro é o
/// periélio de Mercúrio, que a mecânica newtoniana errava por 43 segundos de arco por
/// século e que a relatividade explicou.
/// </summary>
public sealed class SecularPerturbationTests
{
    /// <summary>Segundos de arco por radiano.</summary>
    private const double ArcsecPerRad = 180.0 * 3600.0 / Math.PI;

    [Fact]
    public void PerielioDeMercurioAvancaOsQuarentaETresSegundosDeArcoPorSeculo()
    {
        var sim = SolarSystem.NewEngine();

        var arcsecPorSeculo = AstroConstants.RadPerSecondToArcsecPerCentury(
            sim.SecularRatesOf("mercury").ArgumentOfPeriapsisRadPerSecond);

        // O valor clássico é 42,98"/século. A folga cobre o achatamento do Sol, que
        // acrescenta cinco centésimos de segundo de arco, e o arredondamento dos dados.
        Assert.InRange(arcsecPorSeculo, 42.5, 43.5);
    }

    /// <summary>
    /// A precessão de Mercúrio é relativística, e não um efeito do achatamento do Sol:
    /// o J₂ solar responde por cerca de um milésimo do total. Se um dia os dois números
    /// se aproximarem, é sinal de que o J₂ do arquivo saiu da ordem de grandeza real.
    /// </summary>
    [Fact]
    public void OAchatamentoDoSolEDesprezivelDianteDaRelatividade()
    {
        var sim = SolarSystem.NewEngine();
        var elementos = sim.ElementsOf("mercury")!.Value;
        var mu = sim.GravitationalParameterOf("mercury");
        var sol = sim.BodyOf("sun");

        var relatividade = AstroConstants.RadPerSecondToArcsecPerCentury(
            SecularPerturbations.RelativisticApsidalRateRadPerSecond(elementos, mu));

        var achatamento = AstroConstants.RadPerSecondToArcsecPerCentury(
            SecularPerturbations.OblatenessApsidalRateRadPerSecond(
                elementos, mu, sol.J2, sol.J2ReferenceRadiusKm));

        Assert.InRange(achatamento, 0.0, 0.1);
        Assert.True(
            achatamento < relatividade / 100.0,
            $"Achatamento {achatamento:N4}\"/século contra relatividade "
                + $"{relatividade:N4}\"/século.");
    }

    /// <summary>
    /// A regressão dos nodos de uma órbita baixa é o caso didático do J₂, e o número é
    /// conhecido: pouco menos de cinco graus por dia na inclinação da Estação Espacial.
    /// </summary>
    [Fact]
    public void OrbitaBaixaRegridOsNodosPoucoMenosDeCincoGrausPorDia()
    {
        var elementos = new OrbitalElements(
            SemiMajorAxisKm: 7_000.0,
            Eccentricity: 0.0,
            InclinationRad: AstroConstants.DegreesToRadians(51.6),
            LongitudeOfAscendingNodeRad: 0.0,
            ArgumentOfPeriapsisRad: 0.0,
            MeanAnomalyAtEpochRad: 0.0);

        var grausPorDia = AstroConstants.RadiansToDegrees(
            SecularPerturbations.OblatenessNodalRateRadPerSecond(
                elementos, 398_600.4418, 1.08262668e-3, 6_378.137)
                * AstroConstants.SecondsPerDay);

        Assert.InRange(grausPorDia, -4.55, -4.38);
    }

    /// <summary>
    /// Na inclinação crítica de 63,4 graus o fator 5cos²i − 1 zera e o periápside para de
    /// girar. É o que mantém o apogeu de uma órbita Molniya sempre sobre o mesmo
    /// hemisfério, e serve aqui como verificação de que o sinal do termo está certo.
    /// </summary>
    [Fact]
    public void NaInclinacaoCriticaOPeriapsideNaoGira()
    {
        var critica = Math.Acos(Math.Sqrt(0.2));

        var abaixo = ApsidalPorJ2(critica - 0.2);
        var naCritica = ApsidalPorJ2(critica);
        var acima = ApsidalPorJ2(critica + 0.2);

        Assert.Equal(0.0, naCritica, tolerance: 1e-18);
        Assert.True(abaixo > 0.0, "Abaixo da crítica o periápside deveria avançar.");
        Assert.True(acima < 0.0, "Acima da crítica o periápside deveria recuar.");

        static double ApsidalPorJ2(double inclinationRad)
            => SecularPerturbations.OblatenessApsidalRateRadPerSecond(
                new OrbitalElements(7_000.0, 0.0, inclinationRad, 0.0, 0.0, 0.0),
                398_600.4418,
                1.08262668e-3,
                6_378.137);
    }

    /// <summary>
    /// Uma órbita aberta não tem precessão média: o corpo passa uma vez, e uma taxa por
    /// volta não descreveria nada.
    /// </summary>
    [Fact]
    public void OrbitaAbertaNaoRecebeTaxaNenhuma()
    {
        var hiperbole = new OrbitalElements(
            SemiMajorAxisKm: -50_000.0,
            Eccentricity: 1.6,
            InclinationRad: 0.3,
            LongitudeOfAscendingNodeRad: 0.0,
            ArgumentOfPeriapsisRad: 0.0,
            MeanAnomalyAtEpochRad: 0.0);

        var taxas = SecularPerturbations.For(
            hiperbole, 398_600.4418, 1.08262668e-3, 6_378.137);

        Assert.True(taxas.IsZero);
    }

    /// <summary>
    /// Sem taxa, o caminho novo é o antigo — não equivalente, o mesmo. É isso que garante
    /// que a regressão contra o JPL não possa se mexer por causa deste marco.
    /// </summary>
    [Fact]
    public void SemTaxaOResultadoEIdenticoAoPropagadorDeSempre()
    {
        var elementos = new OrbitalElements(
            SemiMajorAxisKm: 1.5e8,
            Eccentricity: 0.2,
            InclinationRad: 0.4,
            LongitudeOfAscendingNodeRad: 1.1,
            ArgumentOfPeriapsisRad: 2.3,
            MeanAnomalyAtEpochRad: 0.7);

        foreach (var dia in new[] { -12_345.0, 0.0, 137.0, 40_000.0 })
        {
            var esperado = KeplerPropagator.StateAt(elementos, AstroConstants.SunMuKm3S2, dia);

            var obtido = SecularPropagator.StateAt(
                elementos, OrbitalElementRates.None, AstroConstants.SunMuKm3S2, dia);

            Assert.Equal(esperado.PositionKm, obtido.PositionKm);
            Assert.Equal(esperado.VelocityKmS, obtido.VelocityKmS);
        }
    }

    [Fact]
    public void NaPropriaEpocaOsElementosSaoOsDeclarados()
    {
        var sim = SolarSystem.NewEngine();

        var declarados = sim.ElementsOf("mercury")!.Value;
        var naEpoca = sim.ElementsAt("mercury", AstroConstants.J2000)!.Value;

        Assert.Equal(declarados.SemiMajorAxisKm, naEpoca.SemiMajorAxisKm, tolerance: 1e-9);
        Assert.Equal(declarados.Eccentricity, naEpoca.Eccentricity, tolerance: 1e-15);
        Assert.Equal(declarados.InclinationRad, naEpoca.InclinationRad, tolerance: 1e-15);

        Assert.Equal(
            declarados.ArgumentOfPeriapsisRad,
            naEpoca.ArgumentOfPeriapsisRad,
            tolerance: 1e-15);
    }

    /// <summary>
    /// A precessão medida de ponta a ponta, e não pela taxa: o argumento do periápside de
    /// Mercúrio um século depois menos o de J2000 precisa dar os mesmos 43 segundos de
    /// arco. É o teste que pega um erro de unidade entre a taxa e a integração dela.
    /// </summary>
    [Fact]
    public void OPeriapsideDeMercurioAndaOMesmoTantoQuandoMedidoPelaDiferenca()
    {
        var sim = SolarSystem.NewEngine();

        var epoca = sim.ElementsAt("mercury", AstroConstants.J2000)!.Value;

        var seculoDepois = sim.ElementsAt(
            "mercury", AstroConstants.J2000 + AstroConstants.DaysPerJulianCentury)!.Value;

        var avanco = DiferencaAngular(
            seculoDepois.ArgumentOfPeriapsisRad, epoca.ArgumentOfPeriapsisRad);

        Assert.InRange(avanco * ArcsecPerRad, 42.5, 43.5);
    }

    /// <summary>
    /// O tempo continua reversível: um século antes da época o periápside está atrasado
    /// exatamente o quanto estará adiantado um século depois. É o invariante 4 ainda de
    /// pé com as taxas ligadas.
    /// </summary>
    [Fact]
    public void UmSeculoAntesDaEpocaAPrecessaoTemOMesmoTamanhoEOSinalTrocado()
    {
        var sim = SolarSystem.NewEngine();
        var epoca = sim.ElementsAt("mercury", AstroConstants.J2000)!.Value;

        var depois = DiferencaAngular(
            sim.ElementsAt(
                "mercury",
                AstroConstants.J2000 + AstroConstants.DaysPerJulianCentury)!
                .Value.ArgumentOfPeriapsisRad,
            epoca.ArgumentOfPeriapsisRad);

        var antes = DiferencaAngular(
            sim.ElementsAt(
                "mercury",
                AstroConstants.J2000 - AstroConstants.DaysPerJulianCentury)!
                .Value.ArgumentOfPeriapsisRad,
            epoca.ArgumentOfPeriapsisRad);

        Assert.Equal(-depois, antes, tolerance: 1e-15);
    }

    /// <summary>
    /// A precessão gira a órbita, não a redimensiona: semi-eixo, excentricidade e período
    /// ficam onde estavam. Se um deles andasse, a energia da órbita mudaria sozinha.
    /// </summary>
    [Fact]
    public void APrecessaoNaoMexeNoTamanhoNemNaFormaDaOrbita()
    {
        var sim = SolarSystem.NewEngine();
        var epoca = sim.ElementsOf("mercury")!.Value;

        foreach (var seculos in new[] { -20.0, -1.0, 1.0, 20.0 })
        {
            var naData = sim.ElementsAt(
                "mercury",
                AstroConstants.J2000 + seculos * AstroConstants.DaysPerJulianCentury)!.Value;

            Assert.Equal(epoca.SemiMajorAxisKm, naData.SemiMajorAxisKm, tolerance: 1e-6);
            Assert.Equal(epoca.Eccentricity, naData.Eccentricity, tolerance: 1e-15);
        }
    }

    /// <summary>
    /// Io é o caso em que o achatamento manda: o perijove dela dá uma volta completa em
    /// poucos anos, contra os milhares de séculos que a relatividade levaria.
    /// </summary>
    [Fact]
    public void OPerijoveDeIoGiraEmPoucosAnosPorCausaDoAchatamentoDeJupiter()
    {
        var sim = SolarSystem.NewEngine();
        var taxas = sim.SecularRatesOf("io");

        var anos = AstroConstants.TwoPi
            / taxas.ArgumentOfPeriapsisRadPerSecond
            / AstroConstants.SecondsPerDay
            / 365.25;

        Assert.InRange(anos, 2.0, 10.0);
        Assert.True(
            taxas.LongitudeOfAscendingNodeRadPerSecond < 0.0,
            "O nodo de uma órbita direta regride.");
    }

    /// <summary>
    /// A precessão do perigeu lunar, de 8,85 anos, é solar. Pelo achatamento da Terra
    /// sozinho a Lua mal se mexe, e este teste fixa esse limite conhecido para que
    /// ninguém tome o número de tela por realismo que o motor ainda não tem.
    /// </summary>
    [Fact]
    public void APrecessaoDoPerigeuLunarNaoEExplicadaPeloAchatamentoDaTerra()
    {
        var sim = SolarSystem.NewEngine();

        var grausPorSeculo = AstroConstants.RadiansToDegrees(
            sim.SecularRatesOf("moon").ArgumentOfPeriapsisRadPerSecond
                * AstroConstants.SecondsPerJulianCentury);

        // A precessão real é de 360 graus a cada 8,85 anos, ou seja, quatro mil graus por
        // século. O que o modelo entrega é menos de um.
        Assert.InRange(grausPorSeculo, 0.0, 1.0);
    }

    /// <summary>
    /// Com o semi-eixo maior andando, a anomalia média deixa de ser M₀ + n·t: o que vale é
    /// a integral do movimento médio. A forma fechada usada pelo motor é conferida contra
    /// integração numérica, que é uma rota independente para o mesmo número.
    /// </summary>
    [Fact]
    public void AAnomaliaMediaComSemiEixoAndandoConfereComIntegracaoNumerica()
    {
        const double muKm3S2 = 1.0e5;
        const double semiMajorAxisKm = 1.0e6;
        const double dias = 1_000.0;

        var seconds = dias * AstroConstants.SecondsPerDay;

        // Dez por cento de crescimento no trecho: exagerado para uma taxa secular real,
        // e é justamente por isso que separa a integral certa da aproximação preguiçosa.
        var ratePerSecond = 0.1 * semiMajorAxisKm / seconds;

        var elementos = new OrbitalElements(
            semiMajorAxisKm, 0.1, 0.0, 0.0, 0.0, MeanAnomalyAtEpochRad: 0.0);

        var taxas = new OrbitalElementRates(ratePerSecond, 0.0, 0.0, 0.0, 0.0);

        var obtido = SecularPropagator.MeanAnomalyAt(elementos, taxas, muKm3S2, dias);

        var esperado = AstroConstants.NormalizeAngle(
            IntegralDoMovimentoMedio(semiMajorAxisKm, ratePerSecond, muKm3S2, seconds));

        Assert.Equal(0.0, DiferencaAngular(obtido, esperado), tolerance: 1e-9);
    }

    /// <summary>
    /// Sem taxa no semi-eixo, a integral precisa devolver exatamente n·t: é o caso comum,
    /// e um fator que não valesse 1 ali contaminaria todo o sistema.
    /// </summary>
    [Fact]
    public void SemDerivaNoSemiEixoAAnomaliaMediaEADeSempre()
    {
        var elementos = new OrbitalElements(1.0e6, 0.1, 0.0, 0.0, 0.0, 0.5);
        var taxas = new OrbitalElementRates(0.0, 0.0, 0.0, 0.3e-12, 0.7e-12);

        foreach (var dia in new[] { -5_000.0, 0.0, 900.0 })
        {
            Assert.Equal(
                KeplerPropagator.MeanAnomalyAt(elementos, 1.0e5, dia),
                SecularPropagator.MeanAnomalyAt(elementos, taxas, 1.0e5, dia));
        }
    }

    /// <summary>
    /// A integral tem dois ramos — série de Taylor para deriva pequena, forma fechada para
    /// deriva grande — e os dois precisam concordar com a integração numérica na emenda.
    /// Comparar os ramos entre si não diria nada: as duas derivas são diferentes, e o
    /// resultado tem mesmo que diferir.
    /// </summary>
    /// <remarks>
    /// Uma deriva secular real cai sempre do lado da série, que é o ramo em que a forma
    /// fechada subtrairia dois números quase iguais.
    /// </remarks>
    [Theory]
    [InlineData(0.999e-4)]
    [InlineData(1.001e-4)]
    public void OsDoisRamosDaIntegralConcordamComAIntegracaoNumerica(double fracao)
    {
        const double muKm3S2 = 1.0e5;
        const double semiMajorAxisKm = 1.0e6;
        const double dias = 1_000.0;

        var seconds = dias * AstroConstants.SecondsPerDay;
        var ratePerSecond = fracao * semiMajorAxisKm / seconds;

        var elementos = new OrbitalElements(semiMajorAxisKm, 0.0, 0.0, 0.0, 0.0, 0.0);
        var taxas = new OrbitalElementRates(ratePerSecond, 0.0, 0.0, 0.0, 0.0);

        var obtido = SecularPropagator.MeanAnomalyAt(elementos, taxas, muKm3S2, dias);

        var esperado = AstroConstants.NormalizeAngle(
            IntegralDoMovimentoMedio(semiMajorAxisKm, ratePerSecond, muKm3S2, seconds));

        // Bem mais apertado que os 4e-6 rad que separam as duas derivas testadas: um
        // degrau na emenda apareceria aqui, e não passaria por diferença física.
        Assert.Equal(0.0, DiferencaAngular(obtido, esperado), tolerance: 1e-8);
    }

    /// <summary>
    /// Extrapolar um bilhão de anos é abuso, mas precisa devolver número: o salto
    /// geológico do M13 passa por aqui, e um <c>NaN</c> nascido nesta conta só apareceria
    /// três camadas acima, como um corpo desaparecido da tela.
    /// </summary>
    [Fact]
    public void ExtrapolacaoAbsurdaContinuaDevolvendoNumero()
    {
        var elementos = new OrbitalElements(1.0e6, 0.5, 0.2, 0.3, 0.4, 0.5);

        // Taxas grandes o bastante para levar o semi-eixo abaixo de zero e a
        // excentricidade além de 1, que são justamente as duas coisas que o motor recusa.
        var taxas = new OrbitalElementRates(-1.0, -1.0e-6, 0.0, 0.0, 0.0);

        var estado = SecularPropagator.StateAt(elementos, taxas, 1.0e5, 365_250_000_000.0);

        Assert.True(double.IsFinite(estado.PositionKm.Magnitude));
        Assert.True(double.IsFinite(estado.VelocityKmS.Magnitude));
    }

    /// <summary>
    /// Integração de n(t) = √(μ/a(t)³) por Simpson, com a mesma condição inicial que o
    /// motor recebe. É a rota independente que confere a forma fechada.
    /// </summary>
    private static double IntegralDoMovimentoMedio(
        double semiMajorAxisKm,
        double ratePerSecond,
        double muKm3S2,
        double seconds)
    {
        const int passos = 100_000;

        var passo = seconds / passos;
        var soma = MovimentoMedio(0.0) + MovimentoMedio(seconds);

        for (var indice = 1; indice < passos; indice++)
        {
            soma += MovimentoMedio(indice * passo) * (indice % 2 == 0 ? 2.0 : 4.0);
        }

        return soma * passo / 3.0;

        double MovimentoMedio(double instante)
            => KeplerPropagator.MeanMotionRadPerSecond(
                semiMajorAxisKm + ratePerSecond * instante, muKm3S2);
    }

    /// <summary>Diferença entre dois ângulos, trazida para (-pi, pi].</summary>
    private static double DiferencaAngular(double left, double right)
    {
        var diferenca = AstroConstants.NormalizeAngle(left - right);

        return diferenca > Math.PI ? diferenca - AstroConstants.TwoPi : diferenca;
    }
}
