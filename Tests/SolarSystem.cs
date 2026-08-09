using System.Runtime.CompilerServices;
using SolarSim.Engine;
using SolarSim.Engine.Data;

namespace SolarSim.Tests;

/// <summary>
/// Acesso ao arquivo de dados real do projeto. Os testes leem o mesmo
/// <c>Data/solar_system_j2000.json</c> que o jogo carrega, para que um erro nos dados
/// falhe aqui e não só na tela.
/// </summary>
internal static class SolarSystem
{
    /// <summary>
    /// A época dos dados, para o teste que consulta uma data e não quer depender do
    /// relógio do motor.
    /// </summary>
    public const double J2000 = Engine.Core.AstroConstants.J2000;

    // Resolvido em tempo de compilação, como em ArchitectureTests: o teste funciona
    // independentemente de onde o assembly for executado.
    public static string DataPath => Path.Combine(RepoRoot(), JsonBodyRepository.DefaultRelativePath);

    public static string EnvironmentPath
        => Path.Combine(RepoRoot(), EnvironmentLoader.DefaultRelativePath);

    public static string CatalogPath
        => Path.Combine(RepoRoot(), JsonBodyRepository.DefaultCatalogRelativePath);

    /// <summary>A fonte de onde o catálogo é gerado, para conferir que o gerado é o dela.</summary>
    public static string CatalogSourcePath
        => Path.Combine(RepoRoot(), "Tools", "CatalogImporter", "source", "minor_bodies_j2000.csv");

    public static string Json => File.ReadAllText(DataPath);

    public static string CatalogJson => File.ReadAllText(CatalogPath);

    public static SimEngine NewEngine() => new(JsonBodyRepository.FromFile(DataPath));

    /// <summary>
    /// O sistema com o catálogo de corpos menores somado, que é o que o jogo carrega.
    /// </summary>
    public static SimEngine NewEngineWithCatalog()
        => new(JsonBodyRepository.FromFile(DataPath).WithCatalogFile(CatalogPath));

    public static (SimEngine Sim, EnvironmentService Environment) NewEnvironment()
    {
        var sim = NewEngine();
        var env = new EnvironmentService(EnvironmentLoader.FromFile(EnvironmentPath));
        return (sim, env);
    }

    private static string RepoRoot([CallerFilePath] string thisFile = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, ".."));
}
