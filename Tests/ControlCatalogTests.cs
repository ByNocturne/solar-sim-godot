using SolarSim.Bridge;

namespace SolarSim.Tests;

/// <summary>
/// Garante que Help in-game, README e AGENTS.md não voltem a divergir — o bug da
/// câmera no painel H veio exatamente desse drift.
/// </summary>
public sealed class ControlCatalogTests
{
    [Fact]
    public void HelpLines_nao_repete_Home()
    {
        var homes = ControlCatalog.HelpLines.Count(line => line.StartsWith("Home:", StringComparison.Ordinal));

        Assert.Equal(1, homes);
    }

    [Fact]
    public void Readme_usa_o_resumo_do_catalogo()
    {
        var readme = File.ReadAllText(Path.Combine(RepoRoot(), "README.md"));

        Assert.Contains(ControlCatalog.ReadmeSummary, readme, StringComparison.Ordinal);
    }

    [Fact]
    public void Agents_menciona_os_atalhos_do_catalogo()
    {
        var agents = File.ReadAllText(Path.Combine(RepoRoot(), "AGENTS.md"));

        foreach (var token in ControlCatalog.AgentsMustMention)
        {
            Assert.True(
                agents.Contains(token, StringComparison.Ordinal),
                $"AGENTS.md deveria mencionar '{token}' (sincronizar com ControlCatalog).");
        }
    }

    [Fact]
    public void HelpLines_cobrem_os_tokens_de_documentacao()
    {
        var help = string.Join('\n', ControlCatalog.HelpLines);

        Assert.Contains("Botão direito", help, StringComparison.Ordinal);
        Assert.Contains("Shift+direito", help, StringComparison.Ordinal);
        Assert.Contains("Shift+T", help, StringComparison.Ordinal);
        Assert.Contains("Home:", help, StringComparison.Ordinal);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))
                && File.Exists(Path.Combine(dir.FullName, "README.md")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Raiz do repositório não encontrada a partir dos testes.");
    }
}
