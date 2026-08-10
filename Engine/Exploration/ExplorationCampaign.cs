using SolarSim.Engine.Application;

namespace SolarSim.Engine.Exploration;

/// <summary>
/// Campanha: inventário persiste entre idas; gelo vira propelente para a próxima.
/// </summary>
public sealed class ExplorationCampaign
{
    private readonly SimSession _session;
    private readonly IReadOnlyDictionary<string, BodyComposition> _compositions;

    public ExplorationCampaign(
        SimSession session,
        IReadOnlyDictionary<string, BodyComposition> compositions,
        double startingPropellant = 12.0)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _compositions = compositions ?? throw new ArgumentNullException(nameof(compositions));
        Inventory = new CargoInventory();
        Inventory.Add(ResourceKind.Propellant, startingPropellant);
    }

    public CargoInventory Inventory { get; }

    public SimSession Session => _session;

    public ExplorationHazard Assess(string bodyId)
    {
        var report = _session.EnvironmentFor(bodyId);
        return ExplorationHazard.FromEnvironment(report);
    }

    public BodyComposition CompositionOf(string bodyId)
    {
        if (_compositions.TryGetValue(bodyId, out var composition))
        {
            return composition;
        }

        return new BodyComposition
        {
            BodyId = bodyId,
            Deposits = new Dictionary<ResourceKind, ResourceDeposit>
            {
                [ResourceKind.Regolith] = new ResourceDeposit(0.2, 1.5),
            },
        };
    }

    public ExplorationMission Plan(string bodyId)
        => new(Assess(bodyId), CompositionOf(bodyId));

    /// <summary>
    /// Consome propelente do inventário, executa a missão e devolve amostras + sobra.
    /// </summary>
    public MissionStepResult Run(
        string bodyId,
        MissionLoadout loadout,
        ResourceKind? samplePrefer = null,
        double sampleEvaHours = 2.0)
    {
        if (!Inventory.TryConsume(ResourceKind.Propellant, loadout.Propellant))
        {
            return MissionStepResult.Fail(
                MissionFailureReason.InsufficientPropellant,
                "Inventário sem propelente para esta carga.");
        }

        var mission = Plan(bodyId);
        var depart = mission.Depart(loadout);
        if (!depart.Ok)
        {
            // Carga falhou no check — devolve o propelente (não queimou na partida).
            Inventory.Add(ResourceKind.Propellant, loadout.Propellant);
            return depart;
        }

        if (sampleEvaHours > 0.0)
        {
            var sample = mission.Sample(sampleEvaHours, samplePrefer);
            if (!sample.Ok)
            {
                return sample;
            }
        }

        var ret = mission.ReturnToBase();
        if (!ret.Ok)
        {
            return ret;
        }

        foreach (var (kind, amount) in mission.Samples)
        {
            Inventory.Add(kind, amount);
        }

        if (mission.PropellantRemaining > 0.0)
        {
            Inventory.Add(ResourceKind.Propellant, mission.PropellantRemaining);
        }

        return ret;
    }

    public double RefineIce(double iceAmount)
        => Inventory.RefineWaterIceToPropellant(iceAmount);
}
