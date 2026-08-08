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
public partial class OrbitLineRenderer : MeshInstance3D
{
    private readonly ImmediateMesh _line = new();

    private Vector3D[] _samplesKm = [];
    private int _cachedRevision = -1;
    private SimBridge? _bridge;

    public string BodyId { get; set; } = string.Empty;

    public string ParentBodyId { get; set; } = string.Empty;

    public Color LineColor { get; set; } = Colors.White;

    public override void _Ready()
    {
        Mesh = _line;

        MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = LineColor,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };
    }

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

    private void OnFrameReady(RenderFrame frame)
    {
        if (frame.RenderPositions.TryGetValue(ParentBodyId, out var parentPosition))
        {
            Position = parentPosition;
        }

        if (frame.ScaleRevision == _cachedRevision || _bridge is null)
        {
            return;
        }

        _cachedRevision = frame.ScaleRevision;
        Rebuild(_bridge);
    }

    /// <summary>
    /// A linha tem sempre um pixel de espessura, porque é isso que a primitiva de linha
    /// entrega. É a largura desejada: com o zoom no máximo, um traço de espessura
    /// proporcional viraria uma faixa cobrindo o planeta.
    /// </summary>
    private void Rebuild(SimBridge bridge)
    {
        _line.ClearSurfaces();

        if (_samplesKm.Length < 2)
        {
            return;
        }

        _line.SurfaceBegin(Mesh.PrimitiveType.LineStrip);

        foreach (var sampleKm in _samplesKm)
        {
            _line.SurfaceAddVertex(bridge.OrbitSampleToPixels(sampleKm, ParentBodyId));
        }

        // Fecha o traço no primeiro ponto: a amostragem cobre um período, e sem isso
        // sobra uma fresta na órbita.
        _line.SurfaceAddVertex(bridge.OrbitSampleToPixels(_samplesKm[0], ParentBodyId));

        _line.SurfaceEnd();
    }
}
