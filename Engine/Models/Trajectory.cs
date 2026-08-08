namespace SolarSim.Engine.Models;

/// <summary>
/// Um trecho de trajetória: a órbita que um corpo descreve em torno de um pai, a partir
/// de um instante.
/// </summary>
public readonly record struct TrajectoryArc(
    double StartJulianDate,
    string ParentId,
    OrbitalElements Elements);

/// <summary>
/// A trajetória de um corpo dinâmico, como uma sequência de arcos keplerianos emendados.
/// </summary>
/// <remarks>
/// É aqui que o invariante 4 é preservado apesar das cônicas emendadas. A troca de corpo
/// pai depende do caminho percorrido, e portanto não é função da Data Juliana; o que é
/// função da Data Juliana é o estado dentro de cada arco. Guardando os arcos e o
/// instante em que cada um começa, o estado volta a ser determinado pela data, e o
/// save/load volta a ser trivial: salvar a trajetória é salvar esta lista.
/// </remarks>
public sealed class Trajectory
{
    private readonly List<TrajectoryArc> _arcs;

    public Trajectory(TrajectoryArc first) => _arcs = [first];

    public Trajectory(IEnumerable<TrajectoryArc> arcs)
    {
        _arcs = [.. arcs];

        if (_arcs.Count == 0)
        {
            throw new ArgumentException(
                "Uma trajetória precisa de pelo menos um arco.", nameof(arcs));
        }

        for (var index = 1; index < _arcs.Count; index++)
        {
            if (_arcs[index].StartJulianDate < _arcs[index - 1].StartJulianDate)
            {
                throw new ArgumentException(
                    "Os arcos precisam vir em ordem cronológica.", nameof(arcs));
            }
        }
    }

    public IReadOnlyList<TrajectoryArc> Arcs => _arcs;

    /// <summary>O arco mais recente, que é o que a árvore do sistema reflete.</summary>
    public TrajectoryArc Current => _arcs[^1];

    /// <summary>
    /// O arco vigente em um instante. Antes do primeiro arco vale o primeiro: o corpo
    /// não existia, mas a órbita dele estende-se para trás, e é isso que permite
    /// desenhar de onde ele veio.
    /// </summary>
    public TrajectoryArc At(double julianDate)
    {
        for (var index = _arcs.Count - 1; index > 0; index--)
        {
            if (julianDate >= _arcs[index].StartJulianDate)
            {
                return _arcs[index];
            }
        }

        return _arcs[0];
    }

    /// <summary>Emenda um arco novo, descartando o que houvesse a partir dali.</summary>
    public void Append(TrajectoryArc arc)
    {
        RewindTo(arc.StartJulianDate);
        _arcs.Add(arc);
    }

    /// <summary>
    /// Descarta os arcos que começam depois de um instante. É o que acontece quando o
    /// tempo anda para trás: a emenda que ainda não aconteceu deixa de existir, e será
    /// redescoberta se o tempo voltar a passar por ali.
    /// </summary>
    public void RewindTo(double julianDate)
    {
        while (_arcs.Count > 1 && _arcs[^1].StartJulianDate >= julianDate)
        {
            _arcs.RemoveAt(_arcs.Count - 1);
        }
    }
}
