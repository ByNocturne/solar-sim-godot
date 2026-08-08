using Godot;
using SolarSim.Bridge;

namespace SolarSim.UI;

/// <summary>
/// Ficha do corpo ancorado: dados físicos, geometria da órbita e o estado instantâneo.
/// </summary>
/// <remarks>
/// Todo valor sai de <see cref="SimBridge.ReportFor"/>, montado por consulta ao motor no
/// momento da atualização. O painel não guarda nada além dos rótulos onde escreve, então
/// não existe estado aqui que possa divergir da simulação.
/// </remarks>
public partial class InspectorPanel : CanvasLayer
{
    /// <summary>
    /// Intervalo entre atualizações. A 60 Hz os dígitos finais piscam rápido demais para
    /// serem lidos, e reconsultar o motor a cada quadro não acrescenta informação.
    /// </summary>
    private const double RefreshSeconds = 0.1;

    private const string Absent = "—";

    private readonly Label[] _captions = new Label[RowCount];
    private readonly Label[] _values = new Label[RowCount];

    private SimBridge? _bridge;
    private Label _title = null!;
    private double _sinceRefresh = RefreshSeconds;

    private enum Row
    {
        Orbits,
        Radius,
        GravitationalParameter,
        DistanceToParent,
        DistanceToRoot,
        SpeedRelativeToParent,
        SpeedRelativeToRoot,
        Period,
        SemiMajorAxis,
        Eccentricity,
        Inclination,
        AscendingNode,
        ArgumentOfPeriapsis,
        MeanAnomalyAtEpoch,
        TrueAnomaly,
        Periapsis,
        Apoapsis,
        SphereOfInfluence,
    }

    private static readonly int RowCount = Enum.GetValues<Row>().Length;

