using SolarSim.Bridge;
using SolarSim.Engine;
using SolarSim.Engine.Core;
using SolarSim.Engine.Data;
using SolarSim.Engine.Models;

namespace SolarSim.Tests;

/// <summary>
/// O M18 acrescenta drift secular do semi-eixo: Yarkovsky documentado e pressão de
/// radiação via Poynting–Robertson. Continua sendo f(JD), nunca força por quadro.
/// </summary>
public sealed class NonGravitationalDriftTests
{
    /// <summary>
    /// Bennu é o NEO cujo Yarkovsky a OSIRIS-REx/radar mediram com precisão: −19×10⁻⁴
    /// UA/Myr (Chesley et al. 2014). É o teste de ouro do marco.
    /// </summary>
    [Fact]
    public void BennuEncolheOSemiEixoNaOrdemDeGrandezaMedida()
    {
        var sim = SolarSystem.NewEngineWithCatalog();
        var daKmPorSegundo = sim.SecularRatesOf("bennu").SemiMajorAxisKmPerSecond;

        var daAuPorMyr = daKmPorSegundo
            * AstroConstants.YearsPerMillion
            * AstroConstants.SecondsPerJulianYear
            / AstroConstants.AstronomicalUnitKm;

        Assert.InRange(daAuPorMyr, -0.0020, -0.0018);
    }

    /// <summary>
    /// Um milhão de anos depois da época o semi-eixo de Bennu cai cerca de 0,0019 UA —
    /// e um milhão antes sobe o mesmo tanto. É o invariante 4 aplicado ao drift.
    /// </summary>
    [Fact]
    public void OSemiEixoDeBennuEhFuncaoPuraDaDataJuliana()
    {
        var sim = SolarSystem.NewEngineWithCatalog();
        var naEpoca = sim.ElementsAt("bennu", AstroConstants.J2000)!.Value.SemiMajorAxisKm;

        var umMilhaoDepois = sim.ElementsAt(
            "bennu",
            AstroConstants.J2000 + AstroConstants.YearsPerMillion * AstroConstants.DaysPerJulianYear)!
            .Value.SemiMajorAxisKm;

        var umMilhaoAntes = sim.ElementsAt(
            "bennu",
            AstroConstants.J2000 - AstroConstants.YearsPerMillion * AstroConstants.DaysPerJulianYear)!
            .Value.SemiMajorAxisKm;

        var deltaDepois = (umMilhaoDepois - naEpoca) / AstroConstants.AstronomicalUnitKm;
        var deltaAntes = (umMilhaoAntes - naEpoca) / AstroConstants.AstronomicalUnitKm;

        Assert.InRange(deltaDepois, -0.0020, -0.0018);
        Assert.InRange(deltaAntes, 0.0018, 0.0020);
        Assert.Equal(-deltaDepois, deltaAntes, tolerance: 1e-12);
    }

    /// <summary>
    /// Planetas não declaram parâmetros NG, então o semi-eixo deles não anda — o caminho
    /// do M2/M15 continua o mesmo.
    /// </summary>
    [Fact]
    public void PlanetasNaoTemDriftDeSemiEixo()
    {
        var sim = SolarSystem.NewEngineWithCatalog();

        Assert.Equal(0.0, sim.SecularRatesOf("earth").SemiMajorAxisKmPerSecond);
        Assert.Equal(0.0, sim.SecularRatesOf("mars").SemiMajorAxisKmPerSecond);
        Assert.Equal(0.0, sim.SecularRatesOf("ceres").SemiMajorAxisKmPerSecond);
    }

    /// <summary>
    /// A conversão UA/Myr → km/s é exatamente a que o motor usa, e −0,0019 UA/Myr são
    /// cerca de −284 m/ano — o número que a literatura cita em metros.
    /// </summary>
    [Fact]
    public void ConversaoYarkovskyBateComMetrosPorAno()
    {
        var kmPorSegundo = NonGravitationalDrift.SemiMajorAxisKmPerSecondFromYarkovsky(-0.0019);
        var metrosPorAno = kmPorSegundo * AstroConstants.SecondsPerJulianYear * 1000.0;

        Assert.InRange(metrosPorAno, -285.0, -283.0);
    }

