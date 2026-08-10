using Godot;
using SolarSim.Engine;
using SolarSim.Engine.Core;
using SolarSim.Engine.Data;
using SolarSim.Engine.Models;
using SolarSim.Render;

namespace SolarSim.Bridge;

/// <summary>
/// Cálculo do céu da Terra compartilhado pelo protótipo no sim (K) e pelo host do jogo.
/// Só direções — sem <see cref="ScaleMapper"/>.
/// </summary>
public static class EarthSurfaceSky
{
    public static double BestDaylightJulianDate(
        SimEngine sim,
        EnvironmentService environment,
        double startJulianDate)
    {
        if (!environment.TryGetEnvironment("earth", out var profile) || !sim.Contains("earth"))
        {
            return startJulianDate;
        }

        var earth = sim.BodyOf("earth");
        var lat = AstroConstants.DegreesToRadians(LocalSky.DefaultLatitudeDeg);
        var lon = AstroConstants.DegreesToRadians(LocalSky.DefaultLongitudeDeg);
        var bestJd = startJulianDate;
        var bestEl = double.NegativeInfinity;

        for (var step = 0; step <= 48; step++)
        {
            var jd = startJulianDate + step / 48.0;
            var observer = LocalSky.ObserverFromEarthCenterKm(
                lat,
                lon,
                jd,
                earth.RadiusKm,
                profile.RotationPeriodSeconds,
                profile.ObliquityRad);
            var coords = LocalSky.Look(
                sim.PositionAt("sun", jd),
                sim.PositionAt("earth", jd),
                observer,
                profile.ObliquityRad);

            if (coords.ElevationRad > bestEl)
            {
                bestEl = coords.ElevationRad;
                bestJd = jd;
            }
        }

        return bestJd;
    }

    public static IReadOnlyList<SurfaceSkyMarker> Markers(
        SimEngine sim,
        EnvironmentService environment,
        double julianDate)
    {
        if (!environment.TryGetEnvironment("earth", out var profile) || !sim.Contains("earth"))
        {
            return Array.Empty<SurfaceSkyMarker>();
        }

        var earth = sim.BodyOf("earth");
        var lat = AstroConstants.DegreesToRadians(LocalSky.DefaultLatitudeDeg);
        var lon = AstroConstants.DegreesToRadians(LocalSky.DefaultLongitudeDeg);
        var observer = LocalSky.ObserverFromEarthCenterKm(
            lat,
            lon,
            julianDate,
            earth.RadiusKm,
            profile.RotationPeriodSeconds,
            profile.ObliquityRad);
        var earthPos = sim.PositionAt("earth", julianDate);

        var markers = new List<SurfaceSkyMarker>();
        foreach (var body in sim.Bodies)
        {
            if (!IsSkyBody(body))
            {
                continue;
            }

            var coords = LocalSky.Look(
                sim.PositionAt(body.Id, julianDate),
                earthPos,
                observer,
                profile.ObliquityRad);

            if (!coords.AboveHorizon)
            {
                continue;
            }

            var enu = LocalSky.DirectionEnu(coords);
            var r = SurfaceSkyView.CelestialRadius;
            markers.Add(new SurfaceSkyMarker(
                body.Id,
                body.Name,
                BodyPalette.Of(body.ColorRgb),
                new Vector3((float)(enu.X * r), (float)(enu.Y * r), (float)(enu.Z * r))));
        }

        return markers;
    }

    private static bool IsSkyBody(CelestialBodyData body)
        => body.Id != "earth"
            && (body.Kind is BodyKind.Star or BodyKind.Planet || body.Id == "moon");
}
