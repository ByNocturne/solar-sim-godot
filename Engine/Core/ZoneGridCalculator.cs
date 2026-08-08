namespace SolarSim.Engine.Core;

/// <summary>
/// Grade latitudinal simples (equador, temperada, polar) com feedback de albedo por gelo.
/// </summary>
public static class ZoneGridCalculator
{
    public readonly record struct ZoneTemperatures(
        double EquatorK,
        double TemperateK,
        double PolarK,
        bool PolarIce,
        double EffectiveAlbedo);

    public static ZoneTemperatures Evaluate(
        double meanSurfaceTemperatureK,
        double seasonalAmplitudeK,
        double bondAlbedo,
        double trueAnomalyRad,
        double obliquityRad)
    {
        if (meanSurfaceTemperatureK <= 0.0)
        {
            return new ZoneTemperatures(0.0, 0.0, 0.0, false, bondAlbedo);
        }

        // Fração sazonal: verão no hemisfério “norte” do modelo em anomalia ~90°.
        var season = Math.Sin(trueAnomalyRad) * Math.Sin(obliquityRad);
        var equator = meanSurfaceTemperatureK + 0.15 * seasonalAmplitudeK;
        var temperate = meanSurfaceTemperatureK + 0.05 * seasonalAmplitudeK * season;
        var polar = meanSurfaceTemperatureK - 0.55 * seasonalAmplitudeK + 0.25 * seasonalAmplitudeK * season;

        var polarIce = polar < HabitabilityEvaluator.LiquidWaterMinK;
        // Feedback local nas zonas; o albedo efetivo é informativo e NÃO realimenta a
        // temperatura global (isso gerava Terra bola-de-neve espúria no modelo de ensino).
        var effectiveAlbedo = polarIce
            ? Math.Clamp(bondAlbedo + 0.04, 0.0, 0.95)
            : bondAlbedo;

        if (polarIce)
        {
            var cool = 3.0;
            equator -= cool * 0.15;
            temperate -= cool * 0.35;
            polar -= cool;
        }

        return new ZoneTemperatures(equator, temperate, polar, polarIce, effectiveAlbedo);
    }
}
