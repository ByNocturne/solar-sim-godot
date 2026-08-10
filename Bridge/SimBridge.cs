using Godot;
using SolarSim.Engine;
using SolarSim.Engine.Core;
using SolarSim.Engine.Data;
using SolarSim.Engine.Models;
using SolarSim.Render;
using SolarSim.UI;

namespace SolarSim.Bridge;

/// <summary>
/// Posições já convertidas para o espaço do renderizador, prontas para consumo pelos nós.
/// </summary>
/// <param name="ScaleRevision">
/// Muda quando a escala muda. Quem mantém geometria em cache — o desenho das órbitas —
/// compara este número em vez de recalcular para descobrir se o cache venceu.
/// </param>
public readonly record struct RenderFrame(
    double JulianDate,
    IReadOnlyDictionary<string, Vector3> RenderPositions,
    int ScaleRevision);

/// <summary>
/// Único nó do Godot que conhece o motor. Avança o tempo, projeta o snapshot em pixels,
/// posiciona a câmera e publica um quadro para quem estiver escutando.
/// </summary>
public partial class SimBridge : Node3D
{
    /// <summary>Limites do multiplicador de tempo, em módulo.</summary>
    private const double MinSpeedMultiplier = 1.0;

    private const double MaxSpeedMultiplier = 1.0e9;

    private const int OrbitSamples = 240;

    /// <summary>
    /// Quanto de uma órbita aberta é desenhado, em anomalia verdadeira, como fração do
    /// ângulo da assíntota. A hipérbole vai ao infinito, então o traço tem de parar em
    /// algum lugar; parar um pouco antes da assíntota mostra a curvatura sem gastar
    /// vértices em uma reta.
    /// </summary>
    private const double OpenOrbitSpan = 0.92;

    /// <summary>Onde o jogo salvo é gravado, no diretório de dados do usuário.</summary>
    private const string SavePath = "user://savegame.json";

    private readonly Dictionary<string, Vector3> _renderPositions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string?> _parents = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Node3D> _bodyNodes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, OrbitLineRenderer> _orbitNodes =
        new(StringComparer.Ordinal);

    private readonly CameraRig _rig = new();

    private SimEngine _sim = null!;
    private EnvironmentService _environment = null!;
    private SystemProjector _projector = null!;
    private ViewportTransformer _transformer = null!;
    private BodyFilter _filter = null!;

    // O avanço do motor é síncrono: publicar o snapshot acontece dentro de _Process, e a
    // câmera precisa do mesmo delta para andar com a transição.
    private double _frameDelta;

    public event Action<RenderFrame>? FrameReady;

    /// <summary>
    /// A lista de corpos mudou, ou um deles trocou de pai. Quem espelha a árvore precisa
    /// remontá-la: o evento de quadro carrega posições, e não estrutura.
    /// </summary>
    public event Action? StructureChanged;

    /// <summary>
    /// Potencial biosfera detectada (BHI alto ou assinatura química). Relays do motor
    /// ambiental; a UI de ensino pode escutar sem tocar no <see cref="SimEngine"/>.
    /// </summary>
    public event Action<PotentialBiosphereDetectedEventArgs>? PotentialBiosphereDetected;

    // Não se chama Scale porque Node3D já tem uma propriedade com esse nome.
    public ScaleMapper ScaleMap => _projector.Mapper;

    /// <summary>
    /// A vista deixou de mostrar alguma classe, ou voltou a mostrar. Quem desenha esconde
    /// o que sumiu; a simulação continua igual, porque filtro é assunto de quem olha.
    /// </summary>
    public event Action? FilterChanged;

    /// <summary>Corpos em ordem de avaliação: o pai sempre antes do filho.</summary>
    public IReadOnlyList<CelestialBodyData> Bodies => _sim.Bodies;

    /// <summary>As classes de corpo menor que o catálogo carregado contém.</summary>
    public IReadOnlyList<BodyKind> FilterableKinds => _filter.Available;

    public string? AnchorBodyId => _rig.AnchorBodyId;

