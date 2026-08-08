using SolarSim.Engine.Models;

namespace SolarSim.Engine.Core;

/// <summary>
/// Propaga uma órbita cujos elementos andam com o tempo. Continua sendo função pura da
/// data: as taxas são avaliadas em forma fechada no instante pedido, nunca acumuladas de
/// quadro em quadro.
/// </summary>
/// <remarks>
/// Envolve o <see cref="KeplerPropagator"/> em vez de substituí-lo. Sem taxa nenhuma, o
/// caminho é literalmente o de antes, e não apenas equivalente — o que é o que garante
/// que a regressão contra o JPL não se mexa por causa deste arquivo.
/// </remarks>
public static class SecularPropagator
{
    /// <summary>
    /// Abaixo deste |x| a forma fechada do fator do movimento médio perde dígitos por
    /// cancelamento, e a série de Taylor é mais precisa. Uma taxa secular real cai quase
    /// sempre deste lado.
    /// </summary>
    private const double SeriesThreshold = 1e-4;

    /// <summary>
    /// Quanto o semi-eixo maior pode encolher, como fração do valor de época, antes de a
    /// extrapolação ser considerada sem sentido e travada. Existe para que um salto de um
    /// bilhão de anos devolva um número, e não <c>NaN</c> três camadas adiante.
    /// </summary>
    private const double MinimumSemiMajorAxisFraction = 1e-6;

    /// <summary>Posição e velocidade relativas ao pai, com as taxas já aplicadas.</summary>
    public static StateVector StateAt(
        in OrbitalElements epochElements,
        in OrbitalElementRates rates,
        double parentMuKm3S2,
        double daysSinceEpoch)
        => rates.IsZero
            ? KeplerPropagator.StateAt(epochElements, parentMuKm3S2, daysSinceEpoch)

            // Os elementos devolvidos já se referem ao instante pedido, então o que resta
            // é avaliá-los sem deslocamento nenhum no tempo.
            : KeplerPropagator.StateAt(
                ElementsAt(epochElements, rates, parentMuKm3S2, daysSinceEpoch),
                parentMuKm3S2,
                0.0);

    /// <summary>
    /// Os elementos como estão em um instante, referidos a esse mesmo instante: a
    /// anomalia média devolvida é a de agora, não a de J2000.
    /// </summary>
    /// <remarks>
    /// É essa mudança de época que permite entregar o resultado ao propagador de sempre
    /// com deslocamento zero, e é também o que o inspetor consulta para mostrar o
    /// periápside girando.
    /// </remarks>
    public static OrbitalElements ElementsAt(
        in OrbitalElements epochElements,
        in OrbitalElementRates rates,
        double parentMuKm3S2,
        double daysSinceEpoch)
    {
        var seconds = daysSinceEpoch * AstroConstants.SecondsPerDay;

        var semiMajorAxisKm = DriftedSemiMajorAxisKm(
            epochElements.SemiMajorAxisKm, rates.SemiMajorAxisKmPerSecond, seconds);

        var eccentricity = DriftedEccentricity(
            epochElements.Eccentricity + rates.EccentricityPerSecond * seconds,
            epochElements.IsClosed);

        return new OrbitalElements(
            semiMajorAxisKm,
            eccentricity,
            epochElements.InclinationRad + rates.InclinationRadPerSecond * seconds,
            AstroConstants.NormalizeAngle(
                epochElements.LongitudeOfAscendingNodeRad
                    + rates.LongitudeOfAscendingNodeRadPerSecond * seconds),
            AstroConstants.NormalizeAngle(
                epochElements.ArgumentOfPeriapsisRad
                    + rates.ArgumentOfPeriapsisRadPerSecond * seconds),
            MeanAnomalyAt(epochElements, rates, parentMuKm3S2, daysSinceEpoch));
    }

