namespace SolarSim.Engine.Models;

/// <summary>Posição de um corpo em um instante, em km, no referencial global.</summary>
public readonly record struct BodyState(string Id, Vector3D PositionKm);

/// <summary>
/// Estado completo do sistema em um instante. Publicado pelo motor a cada avanço
/// de tempo; consumido pela camada de adaptação.
/// </summary>
public readonly record struct SystemStateSnapshot(
    double JulianDate,
    IReadOnlyList<BodyState> Bodies);
