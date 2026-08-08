using Godot;
using SolarSim.Bridge;
using SolarSim.Engine.Models;

namespace SolarSim.Render;

/// <summary>
/// Desenha a órbita de um corpo como uma linha fechada em torno do pai.
/// </summary>
/// <remarks>
/// O nó fica sobre o corpo pai e desenha a órbita em coordenadas locais. Isso é o que
/// torna a geometria independente da câmera: mover a câmera muda a posição do nó, não os
/// pontos. O cache só vence quando a escala muda, e é a mudança de escala que a
/// <see cref="RenderFrame.ScaleRevision"/> denuncia.
/// </remarks>
public partial class OrbitLineRenderer : Node2D
{
    private Vector3D[] _samplesKm = [];
    private Vector2[] _points = [];
    private int _cachedRevision = -1;
    private SimBridge? _bridge;

    public string BodyId { get; set; } = string.Empty;

    public string ParentBodyId { get; set; } = string.Empty;

    public Color LineColor { get; set; } = Colors.White;

    public void Attach(SimBridge bridge)
    {
        _bridge = bridge;
        _samplesKm = bridge.SampleOrbitKm(BodyId);
        bridge.FrameReady += OnFrameReady;
    }

    public override void _ExitTree()
    {
        if (_bridge is not null)
        {
            _bridge.FrameReady -= OnFrameReady;
            _bridge = null;
        }
    }

    public override void _Draw()
    {
        if (_points.Length > 1)
        {
            // Largura negativa desenha a linha fina de um pixel, que não engorda quando a
            // câmera aproxima. Com zoom de 64 vezes, uma linha de largura 1 viraria uma
            // faixa cobrindo o planeta.
            DrawPolyline(_points, LineColor, width: -1.0f);
        }
    }

    private void OnFrameReady(RenderFrame frame)
    {
        if (frame.ScreenPositions.TryGetValue(ParentBodyId, out var parentPosition))
        {
            Position = parentPosition;
        }

        if (frame.ScaleRevision == _cachedRevision || _bridge is null)
        {
            return;
        }

        _cachedRevision = frame.ScaleRevision;
        _points = new Vector2[_samplesKm.Length + 1];

        for (var index = 0; index < _samplesKm.Length; index++)
        {
            _points[index] = _bridge.OrbitSampleToPixels(_samplesKm[index], ParentBodyId);
        }

        // Fecha o traço no primeiro ponto: a amostragem cobre um período, e sem isso
        // sobra uma fresta na órbita.
        if (_samplesKm.Length > 0)
        {
            _points[^1] = _points[0];
        }

        QueueRedraw();
    }
}
