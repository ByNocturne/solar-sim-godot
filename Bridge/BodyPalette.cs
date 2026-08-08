using Godot;

namespace SolarSim.Bridge;

/// <summary>
/// Cor de um corpo, do inteiro <c>0xRRGGBB</c> que o motor guarda para o tipo do Godot.
/// O motor guarda inteiro justamente para não depender de <c>Color</c>, e a tradução
/// precisa existir em um lugar só para que a órbita, o disco e o rótulo do mesmo corpo
/// não divirjam de tom.
/// </summary>
public static class BodyPalette
{
    public static Color Of(uint rgb) => new(
        ((rgb >> 16) & 0xFF) / 255.0f,
        ((rgb >> 8) & 0xFF) / 255.0f,
        (rgb & 0xFF) / 255.0f);
}
