namespace SolarSim.Engine.Models;

/// <summary>
/// Classe dinâmica de um corpo: onde ele vive e como se move.
/// </summary>
/// <remarks>
/// É a classe dinâmica, e não o rótulo físico, porque é isso que um filtro de vista
/// precisa saber. "Planeta anão" é uma decisão da IAU sobre tamanho e vizinhança, e
/// cortaria a lista em diagonal — Ceres é do cinturão principal e Plutão é
/// transnetuniano, e é onde eles estão, não a categoria que carregam, que decide se
/// aparecem juntos na tela. O rótulo físico, quando importa, vive em
/// <see cref="CelestialBodyData.Family"/> ou na nota do arquivo.
/// </remarks>
public enum BodyKind
{
    /// <summary>Corpo que não declarou classe. É o default, e nunca é filtrado.</summary>
    Unspecified = 0,

    Star,

    Planet,

    Moon,

    /// <summary>Asteroide que não é próximo da Terra nem troiano: cinturão principal.</summary>
    Asteroid,

    NearEarthAsteroid,

    Trojan,

    Centaur,

    Comet,

    TransNeptunian,

    /// <summary>Corpo criado em runtime a partir de um vetor de estado.</summary>
    Spacecraft,
}

public static class BodyKinds
{
    /// <summary>
    /// As classes que compõem o catálogo de corpos menores. São as filtráveis na vista, e
    /// as que não participam do dimensionamento da escala.
    /// </summary>
    public static readonly IReadOnlyList<BodyKind> Minor =
    [
        BodyKind.Asteroid,
        BodyKind.NearEarthAsteroid,
        BodyKind.Trojan,
        BodyKind.Centaur,
        BodyKind.Comet,
        BodyKind.TransNeptunian,
    ];

    public static bool IsMinor(BodyKind kind) => kind
        is BodyKind.Asteroid
        or BodyKind.NearEarthAsteroid
        or BodyKind.Trojan
        or BodyKind.Centaur
        or BodyKind.Comet
        or BodyKind.TransNeptunian;

    /// <summary>
    /// Como a classe se escreve no JSON. Mora aqui, e não no carregador, para que o
    /// importador que gera o arquivo e o carregador que o lê não possam divergir.
    /// </summary>
    public static string JsonName(BodyKind kind)
    {
        var name = kind.ToString();

        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    public static bool TryParse(string jsonName, out BodyKind kind)
    {
        foreach (var candidate in Enum.GetValues<BodyKind>())
        {
            if (candidate != BodyKind.Unspecified
                && JsonName(candidate).Equals(jsonName, StringComparison.Ordinal))
            {
                kind = candidate;
                return true;
            }
        }

        kind = BodyKind.Unspecified;
        return false;
    }

    /// <summary>Os nomes aceitos no JSON, para compor mensagem de erro.</summary>
    public static string JsonNames()
        => string.Join(
            ", ",
            Enum.GetValues<BodyKind>()
                .Where(kind => kind != BodyKind.Unspecified)
                .Select(JsonName));
}
