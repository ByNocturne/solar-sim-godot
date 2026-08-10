using SolarSim.Engine.Models;

namespace SolarSim.Engine.Models;

/// <summary>
/// Resultado de tentar aplicar a partida de uma transferência à sonda ancorada.
/// </summary>
public readonly record struct TransferApplyResult(bool Ok, string Message)
{
    public static TransferApplyResult Applied(string detail)
        => new(true, detail);

    public static TransferApplyResult Failed(string message)
        => new(false, message);
}
