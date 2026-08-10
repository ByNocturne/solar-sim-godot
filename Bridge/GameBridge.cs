using Godot;
using SolarSim.Engine;
using SolarSim.Render;

namespace SolarSim.Bridge;

/// <summary>
/// Host do jogo (Era Jogo / J1): céu da Terra sem árvore, inspetor nem órbitas.
/// O simulador continua em <see cref="SimBridge"/> / Main.tscn.
/// </summary>
public partial class GameBridge : Node3D, ISurfaceSkySource
{
    private SimEngine _sim = null!;
    private EnvironmentService _environment = null!;
    private SurfaceSkyView _sky = null!;
    private Label _hud = null!;

    public override void _Ready()
    {
        _sim = new SimEngine(GodotDataFiles.LoadRepository());
        _environment = new EnvironmentService(GodotDataFiles.LoadEnvironments());

        var daylight = EarthSurfaceSky.BestDaylightJulianDate(
            _sim,
            _environment,
            _sim.Time.JulianDate);
        _sim.Time.JumpTo(daylight);
        // ~1 dia por segundo real: o céu anda sem ser um relógio de parede.
        _sim.Time.SpeedMultiplier = 86_400.0;
        _sim.Time.IsPaused = false;

        _sky = new SurfaceSkyView { Name = "SurfaceSkyView" };
        _sky.Attach(this, "Céu da Terra — S abre o simulador · Espaço pausa · arraste com o direito");
        AddChild(_sky);
        _sky.SetActive(true);

        var hudLayer = new CanvasLayer { Name = "GameHud" };
        AddChild(hudLayer);
        _hud = new Label
        {
            Text = FormatHud(),
            OffsetLeft = 16,
            OffsetTop = 12,
            OffsetRight = 720,
            OffsetBottom = 80,
        };
        _hud.AddThemeFontSizeOverride("font_size", 16);
        _hud.AddThemeColorOverride("font_color", new Color(0.85f, 0.9f, 1.0f));
        hudLayer.AddChild(_hud);
    }

    public override void _Process(double delta)
    {
        _sim.Advance(delta);
        _hud.Text = FormatHud();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key)
        {
            return;
        }

        switch (key.Keycode)
        {
            case Key.Space:
                _sim.Time.IsPaused = !_sim.Time.IsPaused;
                break;

            case Key.Right or Key.Up:
                _sim.Time.SpeedMultiplier *= 2.0;
                break;

            case Key.Left or Key.Down:
                _sim.Time.SpeedMultiplier *= 0.5;
                break;

            case Key.S:
                GetTree().ChangeSceneToFile("res://Scenes/Main.tscn");
                break;

            default:
                return;
        }

        GetViewport().SetInputAsHandled();
    }

    public IReadOnlyList<SurfaceSkyMarker> SurfaceSkyMarkers()
        => EarthSurfaceSky.Markers(_sim, _environment, _sim.Time.JulianDate);

    private string FormatHud()
    {
        var pause = _sim.Time.IsPaused ? "pausado" : "correndo";
        return $"{_sim.Time.UtcDateTime:yyyy-MM-dd HH:mm} UTC · {pause} · "
            + $"×{_sim.Time.SpeedMultiplier:0.###}";
    }
}
