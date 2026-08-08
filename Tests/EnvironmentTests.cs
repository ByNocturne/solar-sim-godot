using SolarSim.Engine;
using SolarSim.Engine.Core;
using SolarSim.Engine.Data;
using SolarSim.Engine.Models;

namespace SolarSim.Tests;

public class EnvironmentLoaderTests
{
    [Fact]
    public void Loads_solar_system_environment_profiles()
    {
        var map = EnvironmentLoader.FromFile(SolarSystem.EnvironmentPath);
        Assert.True(map.ContainsKey("earth"));
        Assert.True(map.ContainsKey("venus"));
        Assert.True(map.ContainsKey("sun"));
        Assert.NotNull(map["sun"].Star);
        Assert.True(map["earth"].Atmosphere!.SurfacePressurePa > 100_000.0);
    }
}

public class ThermalCalculatorTests
{
    [Fact]
    public void Venus_surface_exceeds_700_K()
    {
        var (sim, env) = SolarSystem.NewEnvironment();
        var report = env.ReportFor(sim, "venus");
        Assert.True(report.SurfaceTemperatureK > 700.0, $"T={report.SurfaceTemperatureK}");
        Assert.True(report.GreenhouseOpticalDepth > 50.0);
    }

    [Fact]
    public void Earth_surface_in_liquid_water_range()
    {
        var (sim, env) = SolarSystem.NewEnvironment();
        var report = env.ReportFor(sim, "earth");
        Assert.InRange(report.SurfaceTemperatureK, 280.0, 300.0);
        Assert.Equal(LiquidWaterPresence.Surface, report.LiquidWater);
        Assert.InRange(report.RelativeIonizingRadiation, 0.7, 1.4);
    }
}

public class AtmosphericEscapeTests
{
    [Fact]
    public void Mars_loses_light_gases_earth_retains_heavies()
    {
        var (sim, env) = SolarSystem.NewEnvironment();
        var mars = env.ReportFor(sim, "mars");
        var earth = env.ReportFor(sim, "earth");

        Assert.False(mars.RetainsH2);
        Assert.False(mars.RetainsHe);
        Assert.True(earth.RetainsN2);
        Assert.True(earth.RetainsO2);
        Assert.True(earth.RetainsCO2);
        Assert.True(mars.RelativeIonizingRadiation > earth.RelativeIonizingRadiation);
    }

    [Fact]
    public void Venus_retains_heavy_gases_despite_heat()
    {
        var (sim, env) = SolarSystem.NewEnvironment();
        var venus = env.ReportFor(sim, "venus");
        Assert.True(venus.RetainsCO2);
        Assert.True(venus.RetainsN2);
    }
}

public class TidalHeatingTests
{
    [Fact]
    public void Europa_has_subsurface_ocean()
    {
        var (sim, env) = SolarSystem.NewEnvironment();
        var europa = env.ReportFor(sim, "europa");
        Assert.Equal(LiquidWaterPresence.Subsurface, europa.LiquidWater);
        Assert.Contains("subsurface_ocean", europa.ExplanationFlags);
    }

    [Fact]
    public void Earth_has_surface_water_without_needing_tide()
    {
        var (sim, env) = SolarSystem.NewEnvironment();
        var earth = env.ReportFor(sim, "earth");
        Assert.Equal(LiquidWaterPresence.Surface, earth.LiquidWater);
    }
}

public class HabitabilityTests
{
    [Fact]
    public void Earth_bhi_exceeds_0_85()
    {
        var (sim, env) = SolarSystem.NewEnvironment();
        var earth = env.ReportFor(sim, "earth");
        Assert.True(earth.HabitabilityIndex > 0.85, $"BHI={earth.HabitabilityIndex}");
        Assert.Contains("surface_liquid_water", earth.ExplanationFlags);
        Assert.Contains("high_bhi", earth.ExplanationFlags);
    }

