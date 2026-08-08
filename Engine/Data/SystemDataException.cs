namespace SolarSim.Engine.Data;

/// <summary>
/// Dado de sistema inválido. Erro de configuração, não de execução: a mensagem precisa
/// dizer qual corpo e qual campo, porque quem vai lê-la está editando um arquivo, não
/// depurando código.
/// </summary>
public sealed class SystemDataException : Exception
{
    public SystemDataException(string message)
        : base(message)
    {
    }

    public SystemDataException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
