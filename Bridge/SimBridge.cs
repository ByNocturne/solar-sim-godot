using Godot;
using SolarSim.Engine;
using SolarSim.Engine.Core;
using SolarSim.Engine.Data;
using SolarSim.Engine.Models;
using SolarSim.Render;
using SolarSim.UI;

namespace SolarSim.Bridge;

/// <summary>Posições já convertidas para a tela, prontas para consumo pelos nós.</summary>
/// <param name="ScaleRevision">
/// Muda quando a escala muda. Quem mantém geometria em cache — o desenho das órbitas —
/// compara este número em vez de recalcular para descobrir se o cache venceu.
/// </param>
public readonly record struct RenderFrame(
    double JulianDate,
    IReadOnlyDictionary<string, Vector2> ScreenPositions,
    int ScaleRevision);

/// <summary>
/// Único nó do Godot que conhece o motor. Avança o tempo, projeta o snapshot em pixels,
/// posiciona a câmera e publica um quadro para quem estiver escutando.
/// </summary>
public partial class SimBridge : Node2D
{
    /// <summary>Limites do multiplicador de tempo, em módulo.</summary>
    private const double MinSpeedMultiplier = 1.0;

    private const double MaxSpeedMultiplier = 1.0e9;

    private const int OrbitSamples = 240;

    private readonly Dictionary<string, Vector2> _screenPositions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string?> _parents = new(StringComparer.Ordinal);
    private readonly CameraRig _rig = new();

    private SimEngine _sim = null!;
    private SystemProjector _projector = null!;
    private ViewportTransformer _transformer = null!;

    // O avanço do motor é síncrono: publicar o snapshot acontece dentro de _Process, e a
    // câmera precisa do mesmo delta para andar com a transição.
    private double _frameDelta;

    public event Action<RenderFrame>? FrameReady;

    // Não se chama Scale porque Node2D já tem uma propriedade com esse nome.
    public ScaleMapper ScaleMap => _projector.Mapper;

    /// <summary>Corpos em ordem de avaliação: o pai sempre antes do filho.</summary>
    public IReadOnlyList<CelestialBodyData> Bodies => _sim.Bodies;

    public string? AnchorBodyId => _rig.AnchorBodyId;

    /// <summary>Nome legível do corpo ancorado, para exibição.</summary>
    public string AnchorName
        => _rig.AnchorBodyId is { } id ? _sim.BodyOf(id).Name : "origem";

    public bool IsPaused => _sim.Time.IsPaused;

    /// <summary>Segundos simulados por segundo real. Negativo faz o tempo correr para trás.</summary>
    public double SpeedMultiplier => _sim.Time.SpeedMultiplier;

    public double JulianDate => _sim.Time.JulianDate;

    public DateTime UtcDateTime => _sim.Time.UtcDateTime;

    public override void _Ready()
    {
        _sim = new SimEngine(LoadRepository());

        _projector = new SystemProjector(new ScaleLayout(_sim.Bodies), new ScaleMapper());
        _transformer = new ViewportTransformer();

        foreach (var body in _sim.Bodies)
        {
            _parents[body.Id] = body.ParentId;
        }

        _rig.AnchorTo(_sim.Root.Id);

        // Cerca de 23 dias por segundo real: uma volta da Terra em ~16 segundos.
        _sim.Time.SpeedMultiplier = 2_000_000.0;

        BuildSceneTree();

        _sim.SystemUpdated += OnSystemUpdated;
    }

    public override void _Process(double delta)
    {
        ScaleMap.Advance(delta);
        _frameDelta = delta;
        _sim.Advance(delta);
    }

    /// <summary>
    /// Deslocamento manual da câmera. Recebe pixels de tela e desconta o zoom, para que
    /// arrastar mova sempre a mesma distância sob o cursor.
    /// </summary>
    public void PanByScreenPixels(Vector2 deltaScreen, float zoom)
        => _rig.Pan(new Vector3D(-deltaScreen.X / zoom, deltaScreen.Y / zoom, 0.0));