    /// <summary>
    /// Pressão de radiação com β positivo encolhe a órbita (Poynting–Robertson) e a
    /// excentricidade. Fixture analítica, sem depender do catálogo.
    /// </summary>
    [Fact]
    public void PressaoDeRadiacaoEncolheSemiEixoEExcentricidade()
    {
        var elements = new OrbitalElements(
            SemiMajorAxisKm: AstroConstants.AstronomicalUnitKm,
            Eccentricity: 0.1,
            InclinationRad: 0.0,
            LongitudeOfAscendingNodeRad: 0.0,
            ArgumentOfPeriapsisRad: 0.0,
            MeanAnomalyAtEpochRad: 0.0);

        const double beta = 1e-3;

        var rates = NonGravitationalDrift.For(
            elements,
            AstroConstants.SunMuKm3S2,
            new NonGravitationalParameters(YarkovskyDaAuPerMyr: null, RadiationPressureBeta: beta));

        Assert.True(rates.SemiMajorAxisKmPerSecond < 0.0);
        Assert.True(rates.EccentricityPerSecond < 0.0);

        var da = NonGravitationalDrift.PoyntingRobertsonDaKmPerSecond(
            elements, AstroConstants.SunMuKm3S2, beta);
        var de = NonGravitationalDrift.PoyntingRobertsonDePerSecond(
            elements, AstroConstants.SunMuKm3S2, beta);

        Assert.Equal(da, rates.SemiMajorAxisKmPerSecond);
        Assert.Equal(de, rates.EccentricityPerSecond);
    }

    /// <summary>
    /// Sem parâmetros, o avaliador devolve zero — e o caminho de propagação fica o de
    /// sempre, inclusive por igualdade exata quando só há isso.
    /// </summary>
    [Fact]
    public void SemParametrosNaoHaTaxa()
    {
        var elements = OrbitalElements.FromAuAndDegrees(1.0, 0.0, 0.0, 0.0, 0.0, 0.0);

        Assert.True(
            NonGravitationalDrift.For(
                elements, AstroConstants.SunMuKm3S2, NonGravitationalParameters.None).IsZero);
    }

    [Fact]
    public void CorpoComYarkovskyGanhaFlagDeEnsino()
    {
        var sim = SolarSystem.NewEngineWithCatalog();
        var ambiente = new EnvironmentService(
            EnvironmentLoader.FromFile(SolarSystem.EnvironmentPath));

        Assert.Contains(
            "yarkovsky_drift",
            ambiente.ReportFor(sim, "bennu").ExplanationFlags);
        Assert.DoesNotContain(
            "yarkovsky_drift",
            ambiente.ReportFor(sim, "earth").ExplanationFlags);
    }

    [Fact]
    public void InspetorMostraODriftDeBennu()
    {
        var sim = SolarSystem.NewEngineWithCatalog();
        var relatorio = BodyReport.For(sim, "bennu", AstroConstants.J2000);

        Assert.True(relatorio.SemiMajorAxisKmPerSecond < 0.0);
        Assert.Equal(
            "-0,0019 UA/Myr",
            DisplayFormat.SemiMajorAxisDrift(relatorio.SemiMajorAxisKmPerSecond));
    }

    [Fact]
    public void ParametroNgEmOrbitaAbertaERecusadoNaCarga()
    {
        const string json = """
            {
              "schemaVersion": 1,
              "epoch": { "name": "J2000.0", "julianDate": 2451545.0 },
              "bodies": [
                {
                  "id": "sun",
                  "name": "Sol",
                  "kind": "star",
                  "muKm3S2": 1.32712440018e11,
                  "radiusKm": 695700.0,
                  "colorRgb": "#FFFFFF"
                },
                {
                  "id": "visitor",
                  "name": "Visitante",
                  "parent": "sun",
                  "kind": "comet",
                  "muKm3S2": 1.0,
                  "radiusKm": 1.0,
                  "colorRgb": "#FFFFFF",
                  "nonGravitational": { "yarkovskyDaAuPerMyr": -0.001 },
                  "orbit": {
                    "semiMajorAxisAu": -2.0,
                    "eccentricity": 1.5,
                    "inclinationDeg": 0.0,
                    "longitudeOfAscendingNodeDeg": 0.0,
                    "argumentOfPeriapsisDeg": 0.0,
                    "meanAnomalyAtEpochDeg": 0.0
                  }
                }
              ]
            }
            """;

        var erro = Assert.Throws<SystemDataException>(() => DataLoader.Parse(json));
        Assert.Contains("nonGravitational", erro.Message, StringComparison.Ordinal);
    }
}
