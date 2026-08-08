using SolarSim.Engine.Models;

namespace SolarSim.Bridge;

/// <summary>
/// Onde a câmera está olhando, em pixels do espaço projetado. Guarda o alvo, o
/// deslocamento manual e a transição entre alvos.
/// </summary>
/// <remarks>
/// A transição tem duração fixa e termina exatamente no alvo, em vez de ser uma
/// suavização exponencial permanente. A diferença aparece quando se ancora em um corpo
/// rápido com o tempo acelerado: a suavização exponencial nunca alcança o alvo e deixa o
/// corpo ancorado tremendo fora do centro.
/// </remarks>
public sealed class CameraRig
{
    public const double TransitionSeconds = 0.5;

    private Vector3D _transitionOffset;
    private double _transitionRemaining;
    private bool _startTransitionOnNextFrame;

    /// <summary>Corpo em que a câmera está ancorada, ou nulo para a origem do sistema.</summary>
    public string? AnchorBodyId { get; private set; }

    /// <summary>Deslocamento manual acumulado pelo arrasto do mouse, em pixels.</summary>
    public Vector3D PanPixels { get; private set; }

    /// <summary>Ponto para onde a câmera olha agora, já com a transição aplicada.</summary>
    public Vector3D FocusPixels { get; private set; }

    public bool IsTransitioning => _transitionRemaining > 0.0 || _startTransitionOnNextFrame;

    /// <summary>
    /// Troca o alvo. O pan é zerado junto, porque um deslocamento manual medido em
    /// relação ao corpo anterior não quer dizer nada em relação ao novo.
    /// </summary>
    public void AnchorTo(string? bodyId)
    {
        if (string.Equals(AnchorBodyId, bodyId, StringComparison.Ordinal))
        {
            return;
        }

        AnchorBodyId = bodyId;
        PanPixels = Vector3D.Zero;

        // O deslocamento só pode ser medido quando a posição do novo alvo for conhecida,
        // o que acontece no próximo quadro.
        _startTransitionOnNextFrame = true;
    }

    public void Pan(Vector3D deltaPixels) => PanPixels += deltaPixels;

    /// <summary>
    /// Avança um quadro. <paramref name="anchorPositionPixels"/> é a posição projetada do
    /// corpo ancorado, ou zero quando não há ancoragem.
    /// </summary>
    public void Advance(double deltaSeconds, Vector3D anchorPositionPixels)
    {
        var target = anchorPositionPixels + PanPixels;

        if (_startTransitionOnNextFrame)
        {
            _startTransitionOnNextFrame = false;
            _transitionOffset = FocusPixels - target;
            _transitionRemaining = TransitionSeconds;
        }

        if (_transitionRemaining <= 0.0)
        {
            FocusPixels = target;
            return;
        }

        _transitionRemaining = Math.Max(0.0, _transitionRemaining - deltaSeconds);

        FocusPixels = target + (_transitionOffset * Ease(_transitionRemaining / TransitionSeconds));
    }

    /// <summary>
    /// Cosseno levantado: vale 1 no início e 0 no fim, com derivada nula nas duas pontas.
    /// É o que evita o solavanco na largada e a chegada seca no alvo.
    /// </summary>
    private static double Ease(double remainingFraction)
        => 0.5 - (0.5 * Math.Cos(Math.Clamp(remainingFraction, 0.0, 1.0) * Math.PI));
}
