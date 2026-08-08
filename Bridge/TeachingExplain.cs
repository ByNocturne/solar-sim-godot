using System.Globalization;
using SolarSim.Engine.Models;

namespace SolarSim.Bridge;

/// <summary>
/// Texto de ensino a partir dos flags do relatório ambiental — sem lore narrativo.
/// </summary>
public static class TeachingExplain
{
    private static readonly NumberFormatInfo Numbers = new()
    {
        NumberDecimalSeparator = ",",
        NumberGroupSeparator = ".",
        NumberGroupSizes = [3],
    };

    public static IReadOnlyList<string> LinesFor(in EnvironmentReport report)
    {
        if (report.ExplanationFlags.Contains("stellar_body"))
        {
            return
            [
                $"{report.Name}: corpo estelar — o modelo ambiental de superfície não se aplica.",
                "Use planetas ou luas para BHI, atmosfera e água.",
            ];
        }

        var lines = new List<string>
        {
            $"{report.Name}: BHI {DisplayFormat.HabitabilityIndex(report.HabitabilityIndex)}, "
                + $"T {DisplayFormat.Temperature(report.SurfaceTemperatureK)}, "
                + $"P {DisplayFormat.Pressure(report.SurfacePressurePa)}.",
        };

        foreach (var flag in report.ExplanationFlags)
        {
            if (Phrase(flag) is { } phrase)
            {
                lines.Add(phrase);
            }
        }

        lines.Add(
            $"Radiação {DisplayFormat.RadiationRelative(report.RelativeIonizingRadiation)}; "
                + $"água: {WaterLabel(report.LiquidWater)}.");

        return lines;
    }

    public static IReadOnlyList<string> CompareToEarth(
        in EnvironmentReport subject,
        in EnvironmentReport earth)
    {
        var dT = subject.SurfaceTemperatureK - earth.SurfaceTemperatureK;
        var dBhi = subject.HabitabilityIndex - earth.HabitabilityIndex;
        var dRad = subject.RelativeIonizingRadiation - earth.RelativeIonizingRadiation;

        return
        [
            $"Versus Terra: ΔT {Signed(dT, "N1")} K, "
                + $"ΔBHI {Signed(dBhi, "N2")}, "
                + $"Δradiação {Signed(dRad, "N2")}×.",
        ];
    }

    private static string WaterLabel(LiquidWaterPresence water)
        => water switch
        {
            LiquidWaterPresence.Surface => "líquida na superfície",
            LiquidWaterPresence.Subsurface => "oceano subterrâneo",
            _ => "não detectada",
        };

    private static string? Phrase(string flag)
        => flag switch
        {
            "runaway_greenhouse" => "Estufa descontrolada (profundidade óptica alta).",
            "temperate_surface" => "Temperatura de superfície na faixa de água líquida.",
            "too_hot" => "Superfície quente demais para água líquida estável.",
            "too_cold" => "Superfície fria demais para água líquida estável.",
            "thin_atmosphere" => "Atmosfera tênue; pouca proteção e pouco efeito estufa.",
            "dense_atmosphere" => "Atmosfera densa.",
            "high_radiation" => "Radiação ionizante elevada (escudo magnético fraco ou órbita interna).",
            "shielded_radiation" => "Radiação próxima à da Terra (escudo e/ou distância).",
            "light_gases_escape" => "H₂/He escapam (normal em planetas terrestres).",
            "lost_light_gases" => "H₂/He escapam (normal em planetas terrestres).",
            "retains_heavy_gases" => "Gases pesados (N₂/O₂/CO₂) são retidos.",
            "surface_liquid_water" => "Água líquida superficial.",
            "subsurface_ocean" => "Oceano líquido sob gelo (aquecimento de maré).",
            "no_liquid_water" => "Sem água líquida neste modelo.",
            "strong_tidal_heating" => "Aquecimento de maré intenso.",
            "polar_ice" => "Calotas polares / gelo polar no modelo de zonas.",
            "o2_ch4_disequilibrium" => "Desequilíbrio O₂+CH₄ (biosignature química).",
            "ozone_uv_shield" => "Camada de O₃ como escudo UV secundário.",
            "high_bhi" => "BHI acima do limiar de potencial biosfera.",
            "reference_habitable" => "Corpo de referência habitável do Sistema Solar.",
            "stellar_body" => "Corpo estelar — modelo de superfície não se aplica.",
            _ => null,
        };

    private static string Signed(double value, string format)
    {
        if (!double.IsFinite(value))
        {
            return DisplayFormat.Absent;
        }

        var text = value.ToString(format, Numbers);
        return value > 0.0 ? "+" + text : text;
    }
}
