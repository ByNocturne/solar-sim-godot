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
/// pontos. O cache vence quando a escala muda — o que a
/// <see cref="RenderFrame.ScaleRevision"/> denuncia — e quando a precessão gira a órbita
/// além do que a amostragem consegue distinguir.
/// </remarks>
public partial class OrbitLineRenderer : MeshInstance3D
{
    private readonly ImmediateMesh _line = new();

    private Vector3D[] _samplesKm = [];
    private int _cachedRevision = -1;
    private bool _isClosed = true;
    private SimBridge? _bridge;
    private (double NodeRad, double PeriapsisRad) _sampledOrientation;

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
        Resample(bridge);
        bridge.FrameReady += OnFrameReady;
    }

    /// <summary>
    /// Recolhe a órbita de novo. Serve a quem trocou de pai ou de arco: os pontos em
    /// cache descrevem a curva anterior, e nenhuma mudança de escala vai invalidá-los.
    /// </summary>
    public void Resample(SimBridge bridge)
    {
        ArgumentNullException.ThrowIfNull(bridge);

        _samplesKm = bridge.SampleOrbitKm(BodyId);
        _isClosed = bridge.HasClosedOrbit(BodyId);
        _sampledOrientation = bridge.OrbitOrientation(BodyId);
        _cachedRevision = -1;
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

        if (_bridge is null)
        {
            return;
        }

        if (HasPrecessedPastResolution(_bridge))
        {
            Resample(_bridge);
        }

        if (frame.ScaleRevision == _cachedRevision)
        {
            return;
        }

        _cachedRevision = frame.ScaleRevision;
        Rebuild(_bridge);
    }

    /// <summary>
    /// Verdadeiro quando a órbita girou o bastante para que o traço em cache descreva
    /// outra curva. O corte é a resolução da própria amostragem: acima dela o desenho
    /// mente, abaixo dela reamostrar não muda um pixel — e a maioria dos corpos fica
    /// abaixo dela por séculos.
    /// </summary>
    private bool HasPrecessedPastResolution(SimBridge bridge)
    {
        var (node, periapsis) = bridge.OrbitOrientation(BodyId);

        return AngleGap(node, _sampledOrientation.NodeRad) > SimBridge.OrbitAngularResolutionRad
            || AngleGap(periapsis, _sampledOrientation.PeriapsisRad)
                > SimBridge.OrbitAngularResolutionRad;
    }

    /// <summary>Distância entre dois ângulos, pelo lado curto do círculo.</summary>
    private static double AngleGap(double a, double b)
    {
        var gap = Math.Abs(a - b) % Math.Tau;

        return gap > Math.PI ? Math.Tau - gap : gap;
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
        // sobra uma fresta na órbita. A órbita aberta não fecha, porque ela não volta.
        if (_isClosed)
        {
            _line.SurfaceAddVertex(bridge.OrbitSampleToPixels(_samplesKm[0], ParentBodyId));
        }

        _line.SurfaceEnd();
    }
}
