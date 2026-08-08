using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace SolarSim.Tests;

public sealed class ArchitectureTests
{
    [Fact]
    public void MotorNaoDependeDoGodot()
    {
        var engineDir = Path.Combine(RepoRoot(), "Engine");

        var ofensores = Directory
            .EnumerateFiles(engineDir, "*.cs", SearchOption.AllDirectories)
            .Where(arquivo => File.ReadAllText(arquivo).Contains("using Godot", StringComparison.Ordinal))
            .Select(arquivo => Path.GetRelativePath(RepoRoot(), arquivo))
            .ToArray();

        Assert.True(
            ofensores.Length == 0,
            "Engine/ e dominio puro e nao pode depender do Godot. Mova a responsabilidade "
                + "para Bridge/. Arquivos em violacao:"
                + Environment.NewLine
                + string.Join(Environment.NewLine, ofensores));
    }

    [Fact]
    public void MotorNaoReferenciaPacotesDoGodot()
    {
        var csproj = XDocument.Load(Path.Combine(RepoRoot(), "Engine", "SolarSim.Engine.csproj"));

        var sdk = (string?)csproj.Root!.Attribute("Sdk") ?? string.Empty;
        Assert.DoesNotContain("Godot", sdk, StringComparison.OrdinalIgnoreCase);

        var referencias = csproj.Descendants()
            .Where(elemento => elemento.Name.LocalName
                is "PackageReference" or "ProjectReference" or "Reference")
            .Select(elemento => (string?)elemento.Attribute("Include") ?? string.Empty)
            .Where(include => include.Contains("Godot", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.Empty(referencias);
    }

    // O caminho e resolvido em tempo de compilacao, entao o teste funciona
    // independentemente de onde o assembly for executado.
    private static string RepoRoot([CallerFilePath] string arquivoDesteTeste = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(arquivoDesteTeste)!, ".."));
}
