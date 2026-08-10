using SolarSim.Engine.Core;
using SolarSim.Engine.Models;

namespace SolarSim.Engine;

/// <summary>
/// Monta previews e varreduras de transferência a partir do estado do motor, sem
/// alterá-lo. É a peça de consulta do M19: Lambert puro mais as velocidades dos
/// planetas no instante.
/// </summary>
public static class TransferPlanner
{
    /// <summary>
    /// Preview de transferência entre dois corpos que orbitam o mesmo pai. Nulo quando
    /// a geometria não admite solução no tempo pedido.
    /// </summary>
    public static TransferPreview? Preview(
        SimEngine sim,
        string originBodyId,
        string destinationBodyId,
        double departureJulianDate,
        double timeOfFlightDays,
        bool shortWay = true)
    {
        ArgumentNullException.ThrowIfNull(sim);

        if (timeOfFlightDays <= 0.0)
        {
            return null;
        }

        var origin = sim.BodyOf(originBodyId);
        var destination = sim.BodyOf(destinationBodyId);

        if (origin.ParentId is not { } centralId
            || destination.ParentId != centralId)
        {
            return null;
        }

        var mu = sim.BodyOf(centralId).MuKm3S2;
        var arrivalJulianDate = departureJulianDate + timeOfFlightDays;

        var r1 = RelativePosition(sim, originBodyId, centralId, departureJulianDate);
        var r2 = RelativePosition(sim, destinationBodyId, centralId, arrivalJulianDate);
        var vOrigin = RelativeVelocity(sim, originBodyId, centralId, departureJulianDate);
        var vDestination = RelativeVelocity(
            sim, destinationBodyId, centralId, arrivalJulianDate);

        var solution = LambertSolver.TrySolve(
            r1,
            r2,
            timeOfFlightDays * AstroConstants.SecondsPerDay,
            mu,
            shortWay);

        if (solution is not { } lambert)
        {
            return null;
        }

        return new TransferPreview
        {
            OriginBodyId = originBodyId,
            DestinationBodyId = destinationBodyId,
            CentralBodyId = centralId,
            DepartureJulianDate = departureJulianDate,
            TimeOfFlightDays = timeOfFlightDays,
            DepartureDeltaVKmS = (lambert.DepartureVelocityKmS - vOrigin).Magnitude,
            ArrivalDeltaVKmS = (lambert.ArrivalVelocityKmS - vDestination).Magnitude,
            DepartureVelocityKmS = lambert.DepartureVelocityKmS,
            ArrivalVelocityKmS = lambert.ArrivalVelocityKmS,
            ShortWay = shortWay,
        };
    }

    /// <summary>
    /// O melhor dos dois arcos (curto e longo) para a mesma partida e tempo de voo.
    /// </summary>
    public static TransferPreview? BestPreview(
        SimEngine sim,
        string originBodyId,
        string destinationBodyId,
        double departureJulianDate,
        double timeOfFlightDays)
    {
        var shortWay = Preview(
            sim, originBodyId, destinationBodyId, departureJulianDate, timeOfFlightDays,
            shortWay: true);
        var longWay = Preview(
            sim, originBodyId, destinationBodyId, departureJulianDate, timeOfFlightDays,
            shortWay: false);

        if (shortWay is not { IsValid: true })
        {
            return longWay is { IsValid: true } ? longWay : null;
        }

        if (longWay is not { IsValid: true })
        {
            return shortWay;
        }

        return longWay.Value.TotalDeltaVKmS < shortWay.Value.TotalDeltaVKmS
            ? longWay
            : shortWay;
    }

    /// <summary>
    /// Varre uma grade de datas de partida × tempos de voo e devolve as células com
    /// solução, ordenadas pelo Δv total crescente.
    /// </summary>
    public static IReadOnlyList<TransferWindowSample> ScanWindows(
        SimEngine sim,
        string originBodyId,
        string destinationBodyId,
        double departureJdStart,
        double departureJdEnd,
        double departureStepDays,
        double timeOfFlightDaysMin,
        double timeOfFlightDaysMax,
        double timeOfFlightStepDays)
    {
        ArgumentNullException.ThrowIfNull(sim);

        if (departureStepDays <= 0.0 || timeOfFlightStepDays <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(departureStepDays),
                "O passo da grade precisa ser positivo.");
        }

        var samples = new List<TransferWindowSample>();

        for (var jd = departureJdStart; jd <= departureJdEnd + 1e-9; jd += departureStepDays)
        {
            for (var tof = timeOfFlightDaysMin;
                 tof <= timeOfFlightDaysMax + 1e-9;
                 tof += timeOfFlightStepDays)
            {
                if (BestPreview(sim, originBodyId, destinationBodyId, jd, tof) is { } best)
                {
                    samples.Add(new TransferWindowSample(jd, tof, best.TotalDeltaVKmS));
                }
            }
        }

        samples.Sort((left, right) => left.TotalDeltaVKmS.CompareTo(right.TotalDeltaVKmS));

        return samples;
    }

    private static Vector3D RelativePosition(
        SimEngine sim, string bodyId, string centralId, double julianDate)
        => sim.StateAt(bodyId, julianDate).PositionKm
            - sim.StateAt(centralId, julianDate).PositionKm;

    private static Vector3D RelativeVelocity(
        SimEngine sim, string bodyId, string centralId, double julianDate)
        => sim.StateAt(bodyId, julianDate).VelocityKmS
            - sim.StateAt(centralId, julianDate).VelocityKmS;
}
