using SolarSim.Engine.Data;
using SolarSim.Engine.Models;

namespace SolarSim.Engine.Application;

/// <summary>
/// Fachada host-agnostic: Sistema Solar + ambientes sem Godot nem <c>SimBridge</c>.
/// Qualquer host (teste, CLI, outro engine) entra por aqui.
/// </summary>
public sealed class SimSession
{
    public SimSession(SimEngine sim, EnvironmentService environment)
    {
        Sim = sim ?? throw new ArgumentNullException(nameof(sim));
        Environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    public SimEngine Sim { get; }

    public EnvironmentService Environment { get; }

    /// <summary>
    /// Carrega os JSON canônicos sob a raiz do projeto (ou de um diretório que espelhe
    /// <c>Data/</c>).
    /// </summary>
    public static SimSession FromDataRoot(string dataRoot, bool includeCatalog = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRoot);

        var bodiesPath = Path.Combine(dataRoot, JsonBodyRepository.DefaultRelativePath);
        var envPath = Path.Combine(dataRoot, EnvironmentLoader.DefaultRelativePath);

        var repo = JsonBodyRepository.FromFile(bodiesPath);
        if (includeCatalog)
        {
            var catalogPath = Path.Combine(dataRoot, JsonBodyRepository.DefaultCatalogRelativePath);
            repo = repo.WithCatalogFile(catalogPath);
        }

        var sim = new SimEngine(repo);
        var environment = new EnvironmentService(EnvironmentLoader.FromFile(envPath));
        return new SimSession(sim, environment);
    }

    public EnvironmentReport EnvironmentFor(string bodyId, double? julianDate = null)
        => Environment.ReportFor(Sim, bodyId, julianDate);

    public double HabitabilityFor(string bodyId, double? julianDate = null)
        => EnvironmentFor(bodyId, julianDate).HabitabilityIndex;

    public bool Contains(string bodyId) => Sim.Contains(bodyId);

    public IReadOnlyList<CelestialBodyData> Bodies => Sim.Bodies;
}
