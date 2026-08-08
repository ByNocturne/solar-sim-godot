using SolarSim.Engine.Models;

namespace SolarSim.Bridge;

/// <summary>
/// Descobre, a partir dos dados estáticos, quanto espaço de tela cada nível da hierarquia
/// merece. Calculado uma vez: nada aqui depende do tempo.
/// </summary>
/// <remarks>
/// Um único mapa de escala para todo o sistema não funciona. A órbita da Lua é 390 vezes
/// menor que a da Terra, e a de Io é 300 vezes menor que a de Júpiter: comprimidas pela
/// mesma curva que faz Netuno caber na tela, as luas ficariam dentro do próprio planeta.
/// Cada pai ganha então o seu próprio mapa, dimensionado pela maior órbita que abriga.
/// </remarks>
public sealed class ScaleLayout
{
    private readonly Dictionary<string, OrbitLevel> _levelByParentId;

    public ScaleLayout(IReadOnlyList<CelestialBodyData> bodies)
    {
        ArgumentNullException.ThrowIfNull(bodies);

        var depthById = Depths(bodies);
        var apoapsisByParent = new Dictionary<string, double>(StringComparer.Ordinal);

        foreach (var body in bodies)
        {
            if (body.ParentId is not { } parentId || body.Elements is not { } elements)
            {
                continue;
            }

            // O apoapsis, e não o semi-eixo: é o ponto que precisa caber na tela.
            var apoapsisKm = elements.SemiMajorAxisKm * (1.0 + elements.Eccentricity);

            apoapsisByParent[parentId] = Math.Max(
                apoapsisByParent.GetValueOrDefault(parentId), apoapsisKm);
        }

        _levelByParentId = apoapsisByParent.ToDictionary(
            entry => entry.Key,
            entry => new OrbitLevel(
                entry.Value,
                ScreenRadiusForDepth(depthById.GetValueOrDefault(entry.Key) + 1)),
            StringComparer.Ordinal);
    }

    /// <summary>
    /// Quantos pixels a maior órbita de cada profundidade ocupa. Os satélites cabem em uma
    /// fração do espaço dos planetas para que continuem parecendo satélites: se a órbita
    /// da Lua ocupasse a tela inteira, o sistema deixaria de ser legível como hierarquia.
    /// </summary>
    /// <remarks>
    /// O valor da profundidade 1 é metade da altura útil da janela padrão de 1152x648:
    /// com ele, a órbita de Netuno cabe na tela sem zoom.
    /// </remarks>
    public static double ScreenRadiusForDepth(int depth) => depth switch
    {
        <= 1 => 300.0,
        2 => 34.0,
        _ => 12.0,
    };

    /// <summary>
    /// Nível a aplicar nos filhos deste pai. Para um pai sem filhos, devolve um nível
    /// degenerado, que o mapa trata como escala linear.
    /// </summary>
    public OrbitLevel LevelOf(string parentId)
        => _levelByParentId.GetValueOrDefault(parentId);

    private static Dictionary<string, int> Depths(IReadOnlyList<CelestialBodyData> bodies)
    {
        // A lista chega em ordem de avaliação, então o pai já tem profundidade quando o
        // filho é visitado.
        var depthById = new Dictionary<string, int>(bodies.Count, StringComparer.Ordinal);

        foreach (var body in bodies)
        {
            depthById[body.Id] = body.ParentId is { } parentId
                ? depthById.GetValueOrDefault(parentId) + 1
                : 0;
        }

        return depthById;
    }
}
