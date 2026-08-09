using System.Globalization;
using SolarSim.Engine;
using SolarSim.Engine.Core;
using SolarSim.Engine.Models;

namespace SolarSim.Tests;

/// <summary>
/// A composição P_global(A) = P_global(Pai(A)) + P_local(A) e o sistema completo carregado
/// do JSON. O teste central é o da distância Terra-Lua ao longo de um século: se a
/// composição acumulasse erro, seria ali que apareceria.
/// </summary>
public sealed class HierarchyTests
{
    // Perigeu e apogeu reais da Lua, arredondados para fora.
    private const double PerigeuKm = 363_000.0;
    private const double ApogeuKm = 406_000.0;

    private const double DiasEmUmSeculo = 36_525.0;

    [Fact]
    public void DistanciaTerraLuaFicaNaFaixaRealAoLongoDeUmSeculo()
    {
        var sim = SolarSystem.NewEngine();

        var menor = double.MaxValue;
        var maior = double.MinValue;
        var diaDoExtremo = 0.0;

        // Meio dia de passo: o mês sideral tem 27 dias, então cada órbita é amostrada
        // umas 55 vezes, e o século inteiro dá mais de 73 mil amostras.
        for (var dia = 0.0; dia <= DiasEmUmSeculo; dia += 0.5)
        {
            var distancia = (sim.PositionAt("moon", AstroConstants.J2000 + dia)
                - sim.PositionAt("earth", AstroConstants.J2000 + dia)).Magnitude;

            if (distancia < menor || distancia > maior)
            {
                diaDoExtremo = dia;
            }

            menor = Math.Min(menor, distancia);
            maior = Math.Max(maior, distancia);
        }

        Assert.True(
            menor >= PerigeuKm && maior <= ApogeuKm,
            string.Create(CultureInfo.InvariantCulture,
                $"Distancia Terra-Lua entre {menor:N0} km e {maior:N0} km, fora da faixa "
                    + $"de {PerigeuKm:N0} a {ApogeuKm:N0} km. Extremo perto do dia {diaDoExtremo:N0}."));

        // Ficar dentro da faixa é fácil para uma órbita que não se mexe. Os extremos
        // amostrados também têm que encostar no perigeu e no apogeu que os elementos
        // preveem, a menos de um quilômetro.
        var elementos = sim.ElementsOf("moon")!.Value;

        Assert.Equal(
            elementos.SemiMajorAxisKm * (1.0 - elementos.Eccentricity), menor, tolerance: 1.0);
        Assert.Equal(
            elementos.SemiMajorAxisKm * (1.0 + elementos.Eccentricity), maior, tolerance: 1.0);
    }

    [Fact]
    public void MesSideralConfereComOValorObservado()
    {
        var sim = SolarSystem.NewEngine();

        var periodo = KeplerPropagator.OrbitalPeriodDays(
            sim.ElementsOf("moon")!.Value.SemiMajorAxisKm,
            sim.GravitationalParameterOf("moon"));

        Assert.Equal(27.321661, periodo, tolerance: 0.01);
    }

    [Fact]
    public void PosicaoGlobalDaLuaEAPosicaoDaTerraMaisALocal()
    {
        var sim = SolarSystem.NewEngine();
        var quando = AstroConstants.J2000 + 4_321.0;

        var composta = sim.PositionAt("earth", quando) + sim.LocalStateAt("moon", quando).PositionKm;

        Assert.Equal(composta, sim.PositionAt("moon", quando));
    }

    [Fact]
    public void LuaCarregaAVelocidadeDaTerraEmTornoDoSol()
    {
        var sim = SolarSystem.NewEngine();
        var quando = AstroConstants.J2000 + 100.0;

        var velocidadeGlobal = sim.StateAt("moon", quando).SpeedKmS;
        var velocidadeLocal = sim.LocalStateAt("moon", quando).SpeedKmS;

        // Cerca de 1 km/s em torno da Terra, sobre os 30 km/s da Terra em torno do Sol.
        Assert.InRange(velocidadeLocal, 0.9, 1.1);
        Assert.InRange(velocidadeGlobal, 29.0, 31.0);
    }

