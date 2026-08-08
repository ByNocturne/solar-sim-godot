using SolarSim.Engine.Models;

namespace SolarSim.Engine.Data;

/// <summary>
/// A árvore de corpos, validada e linearizada em ordem de avaliação: todo pai aparece
/// antes de qualquer filho. É essa ordem que permite compor a posição global em uma
/// única passada, sem recursão e sem estado de quadro anterior.
/// </summary>
/// <remarks>
/// Existe separada do repositório porque a validação vale para qualquer origem de
/// dados: o motor a aplica sobre o que quer que a implementação de
/// <see cref="IBodyRepository"/> devolva.
/// </remarks>
public sealed class BodyHierarchy
{
    private const int Unvisited = 0;
    private const int InProgress = 1;
    private const int Done = 2;

    private readonly Dictionary<string, int> _indexById;

    private BodyHierarchy(
        IReadOnlyList<CelestialBodyData> inEvaluationOrder,
        IReadOnlyList<int> parentIndices,
        Dictionary<string, int> indexById)
    {
        InEvaluationOrder = inEvaluationOrder;
        ParentIndices = parentIndices;
        _indexById = indexById;
    }

    /// <summary>Corpos ordenados de forma que o pai sempre preceda o filho.</summary>
    public IReadOnlyList<CelestialBodyData> InEvaluationOrder { get; }

    /// <summary>
    /// Índice do pai de cada corpo dentro de <see cref="InEvaluationOrder"/>, ou -1 para
    /// a raiz. Índice em vez de identificador para evitar consulta a dicionário no laço
    /// que roda a cada quadro.
    /// </summary>
    public IReadOnlyList<int> ParentIndices { get; }

    public int Count => InEvaluationOrder.Count;

    public CelestialBodyData this[int index] => InEvaluationOrder[index];

    /// <summary>
    /// Valida a lista e devolve a hierarquia. Falha quando há identificador duplicado,
    /// pai inexistente, mais de uma raiz ou ciclo.
    /// </summary>
    public static BodyHierarchy Create(IReadOnlyList<CelestialBodyData> bodies)
    {
        ArgumentNullException.ThrowIfNull(bodies);

        if (bodies.Count == 0)
        {
            throw new SystemDataException("O sistema está vazio: é preciso ao menos a raiz.");
        }

        var byId = IndexById(bodies);
        RequireExistingParents(bodies, byId);
        RequireSingleRoot(bodies);

        var ordered = Linearize(bodies, byId);

        var indexById = new Dictionary<string, int>(ordered.Count, StringComparer.Ordinal);
        for (var index = 0; index < ordered.Count; index++)
        {
            indexById[ordered[index].Id] = index;
        }

        var parentIndices = new int[ordered.Count];
        for (var index = 0; index < ordered.Count; index++)
        {
            parentIndices[index] = ordered[index].ParentId is { } parentId
                ? indexById[parentId]
                : -1;
        }

        return new BodyHierarchy(ordered, parentIndices, indexById);
    }

    public bool Contains(string bodyId) => _indexById.ContainsKey(bodyId);

    /// <summary>Índice do corpo na ordem de avaliação.</summary>
    /// <exception cref="KeyNotFoundException">Se o identificador não existir.</exception>
    public int IndexOf(string bodyId)
        => _indexById.TryGetValue(bodyId, out var index)
            ? index
            : throw new KeyNotFoundException($"Corpo desconhecido: '{bodyId}'.");

    /// <exception cref="KeyNotFoundException">Se o identificador não existir.</exception>
    public CelestialBodyData Get(string bodyId) => InEvaluationOrder[IndexOf(bodyId)];

    /// <summary>Pai de um corpo, ou nulo se ele for a raiz.</summary>
    public CelestialBodyData? ParentOf(int index)
    {
        var parentIndex = ParentIndices[index];
        return parentIndex < 0 ? null : InEvaluationOrder[parentIndex];
    }

    private static Dictionary<string, CelestialBodyData> IndexById(
        IReadOnlyList<CelestialBodyData> bodies)
    {
        var byId = new Dictionary<string, CelestialBodyData>(
            bodies.Count, StringComparer.Ordinal);

        foreach (var body in bodies)
        {
            if (string.IsNullOrWhiteSpace(body.Id))
            {
                throw new SystemDataException(
                    $"Corpo '{body.Name}': o campo 'id' está vazio.");
            }

            if (!byId.TryAdd(body.Id, body))
            {
                throw new SystemDataException(
                    $"Identificador duplicado: '{body.Id}'. Cada corpo precisa de um id único.");
            }
        }

        return byId;
    }

    private static void RequireExistingParents(
        IReadOnlyList<CelestialBodyData> bodies,
        Dictionary<string, CelestialBodyData> byId)
    {
        foreach (var body in bodies)
        {
            if (body.ParentId is { } parentId && !byId.ContainsKey(parentId))
            {
                throw new SystemDataException(
                    $"Corpo '{body.Id}': o campo 'parent' aponta para '{parentId}', "
                        + "que não existe no sistema.");
            }
        }
    }

    private static void RequireSingleRoot(IReadOnlyList<CelestialBodyData> bodies)
    {
        var roots = bodies.Where(body => body.ParentId is null).Select(body => body.Id).ToArray();

        // Zero raízes significa que todo mundo tem pai, o que só é possível com um ciclo.
        // A mensagem de ciclo é mais útil, então esse caso fica para a linearização.
        if (roots.Length > 1)
        {
            throw new SystemDataException(
                $"O sistema tem mais de uma raiz ({string.Join(", ", roots)}). "
                    + "A raiz é o corpo que fica na origem, e só pode haver uma.");
        }
    }

    private static List<CelestialBodyData> Linearize(
        IReadOnlyList<CelestialBodyData> bodies,
        Dictionary<string, CelestialBodyData> byId)
    {
        var ordered = new List<CelestialBodyData>(bodies.Count);
        var state = new Dictionary<string, int>(bodies.Count, StringComparer.Ordinal);
        var path = new List<string>();

        foreach (var body in bodies)
        {
            Visit(body, byId, state, ordered, path);
        }

        return ordered;
    }

    /// <summary>
    /// Busca em profundidade subindo pelos pais: o corpo só entra na lista depois que o
    /// pai dele entrou. O estado intermediário marca quem está na pilha, que é como o
    /// ciclo é detectado e reportado com o caminho inteiro.
    /// </summary>
    private static void Visit(
        CelestialBodyData body,
        Dictionary<string, CelestialBodyData> byId,
        Dictionary<string, int> state,
        List<CelestialBodyData> ordered,
        List<string> path)
    {
        state.TryGetValue(body.Id, out var visitState);

        if (visitState == Done)
        {
            return;
        }

        if (visitState == InProgress)
        {
            var start = path.IndexOf(body.Id);
            var cycle = string.Join(" -> ", path.Skip(start).Append(body.Id));

            throw new SystemDataException(
                $"Ciclo na hierarquia: {cycle}. Subindo pelos pais, todo corpo precisa "
                    + "chegar à raiz.");
        }

        state[body.Id] = InProgress;
        path.Add(body.Id);

        if (body.ParentId is { } parentId)
        {
            Visit(byId[parentId], byId, state, ordered, path);
        }

        path.RemoveAt(path.Count - 1);
        state[body.Id] = Done;
        ordered.Add(body);
    }
}
