using SolarSim.Engine.Models;

namespace SolarSim.Engine.Core;

/// <summary>
/// Índice de habitabilidade básica BHI ∈ [0, 1], ponderando temperatura, pressão,
/// radiação e água. Pesos documentados; modelo de ensino.
/// </summary>
public static class HabitabilityEvaluator
{
    public const double TemperatureWeight = 0.35;
    public const double PressureWeight = 0.20;
    public const double RadiationWeight = 0.20;
    public const double WaterWeight = 0.25;

    public const double IdealTemperatureK = 288.0;
    public const double LiquidWaterMinK = 273.15;
    public const double LiquidWaterMaxK = 373.15;

    public const double IdealPressurePa = 101_325.0;

    public static double Evaluate(
        double surfaceTemperatureK,
        double surfacePressurePa,
        double relativeIonizingRadiation,
        LiquidWaterPresence liquidWater)
    {
        var tScore = TemperatureScore(surfaceTemperatureK);
        var pScore = PressureScore(surfacePressurePa);
        var rScore = RadiationScore(relativeIonizingRadiation);
        var wScore = WaterScore(liquidWater);

        return Math.Clamp(
            TemperatureWeight * tScore
            + PressureWeight * pScore
            + RadiationWeight * rScore
            + WaterWeight * wScore,
            0.0,
            1.0);
    }

    public static double TemperatureScore(double temperatureK)
    {
        if (temperatureK <= 0.0)
        {
            return 0.0;
        }

        if (temperatureK is >= LiquidWaterMinK and <= LiquidWaterMaxK)
        {
            var delta = Math.Abs(temperatureK - IdealTemperatureK);
            return Math.Clamp(1.0 - delta / 80.0, 0.4, 1.0);
        }

        var outside = temperatureK < LiquidWaterMinK
            ? LiquidWaterMinK - temperatureK
            : temperatureK - LiquidWaterMaxK;
        return Math.Clamp(0.35 - outside / 400.0, 0.0, 0.35);
    }

    public static double PressureScore(double pressurePa)
    {
        if (pressurePa <= 0.0)
        {
            return 0.05;
        }

        var logRatio = Math.Log10(pressurePa / IdealPressurePa);
        return Math.Clamp(1.0 - Math.Abs(logRatio) / 2.5, 0.0, 1.0);
    }

    public static double RadiationScore(double relativeIonizingRadiation)
    {
        if (!double.IsFinite(relativeIonizingRadiation) || relativeIonizingRadiation < 0.0)
        {
            return 0.0;
        }

        // 1,0 = Terra. Só o excesso acima disso penaliza o BHI.
        var excess = Math.Max(0.0, relativeIonizingRadiation - 1.0);
        return Math.Clamp(1.0 / (1.0 + 0.35 * excess), 0.0, 1.0);
    }

    public static double WaterScore(LiquidWaterPresence water)
        => water switch
        {
            LiquidWaterPresence.Surface => 1.0,
            LiquidWaterPresence.Subsurface => 0.55,
            _ => 0.0,
        };
}
