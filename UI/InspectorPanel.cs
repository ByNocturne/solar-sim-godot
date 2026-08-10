using Godot;
using SolarSim.Bridge;
using SolarSim.Engine.Models;

namespace SolarSim.UI;

/// <summary>
/// Ficha do corpo ancorado: dados físicos, geometria da órbita, estado instantâneo e
/// ambiente (M11). Passar o mouse no rótulo mostra o glossário do termo.
/// </summary>
/// <remarks>
/// Todo valor sai de <see cref="SimBridge.ReportFor"/> / <see cref="SimBridge.EnvironmentFor"/>,
/// montado por consulta ao motor no momento da atualização. O painel não guarda nada além
/// dos rótulos onde escreve.
/// </remarks>
public partial class InspectorPanel : CanvasLayer
{
    /// <summary>
    /// Intervalo entre atualizações. A 60 Hz os dígitos finais piscam rápido demais para
    /// serem lidos, e reconsultar o motor a cada quadro não acrescenta informação.
    /// </summary>
    private const double RefreshSeconds = 0.1;

    private const string Absent = "—";

    /// <summary>
    /// Acima desta folga sobre o limite de Roche a linha da maré some. "Estável a 622
    /// vezes o limite" é o que o inspetor diria de Saturno contra o Sol, e é uma linha
    /// gasta para informar que nada acontece. Abaixo de 25 a maré ainda é uma quantidade
    /// da qual se fala: a Lua entra com 20, Io com 3,4 e Fobos com 0,87.
    /// </summary>
    private const double TidalFateMarginCeiling = 25.0;

    private readonly Label[] _captions = new Label[RowCount];
    private readonly Label[] _values = new Label[RowCount];

    private SimBridge? _bridge;
    private Label _title = null!;
    private double _sinceRefresh = RefreshSeconds;

    private enum Row
    {
        Classification,
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
        ApsidalPrecession,
        SemiMajorAxisDrift,
        MeanAnomaly,
        TrueAnomaly,
        Periapsis,
        Apoapsis,
        SphereOfInfluence,
        TidalFate,
        RingZone,
        SurfaceTemperature,
        EquilibriumTemperature,
        SurfacePressure,
        Radiation,
        LiquidWater,
        HabitabilityIndex,
    }

    private static readonly int RowCount = Enum.GetValues<Row>().Length;

