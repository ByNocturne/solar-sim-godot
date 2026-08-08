namespace SolarSim.Engine.Models;

/// <summary>
/// Descrição estática de um corpo celeste. Imutável: nada aqui muda com o tempo,
/// já que a posição é sempre derivada da Data Juliana.
/// </summary>
public sealed class CelestialBodyData
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
