using SolarSim.Engine.Models;

namespace SolarSim.Engine.Data;

/// <summary>
/// Repositório alimentado por <c>Data/solar_system_j2000.json</c>. Entra pela mesma porta
/// que a implementação em código do M1 usava: nada acima de <see cref="IBodyRepository"/>
/// precisou mudar.
/// </summary>
/// <remarks>
/// A leitura do arquivo é separada da interpretação do conteúdo de propósito. Dentro do
/// Godot exportado, os dados vivem no pacote e só o <c>FileAccess</c> da engine sabe
/// abri-los; a Bridge lê o texto de lá e chama <see cref="FromJson"/>. O motor continua
/// sem saber que o Godot existe.
/// </remarks>
public sealed class JsonBodyRepository : IBodyRepository
{
    /// <summary>Caminho canônico dos dados, relativo à raiz do projeto.</summary>
    public const string DefaultRelativePath = "Data/solar_system_j2000.json";

    private readonly IReadOnlyList<CelestialBodyData> _bodies;

    private JsonBodyRepository(IReadOnlyList<CelestialBodyData> bodies) => _bodies = bodies;

    /// <param name="origin">
    /// De onde veio o conteúdo. Aparece na mensagem de erro, que é o que transforma
    /// "campo obrigatório ausente" em algo acionável.
    /// </param>
    public static JsonBodyRepository FromJson(string json, string origin)
    {
        try
        {
            return new JsonBodyRepository(DataLoader.Parse(json));
        }
        catch (SystemDataException error)
        {
            throw new SystemDataException($"Erro em '{origin}'. {error.Message}", error);
        }
    }

    public static JsonBodyRepository FromFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new SystemDataException($"Arquivo de dados não encontrado: '{path}'.");
        }

        return FromJson(File.ReadAllText(path), path);
    }

    public IReadOnlyList<CelestialBodyData> LoadBodies() => _bodies;
}
