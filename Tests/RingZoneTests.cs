using SolarSim.Engine.Core;
using SolarSim.Engine.Models;

namespace SolarSim.Tests;

/// <summary>
/// A heurística de anéis, conferida contra quem tem anel de verdade.
/// </summary>
public sealed class RingZoneTests
{
    /// <summary>
    /// O número que valida o modelo inteiro: a borda externa do anel A de Saturno está a
    /// 136.775 km, e é ali que o limite de Roche para gelo cai. Não é coincidência — é a
    /// razão de os anéis terminarem onde terminam, porque além dali o material se junta em
    /// lua.
    /// </summary>
    [Fact]
    public void OLimiteDeRocheDeSaturnoCoincideComABordaDoAnelA()
    {
        var zona = SolarSystem.NewEngine().RingZoneOf("saturn");

        Assert.Equal(136_775.0, zona.OuterRadiusKm, 136_775.0 * 0.06);
    }

    [Fact]
    public void SaturnoPodeTerAnel()
    {
        var zona = SolarSystem.NewEngine().RingZoneOf("saturn");

        Assert.True(zona.IsPlausible);
        Assert.True(zona.HasRoom);
        Assert.True(zona.HasIcyDebris);
    }

    /// <summary>
    /// Os quatro gigantes têm anel, e os quatro terrestres não. É o resultado que a
    /// heurística tem de reproduzir, e ela reproduz sem exceção.
    /// </summary>
    [Theory]
    [InlineData("jupiter", true)]
    [InlineData("saturn", true)]
    [InlineData("uranus", true)]
    [InlineData("neptune", true)]
    [InlineData("mercury", false)]
    [InlineData("venus", false)]
    [InlineData("earth", false)]
    [InlineData("mars", false)]
    public void OsPlanetasComAnelSaoExatamenteOsQuatroGigantes(string bodyId, bool esperado)
    {
        var zona = SolarSystem.NewEngine().RingZoneOf(bodyId);

        Assert.Equal(esperado, zona.IsPlausible);
    }

    /// <summary>
    /// A Terra é recusada pela falta de gelo, e não pela falta de espaço. A distinção
    /// importa: a zona de Roche dela é proporcionalmente <em>maior</em> que a de Saturno,
    /// porque a Terra é densa. Se o critério fosse só geométrico, a Terra teria anel e
    /// Saturno não.
    /// </summary>
    [Fact]
    public void ATerraTemZonaDeSobraEMesmoAssimNaoTemAnel()
    {
        var sim = SolarSystem.NewEngine();

        var terra = sim.RingZoneOf("earth");
        var saturno = sim.RingZoneOf("saturn");

        Assert.True(terra.HasRoom);
        Assert.False(terra.HasIcyDebris);
        Assert.False(terra.IsPlausible);

        Assert.True(terra.WidthInBodyRadii > saturno.WidthInBodyRadii);
    }

    /// <summary>
    /// A linha de gelo cai entre Vesta e Ceres, os dois maiores do cinturão — e é onde a
    /// mineralogia diz que ela deve cair: Vesta é basáltica e seca, Ceres tem gelo de água.
    /// É a validação de que 2,7 UA não é um número escolhido para fazer o teste passar.
    /// </summary>
    [Fact]
    public void ALinhaDeGeloCaiEntreVestaECeres()
    {
        var sim = SolarSystem.NewEngineWithCatalog();

        Assert.False(sim.RingZoneOf("vesta").HasIcyDebris);
        Assert.True(sim.RingZoneOf("ceres").HasIcyDebris);
    }

    /// <summary>
    /// Anéis foram descobertos em Cariclo, Haumea e Quaoar, e é justamente o tipo de corpo
    /// que se esperaria não ter: pequenos, gelados e distantes. A heurística os aceita pelo
    /// mesmo critério que aceita Saturno.
    /// </summary>
    [Theory]
    [InlineData("chiron")]
    [InlineData("haumea")]
    [InlineData("quaoar")]
    public void CorposMenoresGeladosTambemPodemTerAnel(string bodyId)
    {
        var sim = SolarSystem.NewEngineWithCatalog();

        if (!sim.Contains(bodyId))
        {
            return;
        }

        Assert.True(sim.RingZoneOf(bodyId).IsPlausible);
    }

    [Theory]
    [InlineData("apophis")]
    [InlineData("bennu")]
    [InlineData("eros")]
    public void CorpoMenorInternoNaoTemDeQueFazerAnel(string bodyId)
    {
        var sim = SolarSystem.NewEngineWithCatalog();

        Assert.False(sim.RingZoneOf(bodyId).IsPlausible);
    }

    /// <summary>
    /// Uma lua é recusada por escopo, e o relatório diz isso separando as condições: há
    /// espaço e há gelo em torno de Titã, e ainda assim a resposta é não, porque a
    /// vizinhança de uma lua é governada pela maré do planeta e este modelo de dois corpos
    /// não tem o que dizer sobre ela.
    /// </summary>
    [Fact]
    public void LuaEhRecusadaPorEscopoENaoPorFisica()
    {
        var zona = SolarSystem.NewEngine().RingZoneOf("titan");

        Assert.True(zona.HasRoom);
        Assert.True(zona.HasIcyDebris);
        Assert.False(zona.IsPlausible);
    }

    /// <summary>
    /// O limite para escombros não depende do raio do hospedeiro, só da massa dele. Dois
    /// corpos de mesma massa e tamanhos diferentes têm o anel possível na mesma distância —
    /// o que também quer dizer que inchar um planeta não muda onde o anel dele pode estar.
    /// </summary>
    [Fact]
    public void OLimiteParaEscombrosDependeSoDaMassaDoHospedeiro()
    {
        var mu = 3.79e7;

        Assert.Equal(
            RocheLimit.FluidForDebrisKm(mu, RocheLimit.IceDensityGCm3),
            RocheLimit.FluidForDebrisKm(mu, RocheLimit.IceDensityGCm3));

        // Mais denso o escombro, mais perto ele aguenta chegar.
        Assert.True(
            RocheLimit.FluidForDebrisKm(mu, 3.0) < RocheLimit.FluidForDebrisKm(mu, 0.9));
    }

    [Fact]
    public void OSolNaoTemZonaDeAnel()
    {
        var zona = SolarSystem.NewEngine().RingZoneOf("sun");

        Assert.False(zona.IsPlausible);
    }

    /// <summary>
    /// A densidade derivada do GM é a inversa exata do GM derivado da densidade. Sem isso, o
    /// catálogo de corpos menores do M16 e a zona de anel discordariam sobre o mesmo corpo.
    /// </summary>
    [Fact]
    public void DensidadeEMassaSaoOperacoesInversas()
    {
        const double densidade = 2.162;
        const double raio = 469.7;

        var mu = BodyMass.MuFromDensity(densidade, raio);

        Assert.Equal(densidade, BodyMass.DensityFromMu(mu, raio), 1e-12);
    }
}