    /// <summary>
    /// Anomalia média em um instante, quando o semi-eixo maior também anda.
    /// </summary>
    /// <remarks>
    /// Com o semi-eixo fixo, a anomalia média é M₀ + n·t. Com ele andando, o movimento
    /// médio deixa de ser constante e o que vale é a integral de n ao longo do trecho —
    /// usar n do instante final daria uma fase errada, e o erro cresceria sem limite.
    /// A integral tem forma fechada para semi-eixo linear no tempo, e é ela que está em
    /// <see cref="MeanMotionIntegralFactor"/>.
    /// </remarks>
    public static double MeanAnomalyAt(
        in OrbitalElements epochElements,
        in OrbitalElementRates rates,
        double parentMuKm3S2,
        double daysSinceEpoch)
    {
        if (rates.SemiMajorAxisKmPerSecond == 0.0)
        {
            return KeplerPropagator.MeanAnomalyAt(
                epochElements, parentMuKm3S2, daysSinceEpoch);
        }

        var seconds = daysSinceEpoch * AstroConstants.SecondsPerDay;
        var epochSemiMajorAxisKm = epochElements.SemiMajorAxisKm;

        var semiMajorAxisKm = DriftedSemiMajorAxisKm(
            epochSemiMajorAxisKm, rates.SemiMajorAxisKmPerSecond, seconds);

        // Derivado do semi-eixo já travado, e não da taxa bruta: assim o fator nunca
        // diverge, mesmo na extrapolação absurda que o travamento existe para conter.
        var fraction = semiMajorAxisKm / epochSemiMajorAxisKm - 1.0;

        var meanMotion = KeplerPropagator.MeanMotionRadPerSecond(
            epochSemiMajorAxisKm, parentMuKm3S2);

        var meanAnomaly = epochElements.MeanAnomalyAtEpochRad
            + meanMotion * seconds * MeanMotionIntegralFactor(fraction);

        return epochElements.IsClosed
            ? AstroConstants.NormalizeAngle(meanAnomaly)
            : meanAnomaly;
    }

    /// <summary>
    /// Quanto a integral do movimento médio difere de n₀·t quando o semi-eixo maior varia
    /// de uma fração x ao longo do trecho. Vale 1 quando o semi-eixo não muda.
    /// </summary>
    private static double MeanMotionIntegralFactor(double fraction)
    {
        if (Math.Abs(fraction) < SeriesThreshold)
        {
            // A forma fechada subtrai dois números quase iguais, e para fração pequena o
            // resultado seria quase só ruído de arredondamento.
            return 1.0 - 0.75 * fraction + 0.625 * fraction * fraction;
        }

        return 2.0 * (1.0 - 1.0 / Math.Sqrt(1.0 + fraction)) / fraction;
    }

    /// <summary>
    /// O semi-eixo maior no instante, sem deixar que ele cruze o zero: mudar de sinal
    /// seria transformar uma elipse em hipérbole por extrapolação, e nenhuma taxa secular
    /// significa isso.
    /// </summary>
    private static double DriftedSemiMajorAxisKm(
        double epochSemiMajorAxisKm,
        double ratePerSecond,
        double seconds)
    {
        var drifted = epochSemiMajorAxisKm + ratePerSecond * seconds;
        var floor = epochSemiMajorAxisKm * MinimumSemiMajorAxisFraction;

        return epochSemiMajorAxisKm > 0.0
            ? Math.Max(drifted, floor)
            : Math.Min(drifted, floor);
    }

    /// <summary>
    /// A excentricidade no instante, presa dentro do que a representação comporta e do
    /// lado do tipo de cônica com que a órbita começou. Sem isso, uma data distante o
    /// bastante levaria a elipse à parábola, que o motor recusa por construção.
    /// </summary>
    private static double DriftedEccentricity(double drifted, bool closed)
        => closed
            ? Math.Clamp(drifted, 0.0, 1.0 - OrbitalElements.MinimumEccentricityGap)
            : Math.Max(drifted, 1.0 + OrbitalElements.MinimumEccentricityGap);
}
