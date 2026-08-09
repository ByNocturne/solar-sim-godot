using SolarSim.CatalogImporter;
using SolarSim.Engine.Core;
using SolarSim.Engine.Data;
using SolarSim.Engine.Models;

namespace SolarSim.Tests;

/// <summary>
/// O importador que gera <c>Data/minor_bodies_j2000.json</c> a partir do CSV curado.
/// </summary>
public sealed class CatalogImporterTests
{
    /// <summary>
    /// O arquivo versionado é o que o importador produz do CSV versionado. Sem este teste
    /// nada impede alguém de editar o JSON à mão, e a fonte de onde ele saiu passaria a
    /// descrever outro catálogo — o pior tipo de divergência, porque o programa continua
    /// funcionando.
    /// </summary>
    [Fact]
    public void ArquivoGeradoEstaEmDiaComOCsvQueOGerou()
    {
        var esperado = Regerar();
        var atual = File.ReadAllText(SolarSystem.CatalogPath);

        Assert.True(
            Normalizar(esperado) == Normalizar(atual),
            "Data/minor_bodies_j2000.json não confere com o CSV da fonte. Rode "
                + "'dotnet run --project Tools/CatalogImporter'.");
    }

    [Fact]
    public void CsvCuradoProduzOMesmoNumeroDeCorposQueOArquivoGerado()
    {
        var doCsv = CatalogSource.Read(File.ReadAllText(SolarSystem.CatalogSourcePath));
        var doJson = DataLoader.ParseCatalog(SolarSystem.CatalogJson);

        Assert.Equal(doCsv.Count, doJson.Count);
    }

    /// <summary>
    /// O que sai do importador entra no motor: é a validação que o importador roda antes
    /// de escrever, aqui verificada de fora.
    /// </summary>
    [Fact]
    public void OQueOImportadorEscreveOCarregadorLe()
    {
        var corpos = DataLoader.ParseCatalog(Regerar());

        Assert.NotEmpty(corpos);
        Assert.All(corpos, corpo => Assert.NotNull(corpo.Elements));
    }

    /// <summary>
    /// Elementos declarados em outra época chegam a J2000 com a anomalia média deslocada
    /// pelo movimento médio, e mais nada mudado. É a razão de o importador existir.
    /// </summary>
    [Fact]
    public void AnomaliaMediaEhTrazidaDaEpocaDaFonteParaJ2000()
    {
        // Um ano juliano antes de J2000, com um semi-eixo de exatamente 1 UA: a Terra
        // andaria quase uma volta completa, então a anomalia volta quase ao mesmo lugar.
        const double semiEixoUa = 1.0;
        const double anomaliaNaFonte = 30.0;

        var deslocada = CatalogSource.MeanAnomalyAtJ2000Deg(
            anomaliaNaFonte, AstroConstants.J2000 - 365.25, semiEixoUa, 0.0);

        var semiEixoKm = semiEixoUa * AstroConstants.AstronomicalUnitKm;
        var movimentoMedioRadPorSegundo = Math.Sqrt(
            AstroConstants.SunMuKm3S2 / (semiEixoKm * semiEixoKm * semiEixoKm));

        var avancoGraus = AstroConstants.RadiansToDegrees(
            movimentoMedioRadPorSegundo * 365.25 * AstroConstants.SecondsPerDay);

        var esperada = (anomaliaNaFonte + avancoGraus) % 360.0;

        Assert.Equal(esperada, deslocada, 1e-9);

        // Uma volta menos meio grau: a órbita de 1 UA e o ano juliano quase coincidem.
        Assert.InRange(avancoGraus, 359.0, 360.0);
    }

    [Fact]
    public void EpocaIgualAJ2000NaoDeslocaNada()
    {
        var mesma = CatalogSource.MeanAnomalyAtJ2000Deg(123.456, AstroConstants.J2000, 2.5, 0.0);

        Assert.Equal(123.456, mesma);
    }

