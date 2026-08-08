using Godot;
using SolarSim.Bridge;

namespace SolarSim.UI;

/// <summary>
/// Overlay de ensino: explica o relatório ambiental do corpo ancorado. Sem estado de
/// simulação — só consulta a fachada.
/// </summary>
public partial class TeachingHud : CanvasLayer
{
    private const double RefreshSeconds = 0.25;

    private SimBridge? _bridge;
    private PanelContainer _box = null!;
    private RichTextLabel _body = null!;
    private bool _visible;
    private double _sinceRefresh = RefreshSeconds;

    public void Attach(SimBridge bridge)
    {
        _bridge = bridge;

        _box = Panels.Box();
        _box.Visible = false;
        _box.AnchorLeft = 0.5f;
        _box.AnchorRight = 0.5f;
        _box.AnchorTop = 0.0f;
        _box.AnchorBottom = 0.0f;
        _box.OffsetLeft = -280.0f;
        _box.OffsetRight = 280.0f;
        _box.OffsetTop = Panels.Margin;
        _box.OffsetBottom = 280.0f;
        AddChild(_box);

        var column = new VBoxContainer();
        _box.AddChild(column);
        column.AddChild(Panels.Title("ENSINO / ANÁLISE"));
        column.AddChild(new HSeparator());

        _body = new RichTextLabel
        {
            FitContent = true,
            ScrollActive = true,
            BbcodeEnabled = false,
            CustomMinimumSize = new Vector2(0.0f, 200.0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        _body.AddThemeFontSizeOverride("normal_font_size", Panels.FontSize);
        _body.AddThemeColorOverride("default_color", Panels.Bright);
        column.AddChild(_body);

        Refresh();
    }

    public void Toggle()
    {
        _visible = !_visible;
        _box.Visible = _visible;
        if (_visible)
        {
            Refresh();
        }
    }

    public override void _Process(double delta)
    {
        if (_bridge is null || !_visible)
        {
            return;
        }

        _sinceRefresh += delta;
        if (_sinceRefresh < RefreshSeconds)
        {
            return;
        }

        _sinceRefresh = 0.0;
        Refresh();
    }

    private void Refresh()
    {
        if (_bridge?.AnchorBodyId is not { } bodyId)
        {
            _body.Text = "Ancore um corpo para ver a análise ambiental.";
            return;
        }

        var report = _bridge.EnvironmentFor(bodyId);
        var lines = new List<string>(TeachingExplain.LinesFor(report));

        if (bodyId != "earth"
            && bodyId != "sun"
            && !report.ExplanationFlags.Contains("stellar_body")
            && _bridge.Bodies.Any(b => b.Id == "earth"))
        {
            var earth = _bridge.EnvironmentFor("earth");
            lines.AddRange(TeachingExplain.CompareToEarth(report, earth));
        }

        _body.Text = string.Join("\n", lines);
    }
}
