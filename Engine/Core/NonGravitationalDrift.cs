using SolarSim.Engine.Models;

namespace SolarSim.Engine.Core;

/// <summary>
/// Converte parâmetros não gravitacionais em taxas seculares de elementos. Continua
/// sendo <c>f(JD)</c>: a taxa é forma fechada avaliada na época, nunca força por quadro.
/// </summary>
/// <remarks>
/// Yarkovsky entra como <c>da/dt</c> documentado — a literatura publica o número em
/// UA/Myr para NEOs medidos —, e a pressão de radiação entra pelo coeficiente β via
/// efeito Poynting–Robertson. Os dois somam ao mesmo <see cref="OrbitalElementRates"/>
/// que a relatividade e o achatamento já usam.
/// </remarks>
public static class NonGravitationalDrift
{
    /// <summary>
    /// Taxa total de origem não gravitacional. Sem parâmetros, ou em órbita aberta,
    /// devolve zero: hipérbole não tem drift secular médio.
    /// </summary>
    public static OrbitalElementRates For(
        in OrbitalElements elements,
        double parentMuKm3S2,
        in NonGravitationalParameters parameters)
    {
        if (parameters.IsAbsent || !elements.IsClosed || parentMuKm3S2 <= 0.0)
        {
            return OrbitalElementRates.None;
        }

        var da = 0.0;
        var de = 0.0;

        if (parameters.YarkovskyDaAuPerMyr is { } daAuPerMyr)
        {
            da += SemiMajorAxisKmPerSecondFromYarkovsky(daAuPerMyr);
        }

        if (parameters.RadiationPressureBeta is { } beta)
        {
            da += PoyntingRobertsonDaKmPerSecond(elements, parentMuKm3S2, beta);
            de += PoyntingRobertsonDePerSecond(elements, parentMuKm3S2, beta);
        }

        if (da == 0.0 && de == 0.0)
        {
            return OrbitalElementRates.None;
        }

        return new OrbitalElementRates(
            SemiMajorAxisKmPerSecond: da,
            EccentricityPerSecond: de,
            InclinationRadPerSecond: 0.0,
            LongitudeOfAscendingNodeRadPerSecond: 0.0,
            ArgumentOfPeriapsisRadPerSecond: 0.0);
    }

    /// <summary>
    /// Converte o drift Yarkovsky da unidade em que a literatura o publica — unidades
    /// astronômicas por milhão de anos — para a unidade interna, km/s.
    /// </summary>
    public static double SemiMajorAxisKmPerSecondFromYarkovsky(double daAuPerMyr)
        => daAuPerMyr * AstroConstants.AstronomicalUnitKm
            / (AstroConstants.YearsPerMillion * AstroConstants.SecondsPerJulianYear);

    /// <summary>
    /// Drift secular do semi-eixo por Poynting–Robertson, em km/s.
    /// </summary>
    /// <remarks>
    /// <c>da/dt = −(β μ /(c a)) · (2+3e²)/(1−e²)^(3/2)</c>. β é a razão entre a força de
    /// radiação e a gravidade do atrator; para um asteróide típico é desprezível diante
    /// do Yarkovsky, e existe aqui para poeira e para quem declarar o parâmetro.
    /// </remarks>
    public static double PoyntingRobertsonDaKmPerSecond(
        in OrbitalElements elements,
        double parentMuKm3S2,
        double beta)
    {
        if (!elements.IsClosed || parentMuKm3S2 <= 0.0 || beta == 0.0)
        {
            return 0.0;
        }

        var eccentricity = elements.Eccentricity;
        var oneMinusE2 = 1.0 - eccentricity * eccentricity;
        var factor = (2.0 + 3.0 * eccentricity * eccentricity)
            / (oneMinusE2 * Math.Sqrt(oneMinusE2));

        return -beta * parentMuKm3S2
            / (AstroConstants.SpeedOfLightKmS * elements.SemiMajorAxisKm)
            * factor;
    }

    /// <summary>
    /// Drift secular da excentricidade por Poynting–Robertson, em 1/s.
    /// </summary>
    /// <remarks>
    /// <c>de/dt = −(5/2) · (β μ /(c a²)) · e / √(1−e²)</c>.
    /// </remarks>
    public static double PoyntingRobertsonDePerSecond(
        in OrbitalElements elements,
        double parentMuKm3S2,
        double beta)
    {
        if (!elements.IsClosed || parentMuKm3S2 <= 0.0 || beta == 0.0)
        {
            return 0.0;
        }

        var eccentricity = elements.Eccentricity;
        var oneMinusE2 = 1.0 - eccentricity * eccentricity;

        return -2.5 * beta * parentMuKm3S2 * eccentricity
            / (AstroConstants.SpeedOfLightKmS
                * elements.SemiMajorAxisKm
                * elements.SemiMajorAxisKm
                * Math.Sqrt(oneMinusE2));
    }
}
