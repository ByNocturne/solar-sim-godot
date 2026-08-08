namespace SolarSim.Engine.Core;

/// <summary>
/// Escape atmosférico de Jeans: compara velocidade de escape com velocidade térmica.
/// </summary>
/// <remarks>
/// Regra clássica de ensino: se v_esc / v_th ≳ 6 a espécie é retida; se ≲ 5, perdida.
/// Massas moleculares em kg; temperatura em K; μ e R em unidades internas (km³/s², km).
/// </remarks>
public static class AtmosphericEscape
{
    public const double BoltzmannJPerK = 1.380_649e-23;

    public const double AtomicMassUnitKg = 1.660_539_066_60e-27;

    /// <summary>Limiar de retenção: razão v_esc / v_th.</summary>
    public const double RetentionRatioThreshold = 6.0;

    public static double EscapeSpeedKmS(double muKm3S2, double radiusKm)
    {
        if (muKm3S2 <= 0.0 || radiusKm <= 0.0)
        {
            return 0.0;
        }

        return Math.Sqrt(2.0 * muKm3S2 / radiusKm);
    }

    public static double ThermalSpeedKmS(double temperatureK, double molarMassAMU)
    {
        if (temperatureK <= 0.0 || molarMassAMU <= 0.0)
        {
            return double.PositiveInfinity;
        }

        var massKg = molarMassAMU * AtomicMassUnitKg;
        var metersPerSecond = Math.Sqrt(3.0 * BoltzmannJPerK * temperatureK / massKg);
        return metersPerSecond / 1000.0;
    }

    public static bool IsRetained(double escapeSpeedKmS, double thermalSpeedKmS)
        => thermalSpeedKmS > 0.0
            && double.IsFinite(thermalSpeedKmS)
            && escapeSpeedKmS / thermalSpeedKmS >= RetentionRatioThreshold;

    public static bool RetainsSpecies(
        double muKm3S2,
        double radiusKm,
        double temperatureK,
        double molarMassAMU)
    {
        var vesc = EscapeSpeedKmS(muKm3S2, radiusKm);
        var vth = ThermalSpeedKmS(temperatureK, molarMassAMU);
        return IsRetained(vesc, vth);
    }

    public static class MolarMassAMU
    {
        public const double H2 = 2.016;
        public const double He = 4.003;
        public const double N2 = 28.014;
        public const double O2 = 31.999;
        public const double CO2 = 44.01;
    }
}
