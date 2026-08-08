namespace SolarSim.Engine.Models;

/// <summary>
/// Perfil ambiental estático de um corpo. Imutável: o que muda com o tempo é derivado
/// da Data Juliana e deste perfil, nunca mutação dos campos.
/// </summary>
public sealed record BodyEnvironment
{
    public required string BodyId { get; init; }

    /// <summary>Albedo de Bond, adimensional em [0, 1].</summary>
    public double BondAlbedo { get; init; }

    /// <summary>Período de rotação sidéreo, em segundos. Zero se desconhecido/irrelevante.</summary>
    public double RotationPeriodSeconds { get; init; }

    /// <summary>Obliquidade do eixo, em radianos.</summary>
    public double ObliquityRad { get; init; }

    /// <summary>Fração mássica do núcleo metálico, usada no estimador de dínamo.</summary>
    public double MetallicCoreFraction { get; init; }

    /// <summary>Número de Love k₂ para aquecimento de maré.</summary>
    public double TidalLoveNumberK2 { get; init; }

    /// <summary>Fator de qualidade dissipativo Q (maior = menos dissipação).</summary>
    public double TidalQualityFactor { get; init; }

    /// <summary>
    /// Inércia térmica superficial, em J/(m²·K·√s). Controla a amplitude diurna.
    /// </summary>
    public double ThermalInertiaJPerM2KSqrtS { get; init; }

    public AtmosphereProfile? Atmosphere { get; init; }

    /// <summary>Preenchido só na estrela raiz.</summary>
    public StellarProperties? Star { get; init; }
}

/// <summary>Composição e pressão superficial. Frações molares somam ~1 quando há atmosfera.</summary>
public sealed record AtmosphereProfile
{
    public double SurfacePressurePa { get; init; }

    public double MoleFractionH2 { get; init; }

    public double MoleFractionHe { get; init; }

    public double MoleFractionN2 { get; init; }

    public double MoleFractionO2 { get; init; }

    public double MoleFractionCO2 { get; init; }

    public double MoleFractionCH4 { get; init; }

    public double MoleFractionH2O { get; init; }

    public double MoleFractionO3 { get; init; }
}

/// <summary>Parâmetros da estrela hospedeira para o balanço radiativo.</summary>
public sealed record StellarProperties
{
    public double EffectiveTemperatureK { get; init; }

    public double RadiusKm { get; init; }
}

/// <summary>Onde a água líquida aparece no relatório ambiental.</summary>
public enum LiquidWaterPresence
{
    None = 0,
    Surface = 1,
    Subsurface = 2,
}
