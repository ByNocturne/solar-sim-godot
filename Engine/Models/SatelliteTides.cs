namespace SolarSim.Engine.Models;

/// <summary>
/// O que a maré do corpo pai impõe a um satélite: os dois limites de Roche e o destino
/// que sai da comparação com o periápside.
/// </summary>
public readonly record struct SatelliteTides
{
    public double RigidLimitKm { get; init; }

    public double FluidLimitKm { get; init; }

    /// <summary>Maior aproximação do pai na órbita de hoje. É o que decide.</summary>
    public double PeriapsisKm { get; init; }

    public SatelliteFate Fate { get; init; }

    /// <summary>
    /// Quanto o periápside sobra sobre o limite fluido: 1 é passar raspando, 2 é passar
    /// ao dobro da distância. Zero quando não há resposta.
    /// </summary>
    public double MarginOverFluid
        => FluidLimitKm > 0.0 ? PeriapsisKm / FluidLimitKm : 0.0;

    public bool IsKnown => Fate != SatelliteFate.Unknown;
}
