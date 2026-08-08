using Godot;
using SolarSim.Bridge;

namespace SolarSim.Render;

/// <summary>
/// Nome de cada corpo, ao lado dele.
/// </summary>
/// <remarks>
/// Os rótulos vivem em uma camada de tela, e não no mundo. Se fossem nós do mundo, a
/// câmera os ampliaria junto com tudo: o nome de Júpiter ocuparia a tela inteira com o
/// zoom no máximo. Aqui eles têm sempre o mesmo tamanho, e o que muda é onde ficam.
/// </remarks>
public partial class BodyLabels : CanvasLayer
{
    private const int FontSize = 12;

    /// <summary>Folga entre a borda do corpo e o começo do texto, em pixels de tela.</summary>
    private const float Margin = 5.0f;

    private readonly List<Entry> _entries = [];

    private SimBridge? _bridge;
    private bool _visible = true;

    public void Attach(SimBridge bridge)
    {
        _bridge = bridge;

        foreach (var body in bridge.Sim.Bodies)
        {
            var label = new Label
            {
                Text = body.Name,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };

            label.AddThemeFontSizeOverride("font_size", FontSize);
            label.AddThemeColorOverride("font_color", ToGodotColor(body.ColorRgb));

            // Contorno preto: sem ele o texto some quando passa por cima de uma órbita
            // ou de outro corpo.
            label.AddThemeColorOverride("font_outline_color", new Color(0.0f, 0.0f, 0.0f));
            label.AddThemeConstantOverride("outline_size", 4);

            AddChild(label);

            _entries.Add(new Entry(
                body.Id,
                label,
                (float)bridge.ScaleMap.BodyRadiusPixels(body.RadiusKm)));
        }

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

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.N })
        {
            _visible = !_visible;
            GetViewport().SetInputAsHandled();
        }
    }

    private void OnFrameReady(RenderFrame frame)
    {
        var canvas = GetViewport().GetCanvasTransform();
        var zoom = canvas.Scale.X;
        var screen = GetViewport().GetVisibleRect();

        // Quem já foi desenhado reserva o seu espaço. A ordem de avaliação coloca o pai
        // antes do filho, então em um aglomerado o planeta ganha do satélite — que é a
        // prioridade certa quando o sistema está distante e tudo se sobrepõe.
        var taken = new List<Rect2>(_entries.Count);

        foreach (var entry in _entries)
        {
            entry.Label.Visible = false;

            if (!_visible || !frame.ScreenPositions.TryGetValue(entry.BodyId, out var world))
            {
                continue;
            }

            var size = entry.Label.GetMinimumSize();
            var position = (canvas * world)
                + new Vector2((entry.RadiusPixels * zoom) + Margin, -size.Y / 2.0f);

            var box = new Rect2(position, size);

            if (!screen.Intersects(box) || taken.Any(other => other.Intersects(box)))
            {
                continue;
            }

            taken.Add(box);
            entry.Label.Position = position;
            entry.Label.Visible = true;
        }
    }

    private static Color ToGodotColor(uint rgb) => new(
        ((rgb >> 16) & 0xFF) / 255.0f,
        ((rgb >> 8) & 0xFF) / 255.0f,
        (rgb & 0xFF) / 255.0f);

    private sealed record Entry(string BodyId, Label Label, float RadiusPixels);
}
