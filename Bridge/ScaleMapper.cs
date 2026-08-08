using SolarSim.Engine.Core;

namespace SolarSim.Bridge;

/// <summary>
/// Converte distâncias físicas em pixels. No M1 existe apenas o modo linear; o modo
/// logarítmico perceptual entra no M4.
/// </summary>
public sealed class ScaleMapper
{
    /// <summary>Fator linear. O padrão coloca 1 UA a 250 pixels da origem.</summary>
    public double PixelsPerKm { get; set; } = 250.0 / AstroConstants.AstronomicalUnitKm;

    public double KmToPixels(double km) => km * PixelsPerKm;

    /// <summary>
    /// Raio visual de um corpo, em pixels. Deliberadamente desacoplado da escala de
    /// distância: em proporção real, a Terra teria centésimos de pixel.
    /// A curva logarítmica é provisória e será revista no M4.
    /// </summary>
    public double BodyRadiusPixels(double radiusKm)
        => radiusKm <= 0.0 ? 3.0 : Math.Clamp(3.0 + 4.0 * Math.Log10(radiusKm), 4.0, 28.0);
}
