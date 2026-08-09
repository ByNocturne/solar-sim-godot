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

    /// <summary>
    /// Faixas da tela reservadas pela interface, em pixels. Um rótulo que não caiba
    /// inteiro fora delas é descartado: metade de um nome atrás de um painel é pior que
    /// nome nenhum. Quem monta a cena informa os valores, para que este nó continue sem
    /// saber que existem painéis.
    /// </summary>
    public float ReservedLeft { get; set; }

    public float ReservedRight { get; set; }

    public float ReservedBottom { get; set; }

    public void Attach(SimBridge bridge)
    {
        _bridge = bridge;

        foreach (var body in bridge.Bodies)
        {
            var label = new Label
            {
                Text = body.Name,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };

            label.AddThemeFontSizeOverride("font_size", FontSize);
            label.AddThemeColorOverride("font_color", BodyPalette.Of(body.ColorRgb));

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
        if (_bridge is not { } bridge || GetViewport().GetCamera3D() is not { } camera)
        {
            return;
        }

        var viewport = GetViewport().GetVisibleRect();
        var screen = FreeArea(viewport);

        // Em projeção ortográfica, quantos pixels de tela vale uma unidade do mundo é
        // constante em todo o quadro — não depende da distância. É o que permite calcular
        // o raio aparente do corpo sem projetar a borda dele.
        var pixelsPerUnit = viewport.Size.Y / camera.Size;

        // Quem já foi desenhado reserva o seu espaço. A ordem de avaliação coloca o pai
        // antes do filho, então em um aglomerado o planeta ganha do satélite — que é a
        // prioridade certa quando o sistema está distante e tudo se sobrepõe.
        var taken = new List<Rect2>(_entries.Count);

        foreach (var entry in _entries)
        {
            entry.Label.Visible = false;

            if (!_visible
                || !frame.RenderPositions.TryGetValue(entry.BodyId, out var world)
                || !bridge.IsBodyVisible(entry.BodyId))
            {
                continue;
            }

            var size = entry.Label.GetMinimumSize();
            var position = camera.UnprojectPosition(world)
                + new Vector2((entry.RadiusPixels * pixelsPerUnit) + Margin, -size.Y / 2.0f);

            var box = new Rect2(position, size);

            if (!screen.Encloses(box) || taken.Any(other => other.Intersects(box)))
            {
                continue;
            }

            taken.Add(box);
            entry.Label.Position = position;
            entry.Label.Visible = true;
        }
    }

    private Rect2 FreeArea(Rect2 screen) => new(
        screen.Position + new Vector2(ReservedLeft, 0.0f),
        screen.Size - new Vector2(ReservedLeft + ReservedRight, ReservedBottom));

    private sealed record Entry(string BodyId, Label Label, float RadiusPixels);
}
