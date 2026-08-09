using SolarSim.Engine.Core;
using SolarSim.Engine.Data;
using SolarSim.Engine.Models;

namespace SolarSim.Tests;

/// <summary>
/// O catálogo de corpos menores como ele está no repositório: o que ele contém, o que a
/// carga recusa e se os números batem com o que se sabe dos corpos.
/// </summary>
public sealed class MinorBodyCatalogTests
{
    [Fact]
    public void CatalogoSomaAoSistemaSemTirarNada()
    {
        var semCatalogo = SolarSystem.NewEngine();
        var comCatalogo = SolarSystem.NewEngineWithCatalog();

        var ids = comCatalogo.Bodies.Select(body => body.Id).ToHashSet(StringComparer.Ordinal);

        Assert.All(semCatalogo.Bodies, body => Assert.Contains(body.Id, ids));
        Assert.True(comCatalogo.Bodies.Count > semCatalogo.Bodies.Count);
    }

    [Fact]
    public void TodoCorpoDoCatalogoOrbitaAEstrelaEEDeClasseMenor()
    {
        var catalogo = DataLoader.ParseCatalog(SolarSystem.CatalogJson);

        Assert.NotEmpty(catalogo);

        Assert.All(catalogo, body =>
        {
            Assert.Equal("sun", body.ParentId);
            Assert.True(
                BodyKinds.IsMinor(body.Kind),
                $"'{body.Id}' está no catálogo com a classe '{BodyKinds.JsonName(body.Kind)}'.");
        });
    }

    /// <summary>
    /// O catálogo cobre as seis classes de corpo menor. Sem isso o filtro da árvore teria
    /// botões que não filtram nada, e a variedade que motiva o marco não existiria.
    /// </summary>
    [Fact]
    public void CatalogoCobreAsSeisClassesDeCorpoMenor()
    {
        var presentes = DataLoader
            .ParseCatalog(SolarSystem.CatalogJson)
            .Select(body => body.Kind)
            .ToHashSet();

        Assert.All(BodyKinds.Minor, kind => Assert.Contains(kind, presentes));
    }

    [Fact]
    public void TodoCorpoDoCatalogoDeclaraFamilia()
    {
        var catalogo = DataLoader.ParseCatalog(SolarSystem.CatalogJson);

        Assert.All(catalogo, body => Assert.False(string.IsNullOrWhiteSpace(body.Family)));
    }

    /// <summary>
    /// Cada corpo do catálogo tem massa, e portanto esfera de influência. Sem massa o
    /// inspetor esconderia a linha da esfera, e a emenda de cônicas nunca entregaria uma
    /// sonda a um asteroide.
    /// </summary>
    [Fact]
    public void TodoCorpoDoCatalogoTemEsferaDeInfluenciaPositiva()
    {
        var sim = SolarSystem.NewEngineWithCatalog();
        var catalogo = DataLoader.ParseCatalog(SolarSystem.CatalogJson);

        Assert.All(catalogo, body =>
        {
            var raio = sim.SphereOfInfluenceKm(body.Id);

            Assert.True(raio > 0.0 && double.IsFinite(raio), $"'{body.Id}' com SOI de {raio} km.");
        });
    }

    /// <summary>
    /// A esfera de influência de Ceres é publicada em torno de 78 mil km, e é o valor que
    /// prova que a massa derivada e o semi-eixo estão os dois certos.
    /// </summary>
    [Fact]
    public void EsferaDeInfluenciaDeCeresBateComOValorPublicado()
    {
        var sim = SolarSystem.NewEngineWithCatalog();

        Assert.Equal(78_000.0, sim.SphereOfInfluenceKm("ceres"), 2_000.0);
    }

    [Theory]
    [InlineData("ceres", 4.60)]
    [InlineData("vesta", 3.63)]
    [InlineData("halley", 75.9)]
    [InlineData("encke", 3.30)]
    [InlineData("pluto", 248.0)]
    public void PeriodoBateComOPublicado(string bodyId, double anosEsperados)
    {
        var sim = SolarSystem.NewEngineWithCatalog();
        var elementos = sim.ElementsOf(bodyId)!.Value;

        var anos = KeplerPropagator.OrbitalPeriodDays(
            elementos.SemiMajorAxisKm, sim.GravitationalParameterOf(bodyId)) / 365.25;

        Assert.Equal(anosEsperados, anos, anosEsperados * 0.01);
    }

