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
    /// <summary>
    /// Altura de janela em que as frações abaixo foram calibradas, em pixels. As frações
    /// existem para que a mesma calibragem valha em qualquer resolução: com valores
    /// absolutos, o sistema ocuparia o mesmo punhado de pixels no meio de uma tela maior.
    /// </summary>
    public const double ReferenceHeightPixels = 648.0;

    private const double SystemFraction = 300.0 / ReferenceHeightPixels;
    private const double SatelliteFraction = 34.0 / ReferenceHeightPixels;
    private const double DeepFraction = 12.0 / ReferenceHeightPixels;

    private readonly Dictionary<string, OrbitLevel> _levelByParentId;

    public ScaleLayout(IReadOnlyList<CelestialBodyData> bodies, double viewportHeightPixels)
    {
        ArgumentNullException.ThrowIfNull(bodies);

        var depthById = Depths(bodies);
        var apoapsisByParent = new Dictionary<string, double>(StringComparer.Ordinal);

        foreach (var body in bodies)
        {
            if (body.ParentId is not { } parentId
                || body.Elements is not { } elements
                || !DefinesLevel(body))
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
                ScreenRadiusForDepth(
                    depthById.GetValueOrDefault(entry.Key) + 1, viewportHeightPixels)),
            StringComparer.Ordinal);
    }

    /// <summary>
    /// Os corpos menores ficam de fora do dimensionamento de propósito. A curva perceptual
    /// é normalizada pela maior órbita do nível, então um único objeto distante encolhe
    /// todo o resto: com Sedna incluído no cálculo, cujo afélio é 1023 UA, a órbita da
    /// Terra perderia dois terços do raio na tela para acomodar um ponto que passa a maior
    /// parte de onze mil anos invisível. Fora do cálculo, ele continua sendo desenhado — a
    /// curva não satura, apenas o coloca além do raio nominal do nível. A consequência que
    /// importa é esta: acrescentar corpos ao catálogo não mexe um pixel no Sistema Solar.
    /// </summary>
    private static bool DefinesLevel(CelestialBodyData body) => !BodyKinds.IsMinor(body.Kind);

    /// <summary>
    /// Quantos pixels a maior órbita de cada profundidade ocupa. Os satélites cabem em uma
    /// fração do espaço dos planetas para que continuem parecendo satélites: se a órbita
    /// da Lua ocupasse a tela inteira, o sistema deixaria de ser legível como hierarquia.
    /// </summary>
    /// <remarks>
    /// A profundidade 1 fica um pouco abaixo de metade da altura da janela, que é o que
    /// faz a órbita de Netuno caber na tela sem zoom em qualquer resolução.
    /// </remarks>
    public static double ScreenRadiusForDepth(int depth, double viewportHeightPixels)
        => viewportHeightPixels * depth switch
        {
            <= 1 => SystemFraction,
            2 => SatelliteFraction,
            _ => DeepFraction,
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
