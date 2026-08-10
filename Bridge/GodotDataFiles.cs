using Godot;
using SolarSim.Engine;
using SolarSim.Engine.Data;
using SolarSim.Engine.Models;

namespace SolarSim.Bridge;

/// <summary>
/// Leitura de JSON via <c>res://</c> — única porta Godot → texto para o motor.
/// </summary>
public static class GodotDataFiles
{
    public static IBodyRepository LoadRepository()
    {
        var systemPath = $"res://{JsonBodyRepository.DefaultRelativePath}";
        var catalogPath = $"res://{JsonBodyRepository.DefaultCatalogRelativePath}";

        return JsonBodyRepository
            .FromJson(ReadText(systemPath), systemPath)
            .WithCatalog(ReadText(catalogPath), catalogPath);
    }

    public static IReadOnlyDictionary<string, BodyEnvironment> LoadEnvironments()
    {
        var path = $"res://{EnvironmentLoader.DefaultRelativePath}";

        try
        {
            return EnvironmentLoader.Parse(ReadText(path));
        }
        catch (SystemDataException error)
        {
            throw new SystemDataException($"Erro em '{path}'. {error.Message}", error);
        }
    }

    public static string ReadText(string path)
    {
        using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);

        if (file is null)
        {
            throw new SystemDataException(
                $"Não foi possível abrir '{path}': {Godot.FileAccess.GetOpenError()}.");
        }

        return file.GetAsText();
    }
}