    /// <summary>
    /// Troiano é o corpo preso na ressonância 1:1 com Júpiter, e o que isso quer dizer é
    /// que o semi-eixo dele é o de Júpiter. É o teste que denuncia um elemento trocado de
    /// linha no CSV: um asteroide qualquer não cai em 5,2 UA por acidente.
    /// </summary>
    [Fact]
    public void TroianosCompartilhamOSemiEixoDeJupiter()
    {
        var sim = SolarSystem.NewEngineWithCatalog();
        var jupiter = sim.ElementsOf("jupiter")!.Value.SemiMajorAxisKm;

        var troianos = sim.Bodies.Where(body => body.Kind == BodyKind.Trojan).ToArray();

        Assert.NotEmpty(troianos);

        Assert.All(troianos, body =>
        {
            var razao = body.Elements!.Value.SemiMajorAxisKm / jupiter;

            Assert.InRange(razao, 0.98, 1.02);
        });
    }

    /// <summary>
    /// O Halley é retrógrado: percorre a órbita no sentido contrário ao dos planetas, o
    /// que na convenção dos elementos aparece como inclinação maior que 90 graus.
    /// </summary>
    [Fact]
    public void HalleyEhRetrogrado()
    {
        var sim = SolarSystem.NewEngineWithCatalog();
        var inclinacaoGraus = AstroConstants.RadiansToDegrees(
            sim.ElementsOf("halley")!.Value.InclinationRad);

        Assert.InRange(inclinacaoGraus, 90.0, 180.0);
    }

    /// <summary>
    /// Sedna é destacada porque o periélio dela, de 76 UA, está longe demais para Netuno
    /// ter posto o corpo ali. É o número que define a família declarada no arquivo.
    /// </summary>
    /// <remarks>
    /// O periélio é o que se confere, e não o período: os elementos do arquivo são
    /// osculadores em J2000, e o semi-eixo osculador de Sedna vale 550 UA contra as 506 UA
    /// do ajuste médio que a literatura publica — 12.900 anos de período em vez dos 11.400
    /// citados. As duas coisas descrevem a mesma órbita; a diferença é qual instante se
    /// escolheu para congelá-la.
    /// </remarks>
    [Fact]
    public void PerielioDeSednaEstaLongeDeNetuno()
    {
        var sim = SolarSystem.NewEngineWithCatalog();

        var perielioUa = sim.ElementsOf("sedna")!.Value.PeriapsisKm
            / AstroConstants.AstronomicalUnitKm;

        Assert.Equal(76.0, perielioUa, 1.0);
    }

    /// <summary>
    /// A massa derivada da densidade tem de ser a massa: com o raio e a densidade
    /// publicados de Ceres, a fórmula reproduz o GM que a Dawn mediu.
    /// </summary>
    [Fact]
    public void MassaDerivadaDaDensidadeReproduzOGmMedidoDeCeres()
    {
        var derivado = BodyMass.MuFromDensity(2.162, 469.7);

        Assert.Equal(62.6284, derivado, 62.6284 * 0.01);
    }

