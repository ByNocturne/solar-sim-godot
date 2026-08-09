using SolarSim.Engine.Models;

namespace SolarSim.Bridge;

/// <summary>
/// Que classes de corpo menor a vista mostra. É o estado por trás dos filtros da árvore.
/// </summary>
/// <remarks>
/// O filtro é por classe, e não por corpo, e o arquivo de dados não tem um campo dizendo
/// quem começa escondido. Visibilidade é decisão de quem olha, e guardá-la no JSON seria
/// pôr estado de interface dentro da fonte da verdade da física — que é justamente onde
/// ela envelheceria sem ninguém notar. O que o arquivo declara é a que família o corpo
/// pertence; o que se faz com isso é da vista.
/// </remarks>
public sealed class BodyFilter
{
    private readonly HashSet<BodyKind> _hidden = [];

    /// <summary>
    /// As classes filtráveis presentes nos dados, na ordem em que
    /// <see cref="BodyKinds.Minor"/> as declara. Um catálogo sem cometas não oferece o
    /// botão de cometas.
    /// </summary>
    public IReadOnlyList<BodyKind> Available { get; }

    public BodyFilter(IReadOnlyList<CelestialBodyData> bodies)
    {
        ArgumentNullException.ThrowIfNull(bodies);

        var present = bodies.Select(body => body.Kind).ToHashSet();

        Available = BodyKinds.Minor.Where(present.Contains).ToArray();
    }

    /// <summary>Disparado quando alguma classe entra ou sai da vista.</summary>
    public event Action? Changed;

    public bool IsVisible(BodyKind kind) => !_hidden.Contains(kind);

    /// <summary>
    /// Um corpo aparece quando a classe dele aparece. Classe que não é de corpo menor
    /// nunca é escondida: o Sistema Solar não é filtrável.
    /// </summary>
    public bool IsVisible(CelestialBodyData body)
    {
        ArgumentNullException.ThrowIfNull(body);

        return IsVisible(body.Kind);
    }

    public void SetVisible(BodyKind kind, bool visible)
    {
        if (!BodyKinds.IsMinor(kind))
        {
            return;
        }

        var changed = visible ? _hidden.Remove(kind) : _hidden.Add(kind);

        if (changed)
        {
            Changed?.Invoke();
        }
    }

    public IEnumerable<CelestialBodyData> Apply(IReadOnlyList<CelestialBodyData> bodies)
    {
        ArgumentNullException.ThrowIfNull(bodies);

        return bodies.Where(IsVisible);
    }
}
