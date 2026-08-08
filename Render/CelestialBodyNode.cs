using Godot;
using SolarSim.Bridge;

namespace SolarSim.Render;

/// <summary>
/// Representação visual de um corpo. Não calcula nada: apenas recebe a posição já
/// convertida e se desenha.
/// </summary>
/// <remarks>
/// O material é sem sombreamento de propósito. A vista é um esquema, não uma fotografia:
/// as distâncias estão comprimidas por uma curva logarítmica e os raios têm escala própria,
/// então iluminar a partir do Sol sugeriria um realismo que a escala não tem. Sem
/// sombreamento, a esfera na tela é o mesmo disco de cor cheia que o andaime 2D desenhava.
/// </remarks>
public partial class CelestialBodyNode : Node3D
{
    /// <summary>
    /// Bastante para que a silhueta continue redonda com o zoom no máximo, quando um
    /// planeta passa de quinze pixels para quase mil.
    /// </summary>
    private const int RadialSegments = 32;

    private const int Rings = 16;

    private SimBridge? _bridge;

    public string BodyId { get; set; } = string.Empty;

    public Color BodyColor { get; set; } = Colors.White;

    public float DisplayRadius { get; set; } = 6.0f;

    public override void _Ready() => AddChild(new MeshInstance3D
    {
        Mesh = new SphereMesh
        {
            Radius = DisplayRadius,
            Height = DisplayRadius * 2.0f,
            RadialSegments = RadialSegments,
            Rings = Rings,
        },
        MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = BodyColor,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        },
    });

    public void Attach(SimBridge bridge)
    {
        _bridge = bridge;
        _bridge.FrameReady += OnFrameReady;
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
        if (frame.RenderPositions.TryGetValue(BodyId, out var renderPosition))
        {
            Position = renderPosition;
        }
    }
}
