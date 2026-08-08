using SolarSim.Engine.Core;
using SolarSim.Engine.Models;

namespace SolarSim.Engine;

/// <summary>
/// Consultas ambientais como função pura da Data Juliana e dos perfis estáticos.
/// </summary>
public sealed class EnvironmentService
{
    public const double BiosphereBhiThreshold = 0.65;

    private readonly IReadOnlyDictionary<string, BodyEnvironment> _environments;
    private readonly HashSet<string> _biosphereAnnounced = new(StringComparer.Ordinal);

    public EnvironmentService(IReadOnlyDictionary<string, BodyEnvironment> environments)
    {
        ArgumentNullException.ThrowIfNull(environments);
        _environments = environments;
    }

    public event Action<PotentialBiosphereDetectedEventArgs>? PotentialBiosphereDetected;

    public bool TryGetEnvironment(string bodyId, out BodyEnvironment environment)
        => _environments.TryGetValue(bodyId, out environment!);

    public EnvironmentReport ReportFor(SimEngine sim, string bodyId, double? julianDate = null)
    {
        ArgumentNullException.ThrowIfNull(sim);
        var jd = julianDate ?? sim.Time.JulianDate;
        var body = sim.BodyOf(bodyId);

        if (bodyId == sim.Root.Id || (_environments.TryGetValue(bodyId, out var rootEnv) && rootEnv.Star is not null))
        {
            return StarReport(body);
        }

        var env = _environments.TryGetValue(bodyId, out var profile)
            ? profile
            : DefaultEnvironment(bodyId);

        var star = ResolveStar(sim);
        var luminosity = GeologicalTimeModel.StellarLuminosityRelative(jd);
        var distanceKm = HeliocentricDistanceKm(sim, bodyId, jd);

        var magnetic = MagnetosphereEstimator.MagneticMomentRelativeToEarth(
            body.MuKm3S2,
            env.MetallicCoreFraction,
            env.RotationPeriodSeconds);

        var atmosphere = GeologicalTimeModel.ErodeAtmosphere(env.Atmosphere, magnetic, jd);

        var teq = ThermalCalculator.EquilibriumTemperatureK(
            star.EffectiveTemperatureK * Math.Pow(luminosity, 0.25),
            star.RadiusKm,
            distanceKm,
            env.BondAlbedo);

        var opticalDepth = ThermalCalculator.GreenhouseOpticalDepth(atmosphere);
        var tSurface = ThermalCalculator.SurfaceTemperatureK(teq, opticalDepth);

        var elements = sim.ElementsOf(bodyId);
        var eccentricity = elements?.Eccentricity ?? 0.0;
        var semiMajor = elements?.SemiMajorAxisKm ?? 0.0;
        var trueAnomaly = 0.0;
        if (elements is { } el && body.ParentId is { } parentId)
        {
            var parentMu = sim.GravitationalParameterOf(parentId);
            var days = jd - AstroConstants.J2000;
            trueAnomaly = KeplerPropagator.TrueAnomalyAt(el, parentMu, days);
        }

        var tidalWatts = 0.0;
        if (body.ParentId is { } pid && elements is not null)
        {
            tidalWatts = TidalHeatingCalculator.HeatingWatts(
                sim.GravitationalParameterOf(pid),
                body.RadiusKm,
                semiMajor,
                eccentricity,
                env.TidalLoveNumberK2,
                env.TidalQualityFactor);
        }

        var liquidWater = ResolveLiquidWater(tSurface, atmosphere, tidalWatts, bodyId);

        var radiation = MagnetosphereEstimator.RelativeIonizingRadiation(
            distanceKm,
            magnetic,
            luminosity);

        var escape = AtmosphericEscape.EscapeSpeedKmS(body.MuKm3S2, body.RadiusKm);
        var retainsH2 = AtmosphericEscape.RetainsSpecies(
            body.MuKm3S2, body.RadiusKm, tSurface, AtmosphericEscape.MolarMassAMU.H2);
        var retainsHe = AtmosphericEscape.RetainsSpecies(
            body.MuKm3S2, body.RadiusKm, tSurface, AtmosphericEscape.MolarMassAMU.He);
        var retainsN2 = AtmosphericEscape.RetainsSpecies(
            body.MuKm3S2, body.RadiusKm, tSurface, AtmosphericEscape.MolarMassAMU.N2);
        var retainsO2 = AtmosphericEscape.RetainsSpecies(
            body.MuKm3S2, body.RadiusKm, tSurface, AtmosphericEscape.MolarMassAMU.O2);
        var retainsCO2 = AtmosphericEscape.RetainsSpecies(
            body.MuKm3S2, body.RadiusKm, tSurface, AtmosphericEscape.MolarMassAMU.CO2);

        var pressure = atmosphere?.SurfacePressurePa ?? 0.0;
        var diurnal = DiurnalSeasonalModel.DiurnalAmplitudeK(
            tSurface,
            env.RotationPeriodSeconds,
            env.ThermalInertiaJPerM2KSqrtS,
            pressure);
        var seasonal = DiurnalSeasonalModel.SeasonalAmplitudeK(
            tSurface,
            env.ObliquityRad,
            eccentricity);

        var zones = ZoneGridCalculator.Evaluate(
            tSurface,
            seasonal,
            env.BondAlbedo,
            trueAnomaly,
            env.ObliquityRad);

        var bhi = HabitabilityEvaluator.Evaluate(
            tSurface,
            pressure,
            radiation,
            liquidWater);

        var o2ch4 = BiosignatureEvaluator.HasOxygenMethaneDisequilibrium(atmosphere);
        var ozone = BiosignatureEvaluator.HasOzoneShield(atmosphere);

        var flags = BuildFlags(
            bodyId,
            tSurface,
            opticalDepth,
            pressure,
            radiation,
            liquidWater,
            retainsH2,
            retainsCO2,
            bhi,
            o2ch4,
            ozone,
            tidalWatts,
            zones.PolarIce);

        var report = new EnvironmentReport
        {
            BodyId = bodyId,
            Name = body.Name,
            EquilibriumTemperatureK = teq,
            SurfaceTemperatureK = tSurface,
            GreenhouseOpticalDepth = opticalDepth,
            SurfacePressurePa = pressure,
            BondAlbedo = env.BondAlbedo,
            EffectiveAlbedo = zones.EffectiveAlbedo,
            EscapeSpeedKmS = escape,
            RelativeIonizingRadiation = radiation,
            MagneticMomentRelativeToEarth = magnetic,
            TidalHeatingWatts = tidalWatts,
            LiquidWater = liquidWater,
            HabitabilityIndex = bhi,
            RetainsH2 = retainsH2,
            RetainsHe = retainsHe,
            RetainsN2 = retainsN2,
            RetainsO2 = retainsO2,
            RetainsCO2 = retainsCO2,
            HasOzoneShield = ozone,
            HasOxygenMethaneDisequilibrium = o2ch4,
            DiurnalAmplitudeK = diurnal,
            SeasonalAmplitudeK = seasonal,
            EquatorTemperatureK = zones.EquatorK,
            TemperateTemperatureK = zones.TemperateK,
            PolarTemperatureK = zones.PolarK,
            PolarIce = zones.PolarIce,
            ExplanationFlags = flags,
        };

        MaybeRaiseBiosphere(report);
        return report;
    }

