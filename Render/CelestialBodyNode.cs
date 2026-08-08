using Godot;
using SolarSim.Bridge;

namespace SolarSim.Render;

/// <summary>
/// Representação visual de um corpo. Não calcula nada: apenas recebe a posição já
/// convertida e se desenha.
/// </summary>
public partial class CelestialBodyNode : Node2D
{
    private SimBridge? _bridge;

    public string BodyId { get; set; } = string.Empty;

    public Color BodyColor { get; set; } = Colors.White;

    public float DisplayRadius { get; set; } = 6.0f;

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

    public override void _Draw() => DrawCircle(Vector2.Zero, DisplayRadius, BodyColor);

    private void OnFrameReady(RenderFrame frame)
    {
        if (frame.ScreenPositions.TryGetValue(BodyId, out var screenPosition))
        {
            Position = screenPosition;
        }
    }
}
