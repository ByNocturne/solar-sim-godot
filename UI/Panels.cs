using Godot;

namespace SolarSim.UI;

/// <summary>
/// Peças comuns aos painéis: a caixa encostada na borda da tela, os rótulos de dentro
/// dela e os botões. Existe para que os três painéis do M5 tratem de conteúdo, e não de
/// âncoras e de sobreposições de tema.
/// </summary>
internal static class Panels
{
    public const int FontSize = 12;

    public const int TitleFontSize = 15;

    /// <summary>Folga entre as caixas e a borda da tela, em pixels.</summary>
    public const float Margin = 12.0f;

    /// <summary>Altura reservada pela barra de tempo no pé da tela.</summary>
    public const float BottomBarHeight = 76.0f;

    public const float TreeWidth = 196.0f;

    public const float InspectorWidth = 300.0f;

    /// <summary>
    /// Faixas da tela que a interface ocupa. Quem desenha no mundo as consulta para não
    /// colocar nada onde um painel vai passar por cima.
    /// </summary>
    public const float ReservedLeft = (Margin * 2.0f) + TreeWidth;

    public const float ReservedRight = (Margin * 2.0f) + InspectorWidth;

    public const float ReservedBottom = BottomBarHeight + (Margin * 2.0f);

    public static readonly Color Dim = new(0.60f, 0.66f, 0.76f);

    public static readonly Color Bright = new(0.91f, 0.94f, 0.99f);

    public static readonly Color Alert = new(1.00f, 0.48f, 0.44f);

    /// <summary>
    /// Caixa translúcida sobre o mundo. A opacidade é alta o bastante para o texto ficar
    /// legível com uma órbita passando por trás, e baixa o bastante para o painel não
    /// parecer um buraco recortado no céu.
    /// </summary>
    public static PanelContainer Box()
    {
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.04f, 0.05f, 0.08f, 0.86f),
            BorderColor = new Color(0.27f, 0.33f, 0.45f, 0.75f),
            ContentMarginLeft = 10.0f,
            ContentMarginRight = 10.0f,
            ContentMarginTop = 8.0f,
            ContentMarginBottom = 8.0f,
        };

        style.SetBorderWidthAll(1);
        style.SetCornerRadiusAll(4);

        var box = new PanelContainer();
        box.AddThemeStyleboxOverride("panel", style);

        return box;
    }

    public static Label Caption(string text) => Text(text, Dim);

    public static Label Value(string text) => Text(text, Bright);

    public static Label Title(string text)
    {
        var label = Text(text, Bright);
        label.AddThemeFontSizeOverride("font_size", TitleFontSize);

        return label;
    }

    public static Label Text(string text, Color color)
    {
        var label = new Label
        {
            Text = text,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

        label.AddThemeFontSizeOverride("font_size", FontSize);
        label.AddThemeColorOverride("font_color", color);

        return label;
    }

    /// <summary>
    /// Botão que não retém o foco do teclado. Sem isso, clicar em "Pausar" faria a barra
    /// de espaço voltar a acionar o botão em vez de chegar ao atalho global.
    /// </summary>
    public static Button Command(string text, Action onPressed)
    {
        var button = new Button
        {
            Text = text,
            FocusMode = Control.FocusModeEnum.None,
        };

        button.AddThemeFontSizeOverride("font_size", FontSize);
        button.Pressed += onPressed;

        return button;
    }

    /// <summary>Coluna encostada na borda esquerda, do topo até a barra de tempo.</summary>
    public static void AnchorLeftColumn(Control box, float width)
    {
        box.AnchorLeft = 0.0f;
        box.AnchorRight = 0.0f;
        box.OffsetLeft = Margin;
        box.OffsetRight = Margin + width;

        AnchorFullHeight(box);
    }

    /// <summary>Coluna encostada na borda direita, do topo até a barra de tempo.</summary>
    public static void AnchorRightColumn(Control box, float width)
    {
        box.AnchorLeft = 1.0f;
        box.AnchorRight = 1.0f;
        box.OffsetLeft = -(Margin + width);
        box.OffsetRight = -Margin;

        AnchorFullHeight(box);
    }

    /// <summary>Barra que atravessa o pé da tela.</summary>
    public static void AnchorBottomBar(Control box)
    {
        box.AnchorLeft = 0.0f;
        box.AnchorRight = 1.0f;
        box.AnchorTop = 1.0f;
        box.AnchorBottom = 1.0f;
        box.OffsetLeft = Margin;
        box.OffsetRight = -Margin;
        box.OffsetTop = -(BottomBarHeight + Margin);
        box.OffsetBottom = -Margin;
    }

    /// <summary>Caixa centrada na tela, para sobreposições momentâneas.</summary>
    public static void AnchorCenter(Control box, float width, float height)
    {
        box.AnchorLeft = 0.5f;
        box.AnchorRight = 0.5f;
        box.AnchorTop = 0.5f;
        box.AnchorBottom = 0.5f;
        box.OffsetLeft = -width / 2.0f;
        box.OffsetRight = width / 2.0f;
        box.OffsetTop = -height / 2.0f;
        box.OffsetBottom = height / 2.0f;
    }

    private static void AnchorFullHeight(Control box)
    {
        box.AnchorTop = 0.0f;
        box.AnchorBottom = 1.0f;
        box.OffsetTop = Margin;
        box.OffsetBottom = -(BottomBarHeight + (Margin * 2.0f));
    }
}
