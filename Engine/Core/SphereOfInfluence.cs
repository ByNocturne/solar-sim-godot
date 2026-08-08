namespace SolarSim.Engine.Core;

/// <summary>
/// O raio dentro do qual um corpo domina a atração sobre o que passa por perto.
/// </summary>
/// <remarks>
/// É a fronteira das cônicas emendadas: dentro dela a nave é tratada como se orbitasse
/// só o planeta, fora dela como se orbitasse só o Sol. A aproximação não é uma
/// simplificação preguiçosa — é o que permite manter o estado como função da data, em
/// vez de integrar N corpos, e é a mesma que planejou todas as missões interplanetárias
/// até a era do cálculo numérico.
/// </remarks>
public static class SphereOfInfluence
{
    /// <summary>
    /// Raio de Laplace: <c>a·(m/M)^(2/5)</c>. O expoente vem de igualar a perturbação que
    /// o Sol causa na órbita em torno do planeta à que o planeta causa na órbita em torno
    /// do Sol.
    /// </summary>
    /// <param name="semiMajorAxisKm">Semi-eixo maior da órbita do próprio corpo.</param>
    /// <param name="bodyMuKm3S2">GM do corpo.</param>
    /// <param name="parentMuKm3S2">GM do corpo em torno do qual ele orbita.</param>
    /// <returns>
    /// O raio em km, ou zero para um corpo sem massa, que não tem esfera de influência
    /// alguma.
    /// </returns>
    public static double RadiusKm(
        double semiMajorAxisKm,
        double bodyMuKm3S2,
        double parentMuKm3S2)
    {
        if (bodyMuKm3S2 <= 0.0 || parentMuKm3S2 <= 0.0)
        {
            return 0.0;
        }

        return Math.Abs(semiMajorAxisKm) * Math.Pow(bodyMuKm3S2 / parentMuKm3S2, 0.4);
    }
}
