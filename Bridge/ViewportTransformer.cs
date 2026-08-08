using Godot;
using SolarSim.Engine.Models;

namespace SolarSim.Bridge;

/// <summary>
/// Único ponto do sistema onde a precisão dupla do domínio se torna precisão simples
/// do motor gráfico.
/// </summary>
public sealed class ViewportTransformer
{
    /// <summary>Ponto para onde a câmera olha, em pixels do espaço projetado.</summary>
    public Vector3D FocusPixels { get; set; } = Vector3D.Zero;

    /// <summary>
    /// A subtração do foco acontece em <c>double</c>, e só o resultado já reduzido é
    /// convertido para <c>float</c>. Converter antes truncaria a mantissa e produziria
    /// trepidação em corpos distantes da origem — que é exatamente a situação de qualquer
    /// corpo do sistema exterior.
    /// </summary>
    public Vector2 ToScreen(Vector3D positionPixels)
    {
        var relativeX = positionPixels.X - FocusPixels.X;
        var relativeY = positionPixels.Y - FocusPixels.Y;

        // O eixo Y do Godot cresce para baixo; o da eclíptica, para cima.
        return new Vector2((float)relativeX, (float)-relativeY);
    }
}
