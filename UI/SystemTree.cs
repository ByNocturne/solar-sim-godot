using Godot;
using SolarSim.Bridge;

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

        // A lista vem em ordem de avaliação, com o pai sempre antes do filho: quando um
        // corpo é criado, o item do pai dele já existe.
        foreach (var body in bridge.Bodies)
        {
            var parent = body.ParentId is { } parentId ? _itemById[parentId] : null;
            var item = _tree.CreateItem(parent);

            item.SetText(0, body.Name);
            item.SetCustomColor(0, BodyPalette.Of(body.ColorRgb));
            item.SetMetadata(0, body.Id);

            _itemById[body.Id] = item;
        }

        _tree.ItemSelected += OnItemSelected;
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
