using SolarSim.Engine.Core;
using SolarSim.Engine.Data;

namespace SolarSim.Tests;

/// <summary>
/// A carga é a fronteira onde graus viram radianos e onde um arquivo mal editado
/// precisa falhar. Cada teste de erro verifica que a mensagem nomeia o corpo ou o campo:
/// quem vai lê-la está com o JSON aberto, não com o depurador.
/// </summary>
public sealed class DataLoaderTests
{
    [Fact]
    public void ArquivoRealDoProjetoCarrega()
    {
        var corpos = DataLoader.Parse(SolarSystem.Json);

        Assert.Equal("sun", corpos[0].Id);
        Assert.Null(corpos[0].ParentId);
        Assert.Contains(corpos, corpo => corpo.Id == "moon");
    }

    [Fact]
    public void GmDoSolNoArquivoConfereComAConstanteDoMotor()
    {
        var sol = DataLoader.Parse(SolarSystem.Json).Single(corpo => corpo.Id == "sun");

        Assert.Equal(AstroConstants.SunMuKm3S2, sol.MuKm3S2);
    }

    [Fact]
    public void UnidadeAstronomicaViraQuilometro()
    {
        var terra = DataLoader.Parse(SolarSystem.Json).Single(corpo => corpo.Id == "earth");

        Assert.Equal(
            1.00000261 * AstroConstants.AstronomicalUnitKm,
            terra.Elements!.Value.SemiMajorAxisKm,
            tolerance: 1e-6);
    }

    [Fact]
    public void SemiEixoEmQuilometroEntraSemConversao()
    {
        var lua = DataLoader.Parse(SolarSystem.Json).Single(corpo => corpo.Id == "moon");

        Assert.Equal(384_748.0, lua.Elements!.Value.SemiMajorAxisKm);
    }

    [Fact]
    public void GrauViraRadiano()
    {
        var marte = DataLoader.Parse(SolarSystem.Json).Single(corpo => corpo.Id == "mars");

        Assert.Equal(
            AstroConstants.DegreesToRadians(1.84969142),
            marte.Elements!.Value.InclinationRad,
            tolerance: 1e-15);
    }

    [Fact]
    public void CorHexadecimalViraInteiro()
    {
        var terra = DataLoader.Parse(SolarSystem.Json).Single(corpo => corpo.Id == "earth");

        Assert.Equal(0x4A90D9u, terra.ColorRgb);
    }