    /// <summary>Quantos corpos foram acrescentados em tempo de execução.</summary>
    public int DynamicBodyCount => _sim.DynamicBodyIds.Count;

    /// <summary>Nome legível do corpo ancorado, para exibição.</summary>
    public string AnchorName
        => _rig.AnchorBodyId is { } id ? _sim.BodyOf(id).Name : "origem";

    public bool IsPaused => _sim.Time.IsPaused;

    /// <summary>Segundos simulados por segundo real. Negativo faz o tempo correr para trás.</summary>
    public double SpeedMultiplier => _sim.Time.SpeedMultiplier;

    public double JulianDate => _sim.Time.JulianDate;

    public DateTime UtcDateTime => _sim.Time.UtcDateTime;

    /// <summary>Modo céu da Terra ativo (perspectiva, esfera celeste de raio fixo).</summary>
    public bool IsSurfaceSky => _surfaceSky?.IsActive == true;

    private SurfaceSkyView? _surfaceSky;

    public override void _Ready()
    {
        _sim = new SimEngine(LoadRepository());
        _environment = new EnvironmentService(LoadEnvironments());
        _environment.PotentialBiosphereDetected += args => PotentialBiosphereDetected?.Invoke(args);

        // A escala é calibrada em frações da altura da janela, não em pixels fixos: sem
        // isso, o sistema ocuparia sempre os mesmos 600 pixels no meio de qualquer tela.
        var viewportHeight = GetViewport().GetVisibleRect().Size.Y;
        var heightRatio = viewportHeight / ScaleLayout.ReferenceHeightPixels;

        var mapper = new ScaleMapper
        {
            PixelsPerKm = ScaleMapper.ReferencePixelsPerKm * heightRatio,
        };

        _projector = new SystemProjector(new ScaleLayout(_sim.Bodies, viewportHeight), mapper);
        _transformer = new ViewportTransformer();

        _filter = new BodyFilter(_sim.Bodies);
        _filter.Changed += OnFilterChanged;

        foreach (var body in _sim.Bodies)
        {
            _parents[body.Id] = body.ParentId;
        }

        _rig.AnchorTo(_sim.Root.Id);

        // Cerca de 23 dias por segundo real: uma volta da Terra em ~16 segundos.
        _sim.Time.SpeedMultiplier = 2_000_000.0;

        BuildSceneTree();

        _sim.SystemUpdated += OnSystemUpdated;
        _sim.StructureChanged += OnStructureChanged;
    }

    public override void _Process(double delta)
    {
        ScaleMap.Advance(delta);
        _frameDelta = delta;
        _sim.Advance(delta);
    }

    /// <summary>
    /// Deslocamento manual da câmera, em unidades do espaço projetado. Quem arrasta é a
    /// câmera, e é ela quem sabe converter pixels de tela nos seus próprios eixos — que
    /// mudam a cada rotação.
    /// </summary>
    public void PanBy(Vector3D deltaPixels) => _rig.Pan(deltaPixels);

    /// <summary>Ancora a câmera em um corpo, ou na origem do sistema se nulo.</summary>
    public void AnchorTo(string? bodyId) => _rig.AnchorTo(bodyId);

    /// <summary>
    /// Ancora no próximo corpo da ordem de avaliação, ou no anterior. Pula o que o filtro
    /// esconde: percorrer a tecla Tab por corpos invisíveis seria a câmera parar no vazio.
    /// </summary>
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

        for (var step = 1; step <= bodies.Count; step++)
        {
            var offset = current + (direction * step);
            var next = (((offset % bodies.Count) + bodies.Count) % bodies.Count);

            if (_filter.IsVisible(bodies[next]))
            {
                _rig.AnchorTo(bodies[next].Id);
                return;
            }
        }
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
    /// Alterna o modo céu da Terra. Não usa <see cref="ScaleMapper"/>: só direções locais.
    /// </summary>
    public void ToggleSurfaceSky()
    {
        if (_surfaceSky is null || !_sim.Contains("earth"))
        {
            return;
        }

        if (_surfaceSky.IsActive)
        {
            _surfaceSky.SetActive(false);
            SetOrbitalPipelineVisible(true);
            GetNodeOrNull<SpaceCamera>("SpaceCamera")?.MakeCurrent();
            return;
        }

        AnchorTo("earth");
        SeekDaylightSky();
        SetOrbitalPipelineVisible(false);
        _surfaceSky.SetActive(true);
    }

