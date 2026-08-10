namespace SolarSim.Engine.Exploration;

/// <summary>O que dá para amostrar num corpo e com que dificuldade (0–1).</summary>
public sealed class BodyComposition
{
    public required string BodyId { get; init; }

    public IReadOnlyDictionary<ResourceKind, ResourceDeposit> Deposits { get; init; }
        = new Dictionary<ResourceKind, ResourceDeposit>();
}

/// <param name="Abundance">Fração relativa do rendimento base (0–1).</param>
/// <param name="ExtractDifficulty">
/// Multiplicador de tempo de EVA necessário por unidade (1 = típico; maior = mais lento).
/// </param>
public readonly record struct ResourceDeposit(double Abundance, double ExtractDifficulty);
