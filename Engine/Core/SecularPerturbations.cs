using SolarSim.Engine.Models;

namespace SolarSim.Engine.Core;

/// <summary>
/// As duas perturbações que o motor calcula em vez de ler do arquivo: a precessão
/// relativística do periápside e o efeito do achatamento do corpo central.
/// </summary>
/// <remarks>
/// As duas entram como taxa média, e não como força a cada quadro. É o que mantém o
/// invariante 4: o estado continua sendo função da Data Juliana, e o custo de saltar mil
/// anos é o mesmo de avançar um segundo.
/// <para>
/// O que fica de fora é o terceiro corpo. A precessão do perigeu lunar, de 8,85 anos, é
/// solar e não cabe aqui: pelo achatamento da Terra sozinho a Lua precessaria meio grau
/// por século. Ver o backlog do ROADMAP.
/// </para>
/// </remarks>
public static class SecularPerturbations
{
    /// <summary>
    /// Taxa total de origem física para uma órbita, somando relatividade e achatamento
    /// do corpo pai.
    /// </summary>
    /// <param name="elements">Órbita do corpo, em torno do pai.</param>
    /// <param name="parentMuKm3S2">Parâmetro gravitacional efetivo da órbita.</param>
    /// <param name="parentJ2">Coeficiente de achatamento do pai. Zero desliga o termo.</param>
    /// <param name="parentEquatorialRadiusKm">Raio a que o J₂ do pai se refere.</param>
    public static OrbitalElementRates For(
        in OrbitalElements elements,
        double parentMuKm3S2,
        double parentJ2,
        double parentEquatorialRadiusKm)
    {
        // Só a órbita fechada tem precessão média: na hipérbole o corpo passa uma vez, e
        // uma taxa por volta não significa nada.
        if (!elements.IsClosed || parentMuKm3S2 <= 0.0)
        {
            return OrbitalElementRates.None;
        }

        return new OrbitalElementRates(
            SemiMajorAxisKmPerSecond: 0.0,
            EccentricityPerSecond: 0.0,
            InclinationRadPerSecond: 0.0,
            LongitudeOfAscendingNodeRadPerSecond: OblatenessNodalRateRadPerSecond(
                elements, parentMuKm3S2, parentJ2, parentEquatorialRadiusKm),
            ArgumentOfPeriapsisRadPerSecond:
                RelativisticApsidalRateRadPerSecond(elements, parentMuKm3S2)
                + OblatenessApsidalRateRadPerSecond(
                    elements, parentMuKm3S2, parentJ2, parentEquatorialRadiusKm));
    }

    /// <summary>
    /// Avanço do periápside previsto pela Relatividade Geral, em radianos por segundo.
    /// </summary>
    /// <remarks>
    /// Por volta o periápside avança 6πμ/(c²p); dividido pelo período, sobra 3nμ/(c²p).
    /// Para Mercúrio dá os 43 segundos de arco por século que a mecânica newtoniana não
    /// explicava, e que foram a primeira confirmação da teoria.
    /// </remarks>
    public static double RelativisticApsidalRateRadPerSecond(
        in OrbitalElements elements,
        double parentMuKm3S2)
    {
        if (!elements.IsClosed || parentMuKm3S2 <= 0.0)
        {
            return 0.0;
        }

        var meanMotion = KeplerPropagator.MeanMotionRadPerSecond(
            elements.SemiMajorAxisKm, parentMuKm3S2);

        return 3.0 * meanMotion * parentMuKm3S2
            / (AstroConstants.SpeedOfLightKmS
                * AstroConstants.SpeedOfLightKmS
                * elements.SemiLatusRectumKm);
    }

    /// <summary>
    /// Regressão da linha dos nodos causada pelo achatamento do corpo pai, em radianos
    /// por segundo. Negativa para órbita direta, que é o sentido de quase tudo aqui.
    /// </summary>
    public static double OblatenessNodalRateRadPerSecond(
        in OrbitalElements elements,
        double parentMuKm3S2,
        double parentJ2,
        double parentEquatorialRadiusKm)
    {
        if (OblatenessFactor(
                elements, parentMuKm3S2, parentJ2, parentEquatorialRadiusKm) is not { } factor)
        {
            return 0.0;
        }

        return -1.5 * factor * Math.Cos(elements.InclinationRad);
    }

    /// <summary>
    /// Giro do periápside causado pelo achatamento do corpo pai, em radianos por segundo.
    /// </summary>
    /// <remarks>
    /// O fator 5cos²i − 1 troca de sinal na inclinação crítica de 63,4 graus: abaixo dela
    /// o periápside avança, acima ele recua, e exatamente nela fica parado — que é como
    /// as órbitas Molniya mantêm o apogeu sempre sobre o mesmo hemisfério.
    /// </remarks>
    public static double OblatenessApsidalRateRadPerSecond(
        in OrbitalElements elements,
        double parentMuKm3S2,
        double parentJ2,
        double parentEquatorialRadiusKm)
    {
        if (OblatenessFactor(
                elements, parentMuKm3S2, parentJ2, parentEquatorialRadiusKm) is not { } factor)
        {
            return 0.0;
        }

        var cosInclination = Math.Cos(elements.InclinationRad);

        return 0.75 * factor * (5.0 * cosInclination * cosInclination - 1.0);
    }

    /// <summary>
    /// A parte comum às duas taxas de achatamento: n·J₂·(R/p)². Nulo quando não há
    /// achatamento a considerar, para que quem chama devolva zero sem calcular o resto.
    /// </summary>
    private static double? OblatenessFactor(
        in OrbitalElements elements,
        double parentMuKm3S2,
        double parentJ2,
        double parentEquatorialRadiusKm)
    {
        if (!elements.IsClosed
            || parentMuKm3S2 <= 0.0
            || parentJ2 == 0.0
            || parentEquatorialRadiusKm <= 0.0)
        {
            return null;
        }

        var radiusOverParameter = parentEquatorialRadiusKm / elements.SemiLatusRectumKm;

        var meanMotion = KeplerPropagator.MeanMotionRadPerSecond(
            elements.SemiMajorAxisKm, parentMuKm3S2);

        return meanMotion * parentJ2 * radiusOverParameter * radiusOverParameter;
    }
}
