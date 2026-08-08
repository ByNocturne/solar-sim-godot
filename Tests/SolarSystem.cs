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
    // Resolvido em tempo de compilação, como em ArchitectureTests: o teste funciona
    // independentemente de onde o assembly for executado.
    public static string DataPath => Path.Combine(RepoRoot(), JsonBodyRepository.DefaultRelativePath);

    public static string Json => File.ReadAllText(DataPath);

    public static SimEngine NewEngine() => new(JsonBodyRepository.FromFile(DataPath));

    private static string RepoRoot([CallerFilePath] string thisFile = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, ".."));
}
