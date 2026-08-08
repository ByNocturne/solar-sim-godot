using Godot;
using SolarSim.Engine.Models;

namespace SolarSim.Bridge;

/// <summary>
/// Único ponto do sistema onde a precisão dupla do domínio se torna precisão simples do
/// motor gráfico, e onde a convenção de eixos da eclíptica vira a do Godot.
/// </summary>
public sealed class ViewportTransformer
{
    /// <summary>Ponto para onde a câmera olha, em pixels do espaço projetado.</summary>
    public Vector3D FocusPixels { get; set; } = Vector3D.Zero;

    /// <summary>
    /// Troca de convenção, sem mudança de escala. A eclíptica tem X e Y no plano e Z para
    /// o norte; o Godot tem Y para cima e Z na direção do observador. O mapeamento preserva
    /// a orientação — é uma rotação, não um espelhamento — e é escolhido para que a vista
    /// de cima reproduza exatamente o que o andaime 2D desenhava.
    /// </summary>
    public static Vector3 EclipticToGodot(Vector3D ecliptic)
        => new((float)ecliptic.X, (float)ecliptic.Z, (float)-ecliptic.Y);

    /// <summary>
    /// A volta. Recebe uma diferença entre pontos, e não uma posição, porque quem precisa
    /// dela é o arrasto da câmera: o deslocamento nasce nos eixos da tela e tem que chegar
    /// ao domínio nos eixos da eclíptica.
    /// </summary>
    public static Vector3D GodotToEcliptic(Vector3 delta)
        => new(delta.X, -delta.Z, delta.Y);

    /// <summary>
    /// A subtração do foco acontece em <c>double</c>, e só o resultado já reduzido é
    /// convertido para <c>float</c>. Converter antes truncaria a mantissa e produziria
    /// trepidação em corpos distantes da origem — que é exatamente a situação de qualquer
    /// corpo do sistema exterior.
    /// </summary>
    public Vector3 ToRenderSpace(Vector3D positionPixels)
        => EclipticToGodot(positionPixels - FocusPixels);
}