    /// <summary>
    /// Escolhe o instante (nas próximas 24 h) em que o Sol está mais alto no site padrão,
    /// para o modo céu não abrir sob um horizonte vazio.
    /// </summary>
    private void SeekDaylightSky()
    {
        if (!_environment.TryGetEnvironment("earth", out var profile))
        {
            return;
        }

        var earth = _sim.BodyOf("earth");
        var lat = AstroConstants.DegreesToRadians(LocalSky.DefaultLatitudeDeg);
        var lon = AstroConstants.DegreesToRadians(LocalSky.DefaultLongitudeDeg);
        var start = _sim.Time.JulianDate;
        var bestJd = start;
        var bestEl = double.NegativeInfinity;

        for (var step = 0; step <= 48; step++)
        {
            var jd = start + step / 48.0;
            var observer = LocalSky.ObserverFromEarthCenterKm(
                lat,
                lon,
                jd,
                earth.RadiusKm,
                profile.RotationPeriodSeconds,
                profile.ObliquityRad);
            var coords = LocalSky.Look(
                _sim.PositionAt("sun", jd),
                _sim.PositionAt("earth", jd),
                observer,
                profile.ObliquityRad);

            if (coords.ElevationRad > bestEl)
            {
                bestEl = coords.ElevationRad;
                bestJd = jd;
            }
        }

        _sim.Time.JumpTo(bestJd);
        // Publica um quadro na nova data para quem escuta FrameReady.
        _sim.Advance(0.0);
    }

    /// <summary>
    /// Marcadores acima do horizonte no site padrão, em coordenadas de cena (R×direção ENU).
    /// </summary>
    public IReadOnlyList<SurfaceSkyMarker> SurfaceSkyMarkers()
    {
        if (!_environment.TryGetEnvironment("earth", out var profile))
        {
            return Array.Empty<SurfaceSkyMarker>();
        }

        var earth = _sim.BodyOf("earth");
        var jd = _sim.Time.JulianDate;
        var lat = AstroConstants.DegreesToRadians(LocalSky.DefaultLatitudeDeg);
        var lon = AstroConstants.DegreesToRadians(LocalSky.DefaultLongitudeDeg);
        var observer = LocalSky.ObserverFromEarthCenterKm(
            lat,
            lon,
            jd,
            earth.RadiusKm,
            profile.RotationPeriodSeconds,
            profile.ObliquityRad);
        var earthPos = _sim.PositionAt("earth", jd);

        var markers = new List<SurfaceSkyMarker>();
        foreach (var body in _sim.Bodies)
        {
            if (!IsSurfaceSkyBody(body))
            {
                continue;
            }

            var coords = LocalSky.Look(
                _sim.PositionAt(body.Id, jd),
                earthPos,
                observer,
                profile.ObliquityRad);

            if (!coords.AboveHorizon)
            {
                continue;
            }

            var enu = LocalSky.DirectionEnu(coords);
            var r = SurfaceSkyView.CelestialRadius;
            markers.Add(new SurfaceSkyMarker(
                body.Id,
                body.Name,
                BodyPalette.Of(body.ColorRgb),
                new Vector3((float)(enu.X * r), (float)(enu.Y * r), (float)(enu.Z * r))));
        }

        return markers;
    }

    private static bool IsSurfaceSkyBody(CelestialBodyData body)
        => body.Id != "earth"
            && (body.Kind is BodyKind.Star or BodyKind.Planet
                || body.Id == "moon");