    private static EnvironmentReport StarReport(CelestialBodyData body)
        => new()
        {
            BodyId = body.Id,
            Name = body.Name,
            EquilibriumTemperatureK = 0.0,
            SurfaceTemperatureK = 0.0,
            GreenhouseOpticalDepth = 0.0,
            SurfacePressurePa = 0.0,
            BondAlbedo = 0.0,
            EffectiveAlbedo = 0.0,
            EscapeSpeedKmS = 0.0,
            RelativeIonizingRadiation = 0.0,
            MagneticMomentRelativeToEarth = 0.0,
            TidalHeatingWatts = 0.0,
            LiquidWater = LiquidWaterPresence.None,
            HabitabilityIndex = 0.0,
            ExplanationFlags = ["stellar_body"],
        };

    private void MaybeRaiseBiosphere(in EnvironmentReport report)
    {
        var chemical = report.HasOxygenMethaneDisequilibrium;
        if (report.HabitabilityIndex < BiosphereBhiThreshold && !chemical)
        {
            return;
        }

        if (!_biosphereAnnounced.Add(report.BodyId))
        {
            return;
        }

        PotentialBiosphereDetected?.Invoke(
            new PotentialBiosphereDetectedEventArgs
            {
                BodyId = report.BodyId,
                Name = report.Name,
                HabitabilityIndex = report.HabitabilityIndex,
                ChemicalBiosignature = chemical,
            });
    }

    /// <summary>Permite rearmar o debounce (ex.: após salto geológico nos testes).</summary>
    public void ResetBiosphereAnnouncements() => _biosphereAnnounced.Clear();

