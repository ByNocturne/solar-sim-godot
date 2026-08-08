namespace SolarSim.Engine.Core;

/// <summary>
/// Dissipação de energia por força de maré em satélites excêntricos.
/// </summary>
/// <remarks>
/// Forma clássica (Peale):
/// <c>P = (21/2) · (k₂/Q) · (μ_pai² / R_pai⁰) · …</c> —
/// aqui usamos
/// <c>P = (21/2) · (k₂/Q) · n · e² · (μ_pai) · (R/a)⁵</c>
/// com n = √(μ_pai / a³), o que é proporcional a G M_pai² R⁵ e² / a⁶.
/// </remarks>
public static class TidalHeatingCalculator
{
    public static double HeatingWatts(
        double parentMuKm3S2,
        double satelliteRadiusKm,
        double semiMajorAxisKm,
        double eccentricity,
        double loveNumberK2,
        double qualityFactor)
    {
        if (parentMuKm3S2 <= 0.0
            || satelliteRadiusKm <= 0.0
            || semiMajorAxisKm <= 0.0
            || qualityFactor <= 0.0
            || loveNumberK2 <= 0.0
            || eccentricity <= 0.0)
        {
            return 0.0;
        }

        var a = Math.Abs(semiMajorAxisKm);
        var n = Math.Sqrt(parentMuKm3S2 / (a * a * a));
        var ratio = satelliteRadiusKm / a;
        var ratio5 = ratio * ratio * ratio * ratio * ratio;

        // μ em km³/s² → potencia em unidades de 10¹⁵ W se usarmos km; convertemos:
        // energia mecânica: fator 1e9 (m³ vs km³) na massa efetiva.
        // P_SI = (21/2)*(k2/Q)*n*e²*(GM)*ρ_geom com GM em m³/s² = mu_km * 1e9.
        var muM3S2 = parentMuKm3S2 * 1.0e9;
        var nSi = n; // 1/s, independente da unidade de distância na √(μ/a³) se μ e a casam
        // Recalcular n em SI: a_m = a*1000, μ_m = μ*1e9 → n = sqrt(μ_m/a_m³) = sqrt(μ_km/a_km³)
        var power =
            (21.0 / 2.0)
            * (loveNumberK2 / qualityFactor)
            * nSi
            * eccentricity
            * eccentricity
            * muM3S2
            * ratio5;

        // ratio usou km/km; muM3S2 é m³/s² — inconsistente dimensionalmente com ratio em km.
        // Forma correta: (R/a)^5 adimensional; GM em m³/s²; n em 1/s → Watts.
        return power;
    }

    /// <summary>
    /// Limiar típico para oceano subterrâneo em lua gelada (W): Europa ~10¹² W.
    /// </summary>
    public const double SubsurfaceOceanHeatingThresholdWatts = 1.0e11;
}