    public void Attach(SimBridge bridge)
    {
        _bridge = bridge;

        var box = Panels.Box();
        Panels.AnchorRightColumn(box, Panels.InspectorWidth);
        AddChild(box);

        var column = new VBoxContainer();
        box.AddChild(column);

        _title = Panels.Title(Absent);
        column.AddChild(Panels.Caption("CORPO"));
        column.AddChild(_title);
        column.AddChild(new HSeparator());

        var grid = new GridContainer { Columns = 2 };
        column.AddChild(grid);

        for (var row = 0; row < RowCount; row++)
        {
            _captions[row] = Panels.Caption(string.Empty);

            _values[row] = Panels.Value(Absent);
            _values[row].HorizontalAlignment = HorizontalAlignment.Right;
            _values[row].SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            grid.AddChild(_captions[row]);
            grid.AddChild(_values[row]);
        }

        // Empurra a ficha para o topo da caixa, que vai até o pé da tela.
        column.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill });

        Refresh();
    }

    public override void _Process(double delta)
    {
        if (_bridge is null)
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
            ShowNothing();
            return;
        }

        Show(_bridge.ReportFor(bodyId));
    }

    private void Show(in BodyReport report)
    {
        _title.Text = report.Name;

        for (var row = 0; row < RowCount; row++)
        {
            _captions[row].Visible = true;
            _values[row].Visible = true;
        }

        var parent = report.ParentName ?? string.Empty;
        var elements = report.Elements.GetValueOrDefault();
        var isRoot = report.IsRoot;

        // A raiz não orbita nada: as linhas de órbita continuam rotuladas, com traço no
        // lugar do número. Escondê-las faria o painel mudar de altura ao trocar de corpo.
        string OrbitOnly(string value) => isRoot ? Absent : value;

        // Um planeta orbita a própria raiz, e aí as duas medidas são a mesma. Mostrar as
        // duas linhas repetiria o número e daria a impressão de defeito; elas só têm o que
        // dizer para um satélite, cujo movimento em torno do planeta é o assunto.
        var hasOwnParent = !isRoot && parent != report.RootName;

        // Um corpo dinâmico troca de pai ao atravessar a esfera de influência, e o rótulo
        // avisa que aquele nome é o de agora, não o de sempre.
        Set(
            Row.Orbits,
            report.IsDynamic ? "Orbita agora" : "Orbita",
            report.ParentName ?? Absent);
        Set(Row.Radius, "Raio", DisplayFormat.Distance(report.RadiusKm));
        Set(
            Row.GravitationalParameter,
            "GM",
            DisplayFormat.GravitationalParameter(report.MuKm3S2));

        // O nome entre parênteses em vez de regido por preposição: "ao Sol" e "a Júpiter"
        // exigiriam saber o artigo de cada corpo, e o arquivo de dados não o traz.
        Show(Row.DistanceToParent, hasOwnParent);
        Set(
            Row.DistanceToParent,
            $"Distância ({parent})",
            DisplayFormat.Distance(report.DistanceToParentKm));

        Set(
            Row.DistanceToRoot,
            $"Distância ({report.RootName})",
            DisplayFormat.Distance(report.DistanceToRootKm));

        Show(Row.SpeedRelativeToParent, hasOwnParent);
        Set(
            Row.SpeedRelativeToParent,
            $"Velocidade ({parent})",
            DisplayFormat.Speed(report.SpeedRelativeToParentKmS));

        Set(
            Row.SpeedRelativeToRoot,
            $"Velocidade ({report.RootName})",
            DisplayFormat.Speed(report.SpeedRelativeToRootKmS));

        Set(Row.Period, "Período", OrbitOnly(DisplayFormat.Duration(report.PeriodDays)));
        Set(
            Row.SemiMajorAxis,
            "Semi-eixo maior",
            OrbitOnly(DisplayFormat.Distance(elements.SemiMajorAxisKm)));
        Set(
            Row.Eccentricity,
            "Excentricidade",
            OrbitOnly(DisplayFormat.Ratio(elements.Eccentricity)));
        Set(
            Row.Inclination,
            "Inclinação",
            OrbitOnly(DisplayFormat.Angle(elements.InclinationRad)));
        Set(
            Row.AscendingNode,
            "Long. do nó asc.",
            OrbitOnly(DisplayFormat.Angle(elements.LongitudeOfAscendingNodeRad)));
        Set(
            Row.ArgumentOfPeriapsis,
            "Arg. do periápside",
            OrbitOnly(DisplayFormat.Angle(elements.ArgumentOfPeriapsisRad)));
        Set(
            Row.MeanAnomalyAtEpoch,
            "Anom. média (J2000)",
            OrbitOnly(DisplayFormat.Angle(elements.MeanAnomalyAtEpochRad)));
        Set(
            Row.TrueAnomaly,
            "Anom. verdadeira",
            OrbitOnly(DisplayFormat.Angle(report.TrueAnomalyRad)));
        Set(
            Row.Periapsis,
            "Periápside",
            OrbitOnly(DisplayFormat.Distance(report.PeriapsisKm)));
        Set(
            Row.Apoapsis,
            "Apoápside",
            OrbitOnly(DisplayFormat.Distance(report.ApoapsisKm)));

        // Sem massa não há esfera, e a da raiz é infinita: nos dois casos não há número a
        // mostrar. O primeiro caso é o de toda sonda, justamente o corpo que mais
        // atravessa esferas alheias.
        Show(
            Row.SphereOfInfluence,
            report.SphereOfInfluenceKm > 0.0 && double.IsFinite(report.SphereOfInfluenceKm));
        Set(
            Row.SphereOfInfluence,
            "Esfera de influência",
            DisplayFormat.Distance(report.SphereOfInfluenceKm));
    }

    private void ShowNothing()
    {
        _title.Text = "Nenhum corpo ancorado";

        for (var row = 0; row < RowCount; row++)
        {
            _captions[row].Visible = false;
            _values[row].Visible = false;
        }
    }

    private void Set(Row row, string caption, string value)
    {
        _captions[(int)row].Text = caption;
        _values[(int)row].Text = value;
    }

    /// <summary>
    /// Esconder as duas células é o que faz a linha desaparecer de fato: um contêiner do
    /// Godot só distribui espaço entre os filhos visíveis, então a grade se fecha em vez
    /// de deixar um vão.
    /// </summary>
    private void Show(Row row, bool visible)
    {
        _captions[(int)row].Visible = visible;
        _values[(int)row].Visible = visible;
    }
}
