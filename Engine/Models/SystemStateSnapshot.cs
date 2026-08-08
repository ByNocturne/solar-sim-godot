namespace SolarSim.Engine.Models;

/// <summary>Posição de um corpo em um instante, em km.</summary>
/// <param name="PositionKm">No referencial global, com a cadeia de pais composta.</param>
/// <param name="LocalPositionKm">
/// Relativa ao corpo pai, nula para a raiz. Publicada junto porque a camada de
/// apresentação precisa dela: uma escala que comprime distâncias interplanetárias não
/// pode ser aplicada à órbita de uma lua sem colar a lua no planeta.
/// </param>
public readonly record struct BodyState(
    string Id,
    Vector3D PositionKm,
    Vector3D LocalPositionKm);

/// <summary>
/// Estado completo do sistema em um instante. Publicado pelo motor a cada avanço
/// de tempo; consumido pela camada de adaptação.
/// </summary>
public readonly record struct SystemStateSnapshot(
    double JulianDate,
    IReadOnlyList<BodyState> Bodies);