    private void SetOrbitalPipelineVisible(bool visible)
    {
        foreach (var (id, node) in _bodyNodes)
        {
            node.Visible = visible && IsBodyVisible(id);
        }

        foreach (var (id, orbit) in _orbitNodes)
        {
            orbit.Visible = visible && IsBodyVisible(id);
        }

        if (GetNodeOrNull<BodyLabels>("BodyLabels") is { } labels)
        {
            labels.Visible = visible;
        }

        if (GetNodeOrNull<SpaceCamera>("SpaceCamera") is { } camera)
        {
            camera.Visible = visible;
        }
    }

    /// <summary>
    /// Retrato do corpo no instante corrente, montado por consulta ao motor. Cada
    /// chamada devolve valores novos: quem exibe não guarda nada.
    /// </summary>
    public BodyReport ReportFor(string bodyId)
        => BodyReport.For(_sim, bodyId, _sim.Time.JulianDate);

    public bool IsKindVisible(BodyKind kind) => _filter.IsVisible(kind);

    /// <summary>Verdadeiro se o corpo aparece na vista com o filtro atual.</summary>
    public bool IsBodyVisible(string bodyId)
        => _sim.Contains(bodyId) && _filter.IsVisible(_sim.BodyOf(bodyId));

    /// <summary>Mostra ou esconde uma classe de corpo menor.</summary>
    public void SetKindVisible(BodyKind kind, bool visible) => _filter.SetVisible(kind, visible);

    /// <summary>Retrato ambiental do corpo na Data Juliana atual.</summary>
    public EnvironmentReport EnvironmentFor(string bodyId)
        => _environment.ReportFor(_sim, bodyId);

    /// <summary>Índice de habitabilidade do corpo na Data Juliana atual.</summary>
    public double HabitabilityFor(string bodyId)
        => EnvironmentFor(bodyId).HabitabilityIndex;

    /// <summary>
    /// Solta uma sonda a partir do corpo ancorado e ancora a câmera nela.
    /// </summary>
    /// <param name="escapeFactor">
    /// Fração da velocidade de escape. Abaixo de 1 a sonda fica em órbita; acima, escapa
    /// e a emenda de cônicas acaba entregando-a ao corpo de cima.
    /// </param>
    /// <returns>O identificador da sonda, ou nulo se não houver de onde soltá-la.</returns>
    public string? LaunchProbe(double escapeFactor)
    {
        if (_rig.AnchorBodyId is not { } hostId)
        {
            return null;
        }

        var host = _sim.BodyOf(hostId);

        // Sem massa não há órbita em torno do corpo, e a sonda cairia em cima dele.
        if (host.MuKm3S2 <= 0.0)
        {
            return null;
        }

        // Três raios de distância: fora do corpo desenhado, e ainda bem dentro da esfera
        // de influência de qualquer um dos corpos do arquivo.
        var radiusKm = Math.Max(host.RadiusKm, 1.0) * 3.0;
        var escapeKmS = Math.Sqrt(2.0 * host.MuKm3S2 / radiusKm);
        var speedKmS = escapeKmS * escapeFactor;

        // Inclinada, para que a sonda não se confunda com o plano do sistema.
        var state = new StateVector(
            new Vector3D(radiusKm, 0.0, 0.0),
            new Vector3D(0.0, speedKmS * 0.87, speedKmS * 0.5));

        var id = NextProbeId();

        _sim.AddFromState(
            new CelestialBodyData
            {
                Id = id,
                Name = $"Sonda {_sim.DynamicBodyIds.Count + 1}",
                ParentId = hostId,
                Kind = BodyKind.Spacecraft,
                MuKm3S2 = 0.0,
                RadiusKm = 0.0,
                ColorRgb = 0xFFE066,
            },
            state,
            _sim.Time.JulianDate);

        AnchorTo(id);

        return id;
    }

    /// <summary>
    /// Preview de transferência Lambert entre dois corpos. Só consulta: o motor não muda.
    /// </summary>
    public TransferPreview? PreviewTransfer(
        string originBodyId,
        string destinationBodyId,
        double timeOfFlightDays,
        bool shortWay = true)
        => TransferPlanner.Preview(
            _sim,
            originBodyId,
            destinationBodyId,
            _sim.Time.JulianDate,
            timeOfFlightDays,
            shortWay);

