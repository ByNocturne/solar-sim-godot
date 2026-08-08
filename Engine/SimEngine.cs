using SolarSim.Engine.Core;
using SolarSim.Engine.Data;
using SolarSim.Engine.Models;

namespace SolarSim.Engine;

/// <summary>
/// Fachada do motor: junta o relógio, os dados e o propagador, e publica o estado
/// resultante. Nada aqui conhece a camada gráfica.
/// </summary>
public sealed class SimEngine
{
    private readonly IReadOnlyList<CelestialBodyData> _bodies;
    private readonly Dictionary<string, CelestialBodyData> _byId;

    // Reaproveitado a cada quadro: o caminho de propagação roda a 60 Hz e não deve
    // gerar lixo para o coletor.
    private readonly BodyState[] _states;

    public SimEngine(IBodyRepository repository, TimeEngine? time = null)
    {
        ArgumentNullException.ThrowIfNull(repository);

        Time = time ?? new TimeEngine();
        _bodies = repository.LoadBodies();
        _byId = _bodies.ToDictionary(body => body.Id, StringComparer.Ordinal);
        _states = new BodyState[_bodies.Count];

        Publish();
    }

    public TimeEngine Time { get; }

    public IReadOnlyList<CelestialBodyData> Bodies => _bodies;

    public event Action<SystemStateSnapshot>? SystemUpdated;

    public void Advance(double realSecondsElapsed)
    {
        Time.Advance(realSecondsElapsed);
        Publish();
    }

    /// <summary>
    /// Posição de um corpo em um instante arbitrário, sem mexer no relógio. É o que
    /// permite desenhar órbitas e consultar datas futuras.
    /// </summary>
    public Vector3D PositionAt(string bodyId, double julianDate)
    {
        if (!_byId.TryGetValue(bodyId, out var body))
        {
            throw new KeyNotFoundException($"Corpo desconhecido: '{bodyId}'.");
        }

        return PositionOf(body, julianDate - AstroConstants.J2000);
    }

    private void Publish()
    {
        if (SystemUpdated is null)
        {
            return;
        }

        var daysSinceEpoch = Time.DaysSinceEpoch;

        for (var index = 0; index < _bodies.Count; index++)
        {
            var body = _bodies[index];
            _states[index] = new BodyState(body.Id, PositionOf(body, daysSinceEpoch));
        }

        SystemUpdated.Invoke(new SystemStateSnapshot(Time.JulianDate, _states));
    }

    /// <summary>
    /// No M1 a lista é plana: a raiz fica na origem e todo o resto orbita diretamente
    /// em torno dela. A composição hierárquica recursiva entra no M3.
    /// </summary>
    private Vector3D PositionOf(CelestialBodyData body, double daysSinceEpoch)
    {
        if (body.Elements is not { } elements || body.ParentId is null)
        {
            return Vector3D.Zero;
        }

        var parentMu = _byId.TryGetValue(body.ParentId, out var parent)
            ? parent.MuKm3S2
            : throw new KeyNotFoundException(
                $"Corpo '{body.Id}' referencia o pai inexistente '{body.ParentId}'.");

        return KeplerPropagator.PositionAt(elements, parentMu, daysSinceEpoch);
    }
}
