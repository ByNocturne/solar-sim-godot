using Godot;
using SolarSim.Bridge;

namespace SolarSim.Render;

/// <summary>
/// Navegação: zoom exponencial, órbita em torno do alvo, arrasto, ancoragem por teclado ou
/// clique, e a troca entre os modos de escala.
/// </summary>
/// <remarks>
/// A projeção é ortográfica, e não em perspectiva, por causa da escala hierárquica. A
/// perspectiva encolhe o que está longe da câmera, mas a curva logarítmica já mentiu sobre
/// a distância de cada corpo — as duas distorções se somariam e o tamanho na tela deixaria
/// de significar coisa alguma. Sem perspectiva, a vista de cima é exatamente a imagem que o
/// andaime 2D produzia, e a inclinação das órbitas aparece só quando se decide inclinar.
///
/// O alvo fica sempre na origem: quem se move é o mundo, projetado em relação ao foco. É
/// isso que mantém as coordenadas pequenas e sem trepidação mesmo com Netuno no centro.
/// </remarks>
public partial class SpaceCamera : Camera3D
{
    private const float ZoomStep = 0.15f;

    // O limite inferior precisa alcançar o modo linear, onde Netuno fica a milhares de
    // unidades da origem e só um afastamento grande traz o sistema inteiro para a tela.
    private const float MinExponent = -6.0f;
    private const float MaxExponent = 4.0f;

    /// <summary>Raio de tolerância do clique, em pixels de tela.</summary>
    private const float PickRadius = 14.0f;

    /// <summary>Radianos de rotação por pixel arrastado.</summary>
    private const float OrbitSensitivity = 0.006f;

    /// <summary>
    /// A câmera fica longe o bastante para que nada do sistema caia atrás dela, e os planos
    /// de corte a cercam com folga. Em projeção ortográfica a distância não altera a escala
    /// — quem faz isso é <see cref="Camera3D.Size"/> — então ela é livre para ser generosa.
    /// </summary>
    private const float Distance = 100_000.0f;

    private const float NearPlane = 1.0f;
    private const float FarPlane = 200_000.0f;

    private SimBridge? _bridge;
    private float _zoomExponent;
    private float _azimuth;
    private float _elevation = Mathf.Pi / 2.0f;
    private float _baseSize = 1080.0f;
    private DragMode _drag;

    private enum DragMode
    {
        None,
        Orbit,
        Pan,
    }

    public void Attach(SimBridge bridge) => _bridge = bridge;

    public override void _Ready()
    {
        // Uma unidade do espaço projetado vale um pixel quando o zoom está em repouso, que
        // é a mesma relação que a câmera 2D tinha.
        _baseSize = GetViewport().GetVisibleRect().Size.Y;

        Projection = ProjectionType.Orthogonal;
        KeepAspect = KeepAspectEnum.Height;
        Near = NearPlane;
        Far = FarPlane;

        ApplyView();
        MakeCurrent();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseButton button:
                HandleButton(button);
                break;

            case InputEventMouseMotion motion when _drag is DragMode.Orbit:
                Orbit(motion.Relative);
                break;

            case InputEventMouseMotion motion when _drag is DragMode.Pan:
                Pan(motion.Relative);
                break;

            case InputEventKey { Pressed: true, Echo: false } key:
                HandleKey(key);
                break;
        }
    }

    private void HandleButton(InputEventMouseButton button)
    {
        switch (button.ButtonIndex)
        {
            case MouseButton.Right:
                // Shift arrasta, para quem não tem o botão do meio.
                _drag = button.Pressed
                    ? button.ShiftPressed ? DragMode.Pan : DragMode.Orbit
                    : DragMode.None;
                return;

            case MouseButton.Middle:
                _drag = button.Pressed ? DragMode.Pan : DragMode.None;
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

        ApplyView();
    }

    /// <summary>
    /// A elevação para nos polos em vez de dar a volta: passar direto inverteria a imagem
    /// de cabeça para baixo no meio do arrasto.
    /// </summary>
    private void Orbit(Vector2 deltaScreen)
    {
        _azimuth = Mathf.Wrap(
            _azimuth - (deltaScreen.X * OrbitSensitivity), -Mathf.Pi, Mathf.Pi);

        _elevation = Mathf.Clamp(
            _elevation + (deltaScreen.Y * OrbitSensitivity), -Mathf.Pi / 2.0f, Mathf.Pi / 2.0f);

        ApplyView();
    }

    /// <summary>
    /// O deslocamento nasce em pixels de tela e precisa chegar ao domínio nos eixos da
    /// eclíptica, passando pelos eixos da câmera — que mudam a cada rotação.
    /// </summary>
    private void Pan(Vector2 deltaScreen)
    {
        var unitsPerPixel = Size / GetViewport().GetVisibleRect().Size.Y;
        var basis = Transform.Basis;

        var delta = ((-basis.X * deltaScreen.X) + (basis.Y * deltaScreen.Y)) * unitsPerPixel;

        _bridge?.PanBy(ViewportTransformer.GodotToEcliptic(delta));
    }

    /// <summary>
    /// Clicar no vazio não desancora: com quinze corpos e a maioria deles do tamanho de
    /// alguns pixels, errar o alvo é o caso comum, e perder a âncora por isso seria hostil.
    /// </summary>
    private void Pick()
    {
        var mouse = GetViewport().GetMousePosition();

        if (_bridge?.NearestBody(mouse, PickRadius, UnprojectPosition) is { } bodyId)
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
        _azimuth = 0.0f;
        _elevation = Mathf.Pi / 2.0f;

        ApplyView();
        _bridge?.AnchorTo(null);
    }

    /// <summary>
    /// A orientação é montada por composição em vez de <c>LookAt</c> porque o alvo comum é
    /// justamente o polo, onde a direção de olhar e a vertical do mundo coincidem e o
    /// <c>LookAt</c> não tem como escolher para onde fica o norte da tela.
    /// </summary>
    private void ApplyView()
    {
        Size = _baseSize / Mathf.Pow(2.0f, _zoomExponent);

        // Elevação de 90 graus deita a câmera sobre o plano da eclíptica olhando para
        // baixo, com o norte da tela no mesmo lugar em que o andaime 2D o colocava.
        var basis = new Basis(Vector3.Up, _azimuth) * new Basis(Vector3.Right, -_elevation);

        Transform = new Transform3D(basis, basis.Z * Distance);
    }
}