    /// <summary>
    /// Melhor arco (curto ou longo) para a partida na data atual.
    /// </summary>
    public TransferPreview? BestTransferPreview(
        string originBodyId,
        string destinationBodyId,
        double timeOfFlightDays)
        => TransferPlanner.BestPreview(
            _sim,
            originBodyId,
            destinationBodyId,
            _sim.Time.JulianDate,
            timeOfFlightDays);

    /// <summary>
    /// Varredura de janelas de transferência. Só consulta.
    /// </summary>
    public IReadOnlyList<TransferWindowSample> ScanTransferWindows(
        string originBodyId,
        string destinationBodyId,
        double departureJdStart,
        double departureJdEnd,
        double departureStepDays,
        double timeOfFlightDaysMin,
        double timeOfFlightDaysMax,
        double timeOfFlightStepDays)
        => TransferPlanner.ScanWindows(
            _sim,
            originBodyId,
            destinationBodyId,
            departureJdStart,
            departureJdEnd,
            departureStepDays,
            timeOfFlightDaysMin,
            timeOfFlightDaysMax,
            timeOfFlightStepDays);

    /// <summary>
    /// Aplica um impulso à sonda ancorada, no instante atual. Só corpo dinâmico.
    /// </summary>
    public bool ApplyImpulseToAnchored(Vector3D deltaVKmS)
    {
        if (_rig.AnchorBodyId is not { } bodyId || !_sim.IsDynamic(bodyId))
        {
            return false;
        }

        _sim.ApplyImpulse(bodyId, deltaVKmS, _sim.Time.JulianDate);

        return true;
    }

    /// <summary>
    /// Aplica a partida de um preview: coloca a sonda logo fora da SOI do originário,
    /// com a velocidade heliocêntrica de Lambert, e emenda o arco. Assim o próximo
    /// <c>Advance</c> não tenta reatribuí-la ao planeta nem cai em estado degenerado.
    /// </summary>
    public TransferApplyResult ApplyTransferDeparture(in TransferPreview preview)
    {
        if (_rig.AnchorBodyId is not { } bodyId || !_sim.IsDynamic(bodyId))
        {
            return TransferApplyResult.Failed(
                "Ancore uma sonda para aplicar a partida.");
        }

        var body = _sim.BodyOf(bodyId);

        if (body.ParentId != preview.CentralBodyId)
        {
            return TransferApplyResult.Failed(
                "A sonda precisa orbitar o Sol (saia da SOI do planeta com Shift+P).");
        }

        var jd = _sim.Time.JulianDate;
        var origin = _sim.StateAt(preview.OriginBodyId, jd)
            - _sim.StateAt(preview.CentralBodyId, jd);

        var soi = _sim.SphereOfInfluenceKm(preview.OriginBodyId);
        var clearanceKm = Math.Max(
            soi * 1.2,
            Math.Max(_sim.BodyOf(preview.OriginBodyId).RadiusKm * 10.0, 1_000.0));

        // Afastamento ao longo do raio heliocêntrico do originário: |r − r_origem| fica
        // bem acima da SOI, sem depender do sinal do Δv (que às vezes aponta para dentro).
        var outward = origin.PositionKm.Normalized();
        var state = new StateVector(
            origin.PositionKm + outward * clearanceKm,
            preview.DepartureVelocityKmS);

        _sim.SetLocalState(bodyId, state, jd);

        return TransferApplyResult.Applied(
            $"Partida aplicada: Δv {DisplayFormat.Speed(preview.DepartureDeltaVKmS)}, "
                + $"fora da SOI de {_sim.BodyOf(preview.OriginBodyId).Name}.");
    }

    /// <summary>
    /// Remove o corpo ancorado, se ele tiver sido acrescentado em runtime, e devolve a
    /// âncora ao pai dele.
    /// </summary>
    public bool RemoveAnchoredBody()
    {
        if (_rig.AnchorBodyId is not { } bodyId || !_sim.IsDynamic(bodyId))
        {
            return false;
        }

        var parentId = _sim.BodyOf(bodyId).ParentId;

        _sim.Remove(bodyId);
        AnchorTo(parentId ?? _sim.Root.Id);

        return true;
    }

