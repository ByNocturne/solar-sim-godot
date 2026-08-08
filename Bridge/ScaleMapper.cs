using SolarSim.Engine.Core;
using SolarSim.Engine.Models;

namespace SolarSim.Bridge;

public enum ScaleMode
{
    /// <summary>Proporção real. Honesto e quase sempre ilegível.</summary>
    Linear,

    /// <summary>Curva perceptual: comprime o exterior sem esmagar o interior.</summary>
    Logarithmic,
}

/// <summary>
/// Um nível da hierarquia visual: o raio da maior órbita que ele contém e quantos pixels
/// essa órbita deve ocupar na tela.
/// </summary>
/// <param name="MaxDistanceKm">Apoapsis do filho mais distante deste pai.</param>
/// <param name="ScreenRadiusPixels">Raio em pixels a que essa distância é mapeada.</param>
public readonly record struct OrbitLevel(double MaxDistanceKm, double ScreenRadiusPixels);

/// <summary>
/// Converte distâncias físicas em pixels. Sem nada do Godot: é aritmética, e aritmética
/// se testa.
/// </summary>
/// <remarks>
/// O problema visual do Sistema Solar em escala real é que ele é quase todo vazio. Netuno
/// está 78 vezes mais longe do Sol que Mercúrio, então qualquer escala linear que caiba
/// na tela empilha os planetas internos em um punhado de pixels. A curva
/// <c>ln(1 + α·r) / ln(1 + α·r_max)</c> resolve isso comprimindo o exterior, e o fator de
/// compressão é o quanto dessa compressão se quer.
/// </remarks>
public sealed class ScaleMapper
{
    /// <summary>Duração da transição entre os dois modos, em segundos.</summary>
    public const double TransitionSeconds = 0.6;

    private const double MinBodyRadiusPixels = 2.5;
    private const double MaxBodyRadiusPixels = 15.0;

    private double _pixelsPerKm = 250.0 / AstroConstants.AstronomicalUnitKm;
    private double _compressionFactor = 30.0;
    private double _blend = 1.0;

    /// <summary>Fator do modo linear. O padrão coloca 1 UA a 250 pixels da origem.</summary>
    public double PixelsPerKm
    {
        get => _pixelsPerKm;
        set => Set(ref _pixelsPerKm, value);
    }

    /// <summary>
    /// Quanto vale <c>α·r_max</c> na curva perceptual. É adimensional de propósito: o
    /// mesmo valor produz a mesma forma de curva no sistema solar e no sistema de
    /// Júpiter, cada um na sua escala. Maior comprime mais o exterior.
    /// </summary>
    public double CompressionFactor
    {
        get => _compressionFactor;
        set => Set(ref _compressionFactor, value);
    }

    /// <summary>Mistura dos dois modos: 0 é linear puro, 1 é logarítmico puro.</summary>
    public double Blend
    {
        get => _blend;
        private set => Set(ref _blend, value);
    }

    /// <summary>Para onde a transição está indo.</summary>
    public ScaleMode Mode { get; private set; } = ScaleMode.Logarithmic;

    /// <summary>
    /// Incrementado a cada mudança que altera a geometria projetada. É o que permite ao
    /// desenho das órbitas manter um cache e saber, sem recalcular nada, se ele venceu.
    /// </summary>
    public int Revision { get; private set; }

    public void ToggleMode()
        => Mode = Mode == ScaleMode.Linear ? ScaleMode.Logarithmic : ScaleMode.Linear;

    /// <summary>
    /// Avança a transição. O passo é em cosseno levantado, que chega e sai com derivada
    /// nula: sem isso, a troca de modo dá um solavanco no início e no fim.
    /// </summary>
    public void Advance(double deltaSeconds)
    {
        var target = Mode == ScaleMode.Logarithmic ? 1.0 : 0.0;

        if (Blend == target)
        {
            return;
        }

        var step = deltaSeconds / TransitionSeconds;

        Blend = target > Blend
            ? Math.Min(target, Blend + step)
            : Math.Max(target, Blend - step);
    }

    /// <summary>Fração já percorrida da transição, suavizada.</summary>
    public double SmoothBlend => 0.5 - (0.5 * Math.Cos(Blend * Math.PI));

    /// <summary>Distância ao corpo pai, em pixels, no nível informado.</summary>
    public double OrbitRadiusPixels(double distanceKm, in OrbitLevel level)
    {
        if (distanceKm <= 0.0)
        {
            return 0.0;
        }

        var linear = distanceKm * PixelsPerKm;

        if (level.MaxDistanceKm <= 0.0)
        {
            return linear;
        }

        var alpha = CompressionFactor / level.MaxDistanceKm;
        var perceptual = Math.Log(1.0 + (alpha * distanceKm))
            / Math.Log(1.0 + CompressionFactor)
            * level.ScreenRadiusPixels;

        var blend = SmoothBlend;

        return (linear * (1.0 - blend)) + (perceptual * blend);
    }

    /// <summary>
    /// Aplica a escala a um deslocamento em relação ao pai, preservando a direção. Só o
    /// comprimento é deformado: a órbita continua apontando para onde deve.
    /// </summary>
    public Vector3D ToPixels(Vector3D localKm, in OrbitLevel level)
    {
        var distanceKm = localKm.Magnitude;

        return distanceKm <= 0.0
            ? Vector3D.Zero
            : localKm * (OrbitRadiusPixels(distanceKm, level) / distanceKm);
    }

    /// <summary>
    /// Raio visual de um corpo, em pixels. Deliberadamente desacoplado da escala de
    /// distância: em proporção real, a Terra teria centésimos de pixel a 1 UA do Sol, e
    /// aumentar a escala de distância para consertar isso jogaria Netuno para fora do
    /// universo observável.
    /// </summary>
    public double BodyRadiusPixels(double radiusKm)
        => radiusKm <= 0.0
            ? MinBodyRadiusPixels
            : Math.Clamp(
                2.2 * Math.Log(1.0 + (radiusKm / 1000.0)),
                MinBodyRadiusPixels,
                MaxBodyRadiusPixels);

    private void Set(ref double field, double value)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        Revision++;
    }
}