    private static LiquidWaterPresence ResolveLiquidWater(
        double surfaceTemperatureK,
        AtmosphereProfile? atmosphere,
        double tidalWatts,
        string bodyId)
    {
        var pressure = atmosphere?.SurfacePressurePa ?? 0.0;
        if (surfaceTemperatureK is >= HabitabilityEvaluator.LiquidWaterMinK
                and <= HabitabilityEvaluator.LiquidWaterMaxK
            && pressure >= 600.0)
        {
            return LiquidWaterPresence.Surface;
        }

        if (tidalWatts >= TidalHeatingCalculator.SubsurfaceOceanHeatingThresholdWatts
            || bodyId is "europa" or "titan")
        {
            // Titã/Europa: oceano interno no modelo de ensino quando há maré relevante
            // ou perfil conhecido.
            if (bodyId == "europa"
                || tidalWatts >= TidalHeatingCalculator.SubsurfaceOceanHeatingThresholdWatts)
            {
                return LiquidWaterPresence.Subsurface;
            }
        }

        return LiquidWaterPresence.None;
    }

    private static double HeliocentricDistanceKm(SimEngine sim, string bodyId, double jd)
    {
        if (bodyId == sim.Root.Id)
        {
            return sim.Root.RadiusKm;
        }

        return sim.PositionAt(bodyId, jd).Magnitude;
    }

    private StellarProperties ResolveStar(SimEngine sim)
    {
        if (_environments.TryGetValue(sim.Root.Id, out var rootEnv) && rootEnv.Star is { } star)
        {
            return star;
        }

        return new StellarProperties
        {
            EffectiveTemperatureK = 5772.0,
            RadiusKm = sim.Root.RadiusKm,
        };
    }

    private static BodyEnvironment DefaultEnvironment(string bodyId)
        => new()
        {
            BodyId = bodyId,
            BondAlbedo = 0.1,
            RotationPeriodSeconds = 86_164.0,
            ObliquityRad = 0.0,
            MetallicCoreFraction = 0.0,
            TidalLoveNumberK2 = 0.0,
            TidalQualityFactor = 100.0,
            ThermalInertiaJPerM2KSqrtS = 100.0,
        };

    private static IReadOnlyList<string> BuildFlags(
        string bodyId,
        double tSurface,
        double opticalDepth,
        double pressure,
        double radiation,
        LiquidWaterPresence water,
        bool retainsH2,
        bool retainsCO2,
        double bhi,
        bool o2ch4,
        bool ozone,
        double tidalWatts,
        bool polarIce)
    {
        var flags = new List<string>();

        if (opticalDepth > 50.0)
        {
            flags.Add("runaway_greenhouse");
        }

        if (tSurface is >= HabitabilityEvaluator.LiquidWaterMinK
            and <= HabitabilityEvaluator.LiquidWaterMaxK)
        {
            flags.Add("temperate_surface");
        }
        else if (tSurface > HabitabilityEvaluator.LiquidWaterMaxK)
        {
            flags.Add("too_hot");
        }
        else
        {
            flags.Add("too_cold");
        }

        if (pressure < 1000.0)
        {
            flags.Add("thin_atmosphere");
        }
        else if (pressure > 1.0e6)
        {
            flags.Add("dense_atmosphere");
        }

        if (radiation > 2.0)
        {
            flags.Add("high_radiation");
        }
        else if (radiation <= 1.25)
        {
            flags.Add("shielded_radiation");
        }

        if (!retainsH2)
        {
            flags.Add("light_gases_escape");
        }

        if (retainsCO2 && pressure > 0.0)
        {
            flags.Add("retains_heavy_gases");
        }

        if (water == LiquidWaterPresence.Surface)
        {
            flags.Add("surface_liquid_water");
        }
        else if (water == LiquidWaterPresence.Subsurface)
        {
            flags.Add("subsurface_ocean");
        }
        else
        {
            flags.Add("no_liquid_water");
        }

        if (tidalWatts >= TidalHeatingCalculator.SubsurfaceOceanHeatingThresholdWatts)
        {
            flags.Add("strong_tidal_heating");
        }

        if (polarIce)
        {
            flags.Add("polar_ice");
        }

        if (o2ch4)
        {
            flags.Add("o2_ch4_disequilibrium");
        }

        if (ozone)
        {
            flags.Add("ozone_uv_shield");
        }

        if (bhi >= BiosphereBhiThreshold)
        {
            flags.Add("high_bhi");
        }

        if (bodyId == "earth")
        {
            flags.Add("reference_habitable");
        }

        return flags;
    }
}
