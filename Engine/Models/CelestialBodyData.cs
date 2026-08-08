namespace SolarSim.Engine.Models;

/// <summary>
/// Descrição estática de um corpo celeste. Imutável: nada aqui muda com o tempo,
/// já que a posição é sempre derivada da Data Juliana.
/// </summary>
/// <remarks>
/// É um record para que a troca de pai de um corpo dinâmico, na emenda de cônicas, seja
/// uma cópia com dois campos trocados, e não uma mutação: o que muda ali é a órbita
/// vigente, não a identidade do corpo.
/// </remarks>
public sealed record CelestialBodyData
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    /// <summary>Corpo em torno do qual este orbita. Nulo apenas para a raiz.</summary>
    public string? ParentId { get; init; }

    /// <summary>GM deste corpo, em km³/s². Usado pelos filhos, não por ele mesmo.</summary>
    public double MuKm3S2 { get; init; }

    public double RadiusKm { get; init; }

    /// <summary>Nulo para a raiz, que não orbita nada.</summary>
    public OrbitalElements? Elements { get; init; }

    /// <summary>Cor em 0xRRGGBB. Guardada como inteiro para não depender do Godot.</summary>
    public uint ColorRgb { get; init; } = 0xFFFFFF;
}
