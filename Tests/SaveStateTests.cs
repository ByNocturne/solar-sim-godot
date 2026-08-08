using SolarSim.Engine;
using SolarSim.Engine.Core;
using SolarSim.Engine.Data;
using SolarSim.Engine.Models;

namespace SolarSim.Tests;

/// <summary>
/// Salvar e restaurar. O que se verifica aqui é sobretudo o que <em>não</em> precisa ser
/// salvo: o Sistema Solar inteiro cabe em um número, porque o estado é função da data.
/// </summary>
public sealed class SaveStateTests
{
    private static CelestialBodyData Sonda(string id, string parentId) => new()
    {
        Id = id,
        Name = $"Sonda {id}",
        ParentId = parentId,
        MuKm3S2 = 0.0,
        RadiusKm = 0.0,
        ColorRgb = 0xC8E060,
    };

    private static StateVector OrbitaBaixa()
    {
        const double raio = 6_778.0;
        var velocidade = Math.Sqrt(398_600.435436 / raio);

        return new StateVector(
            new Vector3D(raio, 0.0, 0.0),
            new Vector3D(0.0, velocidade * 0.6, velocidade * 0.8));
    }

    [Fact]
    public void OArquivoSalvoNaoContemNenhumCorpoDoSistemaSolar()
    {
        var sim = SolarSystem.NewEngine();
        sim.Time.JumpTo(AstroConstants.J2000 + 12_345.678);

        var json = SaveState.Serialize(sim);

        Assert.DoesNotContain("earth", json, StringComparison.Ordinal);
        Assert.DoesNotContain("jupiter", json, StringComparison.Ordinal);

        // Com quinze corpos e nenhum deles no arquivo, o que sobra é a data e uma lista
        // vazia: o Sistema Solar inteiro cabe em um número.
        Assert.Contains("2463890.678", json, StringComparison.Ordinal);
        Assert.True(json.Length < 100, $"O arquivo tem {json.Length} caracteres:\n{json}");
    }

    [Fact]
    public void RelogioEcorposDinamicosVoltamComoEstavam()
    {
        var original = SolarSystem.NewEngine();
        original.Time.JumpTo(AstroConstants.J2000 + 3_000.0);
        original.AddFromState(Sonda("alfa", "earth"), OrbitaBaixa(), original.Time.JulianDate);
        original.AddFromState(Sonda("beta", "mars"), OrbitaBaixa(), original.Time.JulianDate);

        var json = SaveState.Serialize(original);

        var restaurado = SolarSystem.NewEngine();
        SaveState.Restore(restaurado, json);

        Assert.Equal(original.Time.JulianDate, restaurado.Time.JulianDate);
        Assert.Equal(
            original.DynamicBodyIds.Order(StringComparer.Ordinal),
            restaurado.DynamicBodyIds.Order(StringComparer.Ordinal));

        foreach (var bodyId in original.DynamicBodyIds)
        {
            // A comparação é do estado, e não dos elementos: é o estado que a tela
            // mostra, e é ele que precisa ser o mesmo.
            foreach (var dia in new[] { 0.0, 0.5, 30.0 })
            {
                var data = original.Time.JulianDate + dia;

                var esperado = original.StateAt(bodyId, data);
                var obtido = restaurado.StateAt(bodyId, data);

                var desvio = (obtido.PositionKm - esperado.PositionKm).Magnitude;

                Assert.True(
                    desvio < 1e-6, $"'{bodyId}' desviou {desvio:E3} km no dia {dia}.");
            }

            Assert.Equal(
                original.BodyOf(bodyId).Name, restaurado.BodyOf(bodyId).Name);

            Assert.Equal(
                original.BodyOf(bodyId).ColorRgb, restaurado.BodyOf(bodyId).ColorRgb);
        }
    }

    [Fact]
    public void AHistoriaDeEmendasSobreviveAoSalvamento()
    {
        var original = SolarSystem.NewEngine();
        original.Time.SpeedMultiplier = AstroConstants.SecondsPerDay;

        original.AddFromState(
            Sonda("alfa", "earth"),
            new StateVector(
                new Vector3D(900_000.0, 0.0, 0.0), new Vector3D(1.44, 0.39, 0.15)),
            original.Time.JulianDate);

        for (var passo = 0; passo < 600; passo++)
        {
            original.Advance(3.0 / 600.0);
        }

        var arcos = original.TrajectoryOf("alfa");
        Assert.Equal(2, arcos.Count);

        var restaurado = SolarSystem.NewEngine();
        SaveState.Restore(restaurado, SaveState.Serialize(original));

        var restaurados = restaurado.TrajectoryOf("alfa");

        Assert.Equal(arcos.Count, restaurados.Count);
        Assert.Equal(arcos[0].ParentId, restaurados[0].ParentId);
        Assert.Equal(arcos[1].ParentId, restaurados[1].ParentId);
        Assert.Equal(arcos[1].StartJulianDate, restaurados[1].StartJulianDate);

        // E o passado continua consultável: uma data anterior à costura devolve o arco
        // de então, que é justamente o que não se conseguiria redescobrir.
        var antes = arcos[1].StartJulianDate - 1.0;

        Assert.Equal(
            0.0,
            (restaurado.StateAt("alfa", antes).PositionKm
                - original.StateAt("alfa", antes).PositionKm).Magnitude,
            tolerance: 1e-6);
    }

    [Fact]
    public void RestaurarDescartaOsCorposDinamicosQueJaEstavamLa()
    {
        var sim = SolarSystem.NewEngine();
        sim.AddFromState(Sonda("velha", "earth"), OrbitaBaixa(), sim.Time.JulianDate);

        var outro = SolarSystem.NewEngine();
        outro.AddFromState(Sonda("nova", "earth"), OrbitaBaixa(), outro.Time.JulianDate);

        SaveState.Restore(sim, SaveState.Serialize(outro));

        Assert.False(sim.Contains("velha"));
        Assert.True(sim.Contains("nova"));
    }

    [Fact]
    public void VersaoDesconhecidaFalhaDizendoQualE()
    {
        var sim = SolarSystem.NewEngine();
        var json = SaveState.Serialize(sim).Replace(
            "\"version\": 1", "\"version\": 99", StringComparison.Ordinal);

        var erro = Assert.Throws<SystemDataException>(() => SaveState.Restore(sim, json));

        Assert.Contains("99", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void JsonMalFormadoFalhaComoErroDeDados()
    {
        var sim = SolarSystem.NewEngine();

        Assert.Throws<SystemDataException>(() => SaveState.Restore(sim, "{ nao é json"));
    }

    [Fact]
    public void CorpoSalvoSemArcoFalha()
    {
        var sim = SolarSystem.NewEngine();

        var json = """
            { "version": 1, "julianDate": 2451545.0,
              "bodies": [ { "id": "alfa", "name": "Alfa", "arcs": [] } ] }
            """;

        var erro = Assert.Throws<SystemDataException>(() => SaveState.Restore(sim, json));

        Assert.Contains("alfa", erro.Message, StringComparison.Ordinal);
    }
}
