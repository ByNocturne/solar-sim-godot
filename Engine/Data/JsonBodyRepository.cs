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

    /// <summary>Caminho canônico do catálogo de corpos menores.</summary>
    public const string DefaultCatalogRelativePath = "Data/minor_bodies_j2000.json";

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
        => FromJson(ReadText(path), path);

    /// <summary>
    /// Soma um catálogo de corpos menores ao sistema, devolvendo um repositório novo.
    /// </summary>
    /// <remarks>
    /// A união passa por <see cref="BodyHierarchy.Create"/> aqui, e não lá no motor, para
    /// que um pai inexistente ou um id repetido entre os dois arquivos apareça na carga
    /// com o nome do arquivo culpado. É também o que garante a ordem de avaliação da
    /// lista somada.
    /// </remarks>
    public JsonBodyRepository WithCatalog(string json, string origin)
    {
        try
        {
            var combined = new List<CelestialBodyData>(_bodies);
            combined.AddRange(DataLoader.ParseCatalog(json));

            return new JsonBodyRepository(BodyHierarchy.Create(combined).InEvaluationOrder);
        }
        catch (SystemDataException error)
        {
            throw new SystemDataException($"Erro em '{origin}'. {error.Message}", error);
        }
    }

    public JsonBodyRepository WithCatalogFile(string path)
        => WithCatalog(ReadText(path), path);

    public IReadOnlyList<CelestialBodyData> LoadBodies() => _bodies;

    private static string ReadText(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new SystemDataException($"Arquivo de dados não encontrado: '{path}'.");
        }

        return File.ReadAllText(path);
    }
}
