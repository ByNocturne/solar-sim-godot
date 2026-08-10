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

    /// <summary>
    /// Tempo de voo Hohmann Terra→Marte aproximado, em dias. É o valor clássico da
    /// literatura (~259 d) e o que o preview de teclado usa.
    /// </summary>
    private const double EarthMarsHohmannDays = 259.0;

    /// <summary>
    /// Fração da velocidade de escape com que cada sonda parte. A primeira fica em órbita
    /// fechada; a segunda sai da esfera de influência e é entregue ao corpo de cima.
    /// </summary>
    private const double OrbitFactor = 0.7;

    private const double EscapeFactor = 1.15;

    /// <summary>Por quanto tempo o aviso da última ação fica na tela, em segundos.</summary>
    private const double NoticeSeconds = 4.0;

    private SimBridge? _bridge;

    private Label _clock = null!;
    private Label _julianDate = null!;
    private Label _rate = null!;
    private Label _view = null!;
    private Button _pause = null!;
    private LineEdit _dateEntry = null!;
    private PanelContainer _help = null!;
    private Label _notice = null!;

    // Conta o tempo restante do aviso. É estado da tela, e não da simulação: nada aqui
    // seria consultável no motor.
    private double _noticeSeconds;

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
            + $"     {Godot.Engine.GetFramesPerSecond()} fps     H: ajuda     I: ensino";

        if (_noticeSeconds > 0.0)
        {
            _noticeSeconds -= delta;

            if (_noticeSeconds <= 0.0)
            {
                _notice.Text = string.Empty;
            }
        }
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

            case Key.I:
                GetParent()?.GetNodeOrNull<TeachingHud>("TeachingHud")?.Toggle();
                break;

            case Key.K:
                _bridge.ToggleSurfaceSky();
                break;

            case Key.P:
                Launch(key.ShiftPressed ? EscapeFactor : OrbitFactor);
                break;

            case Key.T:
                if (key.ShiftPressed)
                {
                    ApplyEarthMarsDeparture();
                }
                else
                {
                    PreviewEarthMars();
                }

                break;

            case Key.Delete:
                DiscardProbe();
                break;

            case Key.F5:
                Save();
                break;

            case Key.F9:
                Load();
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
        // Sem folga, a terceira linha (sonda) colava na margem e parecia cortada.
        rows.AddThemeConstantOverride("separation", 6);
        box.AddChild(rows);

        rows.AddChild(BuildStatusRow());
        rows.AddChild(BuildCommandRow());
        rows.AddChild(BuildMissionRow());
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

    private HBoxContainer BuildMissionRow()
    {
        var row = new HBoxContainer();

        row.AddChild(Panels.Caption("Sonda"));
        row.AddChild(Panels.Command("Em órbita", () => Launch(OrbitFactor)));
        row.AddChild(Panels.Command("Em fuga", () => Launch(EscapeFactor)));
        row.AddChild(Panels.Command("Descartar", DiscardProbe));

        row.AddChild(new VSeparator());
        row.AddChild(Panels.Caption("Terra→Marte"));
        row.AddChild(Panels.Command("Δv", PreviewEarthMars));
        row.AddChild(Panels.Command("Partida", ApplyEarthMarsDeparture));

        row.AddChild(new VSeparator());
        row.AddChild(Panels.Command("Salvar", Save));
        row.AddChild(Panels.Command("Carregar", Load));

        _notice = Panels.Value(string.Empty);
        _notice.HorizontalAlignment = HorizontalAlignment.Right;
        _notice.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(_notice);

        return row;
    }

    /// <summary>
    /// Solta uma sonda a partir do corpo ancorado. A órbita não é escolhida: ela sai do
    /// vetor de estado com que a sonda parte, e é por isso que a mesma tecla dá uma
    /// elipse ou uma hipérbole conforme a velocidade.
    /// </summary>
    private void Launch(double escapeFactor)
    {
        if (_bridge is null)
        {
            return;
        }

        if (_bridge.LaunchProbe(escapeFactor) is null)
        {
            Notify("Ancore em um corpo com massa para soltar uma sonda.");
            return;
        }

        Notify($"{_bridge.AnchorName} em rota a partir de "
            + $"{_bridge.ReportFor(_bridge.AnchorBodyId!).ParentName}.");
    }

    private void DiscardProbe()
    {
        if (_bridge is null)
        {
            return;
        }

        Notify(_bridge.RemoveAnchoredBody()
            ? "Sonda descartada."
            : "Só é possível descartar uma sonda, e é preciso estar ancorado nela.");
    }

    /// <summary>
    /// Consulta o Δv Terra→Marte na data atual. Não altera a simulação.
    /// </summary>
    private void PreviewEarthMars()
    {
        if (_bridge is null)
        {
            return;
        }

        if (_bridge.BestTransferPreview("earth", "mars", EarthMarsHohmannDays) is not { } preview)
        {
            Notify("Sem solução Lambert Terra→Marte nesta data/ToF.");
            return;
        }

        Notify(
            $"Terra→Marte (ToF fixo {DisplayFormat.Duration(preview.TimeOfFlightDays)}): "
                + $"Δv {DisplayFormat.Speed(preview.TotalDeltaVKmS)} "
                + $"(partida {DisplayFormat.Speed(preview.DepartureDeltaVKmS)}, "
                + $"chegada {DisplayFormat.Speed(preview.ArrivalDeltaVKmS)}).");
    }

    /// <summary>
    /// Aplica o Δv de partida do preview à sonda ancorada no Sol.
    /// </summary>
    private void ApplyEarthMarsDeparture()
    {
        if (_bridge is null)
        {
            return;
        }

        if (_bridge.BestTransferPreview("earth", "mars", EarthMarsHohmannDays) is not { } preview)
        {
            Notify("Sem solução Lambert para aplicar.");
            return;
        }

        var result = _bridge.ApplyTransferDeparture(preview);
        Notify(result.Message);
    }

    private void Save()
    {
        if (_bridge is null)
        {
            return;
        }

        _bridge.Save();
        Notify($"Gravado: {_bridge.DynamicBodyCount} sonda(s) e a data.");
    }

    private void Load()
    {
        if (_bridge is null)
        {
            return;
        }

        Notify(_bridge.Load()
            ? $"Carregado: {_bridge.DynamicBodyCount} sonda(s) e a data."
            : "Não há nada gravado para carregar.");
    }

    /// <summary>Aviso temporário na barra (missões e smoke do Movie Maker).</summary>
    public void ShowNotice(string message) => Notify(message);

    private void Notify(string message)
    {
        _notice.Text = message;
        _noticeSeconds = NoticeSeconds;
    }

    private void BuildHelp()
    {
        _help = Panels.Box();
        Panels.AnchorCenter(_help, 440.0f, 420.0f);
        _help.Visible = false;
        AddChild(_help);

        var lines = new VBoxContainer();
        _help.AddChild(lines);

        lines.AddChild(Panels.Title("Controles"));

        foreach (var line in ControlCatalog.HelpLines)
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
