namespace SolarSim.Engine.Exploration;

public enum MissionPhase
{
    Planning = 0,
    Surface = 1,
    Completed = 2,
    Failed = 3,
}

public enum MissionFailureReason
{
    None = 0,
    InsufficientThermalShield = 1,
    InsufficientRadiationShield = 2,
    EvaExhausted = 3,
    InsufficientPropellant = 4,
    InvalidBody = 5,
    StellarSurface = 6,
}

/// <summary>Resultado de uma tentativa de avanço na missão.</summary>
public readonly record struct MissionStepResult
{
    public bool Ok { get; init; }

    public string Message { get; init; }

    public MissionFailureReason Failure { get; init; }

    public static MissionStepResult Success(string message)
        => new() { Ok = true, Message = message, Failure = MissionFailureReason.None };

    public static MissionStepResult Fail(MissionFailureReason reason, string message)
        => new() { Ok = false, Message = message, Failure = reason };
}

/// <summary>
/// Uma ida: ler risco → preparar carga → superfície com EVA → amostrar → voltar.
/// Não é “chegar e catar”: a carga inadequada falha antes ou durante o EVA.
/// </summary>
public sealed class ExplorationMission
{
    private readonly ExplorationHazard _hazard;
    private readonly BodyComposition _composition;
    private MissionLoadout _loadout;
    private double _evaRemaining;
    private double _propellantRemaining;
    private readonly Dictionary<ResourceKind, double> _samples = new();

    public ExplorationMission(ExplorationHazard hazard, BodyComposition composition)
    {
        _hazard = hazard;
        _composition = composition ?? throw new ArgumentNullException(nameof(composition));
        Phase = MissionPhase.Planning;
    }

    public MissionPhase Phase { get; private set; }

    public MissionFailureReason Failure { get; private set; }

    public ExplorationHazard Hazard => _hazard;

    public BodyComposition Composition => _composition;

    public IReadOnlyDictionary<ResourceKind, double> Samples => _samples;

    public double EvaRemaining => _evaRemaining;

    public double PropellantRemaining => _propellantRemaining;

    public MissionStepResult Depart(MissionLoadout loadout)
    {
        if (Phase != MissionPhase.Planning)
        {
            return MissionStepResult.Fail(MissionFailureReason.None, "A missão já saiu do planejamento.");
        }

        if (_hazard.MaxSafeEvaHours < 0.75 && _hazard.RequiredThermalShield >= 3
            && _hazard.RequiredRadiationShield >= 3)
        {
            return Fail(MissionFailureReason.StellarSurface, "Superfície estelar — EVA inviável.");
        }

        _loadout = loadout;
        var oneWay = _hazard.TransitPropellantOneWay * (1.0 + loadout.ProtectionMass);
        var roundTripReserve = 2.0 * oneWay;

        if (loadout.ThermalShield < _hazard.RequiredThermalShield)
        {
            return Fail(
                MissionFailureReason.InsufficientThermalShield,
                $"Escudo térmico {loadout.ThermalShield} < exigido {_hazard.RequiredThermalShield}.");
        }

        if (loadout.RadiationShield < _hazard.RequiredRadiationShield)
        {
            return Fail(
                MissionFailureReason.InsufficientRadiationShield,
                $"Escudo de radiação {loadout.RadiationShield} < exigido {_hazard.RequiredRadiationShield}.");
        }

        if (loadout.Propellant + 1e-9 < roundTripReserve)
        {
            return Fail(
                MissionFailureReason.InsufficientPropellant,
                $"Propelente insuficiente para ida e volta (precisa ≥ {roundTripReserve:0.##}).");
        }

        var evaCap = Math.Min(loadout.EvaHours, _hazard.MaxSafeEvaHours);
        if (evaCap <= 0.0)
        {
            return Fail(MissionFailureReason.EvaExhausted, "Sem janela de EVA utilizável.");
        }

        _propellantRemaining = loadout.Propellant - oneWay;
        _evaRemaining = evaCap;
        Phase = MissionPhase.Surface;
        return MissionStepResult.Success(
            $"Chegada a {_hazard.Name}. EVA restante {_evaRemaining:0.##} h; propelente {_propellantRemaining:0.##}.");
    }

    /// <summary>
    /// Gasta horas de EVA para amostrar o depósito mais abundante ainda não esgotado
    /// na lógica simples (um recurso por chamada, o de maior abundância).
    /// </summary>
    public MissionStepResult Sample(double evaHours, ResourceKind? prefer = null)
    {
        if (Phase != MissionPhase.Surface)
        {
            return MissionStepResult.Fail(MissionFailureReason.None, "Não há EVA em andamento.");
        }

        if (evaHours <= 0.0)
        {
            return MissionStepResult.Fail(MissionFailureReason.None, "EVA deve ser positivo.");
        }

        if (evaHours - 1e-12 > _evaRemaining)
        {
            return Fail(MissionFailureReason.EvaExhausted, "Janela de EVA esgotada.");
        }

        if (!TryPickDeposit(prefer, out var kind, out var deposit))
        {
            return MissionStepResult.Fail(MissionFailureReason.None, "Nada amostrável neste corpo.");
        }

        _evaRemaining -= evaHours;
        var yield = evaHours * deposit.Abundance / deposit.ExtractDifficulty;
        _samples[kind] = (_samples.TryGetValue(kind, out var have) ? have : 0.0) + yield;

        return MissionStepResult.Success(
            $"Amostrado {yield:0.###} de {kind} ({evaHours:0.##} h). EVA restante {_evaRemaining:0.##} h.");
    }

    public MissionStepResult ReturnToBase()
    {
        if (Phase != MissionPhase.Surface)
        {
            return MissionStepResult.Fail(MissionFailureReason.None, "Só se volta a partir da superfície.");
        }

        var oneWay = _hazard.TransitPropellantOneWay * (1.0 + _loadout.ProtectionMass);
        if (_propellantRemaining + 1e-9 < oneWay)
        {
            return Fail(
                MissionFailureReason.InsufficientPropellant,
                "Propelente insuficiente para a volta.");
        }

        _propellantRemaining -= oneWay;
        Phase = MissionPhase.Completed;
        return MissionStepResult.Success(
            $"Volta concluída. Propelente residual {_propellantRemaining:0.##}; amostras: {FormatSamples()}.");
    }

    private bool TryPickDeposit(
        ResourceKind? prefer,
        out ResourceKind kind,
        out ResourceDeposit deposit)
    {
        if (prefer is { } chosen
            && _composition.Deposits.TryGetValue(chosen, out deposit)
            && deposit.Abundance > 0.0)
        {
            kind = chosen;
            return true;
        }

        kind = default;
        deposit = default;
        var best = -1.0;
        foreach (var (candidate, d) in _composition.Deposits)
        {
            if (d.Abundance <= 0.0)
            {
                continue;
            }

            var score = d.Abundance / d.ExtractDifficulty;
            if (score > best)
            {
                best = score;
                kind = candidate;
                deposit = d;
            }
        }

        return best > 0.0;
    }

    private MissionStepResult Fail(MissionFailureReason reason, string message)
    {
        Phase = MissionPhase.Failed;
        Failure = reason;
        return MissionStepResult.Fail(reason, message);
    }

    private string FormatSamples()
        => _samples.Count == 0
            ? "(nenhuma)"
            : string.Join(", ", _samples.Select(kv => $"{kv.Key}={kv.Value:0.###}"));
}