    /// <summary>
    /// Grava a simulação. O arquivo é pequeno por construção: o Sistema Solar não entra
    /// nele, porque o estado dele é função da data.
    /// </summary>
    public void Save()
    {
        using var file = Godot.FileAccess.Open(SavePath, Godot.FileAccess.ModeFlags.Write);

        if (file is null)
        {
            GD.PushError($"Não foi possível gravar '{SavePath}': "
                + $"{Godot.FileAccess.GetOpenError()}.");
            return;
        }

        file.StoreString(SaveState.Serialize(_sim));
    }

    /// <summary>Restaura a simulação gravada, se houver alguma.</summary>
    public bool Load()
    {
        if (!Godot.FileAccess.FileExists(SavePath))
        {
            return false;
        }

        using var file = Godot.FileAccess.Open(SavePath, Godot.FileAccess.ModeFlags.Read);

        if (file is null)
        {
            GD.PushError($"Não foi possível ler '{SavePath}': "
                + $"{Godot.FileAccess.GetOpenError()}.");
            return false;
        }

        try
        {
            SaveState.Restore(_sim, file.GetAsText());
        }
        catch (SystemDataException erro)
        {
            GD.PushError($"Arquivo salvo inválido: {erro.Message}");
            return false;
        }

        // A âncora pode ter sido descartada junto com os corpos dinâmicos antigos.
        if (_rig.AnchorBodyId is { } anchor && !_sim.Contains(anchor))
        {
            AnchorTo(_sim.Root.Id);
        }

        return true;
    }

    private string NextProbeId()
    {
        for (var numero = 1; ; numero++)
        {
            var id = $"probe{numero}";

            if (!_sim.Contains(id))
            {
                return id;
            }
        }
    }

    /// <summary>
    /// Corpo cuja projeção na tela cai mais perto do ponto apontado, dentro do raio
    /// informado. Devolve nulo se o clique caiu no vazio, para que clicar no fundo não
    /// desancore por acidente.
    /// </summary>
    /// <param name="project">
    /// Leva um ponto do espaço projetado ao pixel de tela correspondente. Quem sabe fazer
    /// isso é a câmera; a comparação em si continua aqui, junto das posições.
    /// </param>
    public string? NearestBody(
        Vector2 screenPoint, float maxDistancePixels, Func<Vector3, Vector2> project)
    {
        string? closest = null;
        var closestDistance = maxDistancePixels;

        foreach (var (id, position) in _renderPositions)
        {
            // Um corpo escondido pelo filtro não é clicável: ancorar em algo que não está
            // na tela seria a câmera saltar para lugar nenhum.
            if (!IsBodyVisible(id))
            {
                continue;
            }

            var distance = project(position).DistanceTo(screenPoint);

            if (distance <= closestDistance)
            {
                closest = id;
                closestDistance = distance;
            }
        }

        return closest;
    }

    /// <summary>
    /// Amostra a órbita em coordenadas relativas ao pai, em km.
    /// </summary>
    /// <remarks>
    /// A amostragem é por anomalia verdadeira, e não por tempo: é o que faz a mesma
    /// rotina servir à elipse e à hipérbole. Na elipse o traço dá a volta inteira; na
    /// hipérbole vai de uma assíntota à outra, sem fechar.
    /// </remarks>
    public Vector3D[] SampleOrbitKm(string bodyId)
    {
        // Os elementos da data, e não os de J2000: com as taxas seculares ligadas, é o
        // que faz o traço desenhado girar junto com a órbita que ele representa.
        if (_sim.ElementsAt(bodyId, _sim.Time.JulianDate) is not { } elements)
        {
            return [];
        }

        var mu = _sim.GravitationalParameterOf(bodyId);
        var samples = new Vector3D[OrbitSamples];

        // A elipse fecha, então o último ponto não repete o primeiro: quem desenha é que
        // emenda os dois. A hipérbole vai de assíntota a assíntota, e os extremos entram.
        var span = elements.IsClosed
            ? Math.PI
            : Math.Acos(-1.0 / elements.Eccentricity) * OpenOrbitSpan;

        var step = elements.IsClosed
            ? 2.0 * span / OrbitSamples
            : 2.0 * span / (OrbitSamples - 1.0);

        for (var index = 0; index < OrbitSamples; index++)
        {
            var trueAnomaly = -span + (step * index);

            samples[index] =
                KeplerPropagator.StateAtTrueAnomaly(elements, mu, trueAnomaly).PositionKm;
        }

        return samples;
    }

