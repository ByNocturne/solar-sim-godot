using SolarSim.Engine.Models;

namespace SolarSim.Engine.Core;

/// <summary>
/// Resolve a posição de um corpo a partir dos seus elementos orbitais e do tempo.
/// Função pura: a mesma entrada sempre produz a mesma saída.
/// </summary>
/// <remarks>
/// Atende elipse e hipérbole. O tipo de cônica é escolhido pela excentricidade em um
/// ponto só, <see cref="TrueAnomalyAt"/>; da anomalia verdadeira em diante a geometria é
/// a mesma para as duas.
/// </remarks>
public static class KeplerPropagator
{
    public const double ConvergenceTolerance = 1e-12;

    private const int MaxIterations = 60;

    /// <summary>
    /// Posição relativa ao corpo pai, em km, para um instante expresso em dias
    /// julianos desde a época J2000.0.
    /// </summary>
    public static Vector3D PositionAt(
        in OrbitalElements elements,
        double parentMuKm3S2,
        double daysSinceEpoch)
        => StateAt(elements, parentMuKm3S2, daysSinceEpoch).PositionKm;

    /// <summary>
    /// Posição e velocidade relativas ao corpo pai. A velocidade sai do mesmo passo de
    /// propagação da posição, então não custa nada além de algumas multiplicações.
    /// </summary>
    public static StateVector StateAt(
        in OrbitalElements elements,
        double parentMuKm3S2,
        double daysSinceEpoch)
        => StateAtTrueAnomaly(
            elements,
            parentMuKm3S2,
            TrueAnomalyAt(elements, parentMuKm3S2, daysSinceEpoch));

    /// <summary>
    /// Posição e velocidade em um ponto da órbita indicado pelo ângulo desde o
    /// periápside, sem passar pelo tempo. É o que permite desenhar um traço aberto, onde
    /// não existe período para amostrar.
    /// </summary>
    public static StateVector StateAtTrueAnomaly(
        in OrbitalElements elements,
        double parentMuKm3S2,
        double trueAnomalyRad)
    {
        var eccentricity = elements.Eccentricity;
        var semiLatusRectum = elements.SemiLatusRectumKm;
        var angularMomentum = Math.Sqrt(parentMuKm3S2 * semiLatusRectum);
        var muOverH = parentMuKm3S2 / angularMomentum;

        var cosTrue = Math.Cos(trueAnomalyRad);
        var sinTrue = Math.Sin(trueAnomalyRad);
        var radius = semiLatusRectum / (1.0 + eccentricity * cosTrue);

        var position = PerifocalToGlobal(radius * cosTrue, radius * sinTrue, elements);
        var velocity = PerifocalToGlobal(
            -muOverH * sinTrue,
            muOverH * (eccentricity + cosTrue),
            elements);

        return new StateVector(position, velocity);
    }

    /// <summary>
    /// Ângulo desde o periápside em um instante. Aqui, e só aqui, o tipo de cônica
    /// importa: a elipse resolve a equação de Kepler em seno, a hipérbole em seno
    /// hiperbólico.
    /// </summary>
    public static double TrueAnomalyAt(
        in OrbitalElements elements,
        double parentMuKm3S2,
        double daysSinceEpoch)
    {
        var eccentricity = elements.Eccentricity;
        var meanAnomaly = MeanAnomalyAt(elements, parentMuKm3S2, daysSinceEpoch);

        return elements.IsClosed
            ? TrueAnomalyFrom(SolveEccentricAnomaly(meanAnomaly, eccentricity), eccentricity)
            : TrueAnomalyFromHyperbolic(
                SolveHyperbolicAnomaly(meanAnomaly, eccentricity), eccentricity);
    }

    /// <summary>
    /// Movimento médio em radianos por segundo. Na hipérbole não há volta a completar,
    /// e o número é apenas o fator que converte tempo em anomalia média.
    /// </summary>
    public static double MeanMotionRadPerSecond(double semiMajorAxisKm, double parentMuKm3S2)
    {
        var magnitude = Math.Abs(semiMajorAxisKm);
        return Math.Sqrt(parentMuKm3S2 / (magnitude * magnitude * magnitude));
    }

    /// <summary>Período orbital em dias. Só existe para órbita fechada.</summary>
    public static double OrbitalPeriodDays(double semiMajorAxisKm, double parentMuKm3S2)
        => AstroConstants.TwoPi
            / MeanMotionRadPerSecond(semiMajorAxisKm, parentMuKm3S2)
            / AstroConstants.SecondsPerDay;

