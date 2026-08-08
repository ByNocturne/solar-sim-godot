using Godot;
using SolarSim.Bridge;

namespace SolarSim.UI;

/// <summary>
/// Controle de tempo improvisado do M1, hoje também servindo de HUD para a vista. O M5
/// separa isso em painéis de verdade, com slider, entrada de data e árvore de corpos.
/// </summary>
public partial class TimeControls : CanvasLayer
{
    private const double MinSpeed = 1.0;
    private const double MaxSpeed = 1.0e9;

    private Label _status = null!;
    private SimBridge? _bridge;

    public void Attach(SimBridge bridge) => _bridge = bridge;

    public override void _Ready()
    {
        _status = new Label
        {
            Position = new Vector2(16.0f, 12.0f),
            Text = string.Empty,
        };

        AddChild(_status);
    }

    public override void _Process(double delta)
    {
        if (_bridge is null)
        {
            return;
        }

        var time = _bridge.Sim.Time;
        var estado = time.IsPaused ? "PAUSADO" : "RODANDO";
        var escala = _bridge.ScaleMap.Mode == ScaleMode.Logarithmic ? "logarítmica" : "linear";

        _status.Text = $"""
            {time.UtcDateTime:yyyy-MM-dd HH:mm} UTC   [{estado}]   {Godot.Engine.GetFramesPerSecond()} fps
            Velocidade: {time.SpeedMultiplier:N0}x tempo real
            JD {time.JulianDate:F3}
            Escala: {escala}   Âncora: {_bridge.AnchorName}

            Espaço: pausa   Setas: velocidade   R: volta a J2000
            Tab: ancora no corpo seguinte   Shift+Tab: no anterior   Clique: no corpo
            L: escala   N: nomes   Home: vista inicial
            Roda: zoom   Botão direito: arrasta
            """;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_bridge is null || @event is not InputEventKey { Pressed: true, Echo: false } key)
        {
            return;
        }

        var time = _bridge.Sim.Time;

        switch (key.Keycode)
        {
            case Key.Space:
                time.IsPaused = !time.IsPaused;
                break;

            case Key.Right or Key.Up:
                time.SpeedMultiplier = Math.Clamp(time.SpeedMultiplier * 2.0, MinSpeed, MaxSpeed);
                break;

            case Key.Left or Key.Down:
                time.SpeedMultiplier = Math.Clamp(time.SpeedMultiplier / 2.0, MinSpeed, MaxSpeed);
                break;

            case Key.R:
                time.ResetToEpoch();
                break;

            default:
                return;
        }

        GetViewport().SetInputAsHandled();
    }
}
