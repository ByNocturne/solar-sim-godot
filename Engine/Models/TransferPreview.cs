using SolarSim.Engine.Models;

namespace SolarSim.Engine.Models;

/// <summary>
/// Retrato de uma transferência Lambert entre dois corpos, em um instante e com um
/// tempo de voo. Montado por consulta e descartado em seguida — preview não altera
/// estado.
/// </summary>
public readonly record struct TransferPreview
{
    public required string OriginBodyId { get; init; }

    public required string DestinationBodyId { get; init; }

    /// <summary>Corpo central comum — em geral o Sol.</summary>
    public required string CentralBodyId { get; init; }

    public double DepartureJulianDate { get; init; }

    public double TimeOfFlightDays { get; init; }

    public double ArrivalJulianDate => DepartureJulianDate + TimeOfFlightDays;

    /// <summary>Quanto falta à velocidade do originário para entrar na transferência.</summary>
    public double DepartureDeltaVKmS { get; init; }

    /// <summary>Quanto falta, na chegada, para igualar a velocidade do destino.</summary>
    public double ArrivalDeltaVKmS { get; init; }

    public double TotalDeltaVKmS => DepartureDeltaVKmS + ArrivalDeltaVKmS;

    /// <summary>Velocidade heliocêntrica (relativa ao central) na partida, da solução.</summary>
    public Vector3D DepartureVelocityKmS { get; init; }

    public Vector3D ArrivalVelocityKmS { get; init; }

    public bool ShortWay { get; init; }

    public bool IsValid => double.IsFinite(TotalDeltaVKmS) && TotalDeltaVKmS > 0.0;
}

/// <summary>Uma célula da grade de janelas: partida × tempo de voo → Δv total.</summary>
public readonly record struct TransferWindowSample(
    double DepartureJulianDate,
    double TimeOfFlightDays,
    double TotalDeltaVKmS);
