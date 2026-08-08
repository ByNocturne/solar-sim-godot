using Godot;
using SolarSim.Engine;
using SolarSim.Engine.Data;
using SolarSim.Engine.Models;
using SolarSim.Render;
using SolarSim.UI;

namespace SolarSim.Bridge;

/// <summary>Posições já convertidas para a tela, prontas para consumo pelos nós.</summary>
public readonly record struct RenderFrame(
    double JulianDate,
    IReadOnlyDictionary<string, Vector2> ScreenPositions);

/// <summary>
/// Único nó do Godot que conhece o motor. Avança o tempo, converte o snapshot em
/// coordenadas de tela e publica um quadro para quem estiver escutando.
/// </summary>
public partial class SimBridge : Node2D
{
    private readonly Dictionary<string, Vector2> _screenPositions = new(StringComparer.Ordinal);

    private SimEngine _sim = null!;
    private ViewportTransformer _transformer = null!;

    public event Action<RenderFrame>? FrameReady;

    public SimEngine Sim => _sim;

    public override void _Ready()
    {
        _transformer = new ViewportTransformer(new ScaleMapper());
        _sim = new SimEngine(new HardcodedBodyRepository());

        // Cerca de 23 dias por segundo real: uma volta da Terra em ~16 segundos.
        _sim.Time.SpeedMultiplier = 2_000_000.0;

        BuildSceneTree();

        _sim.SystemUpdated += OnSystemUpdated;
    }

    public override void _Process(double delta) => _sim.Advance(delta);

    /// <summary>
    /// A árvore é montada em código em vez de em cenas .tscn porque a decisão entre 2D e
    /// 3D só acontece no M6: assim não há arquivos de cena para refazer.
    /// </summary>
    private void BuildSceneTree()
    {
        AddChild(new SpaceCamera { Name = "SpaceCamera" });

        foreach (var body in _sim.Bodies)
        {
            var node = new CelestialBodyNode
            {
                Name = body.Id,
                BodyId = body.Id,
                BodyColor = ToGodotColor(body.ColorRgb),
                DisplayRadius = (float)_transformer.Scale.BodyRadiusPixels(body.RadiusKm),
            };

            AddChild(node);
            node.Attach(this);
        }

        var controls = new TimeControls { Name = "TimeControls" };
        AddChild(controls);
        controls.Attach(this);
    }

    private void OnSystemUpdated(SystemStateSnapshot snapshot)
    {
        for (var index = 0; index < snapshot.Bodies.Count; index++)
        {
            var state = snapshot.Bodies[index];
            _screenPositions[state.Id] = _transformer.ToScreen(state.PositionKm);
        }

        FrameReady?.Invoke(new RenderFrame(snapshot.JulianDate, _screenPositions));
    }

    private static Color ToGodotColor(uint rgb) => new(
        ((rgb >> 16) & 0xFF) / 255.0f,
        ((rgb >> 8) & 0xFF) / 255.0f,
        (rgb & 0xFF) / 255.0f);
}
