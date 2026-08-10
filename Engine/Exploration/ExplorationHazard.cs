using SolarSim.Engine.Models;

namespace SolarSim.Engine.Exploration;

/// <summary>Risco de superfície derivado do relatório ambiental — o que a carga deve cobrir.</summary>
public readonly record struct ExplorationHazard
{
    public required string BodyId { get; init; }

    public required string Name { get; init; }

    /// <summary>Nível 0–3 de escudo térmico exigido.</summary>
    public int RequiredThermalShield { get; init; }

    /// <summary>Nível 0–3 de escudo de radiação exigido.</summary>
    public int RequiredRadiationShield { get; init; }

    /// <summary>Máximo de horas de EVA seguras neste corpo (mesmo com carga adequada).</summary>
    public double MaxSafeEvaHours { get; init; }

    /// <summary>Propelente mínimo só para a ida (ida+volta = 2×).</summary>
    public double TransitPropellantOneWay { get; init; }

    public IReadOnlyList<string> HazardNotes { get; init; }

    public static ExplorationHazard FromEnvironment(EnvironmentReport report)
    {
        ArgumentNullException.ThrowIfNull(report.ExplanationFlags);

        var notes = new List<string>();
        var thermal = 0;
        var radiation = 0;
        var evaHours = 8.0;
        var transit = 2.0;

        var flags = report.ExplanationFlags;

        if (flags.Contains("too_hot") || flags.Contains("runaway_greenhouse"))
        {
            thermal = Math.Max(thermal, 3);
            notes.Add("superfície quente demais sem proteção térmica");
            evaHours = Math.Min(evaHours, 3.0);
        }
        else if (flags.Contains("too_cold") || report.SurfaceTemperatureK < 220.0)
        {
            thermal = Math.Max(thermal, report.SurfaceTemperatureK < 180.0 ? 3 : 2);
            notes.Add("superfície fria — perda de calor");
            evaHours = Math.Min(evaHours, 5.0);
        }

        if (report.DiurnalAmplitudeK >= 200.0)
        {
            thermal = Math.Max(thermal, 3);
            notes.Add("amplitude diurna extrema");
            evaHours = Math.Min(evaHours, 3.0);
        }
        else if (report.DiurnalAmplitudeK >= 100.0)
        {
            thermal = Math.Max(thermal, 2);
            notes.Add("amplitude diurna muito alta");
            evaHours = Math.Min(evaHours, 4.0);
        }
        else if (report.DiurnalAmplitudeK >= 40.0)
        {
            thermal = Math.Max(thermal, 1);
            notes.Add("amplitude diurna alta");
        }

        if (flags.Contains("high_radiation") || report.RelativeIonizingRadiation > 2.0)
        {
            radiation = Math.Max(radiation, 3);
            notes.Add("radiação ionizante elevada");
            evaHours = Math.Min(evaHours, 3.0);
            transit += 1.0;
        }
        else if (report.RelativeIonizingRadiation > 1.3)
        {
            radiation = Math.Max(radiation, 2);
            notes.Add("radiação acima da referência terrestre");
            evaHours = Math.Min(evaHours, 5.0);
        }
        else if (report.RelativeIonizingRadiation > 1.05)
        {
            radiation = Math.Max(radiation, 1);
        }

        if (flags.Contains("thin_atmosphere") || report.SurfacePressurePa < 1000.0)
        {
            notes.Add("atmosfera rarefeita ou ausente — EVA pressurizado");
            transit += 0.5;
            if (report.SurfacePressurePa < 10.0)
            {
                radiation = Math.Max(radiation, 2);
            notes.Add("vácuo — sem blindagem atmosférica");
            }
        }

        // Pouso/decolagem mais caros em poços de potencial mais profundos.
        transit += Math.Clamp(report.EscapeSpeedKmS / 5.0, 0.2, 3.0);

        if (flags.Contains("dense_atmosphere"))
        {
            thermal = Math.Max(thermal, 2);
            notes.Add("atmosfera densa");
            transit += 1.5;
        }

        if (flags.Contains("stellar_body"))
        {
            thermal = 3;
            radiation = 3;
            evaHours = 0.5;
            transit = 20.0;
            notes.Add("corpo estelar — superfície inviável para EVA");
        }

        if (notes.Count == 0)
        {
            notes.Add("condições próximas da referência habitável");
        }

        return new ExplorationHazard
        {
            BodyId = report.BodyId,
            Name = report.Name,
            RequiredThermalShield = thermal,
            RequiredRadiationShield = radiation,
            MaxSafeEvaHours = Math.Max(0.5, evaHours),
            TransitPropellantOneWay = transit,
            HazardNotes = notes,
        };
    }
}
