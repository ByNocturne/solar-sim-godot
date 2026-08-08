using Godot;
using SolarSim.Engine.Models;

namespace SolarSim.Bridge;

/// <summary>
/// Único ponto do sistema onde a precisão dupla do domínio se torna precisão simples
/// do motor gráfico.
/// </summary>
public sealed class ViewportTransformer(ScaleMapper scale)
{
    public ScaleMapper Scale { get; } = scale;

    /// <summary>Posição da câmera no referencial físico, em km.</summary>
    public Vector3D CameraPositionKm { get; set; } = Vector3D.Zero;

    /// <summary>
    /// A subtração da câmera acontece em <c>double</c>, e só o resultado já reduzido é
    /// convertido para <c>float</c>. Converter antes truncaria a mantissa e produziria
    /// trepidação em corpos distantes da origem.
    /// </summary>
    public Vector2 ToScreen(Vector3D positionKm)
    {
        var relativeX = positionKm.X - CameraPositionKm.X;
        var relativeY = positionKm.Y - CameraPositionKm.Y;

        // O eixo Y do Godot cresce para baixo; o da eclíptica, para cima.
        return new Vector2(
            (float)Scale.KmToPixels(relativeX),
            (float)-Scale.KmToPixels(relativeY));
    }
}
