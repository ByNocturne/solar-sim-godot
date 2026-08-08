namespace SolarSim.Engine.Models;

/// <summary>
/// Retrato ambiental de um corpo num instante. Montado por consulta e descartado —
/// a interface não guarda cópia própria.
/// </summary>
public readonly record struct EnvironmentReport
{
    public required string BodyId { get; init; }

    public required string Name { get; init; }

    public double EquilibriumTemperatureK { get; init; }

    public double SurfaceTemperatureK { get; init; }

    public double GreenhouseOpticalDepth { get; init; }

    public double SurfacePressurePa { get; init; }

    public double BondAlbedo { get; init; }

    public double EffectiveAlbedo { get; init; }

    public double EscapeSpeedKmS { get; init; }

    public double RelativeIonizingRadiation { get; init; }

    public double MagneticMomentRelativeToEarth { get; init; }

    public double TidalHeatingWatts { get; init; }

    public LiquidWaterPresence LiquidWater { get; init; }

    public double HabitabilityIndex { get; init; }

    public bool RetainsH2 { get; init; }

    public bool RetainsHe { get; init; }

    public bool RetainsN2 { get; init; }

    public bool RetainsO2 { get; init; }

    public bool RetainsCO2 { get; init; }

    public bool HasOzoneShield { get; init; }

    public bool HasOxygenMethaneDisequilibrium { get; init; }

    public double DiurnalAmplitudeK { get; init; }

    public double SeasonalAmplitudeK { get; init; }

    public double EquatorTemperatureK { get; init; }

    public double TemperateTemperatureK { get; init; }

    public double PolarTemperatureK { get; init; }

    public bool PolarIce { get; init; }

    public IReadOnlyList<string> ExplanationFlags { get; init; }
}

/// <summary>Argumentos do evento de potencial biosfera.</summary>
public sealed class PotentialBiosphereDetectedEventArgs : EventArgs
{
    public required string BodyId { get; init; }

    public required string Name { get; init; }

    public double HabitabilityIndex { get; init; }

    public bool ChemicalBiosignature { get; init; }
}
