using SolarSim.Engine;
using SolarSim.Engine.Core;
using SolarSim.Engine.Models;

namespace SolarSim.Tests;

public sealed class LocalSkyTests
{
    private const double Lat = LocalSky.DefaultLatitudeDeg * Math.PI / 180.0;
    private const double Lon = LocalSky.DefaultLongitudeDeg * Math.PI / 180.0;

    [Fact]
    public void Sol_NoDecorrerDeUmDia_PassaAcimaEAbaixoDoHorizonte()
    {
        var (sim, env) = SolarSystem.NewEnvironment();
        var earth = sim.BodyOf("earth");
        Assert.True(env.TryGetEnvironment("earth", out var profile));

        var maxEl = double.NegativeInfinity;
        var minEl = double.PositiveInfinity;

        for (var step = 0; step <= 48; step++)
        {
            var jd = SolarSystem.J2000 + step / 48.0;
            var coords = LookAt(sim, profile, earth.RadiusKm, "sun", jd);
            maxEl = Math.Max(maxEl, coords.ElevationRad);
            minEl = Math.Min(minEl, coords.ElevationRad);
        }

        Assert.True(
            maxEl > AstroConstants.DegreesToRadians(15.0),
            $"Sol deveria subir (max el={AstroConstants.RadiansToDegrees(maxEl):0.#}°)");
        Assert.True(
            minEl < 0.0,
            $"Sol deveria pôr (min el={AstroConstants.RadiansToDegrees(minEl):0.#}°)");
    }

    [Fact]
    public void Lua_TemElevacaoFinita()
    {
        var (sim, env) = SolarSystem.NewEnvironment();
        var earth = sim.BodyOf("earth");
        Assert.True(env.TryGetEnvironment("earth", out var profile));
        var coords = LookAt(sim, profile, earth.RadiusKm, "moon", SolarSystem.J2000);

        Assert.True(double.IsFinite(coords.ElevationRad));
        Assert.True(double.IsFinite(coords.AzimuthRad));
        Assert.InRange(coords.ElevationRad, -Math.PI / 2.0, Math.PI / 2.0);
    }

    [Fact]
    public void DirectionEnu_EUnitariaERespeitaHorizonte()
    {
        var above = new HorizontalCoords(0.0, AstroConstants.DegreesToRadians(30.0));
        var dir = LocalSky.DirectionEnu(above);
        Assert.InRange(dir.Magnitude, 0.999, 1.001);
        Assert.True(dir.Y > 0.0);

        var below = new HorizontalCoords(0.0, AstroConstants.DegreesToRadians(-20.0));
        Assert.True(LocalSky.DirectionEnu(below).Y < 0.0);
        Assert.False(below.AboveHorizon);
        Assert.True(above.AboveHorizon);
    }

    [Fact]
    public void Observer_EstaAProximamenteORaioDaTerra()
    {
        var (_, env) = SolarSystem.NewEnvironment();
        Assert.True(env.TryGetEnvironment("earth", out var profile));
        var earthRadius = 6371.0;
        var obs = LocalSky.ObserverFromEarthCenterKm(
            Lat,
            Lon,
            SolarSystem.J2000,
            earthRadius,
            profile.RotationPeriodSeconds,
            profile.ObliquityRad);

        Assert.InRange(obs.Magnitude, earthRadius * 0.99, earthRadius * 1.01);
    }

    private static HorizontalCoords LookAt(
        SimEngine sim,
        BodyEnvironment profile,
        double earthRadiusKm,
        string bodyId,
        double jd)
    {
        var observer = LocalSky.ObserverFromEarthCenterKm(
            Lat,
            Lon,
            jd,
            earthRadiusKm,
            profile.RotationPeriodSeconds,
            profile.ObliquityRad);

        return LocalSky.Look(
            sim.PositionAt(bodyId, jd),
            sim.PositionAt("earth", jd),
            observer,
            profile.ObliquityRad);
    }
}