    /// <summary>
    /// Anomalia média em um instante. Normalizada para [0, 2π) na elipse, onde é
    /// cíclica; deixada crescer na hipérbole, onde não é.
    /// </summary>
    public static double MeanAnomalyAt(
        in OrbitalElements elements,
        double parentMuKm3S2,
        double daysSinceEpoch)
    {
        var meanMotion = MeanMotionRadPerSecond(elements.SemiMajorAxisKm, parentMuKm3S2);
        var elapsedSeconds = daysSinceEpoch * AstroConstants.SecondsPerDay;
        var meanAnomaly = elements.MeanAnomalyAtEpochRad + meanMotion * elapsedSeconds;

        return elements.IsClosed ? AstroConstants.NormalizeAngle(meanAnomaly) : meanAnomaly;
    }

    /// <summary>
    /// Resolve M = E - e·sen(E) por Newton-Raphson. A equação é transcendental, então
    /// não há forma fechada para E.
    /// </summary>
    public static double SolveEccentricAnomaly(double meanAnomaly, double eccentricity)
        => SolveEccentricAnomaly(meanAnomaly, eccentricity, out _);

    /// <inheritdoc cref="SolveEccentricAnomaly(double, double)"/>
    /// <param name="iterations">
    /// Quantas iterações foram necessárias. Exposto para que a suíte de testes possa
    /// verificar o critério de convergência em vez de confiar nele.
    /// </param>
    public static double SolveEccentricAnomaly(
        double meanAnomaly,
        double eccentricity,
        out int iterations)
    {
        // Para excentricidade alta, M é um chute inicial ruim e a iteração pode
        // oscilar; pi fica sempre do lado convergente da curva.
        var current = eccentricity > 0.8 ? Math.PI : meanAnomaly;

        for (iterations = 1; iterations <= MaxIterations; iterations++)
        {
            var residual = current - eccentricity * Math.Sin(current) - meanAnomaly;
            var derivative = 1.0 - eccentricity * Math.Cos(current);
            var next = current - residual / derivative;

            if (Math.Abs(next - current) < ConvergenceTolerance)
            {
                return next;
            }

            current = next;
        }

        return current;
    }

    /// <inheritdoc cref="SolveHyperbolicAnomaly(double, double, out int)"/>
    public static double SolveHyperbolicAnomaly(double meanAnomaly, double eccentricity)
        => SolveHyperbolicAnomaly(meanAnomaly, eccentricity, out _);

    /// <summary>
    /// Resolve M = e·senh(H) - H, a equação de Kepler da hipérbole.
    /// </summary>
    /// <remarks>
    /// A função é crescente e convexa para H positivo, e ímpar em torno da origem, o que
    /// faz Newton convergir de qualquer chute. O que o chute decide é a velocidade, e a
    /// diferença é grande: ver <see cref="InitialHyperbolicGuess"/>.
    /// </remarks>
    /// <param name="iterations">
    /// Quantas iterações foram necessárias, pelo mesmo motivo do caso elíptico.
    /// </param>
    public static double SolveHyperbolicAnomaly(
        double meanAnomaly,
        double eccentricity,
        out int iterations)
    {
        var current = InitialHyperbolicGuess(meanAnomaly, eccentricity);

        for (iterations = 1; iterations <= MaxIterations; iterations++)
        {
            var residual = eccentricity * Math.Sinh(current) - current - meanAnomaly;
            var derivative = eccentricity * Math.Cosh(current) - 1.0;
            var next = current - residual / derivative;

            // Tolerância relativa: H não é um ângulo e não tem escala natural, então
            // um limite absoluto seria apertado demais perto da origem e frouxo longe.
            if (Math.Abs(next - current) < ConvergenceTolerance * (1.0 + Math.Abs(next)))
            {
                return next;
            }

            current = next;
        }

        return current;
    }

    /// <summary>
    /// Chute inicial para a equação de Kepler hiperbólica, por dois regimes.
    /// </summary>
    /// <remarks>
    /// Perto do periápside, senh(H) vale H + H³/6 e a equação vira uma cúbica que Cardano
    /// resolve em forma fechada. Esse ramo é o que importa: quando a excentricidade se
    /// aproxima de 1, a curva fica quase plana na origem e Newton partindo de qualquer
    /// outro lugar rasteja — foram onze iterações contra as quatro que o chute cúbico
    /// custa. Longe do periápside o termo linear é desprezível diante de e·senh(H), e
    /// sobra asenh(M/e).
    /// </remarks>
    private static double InitialHyperbolicGuess(double meanAnomaly, double eccentricity)
    {
        var linear = 6.0 * (eccentricity - 1.0) / eccentricity;
        var constant = -6.0 * meanAnomaly / eccentricity;

        var discriminant = Math.Sqrt(
            constant * constant / 4.0 + linear * linear * linear / 27.0);

        var cubic = Math.Cbrt(-constant / 2.0 + discriminant)
            + Math.Cbrt(-constant / 2.0 - discriminant);

        return Math.Abs(cubic) <= 2.0 ? cubic : Math.Asinh(meanAnomaly / eccentricity);
    }

