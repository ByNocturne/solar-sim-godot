using SolarSim.Engine.Application;
using SolarSim.Engine.Exploration;

namespace SolarSim.Tests;

public sealed class SimSessionTests
{
    [Fact]
    public void FromDataRoot_CarregaSistemaEAmbienteSemGodot()
    {
        var session = SimSession.FromDataRoot(SolarSystem.RepoRootPath);

        Assert.True(session.Contains("earth"));
        Assert.True(session.Contains("moon"));
        Assert.True(session.Contains("bennu"));

        var earth = session.EnvironmentFor("earth");
        Assert.Equal("earth", earth.BodyId);
        Assert.True(earth.HabitabilityIndex > 0.5);

        var moon = session.EnvironmentFor("moon");
        Assert.Contains(moon.ExplanationFlags, f => f is "thin_atmosphere" or "high_radiation" or "too_cold");
    }

    [Fact]
    public void HabitabilityFor_EspelhaRelatorio()
    {
        var session = SimSession.FromDataRoot(SolarSystem.RepoRootPath, includeCatalog: false);
        var report = session.EnvironmentFor("earth");
        Assert.Equal(report.HabitabilityIndex, session.HabitabilityFor("earth"));
    }
}
