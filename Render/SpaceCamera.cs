using Godot;
using SolarSim.Bridge;

namespace SolarSim.Render;

/// <summary>
/// Navegação: zoom exponencial, arrasto, ancoragem por teclado ou clique, e a troca entre
/// os modos de escala. A câmera fica sempre na origem — quem se move é o mundo, projetado
/// em relação ao foco. É isso que mantém as coordenadas pequenas e sem trepidação mesmo
/// com Netuno no centro da tela.
/// </summary>
public partial class SpaceCamera : Camera2D
{
    private const float ZoomStep = 0.15f;
    // O limite inferior precisa alcançar o modo linear, onde Netuno fica a 7500 pixels
    // da origem e só um afastamento grande traz o sistema inteiro para a tela.
    private const float MinExponent = -6.0f;
    private const float MaxExponent = 4.0f;

    /// <summary>Raio de tolerância do clique, em pixels de tela.</summary>
    private const float PickRadius = 14.0f;

    private SimBridge? _bridge;
    private float _zoomExponent;
    private bool _dragging;

    public void Attach(SimBridge bridge) => _bridge = bridge;

    public override void _Ready()
    {
        Position = Vector2.Zero;
        MakeCurrent();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseButton button:
                HandleButton(button);
                break;

            case InputEventMouseMotion motion when _dragging:
                _bridge?.PanByScreenPixels(motion.Relative, Zoom.X);
                break;

            case InputEventKey { Pressed: true, Echo: false } key:
                HandleKey(key);
                break;
        }
    }

    private void HandleButton(InputEventMouseButton button)
    {
        if (button.ButtonIndex is MouseButton.Right or MouseButton.Middle)
        {
            _dragging = button.Pressed;
            return;
        }

        if (!button.Pressed)
        {
            return;
        }

        switch (button.ButtonIndex)
        {
            case MouseButton.WheelUp:
                ApplyZoom(1.0f);
                break;

            case MouseButton.WheelDown:
                ApplyZoom(-1.0f);
                break;

            case MouseButton.Left:
                Pick();
                break;
        }
    }

    private void ApplyZoom(float direction)
    {
        // Zoom exponencial: cada passo multiplica a escala, o que dá sensação uniforme
        // em qualquer nível de aproximação.
        _zoomExponent = Mathf.Clamp(
            _zoomExponent + (direction * ZoomStep), MinExponent, MaxExponent);

        Zoom = Vector2.One * Mathf.Pow(2.0f, _zoomExponent);
    }

    /// <summary>
    /// Clicar no vazio não desancora: com quinze corpos e a maioria deles do tamanho de
    /// alguns pixels, errar o alvo é o caso comum, e perder a âncora por isso seria hostil.
    /// </summary>
    private void Pick()
    {
        if (_bridge?.NearestBody(GetGlobalMousePosition(), PickRadius / Zoom.X) is { } bodyId)
        {
            _bridge.AnchorTo(bodyId);
        }
    }

    private void HandleKey(InputEventKey key)
    {
        switch (key.Keycode)
        {
            case Key.Tab:
                _bridge?.CycleAnchor(key.ShiftPressed ? -1 : 1);
                break;

            case Key.L:
                _bridge?.ScaleMap.ToggleMode();
                break;

            case Key.Home:
                ResetView();
                break;

            default:
                return;
        }

        GetViewport().SetInputAsHandled();
    }

    private void ResetView()
    {
        _zoomExponent = 0.0f;
        Zoom = Vector2.One;
        _bridge?.AnchorTo(null);
    }
}