    /// <summary>
    /// Anomalia verdadeira por atan2. A forma equivalente com tan(nu/2) perde precisão
    /// perto de E = pi, ou seja, em toda a metade da órbita próxima do apoapsis.
    /// </summary>
    public static double TrueAnomalyFrom(double eccentricAnomaly, double eccentricity)
        => Math.Atan2(
            Math.Sqrt(1.0 - eccentricity * eccentricity) * Math.Sin(eccentricAnomaly),
            Math.Cos(eccentricAnomaly) - eccentricity);

    /// <summary>Anomalia verdadeira a partir da anomalia hiperbólica.</summary>
    public static double TrueAnomalyFromHyperbolic(
        double hyperbolicAnomaly,
        double eccentricity)
        => Math.Atan2(
            Math.Sqrt(eccentricity * eccentricity - 1.0) * Math.Sinh(hyperbolicAnomaly),
            eccentricity - Math.Cosh(hyperbolicAnomaly));

    /// <summary>Caminho inverso: da anomalia verdadeira para a excêntrica.</summary>
    public static double EccentricAnomalyFrom(double trueAnomalyRad, double eccentricity)
        => Math.Atan2(
            Math.Sqrt(1.0 - eccentricity * eccentricity) * Math.Sin(trueAnomalyRad),
            eccentricity + Math.Cos(trueAnomalyRad));

    /// <summary>Caminho inverso: da anomalia verdadeira para a hiperbólica.</summary>
    /// <remarks>
    /// Por asenh do seno hiperbólico, e não por atanh da tangente de meia anomalia: a
    /// segunda forma empilha duas funções que estouram perto da assíntota, enquanto
    /// asenh se comporta bem em toda a faixa, inclusive para H acima de dez.
    /// </remarks>
    public static double HyperbolicAnomalyFrom(double trueAnomalyRad, double eccentricity)
    {
        var cosTrue = Math.Cos(trueAnomalyRad);
        var conicFactor = 1.0 + eccentricity * cosTrue;

        return Math.Asinh(
            Math.Sqrt(eccentricity * eccentricity - 1.0)
                * Math.Sin(trueAnomalyRad)
                / conicFactor);
    }

    /// <summary>
    /// Anomalia média a partir da verdadeira, pela equação de Kepler do tipo de cônica
    /// correspondente. É o passo que fecha a conversão de estado para elementos.
    /// </summary>
    public static double MeanAnomalyFromTrue(double trueAnomalyRad, double eccentricity)
    {
        if (eccentricity < 1.0)
        {
            var eccentricAnomaly = EccentricAnomalyFrom(trueAnomalyRad, eccentricity);
            return eccentricAnomaly - eccentricity * Math.Sin(eccentricAnomaly);
        }

        var hyperbolicAnomaly = HyperbolicAnomalyFrom(trueAnomalyRad, eccentricity);
        return eccentricity * Math.Sinh(hyperbolicAnomaly) - hyperbolicAnomaly;
    }

    /// <summary>
    /// Aplica Rz(-Omega)·Rx(-i)·Rz(-omega) a um vetor qualquer do plano orbital, levando
    /// das coordenadas perifocais para o referencial global. Serve tanto para posição
    /// quanto para velocidade, que é o motivo de receber as componentes soltas em vez de
    /// raio e anomalia.
    /// </summary>
    private static Vector3D PerifocalToGlobal(
        double xPerifocal,
        double yPerifocal,
        in OrbitalElements elements)
    {
        var cosArgument = Math.Cos(elements.ArgumentOfPeriapsisRad);
        var sinArgument = Math.Sin(elements.ArgumentOfPeriapsisRad);

        var cosNode = Math.Cos(elements.LongitudeOfAscendingNodeRad);
        var sinNode = Math.Sin(elements.LongitudeOfAscendingNodeRad);

        var cosInclination = Math.Cos(elements.InclinationRad);
        var sinInclination = Math.Sin(elements.InclinationRad);

        return new Vector3D(
            xPerifocal * (cosNode * cosArgument - sinNode * sinArgument * cosInclination)
                - yPerifocal * (cosNode * sinArgument + sinNode * cosArgument * cosInclination),
            xPerifocal * (sinNode * cosArgument + cosNode * sinArgument * cosInclination)
                + yPerifocal * (cosNode * cosArgument * cosInclination - sinNode * sinArgument),
            xPerifocal * (sinArgument * sinInclination)
                + yPerifocal * (cosArgument * sinInclination));
    }
}