    /// <summary>
    /// O menor giro da órbita que o traço amostrado consegue mostrar. Girar menos que
    /// isso move cada vértice para menos de um passo de amostragem: reamostrar antes
    /// disso é redesenhar a mesma curva.
    /// </summary>
    public const double OrbitAngularResolutionRad = AstroConstants.TwoPi / OrbitSamples;

    /// <summary>
    /// Como a órbita está orientada agora, para quem guarda um traço amostrado e precisa
    /// saber se ele envelheceu. A precessão gira nodo e periápside, e é só isso que muda
    /// a curva no espaço enquanto o corpo não troca de pai nem de arco.
    /// </summary>
    public (double NodeRad, double PeriapsisRad) OrbitOrientation(string bodyId)
        => _sim.ElementsAt(bodyId, _sim.Time.JulianDate) is { } elements
            ? (elements.LongitudeOfAscendingNodeRad, elements.ArgumentOfPeriapsisRad)
            : (0.0, 0.0);

    /// <summary>Verdadeiro se a órbita do corpo fecha, e portanto o traço dela também.</summary>
    public bool HasClosedOrbit(string bodyId)
        => _sim.ElementsOf(bodyId) is { } elements && elements.IsClosed;

    /// <summary>Converte uma amostra local em km para o deslocamento em pixels.</summary>
    public Vector3 OrbitSampleToPixels(Vector3D localKm, string parentId)
        => ViewportTransformer.EclipticToGodot(
            ScaleMap.ToPixels(localKm, _projector.Layout.LevelOf(parentId)));

    /// <summary>
    /// Ler o arquivo é responsabilidade da Bridge porque, no jogo exportado, os dados
    /// vivem dentro do pacote e só o <c>FileAccess</c> do Godot sabe abri-los. O motor
    /// recebe apenas o texto.
    /// </summary>
    private static IBodyRepository LoadRepository()
    {
        var systemPath = $"res://{JsonBodyRepository.DefaultRelativePath}";
        var catalogPath = $"res://{JsonBodyRepository.DefaultCatalogRelativePath}";

        return JsonBodyRepository
            .FromJson(ReadDataFile(systemPath), systemPath)
            .WithCatalog(ReadDataFile(catalogPath), catalogPath);
    }

    private static string ReadDataFile(string path)
    {
        // Qualificado porque System.IO.FileAccess também está no escopo.
        using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);

        if (file is null)
        {
            throw new SystemDataException(
                $"Não foi possível abrir '{path}': {Godot.FileAccess.GetOpenError()}.");
        }

