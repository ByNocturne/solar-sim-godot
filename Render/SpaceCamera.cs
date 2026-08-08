using Godot;

namespace SolarSim.Render;

/// <summary>
/// Câmera provisória do M1: ancorada na origem, com zoom por roda do mouse. Pan e
/// ancoragem em corpos entram no M4.
/// </summary>
public partial class SpaceCamera : Camera2D
{
    private const float ZoomStep = 0.15f;
    private const float MinExponent = -4.0f;
    private const float MaxExponent = 4.0f;

    private float _zoomExponent;

    public override void _Ready()
    {
        Position = Vector2.Zero;
        MakeCurrent();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton { Pressed: true } mouseButton)
        {
            return;
        }

        var direction = mouseButton.ButtonIndex switch
        {
            MouseButton.WheelUp => 1.0f,
            MouseButton.WheelDown => -1.0f,
            _ => 0.0f,
        };

        if (direction == 0.0f)
        {
            return;
        }

        // Zoom exponencial: cada passo multiplica a escala, o que dá sensação uniforme
        // em qualquer nível de aproximação.
        _zoomExponent = Mathf.Clamp(_zoomExponent + direction * ZoomStep, MinExponent, MaxExponent);
        Zoom = Vector2.One * Mathf.Pow(2.0f, _zoomExponent);
    }
}
