using SolarSim.Engine.Models;

namespace SolarSim.Bridge;

/// <summary>
/// Leva o sistema de quilômetros para pixels, um nível de cada vez: a posição projetada de
/// um corpo é a do pai mais o deslocamento local já escalado pelo mapa daquele nível.
/// </summary>
/// <remarks>
/// Tudo em <c>double</c>. Esta classe não converte para <c>float</c> e não conhece a
/// câmera — é o <see cref="ViewportTransformer"/> que faz as duas coisas, nessa ordem.
/// </remarks>
public sealed class SystemProjector(ScaleLayout layout, ScaleMapper mapper)
{
    private readonly Dictionary<string, Vector3D> _positions = new(StringComparer.Ordinal);

    public ScaleLayout Layout { get; } = layout;

    public ScaleMapper Mapper { get; } = mapper;

    /// <summary>Posições em pixels, na mesma origem do sistema: a raiz fica em zero.</summary>
    public IReadOnlyDictionary<string, Vector3D> Positions => _positions;

    /// <summary>
    /// Recalcula o quadro. Depende de o snapshot vir em ordem de avaliação, que é o que o
    /// motor garante: quando um corpo é processado, o pai já está no dicionário.
    /// </summary>
    public void Project(in SystemStateSnapshot snapshot, IReadOnlyDictionary<string, string?> parents)
    {
        for (var index = 0; index < snapshot.Bodies.Count; index++)
        {
            var state = snapshot.Bodies[index];

            if (!parents.TryGetValue(state.Id, out var parentId) || parentId is null)
            {
                _positions[state.Id] = Vector3D.Zero;
                continue;
            }

            var level = Layout.LevelOf(parentId);
            var offset = Mapper.ToPixels(state.LocalPositionKm, level);

            _positions[state.Id] = _positions.GetValueOrDefault(parentId) + offset;
        }
    }

    public Vector3D PositionOf(string bodyId) => _positions.GetValueOrDefault(bodyId);
}
