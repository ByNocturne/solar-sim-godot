namespace SolarSim.Engine.Exploration;

/// <summary>Carga preparada antes da superfície. Cada nível custa massa e propelente.</summary>
public readonly record struct MissionLoadout
{
    public int ThermalShield { get; init; }

    public int RadiationShield { get; init; }

    /// <summary>Horas de EVA reservadas na carga.</summary>
    public double EvaHours { get; init; }

    /// <summary>
    /// Propelente carregado para a missão (ida, operações e volta). Unidades abstratas.
    /// </summary>
    public double Propellant { get; init; }

    /// <summary>Massa extra da proteção — aumenta o gasto de propelente na ida.</summary>
    public double ProtectionMass
        => (ThermalShield * 0.4) + (RadiationShield * 0.5);

    public static MissionLoadout Minimal => new()
    {
        ThermalShield = 0,
        RadiationShield = 0,
        EvaHours = 2.0,
        Propellant = 2.0,
    };

    public static MissionLoadout ForHazard(ExplorationHazard hazard, double evaHours)
    {
        var oneWay = hazard.TransitPropellantOneWay * (1.0 + ProtectionMassOf(
            hazard.RequiredThermalShield,
            hazard.RequiredRadiationShield));
        var propellant = (2.0 * oneWay) + 1.0;

        return new MissionLoadout
        {
            ThermalShield = hazard.RequiredThermalShield,
            RadiationShield = hazard.RequiredRadiationShield,
            EvaHours = Math.Min(evaHours, hazard.MaxSafeEvaHours),
            Propellant = propellant,
        };
    }

    private static double ProtectionMassOf(int thermal, int radiation)
        => (thermal * 0.4) + (radiation * 0.5);
}
