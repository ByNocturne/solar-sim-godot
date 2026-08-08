using SolarSim.Engine.Models;

namespace SolarSim.Engine.Core;

/// <summary>
/// Resolve a posição de um corpo a partir dos seus elementos orbitais e do tempo.
/// Função pura: a mesma entrada sempre produz a mesma saída.
/// </summary>
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
    {
        var eccentricity = elements.Eccentricity;
        var meanAnomaly = MeanAnomalyAt(elements, parentMuKm3S2, daysSinceEpoch);
        var eccentricAnomaly = SolveEccentricAnomaly(meanAnomaly, eccentricity);
        var trueAnomaly = TrueAnomalyFrom(eccentricAnomaly, eccentricity);
        var radius = elements.SemiMajorAxisKm * (1.0 - eccentricity * Math.Cos(eccentricAnomaly));

        // Semi-latus rectum e momento angular específico.
        var semiLatusRectum = elements.SemiMajorAxisKm * (1.0 - eccentricity * eccentricity);
        var angularMomentum = Math.Sqrt(parentMuKm3S2 * semiLatusRectum);
        var muOverH = parentMuKm3S2 / angularMomentum;

        var cosTrue = Math.Cos(trueAnomaly);
        var sinTrue = Math.Sin(trueAnomaly);

        var position = PerifocalToGlobal(radius * cosTrue, radius * sinTrue, elements);
        var velocity = PerifocalToGlobal(
            -muOverH * sinTrue,
            muOverH * (eccentricity + cosTrue),
            elements);

        return new StateVector(position, velocity);
    }

    /// <summary>Movimento médio em radianos por segundo.</summary>
    public static double MeanMotionRadPerSecond(double semiMajorAxisKm, double parentMuKm3S2)
        => Math.Sqrt(parentMuKm3S2 / (semiMajorAxisKm * semiMajorAxisKm * semiMajorAxisKm));

    public static double OrbitalPeriodDays(double semiMajorAxisKm, double parentMuKm3S2)
        => AstroConstants.TwoPi
            / MeanMotionRadPerSecond(semiMajorAxisKm, parentMuKm3S2)
            / AstroConstants.SecondsPerDay;

    public static double MeanAnomalyAt(
        in OrbitalElements elements,
        double parentMuKm3S2,
        double daysSinceEpoch)
    {
        var meanMotion = MeanMotionRadPerSecond(elements.SemiMajorAxisKm, parentMuKm3S2);
        var elapsedSeconds = daysSinceEpoch * AstroConstants.SecondsPerDay;

        return AstroConstants.NormalizeAngle(
            elements.MeanAnomalyAtEpochRad + meanMotion * elapsedSeconds);
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

    /// <summary>
    /// Anomalia verdadeira por atan2. A forma equivalente com tan(nu/2) perde precisão
    /// perto de E = pi, ou seja, em toda a metade da órbita próxima do apoapsis.
    /// </summary>
    public static double TrueAnomalyFrom(double eccentricAnomaly, double eccentricity)
        => Math.Atan2(
            Math.Sqrt(1.0 - eccentricity * eccentricity) * Math.Sin(eccentricAnomaly),
            Math.Cos(eccentricAnomaly) - eccentricity);

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
