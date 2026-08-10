namespace SolarSim.Engine.Models;

/// <summary>
/// Parâmetros de forças não gravitacionais declarados no arquivo. Ausentes, o corpo
/// propaga só pela gravidade — planetas e luas não os têm, e é assim que o M18 os deixa
/// de fora sem regra especial.
/// </summary>
/// <remarks>
/// O motor não inventa estes números: o efeito Yarkovsky depende de spin, inércia térmica
/// e forma, e a pressão de radiação de área e refletância. Quem os mediu declara; quem
/// não mediu fica sem drift.
/// </remarks>
public readonly record struct NonGravitationalParameters(
    double? YarkovskyDaAuPerMyr,
    double? RadiationPressureBeta)
{
    /// <summary>Sem parâmetros: o caminho de antes do M18.</summary>
    public static NonGravitationalParameters None => default;

    /// <summary>Verdadeiro quando nenhum dos dois efeitos está declarado.</summary>
    public bool IsAbsent
        => YarkovskyDaAuPerMyr is null && RadiationPressureBeta is null;

    /// <summary>Verdadeiro quando há drift secular de Yarkovsky declarado.</summary>
    public bool HasYarkovsky => YarkovskyDaAuPerMyr is not null;

    /// <summary>Verdadeiro quando o coeficiente β de pressão de radiação está declarado.</summary>
    public bool HasRadiationPressure => RadiationPressureBeta is not null;
}