    /// <summary>Ancora a câmera em um corpo, ou na origem do sistema se nulo.</summary>
    public void AnchorTo(string? bodyId) => _rig.AnchorTo(bodyId);

    /// <summary>Ancora no próximo corpo da ordem de avaliação, ou no anterior.</summary>
    public void CycleAnchor(int direction)
    {
        var bodies = _sim.Bodies;
        var current = 0;

        for (var index = 0; index < bodies.Count; index++)
        {
            if (bodies[index].Id == _rig.AnchorBodyId)
            {
                current = index;
                break;
            }
        }

        var next = (((current + direction) % bodies.Count) + bodies.Count) % bodies.Count;

        _rig.AnchorTo(bodies[next].Id);
    }

    public void TogglePause() => _sim.Time.IsPaused = !_sim.Time.IsPaused;

    /// <summary>
    /// Multiplica a velocidade do tempo preservando o sentido em que ele corre. Acelerar
    /// com o tempo invertido acelera para trás, que é o que se espera.
    /// </summary>
    public void ScaleSpeed(double factor)
        => SetSpeed(Math.Abs(_sim.Time.SpeedMultiplier) * factor);

    /// <summary>Fixa o módulo da velocidade, preservando o sentido.</summary>
    public void SetSpeed(double magnitude)
    {
        var clamped = Math.Clamp(
            Math.Abs(magnitude), MinSpeedMultiplier, MaxSpeedMultiplier);

        _sim.Time.SpeedMultiplier = _sim.Time.SpeedMultiplier < 0.0 ? -clamped : clamped;
    }

    /// <summary>
    /// Inverte o sentido do tempo. Sai de graça do invariante 4: como o estado é função
    /// pura da data, andar para trás é só diminuir a data.
    /// </summary>
    public void ReverseTime() => _sim.Time.SpeedMultiplier = -_sim.Time.SpeedMultiplier;

    public void ResetToEpoch() => _sim.Time.ResetToEpoch();

    public void JumpTo(DateTime utc) => _sim.Time.JumpTo(utc);

    /// <summary>
    /// Retrato do corpo no instante corrente, montado por consulta ao motor. Cada
    /// chamada devolve valores novos: quem exibe não guarda nada.
    /// </summary>
    public BodyReport ReportFor(string bodyId)
        => BodyReport.For(_sim, bodyId, _sim.Time.JulianDate);

    /// <summary>
    /// Corpo mais próximo de um ponto do mundo, dentro do raio informado. Devolve nulo se
    /// o clique caiu no vazio, para que clicar no fundo não desancore por acidente.
    /// </summary>
    public string? NearestBody(Vector2 worldPosition, float maxDistancePixels)
    {
        string? closest = null;
        var closestDistance = maxDistancePixels;

        foreach (var (id, position) in _screenPositions)
        {
            var distance = position.DistanceTo(worldPosition);

            if (distance <= closestDistance)
            {
                closest = id;
                closestDistance = distance;
            }
        }

        return closest;
    }

    /// <summary>
    /// Amostra uma órbita completa em coordenadas relativas ao pai, em km. Fixa no tempo:
    /// os elementos não mudam, então isso é calculado uma vez por corpo.
    /// </summary>
    public Vector3D[] SampleOrbitKm(string bodyId)
    {
        if (_sim.ElementsOf(bodyId) is not { } elements)
        {
            return [];
        }

        var periodDays = KeplerPropagator.OrbitalPeriodDays(
            elements.SemiMajorAxisKm, _sim.GravitationalParameterOf(bodyId));

        var samples = new Vector3D[OrbitSamples];

        for (var index = 0; index < OrbitSamples; index++)
        {
            var julianDate = AstroConstants.J2000 + (periodDays * index / OrbitSamples);
            samples[index] = _sim.LocalStateAt(bodyId, julianDate).PositionKm;
        }

        return samples;
    }

