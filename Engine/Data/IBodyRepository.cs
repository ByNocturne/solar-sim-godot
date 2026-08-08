using SolarSim.Engine.Models;

namespace SolarSim.Engine.Data;

/// <summary>
/// Fonte dos dados estáticos do sistema. A interface existe desde o M1 para que a
/// troca da implementação hardcoded pela leitura de JSON, no M3, não exija mudança
/// em nenhuma camada acima.
/// </summary>
public interface IBodyRepository
{
    IReadOnlyList<CelestialBodyData> LoadBodies();
}
