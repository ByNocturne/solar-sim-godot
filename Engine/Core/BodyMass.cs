namespace SolarSim.Engine.Core;

/// <summary>
/// Massa a partir da geometria, para o caso em que o GM não foi medido.
/// </summary>
/// <remarks>
/// Só um punhado de corpos menores teve a massa determinada por sonda ou por satélite, e
/// para os demais o que existe publicado é o diâmetro e uma densidade típica da classe.
/// Derivar o mu daí é uma estimativa, e vale a pena porque sem mu o corpo não tem esfera
/// de influência nenhuma — nem no relatório, nem na emenda de cônicas.
/// </remarks>
public static class BodyMass
{
    /// <summary>Constante gravitacional em km³/(kg·s²), CODATA 2018.</summary>
    public const double GravitationalConstantKm3PerKgS2 = 6.674_30e-20;

    /// <summary>Uma grama por centímetro cúbico, em quilogramas por quilômetro cúbico.</summary>
    private const double GramPerCm3InKgPerKm3 = 1.0e12;

    /// <summary>
    /// GM de uma esfera homogênea, em km³/s², a partir da densidade em g/cm³ e do raio
    /// médio em km.
    /// </summary>
    public static double MuFromDensity(double densityGCm3, double radiusKm)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(densityGCm3);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radiusKm);

        var volumeKm3 = 4.0 / 3.0 * Math.PI * radiusKm * radiusKm * radiusKm;

        return GravitationalConstantKm3PerKgS2
            * densityGCm3
            * GramPerCm3InKgPerKm3
            * volumeKm3;
    }

    /// <summary>
    /// Densidade média em g/cm³ a partir do GM e do raio médio. Zero quando falta um dos
    /// dois, que é o caso de uma sonda.
    /// </summary>
    /// <remarks>
    /// O raio tem de ser o médio, e não o equatorial: é o raio da esfera de mesmo volume
    /// que a fórmula supõe, e num corpo achatado como Saturno os dois diferem 3,5%, o que
    /// vira 11% na densidade.
    /// </remarks>
    public static double DensityFromMu(double muKm3S2, double radiusKm)
    {
        if (muKm3S2 <= 0.0 || radiusKm <= 0.0)
        {
            return 0.0;
        }

        var volumeKm3 = 4.0 / 3.0 * Math.PI * radiusKm * radiusKm * radiusKm;

        return muKm3S2
            / GravitationalConstantKm3PerKgS2
            / volumeKm3
            / GramPerCm3InKgPerKm3;
    }
}