    [Fact]
    public void Mars_radiation_exceeds_earth()
    {
        var (sim, env) = SolarSystem.NewEnvironment();
        var earth = env.ReportFor(sim, "earth");
        var mars = env.ReportFor(sim, "mars");
        Assert.True(
            mars.RelativeIonizingRadiation > earth.RelativeIonizingRadiation * 2.0,
            $"Mars={mars.RelativeIonizingRadiation} Earth={earth.RelativeIonizingRadiation}");
    }

    [Fact]
    public void Sun_is_not_treated_as_habitable_surface()
    {
        var (sim, env) = SolarSystem.NewEnvironment();
        var sun = env.ReportFor(sim, "sun");
        Assert.Contains("stellar_body", sun.ExplanationFlags);
        Assert.Equal(0.0, sun.HabitabilityIndex);
    }

    [Fact]
    public void Mars_and_venus_bhi_well_below_earth()
    {
        var (sim, env) = SolarSystem.NewEnvironment();
        var earth = env.ReportFor(sim, "earth").HabitabilityIndex;
        var mars = env.ReportFor(sim, "mars").HabitabilityIndex;
        var venus = env.ReportFor(sim, "venus").HabitabilityIndex;
        Assert.True(mars < 0.5, $"Mars BHI={mars}");
        Assert.True(venus < 0.5, $"Venus BHI={venus}");
        Assert.True(mars < earth);
        Assert.True(venus < earth);
    }
}

public class DiurnalSeasonalTests
{
    [Fact]
    public void Mars_diurnal_amplitude_exceeds_earth()
    {
        var (sim, env) = SolarSystem.NewEnvironment();
        var earth = env.ReportFor(sim, "earth");
        var mars = env.ReportFor(sim, "mars");
        Assert.True(
            mars.DiurnalAmplitudeK > earth.DiurnalAmplitudeK,
            $"Mars={mars.DiurnalAmplitudeK} Earth={earth.DiurnalAmplitudeK}");
    }

    [Fact]
    public void Earth_has_equator_warmer_than_pole()
    {
        var (sim, env) = SolarSystem.NewEnvironment();
        var earth = env.ReportFor(sim, "earth");
        Assert.True(earth.EquatorTemperatureK > earth.PolarTemperatureK);
    }
}

public class BiosignatureAndGeologicalTests
{
    [Fact]
    public void Earth_shows_biosignatures()
    {
        var (sim, env) = SolarSystem.NewEnvironment();
        var earth = env.ReportFor(sim, "earth");
        Assert.True(earth.HasOxygenMethaneDisequilibrium);
        Assert.True(earth.HasOzoneShield);
    }

    [Fact]
    public void Billion_year_jump_cools_mars_and_shifts_earth()
    {
        var (sim, env) = SolarSystem.NewEnvironment();
        var earthNow = env.ReportFor(sim, "earth");
        var marsNow = env.ReportFor(sim, "mars");

        var futureJd = AstroConstants.J2000 + 1.0e9 * 365.25;
        var earthFuture = env.ReportFor(sim, "earth", futureJd);
        var marsFuture = env.ReportFor(sim, "mars", futureJd);

        // Sol mais brilhante → Terra mais quente; Marte perde ainda mais atmosfera.
        Assert.True(
            earthFuture.SurfaceTemperatureK > earthNow.SurfaceTemperatureK,
            $"Earth now={earthNow.SurfaceTemperatureK} future={earthFuture.SurfaceTemperatureK}");
        Assert.True(
            marsFuture.SurfacePressurePa < marsNow.SurfacePressurePa,
            $"Mars P now={marsNow.SurfacePressurePa} future={marsFuture.SurfacePressurePa}");
    }

    [Fact]
    public void Potential_biosphere_event_fires_for_earth()
    {
        var (sim, env) = SolarSystem.NewEnvironment();
        env.ResetBiosphereAnnouncements();
        PotentialBiosphereDetectedEventArgs? args = null;
        env.PotentialBiosphereDetected += a => args = a;

        _ = env.ReportFor(sim, "earth");

        Assert.NotNull(args);
        Assert.Equal("earth", args!.BodyId);
        Assert.True(args.HabitabilityIndex >= EnvironmentService.BiosphereBhiThreshold
            || args.ChemicalBiosignature);
    }
}
