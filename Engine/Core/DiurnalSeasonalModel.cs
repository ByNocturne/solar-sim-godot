namespace SolarSim.Engine.Core;

/// <summary>
/// Amplitude térmica diurna e sazonal a partir de inércia térmica, atmosfera, obliquidade
/// e excentricidade. Tudo avaliado no instante — sem estado acumulado.
/// </summary>
public static class DiurnalSeasonalModel
{
    public static double DiurnalAmplitudeK(
        double surfaceTemperatureK,
        double rotationPeriodSeconds,
        double thermalInertiaJPerM2KSqrtS,
        double surfacePressurePa)
    {
        if (surfaceTemperatureK <= 0.0 || rotationPeriodSeconds <= 0.0)
        {
            return 0.0;
        }

        var inertia = Math.Max(thermalInertiaJPerM2KSqrtS, 1.0);
        var atmosphereDamping = 1.0 + surfacePressurePa / 20_000.0;
        var periodFactor = Math.Sqrt(rotationPeriodSeconds / 86_164.0);
        var raw = 180.0 * periodFactor / (inertia / 50.0) / atmosphereDamping;
        return Math.Clamp(raw, 0.0, surfaceTemperatureK * 0.9);
    }

    public static double SeasonalAmplitudeK(
        double surfaceTemperatureK,
        double obliquityRad,
        double eccentricity)
    {
        if (surfaceTemperatureK <= 0.0)
        {
            return 0.0;
        }

        var tilt = Math.Abs(Math.Sin(obliquityRad));
        // Obliquidade retrógrada perto de 180° (Vênus) tem sin pequeno — ok.
        var ecc = Math.Abs(eccentricity);
        return surfaceTemperatureK * (0.35 * tilt + 0.15 * ecc);
    }
}
