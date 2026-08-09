using System.Globalization;
using SolarSim.Engine.Models;

namespace SolarSim.CatalogImporter;

/// <summary>
/// Cor de um corpo menor: a da classe, salvo quando a linha do CSV declara a sua.
/// </summary>
/// <remarks>
/// Os planetas têm cor porque cada um tem uma cor conhecida; um asteroide de quinze
/// quilômetros não tem. O que a cor precisa dizer na tela é a que família o ponto
/// pertence, e por isso o default é por classe: cinco troianos com a mesma cor são cinco
/// troianos, e não cinco pontos escolhidos ao acaso.
/// </remarks>
public static class Palette
{
    public static uint Default(BodyKind kind) => kind switch
    {
        BodyKind.Asteroid => 0x9C8F7A,
        BodyKind.NearEarthAsteroid => 0xE08A4C,
        BodyKind.Trojan => 0xB0706A,
        BodyKind.Centaur => 0xA56ED8,
        BodyKind.Comet => 0x6FD8D8,
        BodyKind.TransNeptunian => 0x8FA8E0,
        _ => 0xFFFFFF,
    };

    public static uint Parse(string? colorRgb, BodyKind kind, int lineNumber)
    {
        if (colorRgb is null)
        {
            return Default(kind);
        }

        var digits = colorRgb.StartsWith('#') ? colorRgb[1..] : colorRgb;

        if (digits.Length != 6
            || !uint.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
        {
            throw new CatalogSourceException(
                $"Linha {lineNumber}: 'colorRgb' vale '{colorRgb}', que não é '#RRGGBB'.");
        }

        return rgb;
    }
}