    [Fact]
    public void PaiInexistenteFalhaDizendoQuemEQuem()
    {
        var erro = Assert.Throws<SystemDataException>(() => DataLoader.Parse(Documento($$"""
            {{Sol}},
            {
              "id": "phobos", "name": "Fobos", "parent": "mars",
              "muKm3S2": 7.087e-4, "radiusKm": 11.1,
              "orbit": {{Orbita}}
            }
            """)));

        Assert.Contains("phobos", erro.Message, StringComparison.Ordinal);
        Assert.Contains("mars", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CicloFalhaMostrandoOCaminho()
    {
        var erro = Assert.Throws<SystemDataException>(() => DataLoader.Parse(Documento($$"""
            {{Sol}},
            {
              "id": "a", "name": "A", "parent": "b",
              "muKm3S2": 1.0, "radiusKm": 1.0, "orbit": {{Orbita}}
            },
            {
              "id": "b", "name": "B", "parent": "a",
              "muKm3S2": 1.0, "radiusKm": 1.0, "orbit": {{Orbita}}
            }
            """)));

        Assert.Contains("Ciclo", erro.Message, StringComparison.Ordinal);
        Assert.Contains("a -> b -> a", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void IdDuplicadoFalha()
    {
        var erro = Assert.Throws<SystemDataException>(() => DataLoader.Parse(Documento($$"""
            {{Sol}},
            {{Planeta("earth")}},
            {{Planeta("earth")}}
            """)));

        Assert.Contains("earth", erro.Message, StringComparison.Ordinal);
        Assert.Contains("duplicado", erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MaisDeUmaRaizFalha()
    {
        var erro = Assert.Throws<SystemDataException>(() => DataLoader.Parse(Documento($$"""
            {{Sol}},
            { "id": "outra", "name": "Outra", "parent": null,
              "muKm3S2": 1.0, "radiusKm": 1.0 }
            """)));

        Assert.Contains("raiz", erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SemiEixoNuloFalha()
    {
        var erro = Assert.Throws<SystemDataException>(() => DataLoader.Parse(Documento($$"""
            {{Sol}},
            { "id": "earth", "name": "Terra", "parent": "sun",
              "muKm3S2": 1.0, "radiusKm": 1.0,
              "orbit": { "semiMajorAxisKm": 0.0, "eccentricity": 0.0,
                "inclinationDeg": 0.0, "longitudeOfAscendingNodeDeg": 0.0,
                "argumentOfPeriapsisDeg": 0.0, "meanAnomalyAtEpochDeg": 0.0 } }
            """)));

        Assert.Contains("earth", erro.Message, StringComparison.Ordinal);
        Assert.Contains("semi-eixo", erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CampoAusenteFalhaDizendoCorpoECampo()
    {
        var erro = Assert.Throws<SystemDataException>(() => DataLoader.Parse(Documento($$"""
            {{Sol}},
            { "id": "earth", "name": "Terra", "parent": "sun",
              "muKm3S2": 1.0, "radiusKm": 1.0,
              "orbit": { "semiMajorAxisKm": 1.0, "eccentricity": 0.0,
                "inclinationDeg": 0.0, "longitudeOfAscendingNodeDeg": 0.0,
                "argumentOfPeriapsisDeg": 0.0 } }
            """)));

        Assert.Contains("earth", erro.Message, StringComparison.Ordinal);
        Assert.Contains("meanAnomalyAtEpochDeg", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DuasUnidadesParaOMesmoSemiEixoFalha()
    {
        var erro = Assert.Throws<SystemDataException>(() => DataLoader.Parse(Documento($$"""
            {{Sol}},
            { "id": "earth", "name": "Terra", "parent": "sun",
              "muKm3S2": 1.0, "radiusKm": 1.0,
              "orbit": { "semiMajorAxisKm": 1.0, "semiMajorAxisAu": 1.0,
                "eccentricity": 0.0, "inclinationDeg": 0.0,
                "longitudeOfAscendingNodeDeg": 0.0, "argumentOfPeriapsisDeg": 0.0,
                "meanAnomalyAtEpochDeg": 0.0 } }
            """)));

        Assert.Contains("unidade", erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CampoComNomeErradoFalhaEmVezDeSerIgnorado()
    {
        var erro = Assert.Throws<SystemDataException>(() => DataLoader.Parse(Documento($$"""
            {{Sol}},
            { "id": "earth", "name": "Terra", "parent": "sun",
              "muKm3S2": 1.0, "radiusKm": 1.0, "raioKm": 6371.0,
              "orbit": {{Orbita}} }
            """)));

        Assert.Contains("raioKm", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RaizComOrbitaFalha()
    {
        var erro = Assert.Throws<SystemDataException>(() => DataLoader.Parse(Documento($$"""
            { "id": "sun", "name": "Sol", "parent": null,
              "muKm3S2": 1.32712440018e11, "radiusKm": 695700.0,
              "orbit": {{Orbita}} }
            """)));

        Assert.Contains("sun", erro.Message, StringComparison.Ordinal);
        Assert.Contains("raiz", erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FilhoSemOrbitaFalha()
    {
        var erro = Assert.Throws<SystemDataException>(() => DataLoader.Parse(Documento($$"""
            {{Sol}},
            { "id": "earth", "name": "Terra", "parent": "sun",
              "muKm3S2": 1.0, "radiusKm": 1.0 }
            """)));

        Assert.Contains("earth", erro.Message, StringComparison.Ordinal);
        Assert.Contains("orbit", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OrbitaAbertaEAceitaComSemiEixoNegativo()
    {
        var corpos = DataLoader.Parse(Documento($$"""
            {{Sol}},
            { "id": "cometa", "name": "Cometa", "parent": "sun",
              "muKm3S2": 1.0, "radiusKm": 1.0,
              "orbit": { "semiMajorAxisAu": -1.0, "eccentricity": 1.2,
                "inclinationDeg": 0.0, "longitudeOfAscendingNodeDeg": 0.0,
                "argumentOfPeriapsisDeg": 0.0, "meanAnomalyAtEpochDeg": 0.0 } }
            """));

        var elementos = corpos.Single(corpo => corpo.Id == "cometa").Elements!.Value;

        Assert.False(elementos.IsClosed);
        Assert.True(elementos.PeriapsisKm > 0.0);
    }

    [Fact]
    public void SinalDoSemiEixoIncompativelComAExcentricidadeFalha()
    {
        var erro = Assert.Throws<SystemDataException>(() => DataLoader.Parse(Documento($$"""
            {{Sol}},
            { "id": "cometa", "name": "Cometa", "parent": "sun",
              "muKm3S2": 1.0, "radiusKm": 1.0,
              "orbit": { "semiMajorAxisAu": 1.0, "eccentricity": 1.2,
                "inclinationDeg": 0.0, "longitudeOfAscendingNodeDeg": 0.0,
                "argumentOfPeriapsisDeg": 0.0, "meanAnomalyAtEpochDeg": 0.0 } }
            """)));

        Assert.Contains("cometa", erro.Message, StringComparison.Ordinal);
        Assert.Contains("negativo", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OrbitaParabolicaFalhaPorNaoSerRepresentavel()
    {
        var erro = Assert.Throws<SystemDataException>(() => DataLoader.Parse(Documento($$"""
            {{Sol}},
            { "id": "cometa", "name": "Cometa", "parent": "sun",
              "muKm3S2": 1.0, "radiusKm": 1.0,
              "orbit": { "semiMajorAxisAu": -1.0, "eccentricity": 1.0,
                "inclinationDeg": 0.0, "longitudeOfAscendingNodeDeg": 0.0,
                "argumentOfPeriapsisDeg": 0.0, "meanAnomalyAtEpochDeg": 0.0 } }
            """)));

        Assert.Contains("cometa", erro.Message, StringComparison.Ordinal);
        Assert.Contains("parabólica", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AchatamentoEORaioDeReferenciaCarregamDoArquivoReal()
    {
        var terra = DataLoader.Parse(SolarSystem.Json).Single(corpo => corpo.Id == "earth");

        Assert.Equal(1.08262668e-3, terra.J2);

        // O raio equatorial, e não o médio de 6371 km: o J₂ é publicado contra ele, e a
        // taxa de precessão escala com o quadrado da razão entre os dois.
        Assert.Equal(6_378.137, terra.J2ReferenceRadiusKm);
    }

    [Fact]
    public void SemRaioEquatorialOAchatamentoSeRefereAoRaioDoCorpo()
    {
        var corpo = DataLoader.Parse(Documento("""
            { "id": "sun", "name": "Sol", "parent": null,
              "muKm3S2": 1.0, "radiusKm": 1234.0, "j2": 1.0e-3 }
            """))[0];

        Assert.Equal(1234.0, corpo.J2ReferenceRadiusKm);
    }

    [Fact]
    public void AchatamentoNegativoFalha()
    {
        var erro = Assert.Throws<SystemDataException>(() => DataLoader.Parse(Documento("""
            { "id": "sun", "name": "Sol", "parent": null,
              "muKm3S2": 1.0, "radiusKm": 1.0, "j2": -1.0e-3 }
            """)));

        Assert.Contains("j2", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TaxaSecularEmGrausPorSeculoViraRadianoPorSegundo()
    {
        var corpo = DataLoader.Parse(Documento($$"""
            {{Sol}},
            { "id": "planeta", "name": "Planeta", "parent": "sun",
              "muKm3S2": 1.0, "radiusKm": 1.0,
              "orbit": { "semiMajorAxisAu": 1.0, "eccentricity": 0.0,
                "inclinationDeg": 0.0, "longitudeOfAscendingNodeDeg": 0.0,
                "argumentOfPeriapsisDeg": 0.0, "meanAnomalyAtEpochDeg": 0.0,
                "rates": { "argumentOfPeriapsisDegPerCentury": 1.0 } } }
            """))[1];

        Assert.Equal(
            AstroConstants.DegreesToRadians(1.0) / AstroConstants.SecondsPerJulianCentury,
            corpo.Rates.ArgumentOfPeriapsisRadPerSecond,
            tolerance: 1e-24);

        // O que não foi declarado fica zerado, em vez de virar um valor plausível
        // inventado pelo desserializador.
        Assert.Equal(0.0, corpo.Rates.EccentricityPerSecond);
    }

    [Fact]
    public void TaxaSecularEmOrbitaAbertaFalha()
    {
        var erro = Assert.Throws<SystemDataException>(() => DataLoader.Parse(Documento($$"""
            {{Sol}},
            { "id": "cometa", "name": "Cometa", "parent": "sun",
              "muKm3S2": 1.0, "radiusKm": 1.0,
              "orbit": { "semiMajorAxisAu": -1.0, "eccentricity": 1.6,
                "inclinationDeg": 0.0, "longitudeOfAscendingNodeDeg": 0.0,
                "argumentOfPeriapsisDeg": 0.0, "meanAnomalyAtEpochDeg": 0.0,
                "rates": { "eccentricityPerCentury": 0.1 } } }
            """)));

        Assert.Contains("cometa", erro.Message, StringComparison.Ordinal);
        Assert.Contains("rates", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EpocaDiferenteDeJ2000Falha()
    {
        var json = Documento(Sol).Replace("2451545.0", "2451546.0", StringComparison.Ordinal);

        var erro = Assert.Throws<SystemDataException>(() => DataLoader.Parse(json));

        Assert.Contains("J2000", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void VersaoDeSchemaDesconhecidaFalha()
    {
        var json = Documento(Sol).Replace("\"schemaVersion\": 1", "\"schemaVersion\": 2",
            StringComparison.Ordinal);

        var erro = Assert.Throws<SystemDataException>(() => DataLoader.Parse(json));

        Assert.Contains("schemaVersion", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CorForaDoFormatoFalha()
    {
        var erro = Assert.Throws<SystemDataException>(() => DataLoader.Parse(Documento("""
            { "id": "sun", "name": "Sol", "parent": null,
              "muKm3S2": 1.0, "radiusKm": 1.0, "colorRgb": "azul" }
            """)));

        Assert.Contains("colorRgb", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void JsonMalFormadoFalhaComoErroDeDados()
    {
        var erro = Assert.Throws<SystemDataException>(() => DataLoader.Parse("{ isto nao e json"));

        Assert.Contains("JSON", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ArquivoInexistenteFalhaComOCaminho()
    {
        var caminho = Path.Combine(Path.GetTempPath(), "sistema_que_nao_existe.json");

        var erro = Assert.Throws<SystemDataException>(() => JsonBodyRepository.FromFile(caminho));

        Assert.Contains(caminho, erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ErroDeConteudoCarregaAOrigemDoTexto()
    {
        var erro = Assert.Throws<SystemDataException>(
            () => JsonBodyRepository.FromJson("{}", "sistema_de_teste.json"));

        Assert.Contains("sistema_de_teste.json", erro.Message, StringComparison.Ordinal);
    }

    private const string Orbita = """
        { "semiMajorAxisAu": 1.0, "eccentricity": 0.0, "inclinationDeg": 0.0,
          "longitudeOfAscendingNodeDeg": 0.0, "argumentOfPeriapsisDeg": 0.0,
          "meanAnomalyAtEpochDeg": 0.0 }
        """;

    private const string Sol = """
        { "id": "sun", "name": "Sol", "parent": null,
          "muKm3S2": 1.32712440018e11, "radiusKm": 695700.0 }
        """;

    private static string Planeta(string id) => $$"""
        { "id": "{{id}}", "name": "{{id}}", "parent": "sun",
          "muKm3S2": 1.0, "radiusKm": 1.0, "orbit": {{Orbita}} }
        """;

    private static string Documento(string corpos) => $$"""
        {
          "schemaVersion": 1,
          "epoch": { "name": "J2000.0", "julianDate": 2451545.0 },
          "bodies": [ {{corpos}} ]
        }
        """;
}
