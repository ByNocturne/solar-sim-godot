using Godot;
using SolarSim.Bridge;

namespace SolarSim.UI;

/// <summary>
/// Coreografia só no Movie Maker: ancora Fobos, Bennu, solta sonda e mostra Δv
/// Terra→Marte, para os frames do smoke cobrirem os alvos da auditoria.
/// </summary>
/// <remarks>
/// Não roda no jogo normal. <see cref="OS.HasFeature"/> "movie" liga só com
/// <c>--write-movie</c>. O tempo da simulação fica pausado para o inspetor não piscar.
/// Attach tem de rodar <b>antes</b> de AddChild: o Godot chama _Ready ao entrar na árvore.
/// </remarks>
public partial class MovieSmokeDriver : Node
{
    private const double StepSeconds = 1.0;

    private const double EarthMarsHohmannDays = 259.0;

    private SimBridge? _bridge;

    private TimeControls? _controls;

    private double _elapsed;

    private int _step = -1;

    private bool _started;

    public void Attach(SimBridge bridge, TimeControls controls)
    {
        _bridge = bridge;
        _controls = controls;
    }

    public override void _Ready()
    {
        if (!OS.HasFeature("movie") || _bridge is null || _controls is null)
        {
            SetProcess(false);
            return;
        }

        Start();
    }

    public override void _Process(double delta)
    {
        if (!_started || _bridge is null || _controls is null)
        {
            return;
        }

        _elapsed += delta;
        var step = (int)(_elapsed / StepSeconds);

        if (step == _step || step > 3)
        {
            return;
        }

        _step = step;
        ApplyStep(step);
    }

    private void Start()
    {
        if (_started || _bridge is null)
        {
            return;
        }

        _started = true;

        if (!_bridge.IsPaused)
        {
            _bridge.TogglePause();
        }

        _bridge.SetSpeed(1.0);
        ApplyStep(0);
        _step = 0;
    }

    private void ApplyStep(int step)
    {
        if (_bridge is null || _controls is null)
        {
            return;
        }

        switch (step)
        {
            case 0:
                _bridge.AnchorTo("phobos");
                _controls.ShowNotice("Smoke: Fobos — maré / Roche no inspetor");
                break;

            case 1:
                _bridge.AnchorTo("bennu");
                _controls.ShowNotice("Smoke: Bennu — drift Yarkovsky no inspetor");
                break;

            case 2:
                _bridge.AnchorTo("earth");
                _ = _bridge.LaunchProbe(0.7);
                _controls.ShowNotice("Smoke: sonda em órbita (rótulo na tela)");
                break;

            case 3:
                if (_bridge.BestTransferPreview("earth", "mars", EarthMarsHohmannDays) is { } preview)
                {
                    _controls.ShowNotice(
                        $"Smoke: Terra→Marte Δv {DisplayFormat.Speed(preview.TotalDeltaVKmS)}");
                }
                else
                {
                    _controls.ShowNotice("Smoke: sem solução Lambert Terra→Marte");
                }

                break;
        }
    }
}
