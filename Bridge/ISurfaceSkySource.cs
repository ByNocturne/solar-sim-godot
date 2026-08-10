namespace SolarSim.Bridge;

/// <summary>Fonte de marcadores do céu local — simulador ou host do jogo.</summary>
public interface ISurfaceSkySource
{
    IReadOnlyList<SurfaceSkyMarker> SurfaceSkyMarkers();
}