    /// <summary>
    /// Deslocar para frente e depois para trás pelo mesmo intervalo devolve o ponto de
    /// partida. É a mesma reversibilidade do invariante 4, um nível acima.
    /// </summary>
    [Fact]
    public void DeslocamentoDeEpocaEhReversivel()
    {
        const double original = 200.0;
        const double semiEixoUa = 2.7664;
        const double dias = 4000.0;

        var adiante = CatalogSource.MeanAnomalyAtJ2000Deg(
            original, AstroConstants.J2000 - dias, semiEixoUa, 0.0);

        var devolta = CatalogSource.MeanAnomalyAtJ2000Deg(
            adiante, AstroConstants.J2000 + dias, semiEixoUa, 0.0);

        Assert.Equal(original, devolta, 1e-9);
    }

    [Fact]
    public void CsvRecusaCabecalhoDiferente()
    {
        var erro = Assert.Throws<CatalogSourceException>(
            () => CatalogSource.Read("id,name\nceres,Ceres\n"));

        Assert.Contains("cabeçalho", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CsvRecusaMassaEDensidadeNaMesmaLinha()
    {
        var erro = Assert.Throws<CatalogSourceException>(
            () => CatalogSource.Read(Linha(mu: "1.0", densidade: "2.0")));

        Assert.Contains("muKm3S2", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CsvRecusaLinhaSemMassaESemDensidade()
        => Assert.Throws<CatalogSourceException>(
            () => CatalogSource.Read(Linha(mu: string.Empty, densidade: string.Empty)));

    [Fact]
    public void CsvRecusaClasseQueNaoEhDeCorpoMenor()
        => Assert.Throws<CatalogSourceException>(
            () => CatalogSource.Read(Linha(kind: "planet")));

    /// <summary>
    /// Órbita aberta não tem movimento médio que sirva de relógio, então o importador
    /// exige que a fonte já tenha entregado os elementos em J2000, em vez de fingir que
    /// sabe deslocá-los.
    /// </summary>
    [Fact]
    public void CsvRecusaDeslocarEpocaDeOrbitaAberta()
    {
        var erro = Assert.Throws<CatalogSourceException>(
            () => CatalogSource.Read(
                Linha(semiEixo: "-2.5", excentricidade: "1.2", epoca: "2460000.5")));

        Assert.Contains("aberta", erro.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A nota traz vírgula porque é texto corrido, e o leitor precisa saber ler o campo
    /// entre aspas sem parti-lo em dois.
    /// </summary>
    [Fact]
    public void NotaEntreAspasSobreviveAsVirgulas()
    {
        var corpos = CatalogSource.Read(Linha(nota: "\"Um, dois, três.\""));

        Assert.Equal("Um, dois, três.", corpos[0].Note);
    }

    [Fact]
    public void CorpoSemCorDeclaradaRecebeACorDaClasse()
    {
        var corpos = CatalogSource.Read(Linha(kind: "comet"));

        Assert.Equal(Palette.Default(BodyKind.Comet), corpos[0].ColorRgb);
    }

    private static string Regerar()
    {
        var corpos = CatalogSource.Read(File.ReadAllText(SolarSystem.CatalogSourcePath));
        var gerado = File.ReadAllText(SolarSystem.CatalogPath);

        // As listas de procedência do cabeçalho são escritas pelo programa de linha de
        // comando, não pela biblioteca: reaproveitá-las do arquivo atual mantém este teste
        // sobre os corpos, que é o que o CSV determina.
        return CatalogWriter.ToJson(corpos, Lista(gerado, "sources"), Lista(gerado, "notes"));
    }

    private static string[] Lista(string json, string campo)
        => System.Text.Json.JsonDocument
            .Parse(json)
            .RootElement
            .GetProperty(campo)
            .EnumerateArray()
            .Select(item => item.GetString()!)
            .ToArray();

    private static string Normalizar(string json)
        => json.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd();

    private static string Linha(
        string kind = "asteroid",
        string epoca = "2451545.0",
        string semiEixo = "2.5",
        string excentricidade = "0.1",
        string mu = "",
        string densidade = "2.0",
        string nota = "")
        => "id,name,kind,family,colorRgb,epochJd,semiMajorAxisAu,eccentricity,inclinationDeg,"
            + "longitudeOfAscendingNodeDeg,argumentOfPeriapsisDeg,meanAnomalyDeg,radiusKm,"
            + "muKm3S2,densityGCm3,note\n"
            + $"teste,Teste,{kind},Família,,{epoca},{semiEixo},{excentricidade},5.0,10.0,20.0,"
            + $"30.0,10.0,{mu},{densidade},{nota}\n";
}
