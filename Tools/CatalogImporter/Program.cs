using SolarSim.CatalogImporter;
using SolarSim.Engine.Data;

// Importador do catálogo de corpos menores. Roda a mão, nunca em runtime: lê o CSV
// curado, normaliza para J2000 e escreve o JSON que o jogo carrega. Não acessa a rede —
// atualizar os dados é baixar as tabelas do JPL a mão e reescrever o CSV.
var repositoryRoot = Arguments.Value(args, "--root")
    ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

var sourcePath = Arguments.Value(args, "--source")
    ?? Path.Combine(repositoryRoot, "Tools", "CatalogImporter", "source", "minor_bodies_j2000.csv");

var outputPath = Arguments.Value(args, "--output")
    ?? Path.Combine(repositoryRoot, JsonBodyRepository.DefaultCatalogRelativePath);

var systemPath = Arguments.Value(args, "--system")
    ?? Path.Combine(repositoryRoot, JsonBodyRepository.DefaultRelativePath);

try
{
    if (!File.Exists(sourcePath))
    {
        throw new CatalogSourceException($"Fonte não encontrada: '{sourcePath}'.");
    }

    var bodies = CatalogSource.Read(File.ReadAllText(sourcePath));

    var json = CatalogWriter.ToJson(
        bodies,
        [
            "Elementos: JPL Horizons, elementos osculadores geométricos em JD 2451545.0, "
                + "centro 500@10 (Sol), plano da eclíptica, referencial ICRF.",
            "Tamanho e massa: JPL Small-Body Database (phys_par) e literatura, linha a linha "
                + "na nota de cada corpo.",
            $"Gerado por Tools/CatalogImporter a partir de {ToRelative(repositoryRoot, sourcePath)}.",
        ],
        [
            "Este arquivo é gerado. Editar o CSV da fonte e rodar o importador de novo, "
                + "em vez de editar aqui.",
            "Nenhum corpo declara 'orbit.rates': a precessão que o motor sabe calcular "
                + "sozinho não se declara, e a que ele não sabe não está medida para estes corpos.",
            "Yarkovsky e pressão de radiação vão em 'nonGravitational', não em 'orbit.rates': "
                + "só quem teve o efeito medido declara o parâmetro.",
            "Onde não há GM medido, entra 'densityGCm3' e o motor deriva a massa da esfera "
                + "equivalente. É estimativa, e serve à esfera de influência, não à balança.",
        ]);

    // A prova de que o arquivo presta é o próprio motor lê-lo, com a mesma validação de
    // sempre, e ainda casar com a hierarquia do sistema. Escrever antes de conferir seria
    // deixar um arquivo quebrado no lugar de um que funcionava.
    DataLoader.ParseCatalog(json);

    var repository = JsonBodyRepository.FromFile(systemPath).WithCatalog(json, outputPath);

    File.WriteAllText(outputPath, json);

    Console.WriteLine(
        $"{bodies.Count} corpos menores escritos em {ToRelative(repositoryRoot, outputPath)}; "
            + $"o sistema fica com {repository.LoadBodies().Count} corpos.");

    return 0;
}
catch (Exception error) when (error is CatalogSourceException or SystemDataException or IOException)
{
    Console.Error.WriteLine(error.Message);

    return 1;
}

static string ToRelative(string root, string path)
    => Path.GetRelativePath(root, path).Replace('\\', '/');

internal static class Arguments
{
    public static string? Value(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);

        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