    [Fact]
    public void CatalogoRecusaCorpoSemPai()
    {
        var erro = Assert.Throws<SystemDataException>(
            () => DataLoader.ParseCatalog(Catalogo("""
                {
                  "id": "orfao",
                  "name": "Órfão",
                  "kind": "asteroid",
                  "densityGCm3": 2.0,
                  "radiusKm": 1.0
                }
                """)));

        Assert.Contains("orfao", erro.Message, StringComparison.Ordinal);
        Assert.Contains("parent", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CatalogoRecusaClasseQueNaoEhDeCorpoMenor()
    {
        var erro = Assert.Throws<SystemDataException>(
            () => DataLoader.ParseCatalog(Catalogo(CorpoDeTeste(kind: "planet"))));

        Assert.Contains("planet", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CargaRecusaClasseDesconhecida()
    {
        var erro = Assert.Throws<SystemDataException>(
            () => DataLoader.ParseCatalog(Catalogo(CorpoDeTeste(kind: "planetoide"))));

        Assert.Contains("planetoide", erro.Message, StringComparison.Ordinal);
        Assert.Contains("transNeptunian", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CargaRecusaMassaEDensidadeAoMesmoTempo()
    {
        var erro = Assert.Throws<SystemDataException>(
            () => DataLoader.ParseCatalog(Catalogo("""
                {
                  "id": "teste",
                  "name": "Teste",
                  "parent": "sun",
                  "kind": "asteroid",
                  "muKm3S2": 1.0,
                  "densityGCm3": 2.0,
                  "radiusKm": 1.0,
                  "orbit": {
                    "semiMajorAxisAu": 2.5,
                    "eccentricity": 0.1,
                    "inclinationDeg": 5.0,
                    "longitudeOfAscendingNodeDeg": 10.0,
                    "argumentOfPeriapsisDeg": 20.0,
                    "meanAnomalyAtEpochDeg": 30.0
                  }
                }
                """)));

        Assert.Contains("teste", erro.Message, StringComparison.Ordinal);
        Assert.Contains("densityGCm3", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CargaRecusaCorpoSemMassaESemDensidade()
    {
        var erro = Assert.Throws<SystemDataException>(
            () => DataLoader.ParseCatalog(Catalogo("""
                {
                  "id": "teste",
                  "name": "Teste",
                  "parent": "sun",
                  "kind": "asteroid",
                  "radiusKm": 1.0,
                  "orbit": {
                    "semiMajorAxisAu": 2.5,
                    "eccentricity": 0.1,
                    "inclinationDeg": 5.0,
                    "longitudeOfAscendingNodeDeg": 10.0,
                    "argumentOfPeriapsisDeg": 20.0,
                    "meanAnomalyAtEpochDeg": 30.0
                  }
                }
                """)));

        Assert.Contains("muKm3S2", erro.Message, StringComparison.Ordinal);
        Assert.Contains("densityGCm3", erro.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Um id repetido entre os dois arquivos é o erro que a junção existe para pegar, e a
    /// mensagem tem de dizer em que arquivo procurar.
    /// </summary>
    [Fact]
    public void UniaoRecusaIdQueJaExisteNoSistemaEDizDeOndeVeio()
    {
        var repositorio = JsonBodyRepository.FromFile(SolarSystem.DataPath);

        var erro = Assert.Throws<SystemDataException>(
            () => repositorio.WithCatalog(
                Catalogo(CorpoDeTeste(id: "earth", kind: "asteroid")), "catalogo-de-teste.json"));

        Assert.Contains("catalogo-de-teste.json", erro.Message, StringComparison.Ordinal);
        Assert.Contains("earth", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UniaoRecusaPaiQueNaoExisteEmNenhumDosDoisArquivos()
    {
        var repositorio = JsonBodyRepository.FromFile(SolarSystem.DataPath);

        var erro = Assert.Throws<SystemDataException>(
            () => repositorio.WithCatalog(
                Catalogo(CorpoDeTeste(parent: "planeta-nove")), "catalogo-de-teste.json"));

        Assert.Contains("planeta-nove", erro.Message, StringComparison.Ordinal);
    }

    private static string CorpoDeTeste(
        string id = "teste",
        string parent = "sun",
        string kind = "asteroid")
        => $$"""
            {
              "id": "{{id}}",
              "name": "Teste",
              "parent": "{{parent}}",
              "kind": "{{kind}}",
              "densityGCm3": 2.0,
              "radiusKm": 1.0,
              "orbit": {
                "semiMajorAxisAu": 2.5,
                "eccentricity": 0.1,
                "inclinationDeg": 5.0,
                "longitudeOfAscendingNodeDeg": 10.0,
                "argumentOfPeriapsisDeg": 20.0,
                "meanAnomalyAtEpochDeg": 30.0
              }
            }
            """;

    private static string Catalogo(string corpo) => $$"""
        {
          "schemaVersion": 1,
          "epoch": { "name": "J2000.0", "julianDate": 2451545.0 },
          "bodies": [ {{corpo}} ]
        }
        """;
}