    /// <summary>Glossário curto ao passar o mouse no rótulo da linha.</summary>
    private static readonly Dictionary<Row, string> Glossary = new()
    {
        [Row.Classification] =
            "Classe dinâmica do corpo e, quando há, a família a que pertence dentro dela — cinturão principal, troiano de um dos pontos de Lagrange de Júpiter, plutino.",
        [Row.Orbits] =
            "Corpo em torno do qual este orbita (o atrator atual). Em sondas, pode mudar ao cruzar uma esfera de influência.",
        [Row.Radius] = "Raio médio do corpo, em quilômetros.",
        [Row.GravitationalParameter] =
            "GM: constante gravitacional vezes a massa. Define a força com que o corpo atrai satélites.",
        [Row.DistanceToParent] = "Distância instantânea até o corpo pai.",
        [Row.DistanceToRoot] = "Distância instantânea até a raiz do sistema (em geral o Sol).",
        [Row.SpeedRelativeToParent] = "Velocidade relativa ao pai — a que a órbita kepleriana descreve.",
        [Row.SpeedRelativeToRoot] = "Velocidade relativa à raiz do sistema (heliocêntrica, se a raiz for o Sol).",
        [Row.Period] = "Tempo para completar uma volta na órbita fechada. Infinito em hipérbole.",
        [Row.SemiMajorAxis] =
            "Semi-eixo maior: metade do eixo longo da elipse (ou parâmetro equivalente na hipérbole).",
        [Row.Eccentricity] =
            "Excentricidade: 0 = círculo; entre 0 e 1 = elipse; > 1 = hipérbole (fuga).",
        [Row.Inclination] = "Inclinação do plano da órbita em relação ao plano de referência (eclíptica).",
        [Row.AscendingNode] =
            "Longitude do nó ascendente (Ω): onde a órbita cruza o plano de referência subindo.",
        [Row.ArgumentOfPeriapsis] =
            "Argumento do periápside (ω): ângulo do nó ascendente até o ponto mais próximo do foco. Mostrado na data atual, já com a precessão.",
        [Row.ApsidalPrecession] =
            "Quanto o periápside gira por século: relatividade geral mais achatamento do corpo pai. Em Mercúrio são os 43″/século que a gravitação newtoniana não explicava.",
        [Row.SemiMajorAxisDrift] =
            "Quanto o semi-eixo maior anda por milhão de anos: efeito Yarkovsky (radiação térmica de um corpo que gira) e, quando declarado, pressão de radiação. Em Bennu são −0,0019 UA/Myr.",
        [Row.MeanAnomaly] =
            "Anomalia média na data atual: posição angular que o corpo teria se a órbita fosse percorrida a velocidade constante.",
        [Row.TrueAnomaly] =
            "Anomalia verdadeira: ângulo atual entre o periápside e a posição do corpo, no foco.",
        [Row.Periapsis] = "Distância mínima ao atrator (periélio se o atrator for o Sol).",
        [Row.Apoapsis] = "Distância máxima ao atrator. Não existe em órbita aberta.",
        [Row.SphereOfInfluence] =
            "Raio aproximado em que este corpo domina a atração sobre uma sonda (cônicas emendadas).",
        [Row.TidalFate] =
            "O que a maré do corpo pai faz com este satélite, e a que múltiplo do limite de Roche fluido ele passa no periápside. Abaixo de 1 a maré vence a gravidade própria e o corpo se desmancha; Fobos passa a 0,87.",
        [Row.RingZone] =
            "Faixa em que escombros de gelo não conseguem se juntar em lua, medida em raios do corpo. É onde um anel pode existir — em Saturno ela termina na borda do anel A.",
        [Row.SurfaceTemperature] =
            "Temperatura de superfície estimada (equilíbrio radiativo + estufa paramétrica).",
        [Row.EquilibriumTemperature] =
            "Temperatura de equilíbrio sem estufa: só albedo e fluxo estelar.",
        [Row.SurfacePressure] = "Pressão atmosférica na superfície (perfil ambiental).",
        [Row.Radiation] =
            "Dose ionizante relativa à Terra em 1 UA com campo terrestre (= 1×).",
        [Row.LiquidWater] =
            "Água líquida na superfície, oceano sob gelo, ou nenhuma — no modelo de ensino.",
        [Row.HabitabilityIndex] =
            "BHI (0–1): índice de habitabilidade básica (temperatura, pressão, radiação, água).",
    };

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
            // Caption ignora o mouse por padrão; o glossário precisa receber hover.
            _captions[row].MouseFilter = Control.MouseFilterEnum.Stop;
            if (Glossary.TryGetValue((Row)row, out var tip))
            {
                _captions[row].TooltipText = tip;
            }

            _values[row] = Panels.Value(Absent);
            _values[row].HorizontalAlignment = HorizontalAlignment.Right;
            _values[row].SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _values[row].MouseFilter = Control.MouseFilterEnum.Stop;

            // A largura do inspetor é do layout, e nenhum valor pode disputá-la. Um rótulo
            // do Godot pede como largura mínima o texto inteiro, e a caixa cede: bastou a
            // classe de Ceres, "Asteroide · Cinturão principal", para empurrar a coluna
            // toda para fora da borda direita e cortar cada linha da ficha, e não só a
            // linha comprida.
            if ((Row)row == Row.Classification)
            {
                // Esta é a única prosa da ficha, e cortá-la esconderia justamente a
                // família, que é o que o corpo menor tem de particular. Quebra em duas
                // linhas: aí a largura mínima passa a ser a da maior palavra.
                _values[row].AutowrapMode = TextServer.AutowrapMode.Word;
            }
            else
            {
                _values[row].ClipText = true;
                _values[row].TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            }

