using Godot;
using SolarSim.Bridge;
using SolarSim.Engine.Models;

namespace SolarSim.UI;

/// <summary>
/// A hierarquia do sistema como árvore navegável. Selecionar um corpo ancora a câmera
/// nele.
/// </summary>
/// <remarks>
/// A seleção não é a fonte da verdade: quem manda é a âncora da câmera, que também muda
/// por clique no mundo e por Tab. A árvore acompanha essa âncora a cada quadro, o que
/// mantém os dois caminhos concordando sem que nenhum dos dois precise conhecer o outro.
/// </remarks>
public partial class SystemTree : CanvasLayer
{
    private readonly Dictionary<string, TreeItem> _itemById = new(StringComparer.Ordinal);

    private SimBridge? _bridge;
    private Tree _tree = null!;
    private string? _shownAnchor;

    // Selecionar por código dispara o mesmo sinal do clique. Sem esta marca, sincronizar
    // a árvore com a âncora reancoraria a câmera e reiniciaria a transição a cada quadro.
    private bool _syncing;

    public void Attach(SimBridge bridge)
    {
        _bridge = bridge;

        var box = Panels.Box();
        Panels.AnchorLeftColumn(box, Panels.TreeWidth);
        AddChild(box);

        var column = new VBoxContainer();
        box.AddChild(column);
        column.AddChild(Panels.Caption("SISTEMA"));

        _tree = new Tree
        {
            Columns = 1,
            HideRoot = false,
            AllowReselect = true,
            SelectMode = Tree.SelectModeEnum.Single,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };

        _tree.AddThemeFontSizeOverride("font_size", Panels.FontSize);
        column.AddChild(_tree);

        BuildItems(bridge);
        AddFilters(bridge, column);

        _tree.ItemSelected += OnItemSelected;
        bridge.StructureChanged += OnStructureChanged;
        bridge.FilterChanged += OnStructureChanged;
    }

    public override void _ExitTree()
    {
        if (_bridge is not null)
        {
            _bridge.StructureChanged -= OnStructureChanged;
            _bridge.FilterChanged -= OnStructureChanged;
            _bridge = null;
        }
    }

    public override void _Process(double delta)
    {
        if (_bridge is null || _bridge.AnchorBodyId == _shownAnchor)
        {
            return;
        }

        _shownAnchor = _bridge.AnchorBodyId;
        _syncing = true;

        if (_shownAnchor is { } anchor && _itemById.TryGetValue(anchor, out var item))
        {
            item.Select(0);
            _tree.ScrollToItem(item);
        }
        else
        {
            _tree.DeselectAll();
        }

        _syncing = false;
    }

    /// <summary>
    /// Remonta a árvore inteira. Uma sonda entrando, saindo ou trocando de pai muda o
    /// lugar de um item, e o <c>Tree</c> do Godot não reparenta: refazer os itens é mais
    /// simples do que perseguir o que mudou, e acontece só quando a estrutura muda.
    /// </summary>
    private void OnStructureChanged()
    {
        if (_bridge is null)
        {
            return;
        }

        _syncing = true;
        _tree.Clear();
        _itemById.Clear();

        BuildItems(_bridge);

        _syncing = false;

        // Força o próximo quadro a reencontrar a âncora no item novo.
        _shownAnchor = null;
    }

    /// <summary>
    /// A lista vem em ordem de avaliação, com o pai sempre antes do filho: quando um
    /// corpo é criado, o item do pai dele já existe.
    /// </summary>
    private void BuildItems(SimBridge bridge)
    {
        foreach (var body in bridge.Bodies)
        {
            if (!bridge.IsKindVisible(body.Kind))
            {
                continue;
            }

            TreeItem? parent = null;

            // O pai escondido leva o filho junto: uma sonda em torno de um asteroide
            // filtrado não teria em que galho pendurar.
            if (body.ParentId is { } parentId && !_itemById.TryGetValue(parentId, out parent))
            {
                continue;
            }

            var item = _tree.CreateItem(parent);

            item.SetText(0, body.Name);
            item.SetCustomColor(0, BodyPalette.Of(body.ColorRgb));
            item.SetMetadata(0, body.Id);

            _itemById[body.Id] = item;
        }
    }

    /// <summary>
    /// Uma caixa por classe de corpo menor presente no catálogo, abaixo da árvore.
    /// </summary>
    /// <remarks>
    /// Ficam aqui, e não em uma tecla de atalho, porque são cinco estados independentes:
    /// uma tecla que percorresse combinações não diria em qual delas se está. E ficam
    /// abaixo da árvore para que o painel continue começando pelo Sistema Solar, que é o
    /// que quase sempre se procura.
    /// </remarks>
    private void AddFilters(SimBridge bridge, Control column)
    {
        if (bridge.FilterableKinds.Count == 0)
        {
            return;
        }

        column.AddChild(Panels.Caption("CORPOS MENORES"));

        foreach (var kind in bridge.FilterableKinds)
        {
            var toggle = new CheckBox
            {
                Text = DisplayFormat.Kind(kind),
                ButtonPressed = bridge.IsKindVisible(kind),
            };

            toggle.AddThemeFontSizeOverride("font_size", Panels.FontSize);

            // Capturado por valor: o laço reaproveita a variável, e sem a cópia todas as
            // caixas mexeriam na última classe.
            var filtered = kind;
            toggle.Toggled += pressed => bridge.SetKindVisible(filtered, pressed);

            column.AddChild(toggle);
        }
    }

    private void OnItemSelected()
    {
        if (_syncing || _bridge is null || _tree.GetSelected() is not { } item)
        {
            return;
        }

        _shownAnchor = item.GetMetadata(0).AsString();
        _bridge.AnchorTo(_shownAnchor);
    }
}
