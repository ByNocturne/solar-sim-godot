using Godot;

namespace SolarSim.Bridge;

/// <summary>
/// Entrada do projeto: Movie Maker e smoke vão ao simulador; F5 normal abre o jogo.
/// </summary>
public partial class Boot : Node
{
    public override void _Ready()
    {
        var scene = OS.HasFeature("movie")
            ? "res://Scenes/Main.tscn"
            : "res://Scenes/Game.tscn";
        // ChangeScene não pode rodar no meio do _Ready da árvore atual.
        CallDeferred(nameof(GoTo), scene);
    }

    private void GoTo(string scenePath)
        => GetTree().ChangeSceneToFile(scenePath);
}