            if (Glossary.TryGetValue((Row)row, out tip))
            {
                _values[row].TooltipText = tip;
            }

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

        Show(_bridge.ReportFor(bodyId), _bridge.EnvironmentFor(bodyId));
    }

    private void Show(in BodyReport report, in EnvironmentReport environment)
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

        string OrbitOnly(string value) => isRoot ? Absent : value;

        var hasOwnParent = !isRoot && parent != report.RootName;

        Show(Row.Classification, report.Kind != BodyKind.Unspecified || report.Family is not null);
        Set(
            Row.Classification,
            "Classe",
            DisplayFormat.Classification(report.Kind, report.Family));

        Set(
            Row.Orbits,
            report.IsDynamic ? "Orbita agora" : "Orbita",
            report.ParentName ?? Absent);
        Set(Row.Radius, "Raio", DisplayFormat.Distance(report.RadiusKm));
        Set(
            Row.GravitationalParameter,
            "GM",
            DisplayFormat.GravitationalParameter(report.MuKm3S2));

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

        Show(Row.ApsidalPrecession, report.ApsidalPrecessionRadPerSecond != 0.0);
        Set(
            Row.ApsidalPrecession,
            "Precessão do periáps.",
            DisplayFormat.PrecessionRate(report.ApsidalPrecessionRadPerSecond));

        Show(Row.SemiMajorAxisDrift, report.SemiMajorAxisKmPerSecond != 0.0);
        Set(
            Row.SemiMajorAxisDrift,
            "Drift do semi-eixo",
            DisplayFormat.SemiMajorAxisDrift(report.SemiMajorAxisKmPerSecond));

        Set(
            Row.MeanAnomaly,
            "Anom. média",
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

        Show(
            Row.SphereOfInfluence,
            report.SphereOfInfluenceKm > 0.0 && double.IsFinite(report.SphereOfInfluenceKm));
        Set(
            Row.SphereOfInfluence,
            "Esfera de influência",
            DisplayFormat.Distance(report.SphereOfInfluenceKm));

        Show(
            Row.TidalFate,
            report.Tides.IsKnown && report.Tides.MarginOverFluid < TidalFateMarginCeiling);
        Set(Row.TidalFate, "Maré do pai", DisplayFormat.Fate(report.Tides));

        // A faixa só interessa em quem poderia hospedá-la. Mostrá-la em toda pedra do
        // cinturão encheria a ficha de uma linha que diz sempre a mesma coisa.
        Show(Row.RingZone, report.Rings.IsPlausible);
        Set(Row.RingZone, "Zona de anel", DisplayFormat.RingZone(report.Rings));

        var showEnvironment = !isRoot;
        Show(Row.SurfaceTemperature, showEnvironment);
        Show(Row.EquilibriumTemperature, showEnvironment);
        Show(Row.SurfacePressure, showEnvironment);
        Show(Row.Radiation, showEnvironment);
        Show(Row.LiquidWater, showEnvironment);
        Show(Row.HabitabilityIndex, showEnvironment);

        Set(
            Row.SurfaceTemperature,
            "T superfície",
            DisplayFormat.Temperature(environment.SurfaceTemperatureK));
        Set(
            Row.EquilibriumTemperature,
            "T equilíbrio",
            DisplayFormat.Temperature(environment.EquilibriumTemperatureK));
        Set(
            Row.SurfacePressure,
            "Pressão",
            DisplayFormat.Pressure(environment.SurfacePressurePa));
        Set(
            Row.Radiation,
            "Radiação",
            DisplayFormat.RadiationRelative(environment.RelativeIonizingRadiation));
        Set(Row.LiquidWater, "Água líquida", WaterLabel(environment.LiquidWater));
        Set(
            Row.HabitabilityIndex,
            "BHI",
            DisplayFormat.HabitabilityIndex(environment.HabitabilityIndex));
    }

    private static string WaterLabel(LiquidWaterPresence water)
        => water switch
        {
            LiquidWaterPresence.Surface => "superfície",
            LiquidWaterPresence.Subsurface => "subterrânea",
            _ => "não",
        };

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
