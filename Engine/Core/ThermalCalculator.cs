using SolarSim.Engine.Models;

namespace SolarSim.Engine.Core;

/// <summary>
/// Balanço térmico radiativo e estufa paramétrica. Modelo de ensino: não é GCM.
/// </summary>
/// <remarks>
/// A temperatura de equilíbrio é
/// <c>T_eq = T★ · √(R★ / (2d)) · (1 − A)^(1/4)</c>.
/// A estufa usa profundidade óptica efetiva τ a partir das pressões parciais de CO₂, H₂O
/// e CH₄, e T_s = T_eq · (1 + ¾τ)^(1/4). Os coeficientes são calibrados para o Sistema
/// Solar J2000 (Vênus &gt; 700 K, Terra ~288 K), não para exoplanetas arbitrários.
/// </remarks>
public static class ThermalCalculator
{
    /// <summary>Coeficiente de τ por pascal de pressão parcial de CO₂.</summary>
    public const double Co2OpticalDepthPerPa = 2.2e-4;

    public const double H2oOpticalDepthPerPa = 1.1e-3;

    public const double Ch4OpticalDepthPerPa = 5.0e-3;

    /// <summary>
    /// Continuidade cinza mínima para atmosferas densas (≳0,1 bar), aproximando o
    /// efeito de banda larga que o vapor e o N₂/O₂ produzem na Terra (~33 K de estufa).
    /// </summary>
    public const double DenseAtmosphereFloorOpticalDepth = 0.55;

    public const double DenseAtmospherePressureThresholdPa = 2.0e4;

    /// <summary>Piso de τ para atmosferas densas de CO₂ (Vênus).</summary>
    public const double DenseCo2BonusOpticalDepth = 120.0;

    public const double DenseCo2PressureThresholdPa = 1.0e6;

    public static double EquilibriumTemperatureK(
        double starEffectiveTemperatureK,
        double starRadiusKm,
        double heliocentricDistanceKm,
        double bondAlbedo)
    {
        if (heliocentricDistanceKm <= 0.0
            || starEffectiveTemperatureK <= 0.0
            || starRadiusKm <= 0.0)
        {
            return 0.0;
        }

        var albedo = Math.Clamp(bondAlbedo, 0.0, 0.999);
        return starEffectiveTemperatureK
            * Math.Sqrt(starRadiusKm / (2.0 * heliocentricDistanceKm))
            * Math.Pow(1.0 - albedo, 0.25);
    }

    public static double GreenhouseOpticalDepth(AtmosphereProfile? atmosphere)
    {
        if (atmosphere is null || atmosphere.SurfacePressurePa <= 0.0)
        {
            return 0.0;
        }

        var p = atmosphere.SurfacePressurePa;
        var tau =
            Co2OpticalDepthPerPa * p * atmosphere.MoleFractionCO2
            + H2oOpticalDepthPerPa * p * atmosphere.MoleFractionH2O
            + Ch4OpticalDepthPerPa * p * atmosphere.MoleFractionCH4;

        if (p >= DenseAtmospherePressureThresholdPa)
        {
            tau = Math.Max(tau, DenseAtmosphereFloorOpticalDepth);
        }

        if (p * atmosphere.MoleFractionCO2 >= DenseCo2PressureThresholdPa)
        {
            tau += DenseCo2BonusOpticalDepth;
        }

        return Math.Max(0.0, tau);
    }

    public static double SurfaceTemperatureK(double equilibriumTemperatureK, double opticalDepth)
    {
        if (equilibriumTemperatureK <= 0.0)
        {
            return 0.0;
        }

        var tau = Math.Max(0.0, opticalDepth);
        return equilibriumTemperatureK * Math.Pow(1.0 + 0.75 * tau, 0.25);
    }
}
