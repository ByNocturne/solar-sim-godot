namespace SolarSim.Engine.Exploration;

/// <summary>Inventário da campanha — amostras e propelente que sobrevivem entre idas.</summary>
public sealed class CargoInventory
{
    private readonly Dictionary<ResourceKind, double> _amounts = new();

    public IReadOnlyDictionary<ResourceKind, double> Amounts => _amounts;

    public double Get(ResourceKind kind)
        => _amounts.TryGetValue(kind, out var value) ? value : 0.0;

    public void Add(ResourceKind kind, double amount)
    {
        if (amount < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        if (amount == 0.0)
        {
            return;
        }

        _amounts[kind] = Get(kind) + amount;
    }

    public bool TryConsume(ResourceKind kind, double amount)
    {
        if (amount < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        if (Get(kind) + 1e-12 < amount)
        {
            return false;
        }

        var next = Get(kind) - amount;
        if (next <= 1e-12)
        {
            _amounts.Remove(kind);
        }
        else
        {
            _amounts[kind] = next;
        }

        return true;
    }

    /// <summary>
    /// Converte gelo em propelente (ISRU tosco). Retorna quanto propelente foi produzido.
    /// </summary>
    public double RefineWaterIceToPropellant(double iceAmount, double yield = 0.8)
    {
        if (iceAmount <= 0.0 || yield <= 0.0)
        {
            return 0.0;
        }

        if (!TryConsume(ResourceKind.WaterIce, iceAmount))
        {
            return 0.0;
        }

        var produced = iceAmount * yield;
        Add(ResourceKind.Propellant, produced);
        return produced;
    }
}