        return file.GetAsText();
    }

    private static IReadOnlyDictionary<string, BodyEnvironment> LoadEnvironments()
    {
        var path = $"res://{EnvironmentLoader.DefaultRelativePath}";

        try
        {
            return EnvironmentLoader.Parse(ReadDataFile(path));
        }
        catch (SystemDataException error)
        {
            throw new SystemDataException($"Erro em '{path}'. {error.Message}", error);
        }
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
            AddOrbitNode(body);
        }

        foreach (var body in _sim.Bodies)
        {
            AddBodyNode(body);
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

        if (OS.HasFeature("movie"))
        {
            var smoke = new MovieSmokeDriver { Name = "MovieSmokeDriver" };
            smoke.Attach(this, controls);
            AddChild(smoke);
        }

        var tree = new SystemTree { Name = "SystemTree" };
        AddChild(tree);
        tree.Attach(this);

        var inspector = new InspectorPanel { Name = "InspectorPanel" };
        AddChild(inspector);
        inspector.Attach(this);

        var teaching = new TeachingHud { Name = "TeachingHud" };
        AddChild(teaching);
        teaching.Attach(this);

        _surfaceSky = new SurfaceSkyView { Name = "SurfaceSkyView" };
        AddChild(_surfaceSky);
        _surfaceSky.Attach(this);
    }

    private void AddOrbitNode(CelestialBodyData body)
    {
        var orbit = new OrbitLineRenderer
        {
            Name = $"{body.Id}_orbit",
            BodyId = body.Id,
            ParentBodyId = body.ParentId!,
            LineColor = BodyPalette.Of(body.ColorRgb) with { A = 0.35f },
            Visible = _filter.IsVisible(body),
        };

        AddChild(orbit);
        orbit.Attach(this);

        _orbitNodes[body.Id] = orbit;
    }

    private void AddBodyNode(CelestialBodyData body)
    {
        var node = new CelestialBodyNode
        {
            Name = body.Id,
            BodyId = body.Id,
            BodyColor = BodyPalette.Of(body.ColorRgb),
            DisplayRadius = (float)ScaleMap.BodyRadiusPixels(body.RadiusKm),
            Visible = _filter.IsVisible(body),
        };

        AddChild(node);
        node.Attach(this);

        _bodyNodes[body.Id] = node;
    }

    /// <summary>
    /// Refaz o que a mudança de estrutura invalidou: nós de corpos que entraram ou
    /// saíram, e o traço de quem trocou de pai ou de órbita. Os corpos do arquivo não são
    /// tocados, porque a órbita deles não muda.
    /// </summary>
    private void OnStructureChanged()
    {
        var vivos = _sim.Bodies.Select(body => body.Id).ToHashSet(StringComparer.Ordinal);

        foreach (var id in _bodyNodes.Keys.Where(id => !vivos.Contains(id)).ToArray())
        {
            _bodyNodes[id].QueueFree();
            _bodyNodes.Remove(id);
            _renderPositions.Remove(id);
            _parents.Remove(id);
        }

        foreach (var id in _orbitNodes.Keys.Where(id => !vivos.Contains(id)).ToArray())
        {
            _orbitNodes[id].QueueFree();
            _orbitNodes.Remove(id);
        }

        foreach (var body in _sim.Bodies)
        {
            _parents[body.Id] = body.ParentId;

            if (!_bodyNodes.ContainsKey(body.Id))
            {
                AddBodyNode(body);
            }

            if (body.ParentId is not { } parentId)
            {
                continue;
            }

            // Trocar de pai muda o nó sobre o qual o traço se apoia, e a emenda de
            // cônicas troca os elementos junto: refazer o traço é obrigatório, não uma
            // otimização perdida.
            if (_orbitNodes.TryGetValue(body.Id, out var orbit))
            {
                if (_sim.IsDynamic(body.Id))
                {
                    orbit.ParentBodyId = parentId;
                    orbit.Resample(this);
                }
            }
            else
            {
                AddOrbitNode(body);
            }
        }

        StructureChanged?.Invoke();
    }

    /// <summary>
    /// O filtro não mexe no motor: o corpo escondido continua sendo propagado, e o
    /// inspetor continua sabendo dele se a câmera estiver ancorada nele. O que muda é só
    /// quem é desenhado.
    /// </summary>
    private void OnFilterChanged()
    {
        foreach (var body in _sim.Bodies)
        {
            var visible = _filter.IsVisible(body);

            if (_bodyNodes.TryGetValue(body.Id, out var node))
            {
                node.Visible = visible;
            }

            if (_orbitNodes.TryGetValue(body.Id, out var orbit))
            {
                orbit.Visible = visible;
            }
        }

        FilterChanged?.Invoke();
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

            _renderPositions[state.Id] =
                _transformer.ToRenderSpace(_projector.PositionOf(state.Id));
        }

        FrameReady?.Invoke(
            new RenderFrame(snapshot.JulianDate, _renderPositions, ScaleMap.Revision));
    }
}
