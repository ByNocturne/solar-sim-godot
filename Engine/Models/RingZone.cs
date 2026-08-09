namespace SolarSim.Engine.Models;

/// <summary>
/// A faixa em torno de um corpo onde escombros não conseguem se juntar em lua, e o
/// veredito sobre haver anel ali.
/// </summary>
public readonly record struct RingZone
{
    /// <summary>Piso da faixa: a superfície do corpo.</summary>
    public double InnerRadiusKm { get; init; }

    /// <summary>Teto da faixa: o limite de Roche fluido para gelo.</summary>
    public double OuterRadiusKm { get; init; }

    /// <summary>Há faixa: o limite de Roche está acima da superfície.</summary>
    public bool HasRoom { get; init; }

    /// <summary>O corpo está além da linha de gelo, onde há de que fazer um anel.</summary>
    public bool HasIcyDebris { get; init; }

    /// <summary>As condições todas satisfeitas.</summary>
    public bool IsPlausible { get; init; }

    /// <summary>
    /// Largura da faixa em raios do corpo. É a medida que se compara entre corpos de
    /// tamanhos diferentes; em quilômetros, Saturno ganharia de Ceres sem dizer nada.
    /// </summary>
    public double WidthInBodyRadii
        => HasRoom ? (OuterRadiusKm - InnerRadiusKm) / InnerRadiusKm : 0.0;
}
