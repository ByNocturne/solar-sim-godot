using System.Globalization;
using Godot;
using SolarSim.Bridge;

namespace SolarSim.UI;

/// <summary>
/// Barra de tempo: estado do relógio, controle de velocidade e salto para uma data
/// arbitrária, com os mesmos comandos disponíveis por atalho de teclado.
/// </summary>
/// <remarks>
/// Nenhum valor mostrado aqui é guardado: a cada quadro os rótulos são reescritos a
/// partir do relógio do motor. Um campo que lembrasse a última data exibida ficaria
/// desatualizado no instante em que qualquer outro caminho — o atalho, o clique, o
/// tempo correndo — mudasse o relógio.
/// </remarks>
public partial class TimeControls : CanvasLayer
{
    /// <summary>Presets de velocidade, em segundos simulados por segundo real.</summary>
    private static readonly double[] SpeedPresets = [1.0, 1.0e3, 1.0e5, 1.0e7];

    private static readonly string[] HelpLines =
    [
        "Espaço: pausa e retoma",
        "Setas: dobra e divide a velocidade",
        "R: volta para J2000",
        "Tab / Shift+Tab: ancora no corpo seguinte ou anterior",
        "Clique: ancora no corpo apontado",
        "L: alterna escala logarítmica e linear",
        "N: mostra ou esconde os nomes",
        "Home: devolve a vista inicial",
        "Roda: zoom     Botão direito: arrasta",
        "H: mostra ou esconde esta ajuda",
    ];

    private SimBridge? _bridge;

    private Label _clock = null!;
    private Label _julianDate = null!;
    private Label _rate = null!;
    private Label _view = null!;
    private Button _pause = null!;
    private LineEdit _dateEntry = null!;
    private PanelContainer _help = null!;

    public void Attach(SimBridge bridge) => _bridge = bridge;

    public override void _Ready()
    {
        BuildBar();
        BuildHelp();
    }

    public override void _Process(double delta)
    {
        if (_bridge is null)
        {
            return;
        }

        _clock.Text = _bridge.UtcDateTime.ToString(
            "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC";

        _julianDate.Text = DisplayFormat.JulianDate(_bridge.JulianDate);

        _rate.Text = _bridge.IsPaused
            ? "pausado"
            : DisplayFormat.TimeRate(_bridge.SpeedMultiplier)
                + $"  ({DisplayFormat.Multiplier(_bridge.SpeedMultiplier)})";

        _pause.Text = _bridge.IsPaused ? "Retomar" : "Pausar";

        var scale = _bridge.ScaleMap.Mode == ScaleMode.Logarithmic ? "logarítmica" : "linear";

        _view.Text = $"Escala {scale}     Âncora: {_bridge.AnchorName}"
            + $"     {Godot.Engine.GetFramesPerSecond()} fps     H: ajuda";
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_bridge is null || @event is not InputEventKey { Pressed: true, Echo: false } key)
        {
            return;
        }

        switch (key.Keycode)
        {
            case Key.Space:
                _bridge.TogglePause();
                break;

            case Key.Right or Key.Up:
                _bridge.ScaleSpeed(2.0);
                break;

            case Key.Left or Key.Down:
                _bridge.ScaleSpeed(0.5);
                break;

            case Key.R:
                _bridge.ResetToEpoch();
                break;

            case Key.H:
                _help.Visible = !_help.Visible;
                break;

            default:
                return;
        }

        GetViewport().SetInputAsHandled();
    }

    private void BuildBar()
    {
        var box = Panels.Box();
        Panels.AnchorBottomBar(box);
        AddChild(box);

        var rows = new VBoxContainer();
        box.AddChild(rows);

        rows.AddChild(BuildStatusRow());
        rows.AddChild(BuildCommandRow());
    }

    private HBoxContainer BuildStatusRow()
    {
        var row = new HBoxContainer();

        _clock = Panels.Title("—");
        row.AddChild(_clock);

        _julianDate = Panels.Caption("—");
        row.AddChild(Spacer(16.0f));
        row.AddChild(_julianDate);

        _rate = Panels.Value("—");
        row.AddChild(Spacer(16.0f));
        row.AddChild(_rate);

        _view = Panels.Caption("—");
        _view.HorizontalAlignment = HorizontalAlignment.Right;
        _view.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(_view);

        return row;
    }

    private HBoxContainer BuildCommandRow()
    {
        var row = new HBoxContainer();

        _pause = Panels.Command("Pausar", () => _bridge?.TogglePause());

        row.AddChild(Panels.Command("/2", () => _bridge?.ScaleSpeed(0.5)));
        row.AddChild(_pause);
        row.AddChild(Panels.Command("x2", () => _bridge?.ScaleSpeed(2.0)));
        row.AddChild(Panels.Command("Inverter", () => _bridge?.ReverseTime()));

        row.AddChild(new VSeparator());

        foreach (var preset in SpeedPresets)
        {
            var magnitude = preset;
            row.AddChild(Panels.Command(
                DisplayFormat.Multiplier(magnitude), () => _bridge?.SetSpeed(magnitude)));
        }

        row.AddChild(new VSeparator());
        row.AddChild(Panels.Command("J2000", () => _bridge?.ResetToEpoch()));

        row.AddChild(Spacer(8.0f));
        row.AddChild(Panels.Caption("Ir para"));

        _dateEntry = new LineEdit
        {
            PlaceholderText = "AAAA-MM-DD",
            CustomMinimumSize = new Vector2(120.0f, 0.0f),
        };

        _dateEntry.AddThemeFontSizeOverride("font_size", Panels.FontSize);
        _dateEntry.TextSubmitted += _ => JumpToTypedDate();

        row.AddChild(_dateEntry);
        row.AddChild(Panels.Command("Ir", JumpToTypedDate));

        return row;
    }

    private void BuildHelp()
    {
        _help = Panels.Box();
        Panels.AnchorCenter(_help, 400.0f, 252.0f);
        _help.Visible = false;
        AddChild(_help);

        var lines = new VBoxContainer();
        _help.AddChild(lines);

        lines.AddChild(Panels.Title("Controles"));

        foreach (var line in HelpLines)
        {
            lines.AddChild(Panels.Caption(line));
        }
    }

    /// <summary>
    /// Salta para a data digitada. O texto é interpretado como UTC: a simulação não tem
    /// fuso, e assumir o do computador faria a mesma entrada cair em instantes
    /// diferentes conforme quem digita.
    /// </summary>
    private void JumpToTypedDate()
    {
        if (_bridge is null)
        {
            return;
        }

        if (!DateTime.TryParse(
                _dateEntry.Text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out var utc))
        {
            _dateEntry.AddThemeColorOverride("font_color", Panels.Alert);
            return;
        }

        _dateEntry.RemoveThemeColorOverride("font_color");
        _dateEntry.ReleaseFocus();
        _bridge.JumpTo(utc);
    }

    private static Control Spacer(float width)
        => new() { CustomMinimumSize = new Vector2(width, 0.0f) };
}
