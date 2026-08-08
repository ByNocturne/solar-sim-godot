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
    private readonly BodyHierarchy _hierarchy;

    // Parâmetro gravitacional efetivo de cada órbita, indexado como a hierarquia.
    private readonly double[] _mu;

    // Reaproveitados a cada quadro: o caminho de propagação roda a 60 Hz e não deve
    // gerar lixo para o coletor.
    private readonly StateVector[] _globals;
    private readonly BodyState[] _states;

    public SimEngine(IBodyRepository repository, TimeEngine? time = null)
    {
        ArgumentNullException.ThrowIfNull(repository);

        Time = time ?? new TimeEngine();
        _hierarchy = BodyHierarchy.Create(repository.LoadBodies());

        _mu = new double[_hierarchy.Count];
        for (var index = 0; index < _hierarchy.Count; index++)
        {
            _mu[index] = EffectiveMu(index);
        }

        _globals = new StateVector[_hierarchy.Count];
        _states = new BodyState[_hierarchy.Count];

        Publish();
    }

    public TimeEngine Time { get; }

    /// <summary>Corpos em ordem de avaliação: o pai sempre antes do filho.</summary>
    public IReadOnlyList<CelestialBodyData> Bodies => _hierarchy.InEvaluationOrder;

    /// <summary>
    /// Corpo na origem do sistema. É a referência das distâncias heliocêntricas, e o
    /// nome dele é o que rotula essa distância na tela.
    /// </summary>
    public CelestialBodyData Root => _hierarchy.Root;

    /// <summary>Dados estáticos de um corpo.</summary>
    /// <exception cref="KeyNotFoundException">Se o identificador não existir.</exception>
    public CelestialBodyData BodyOf(string bodyId) => _hierarchy.Get(bodyId);

    public bool Contains(string bodyId) => _hierarchy.Contains(bodyId);

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
        => StateAt(bodyId, julianDate).PositionKm;

    /// <summary>
    /// Posição e velocidade no referencial global, com a cadeia de pais já composta:
    /// a Lua carrega o movimento da Terra em torno do Sol.
    /// </summary>
    public StateVector StateAt(string bodyId, double julianDate)
        => GlobalStateOf(_hierarchy.IndexOf(bodyId), julianDate - AstroConstants.J2000);

    /// <summary>
    /// Posição e velocidade relativas ao corpo pai, que é o referencial em que os
    /// elementos orbitais são definidos. Para a raiz, o estado é nulo.
    /// </summary>
    public StateVector LocalStateAt(string bodyId, double julianDate)
        => LocalStateOf(_hierarchy.IndexOf(bodyId), julianDate - AstroConstants.J2000);

    /// <summary>Elementos orbitais de um corpo, ou nulo se ele for a raiz.</summary>
    public OrbitalElements? ElementsOf(string bodyId) => _hierarchy.Get(bodyId).Elements;

    /// <summary>
    /// Parâmetro gravitacional que rege a órbita deste corpo, em km³/s². Vale zero para
    /// a raiz.
    /// </summary>
    public double GravitationalParameterOf(string bodyId)
        => _mu[_hierarchy.IndexOf(bodyId)];

    /// <summary>
    /// A equação do movimento relativo de dois corpos usa a soma dos dois parâmetros
    /// gravitacionais, não só o do corpo central. Para um planeta em torno do Sol a
    /// diferença é imperceptível; para a Lua em torno da Terra vale 1,2%, o suficiente
    /// para deslocar o mês sideral em quatro horas.
    /// </summary>
    private double EffectiveMu(int index)
        => _hierarchy.ParentOf(index) is { } parent
            ? parent.MuKm3S2 + _hierarchy[index].MuKm3S2
            : 0.0;

    /// <summary>
    /// Uma passada só, na ordem de avaliação: quando um corpo é processado, o estado
    /// global do pai dele já está pronto na mesma tabela.
    /// </summary>
    private void Publish()
    {
        if (SystemUpdated is null)
        {
            return;
        }

        var daysSinceEpoch = Time.DaysSinceEpoch;

        for (var index = 0; index < _hierarchy.Count; index++)
        {
            var parentIndex = _hierarchy.ParentIndices[index];
            var local = LocalStateOf(index, daysSinceEpoch);

            _globals[index] = parentIndex < 0 ? local : _globals[parentIndex] + local;

            _states[index] = new BodyState(
                _hierarchy[index].Id, _globals[index].PositionKm, local.PositionKm);
        }

        SystemUpdated.Invoke(new SystemStateSnapshot(Time.JulianDate, _states));
    }

    /// <summary>
    /// P_global(A) = P_global(Pai(A)) + P_local(A). Fora do laço de quadro a composição
    /// é recursiva, porque a profundidade da árvore é pequena e a clareza compensa.
    /// </summary>
    private StateVector GlobalStateOf(int index, double daysSinceEpoch)
    {
        var local = LocalStateOf(index, daysSinceEpoch);
        var parentIndex = _hierarchy.ParentIndices[index];

        return parentIndex < 0
            ? local
            : GlobalStateOf(parentIndex, daysSinceEpoch) + local;
    }

    private StateVector LocalStateOf(int index, double daysSinceEpoch)
        => _hierarchy[index].Elements is { } elements
            ? KeplerPropagator.StateAt(elements, _mu[index], daysSinceEpoch)
            : default;
}
