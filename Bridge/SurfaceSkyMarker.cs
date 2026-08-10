using Godot;

namespace SolarSim.Bridge;

/// <summary>Marcador do modo céu: posição já em unidades de cena (R×direção), não km.</summary>
public readonly record struct SurfaceSkyMarker(
    string BodyId,
    string Name,
    Color Color,
    Vector3 ScenePosition);