    [Fact]
    public void SatelitesDeJupiterAcompanhamOPlaneta()
    {
        var sim = SolarSystem.NewEngine();

        foreach (var (id, semiEixoKm) in new[]
        {
            ("io", 421_800.0),
            ("europa", 671_100.0),
            ("ganymede", 1_070_400.0),
            ("callisto", 1_882_700.0),
        })
        {
            for (var dia = 0.0; dia < 400.0; dia += 0.25)
            {
                var quando = AstroConstants.J2000 + dia;
                var distancia = (sim.PositionAt(id, quando) - sim.PositionAt("jupiter", quando))
                    .Magnitude;

                Assert.InRange(distancia, semiEixoKm * 0.98, semiEixoKm * 1.02);
            }
        }
    }

    /// <summary>
    /// Io, Europa e Ganimedes estão presos na ressonância de Laplace 1:2:4. Como o
    /// período aqui é derivado do semi-eixo e do GM, e não tabelado, a razão só sai certa
    /// se os dois dados estiverem coerentes entre si.
    /// </summary>
    [Fact]
    public void PeriodosDasGalileanasRespeitamARessonanciaDeLaplace()
    {
        var sim = SolarSystem.NewEngine();

        var io = Periodo(sim, "io");
        var europa = Periodo(sim, "europa");
        var ganimedes = Periodo(sim, "ganymede");

        Assert.Equal(2.0, europa / io, tolerance: 0.02);
        Assert.Equal(2.0, ganimedes / europa, tolerance: 0.02);
    }

    [Fact]
    public void PlanetasFicamEntrePerielioEAfelioAoLongoDeUmSeculo()
    {
        var sim = SolarSystem.NewEngine();

        foreach (var corpo in sim.Bodies.Where(corpo => corpo.ParentId == "sun"))
        {
            var elementos = corpo.Elements!.Value;
            var perielio = elementos.SemiMajorAxisKm * (1.0 - elementos.Eccentricity);
            var afelio = elementos.SemiMajorAxisKm * (1.0 + elementos.Eccentricity);

            for (var dia = 0.0; dia <= DiasEmUmSeculo; dia += 10.0)
            {
                var distancia = sim.PositionAt(corpo.Id, AstroConstants.J2000 + dia).Magnitude;

                Assert.InRange(distancia, perielio * (1.0 - 1e-9), afelio * (1.0 + 1e-9));
            }
        }
    }

    [Fact]
    public void OrdemDeAvaliacaoColocaOPaiAntesDoFilho()
    {
        var sim = SolarSystem.NewEngine();
        var jaVistos = new HashSet<string>(StringComparer.Ordinal);

        foreach (var corpo in sim.Bodies)
        {
            if (corpo.ParentId is { } pai)
            {
                Assert.True(
                    jaVistos.Contains(pai),
                    $"'{corpo.Id}' aparece antes do pai '{pai}' na ordem de avaliacao.");
            }

            jaVistos.Add(corpo.Id);
        }
    }

    [Fact]
    public void SistemaTemOSolOsOitoPlanetasEAsOitoLuas()
    {
        var sim = SolarSystem.NewEngine();

        string[] esperados =
        [
            "callisto", "deimos", "earth", "europa", "ganymede", "io", "jupiter", "mars",
            "mercury", "moon", "neptune", "phobos", "saturn", "sun", "titan", "uranus",
            "venus",
        ];

        Assert.Equal(
            esperados,
            sim.Bodies.Select(corpo => corpo.Id).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void SnapshotTrazTodosOsCorposComPosicaoGlobal()
    {
        var sim = SolarSystem.NewEngine();
        SystemStateSnapshot? recebido = null;
        sim.SystemUpdated += snapshot => recebido = snapshot;

        sim.Time.SpeedMultiplier = AstroConstants.SecondsPerDay;
        sim.Advance(realSecondsElapsed: 30.0);

        Assert.NotNull(recebido);

        var lua = recebido!.Value.Bodies.Single(estado => estado.Id == "moon");

        Assert.Equal(sim.PositionAt("moon", recebido.Value.JulianDate), lua.PositionKm);
    }

    private static double Periodo(SimEngine sim, string bodyId)
        => KeplerPropagator.OrbitalPeriodDays(
            sim.ElementsOf(bodyId)!.Value.SemiMajorAxisKm,
            sim.GravitationalParameterOf(bodyId));
}