    /// <summary>Converte uma amostra local em km para o deslocamento em pixels.</summary>
    public Vector2 OrbitSampleToPixels(Vector3D localKm, string parentId)
    {
        var scaled = ScaleMap.ToPixels(localKm, _projector.Layout.LevelOf(parentId));

        return new Vector2((float)scaled.X, (float)-scaled.Y);
    }

    /// <summary>
    /// Ler o arquivo é responsabilidade da Bridge porque, no jogo exportado, os dados
    /// vivem dentro do pacote e só o <c>FileAccess</c> do Godot sabe abri-los. O motor
    /// recebe apenas o texto.
    /// </summary>
    private static IBodyRepository LoadRepository()
    {
        var path = $"res://{JsonBodyRepository.DefaultRelativePath}";

        // Qualificado porque System.IO.FileAccess também está no escopo.
        using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);

        if (file is null)
        {
            throw new SystemDataException(
                $"Não foi possível abrir '{path}': {Godot.FileAccess.GetOpenError()}.");
        }

        return JsonBodyRepository.FromJson(file.GetAsText(), path);
    }

    /// <summary>
    /// A árvore é montada em código em vez de em cenas .tscn porque a decisão entre 2D e
    /// 3D só acontece no M6: assim não há arquivos de cena para refazer.
    /// </summary>
    private void BuildSceneTree()
    {
        // As órbitas entram antes dos corpos para ficarem atrás deles no desenho.
        foreach (var body in _sim.Bodies.Where(body => body.ParentId is not null))
        {
            var orbit = new OrbitLineRenderer
            {
                Name = $"{body.Id}_orbit",
                BodyId = body.Id,
                ParentBodyId = body.ParentId!,
                LineColor = BodyPalette.Of(body.ColorRgb) with { A = 0.35f },
            };

            AddChild(orbit);
            orbit.Attach(this);
        }

        foreach (var body in _sim.Bodies)
        {
            var node = new CelestialBodyNode
            {
                Name = body.Id,
                BodyId = body.Id,
                BodyColor = BodyPalette.Of(body.ColorRgb),
                DisplayRadius = (float)ScaleMap.BodyRadiusPixels(body.RadiusKm),
            };

            AddChild(node);
            node.Attach(this);
        }

        var camera = new SpaceCamera { Name = "SpaceCamera" };
        AddChild(camera);
        camera.Attach(this);

        // A ponte é quem monta a cena, e por isso é quem sabe ao mesmo tempo que existem
        // painéis e que existem rótulos. Nenhum dos dois lados precisa saber do outro.
        var labels = new BodyLabels
        {
            Name = "BodyLabels",
            ReservedLeft = Panels.ReservedLeft,
            ReservedRight = Panels.ReservedRight,
            ReservedBottom = Panels.ReservedBottom,
        };

        AddChild(labels);
        labels.Attach(this);

        var controls = new TimeControls { Name = "TimeControls" };
        AddChild(controls);
        controls.Attach(this);

        var tree = new SystemTree { Name = "SystemTree" };
        AddChild(tree);
        tree.Attach(this);

        var inspector = new InspectorPanel { Name = "InspectorPanel" };
        AddChild(inspector);
        inspector.Attach(this);
    }

    private void OnSystemUpdated(SystemStateSnapshot snapshot)
    {
        _projector.Project(snapshot, _parents);

        _rig.Advance(
            _frameDelta,
            _rig.AnchorBodyId is { } anchor ? _projector.PositionOf(anchor) : Vector3D.Zero);

        _transformer.FocusPixels = _rig.FocusPixels;

        for (var index = 0; index < snapshot.Bodies.Count; index++)
        {
            var state = snapshot.Bodies[index];
            _screenPositions[state.Id] = _transformer.ToScreen(_projector.PositionOf(state.Id));
        }

        FrameReady?.Invoke(
            new RenderFrame(snapshot.JulianDate, _screenPositions, ScaleMap.Revision));
    }
}
