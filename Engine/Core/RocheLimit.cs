using SolarSim.Engine.Models;

namespace SolarSim.Engine.Core;

/// <summary>
/// A distância abaixo da qual a maré do corpo pai vence a gravidade própria do satélite e
/// o desmancha.
/// </summary>
/// <remarks>
/// A conta é a comparação entre duas acelerações na superfície do satélite: a diferença
/// de atração do pai entre o lado próximo e o centro, que puxa o corpo em duas metades, e
/// a gravidade do próprio satélite, que o mantém junto. Onde as duas se igualam está o
/// limite.
///
/// Nada aqui depende de resistência do material, e é por isso que o limite vale para uma
/// pilha de escombros e não para uma pedra: um corpo de dez metros passa raspando na
/// atmosfera de Júpiter sem se partir, porque a coesão dele não é gravitacional. O limite
/// é sobre corpos grandes o bastante para serem redondos por gravidade — que são
/// exatamente os que este simulador tem.
/// </remarks>
public static class RocheLimit
{
    /// <summary>
    /// Coeficiente do caso fluido. Sai da condição de equilíbrio de um elipsoide de
    /// Roche — o corpo se alonga na direção do pai, o que o torna mais fácil de romper —
    /// e não tem forma fechada bonita: 2,455 é o valor numérico clássico.
    /// </summary>
    public const double FluidCoefficient = 2.455;

    /// <summary>
    /// Densidade típica de gelo de água, em g/cm³. É de que são feitos os anéis, e a
    /// densidade que se supõe para escombros no sistema exterior.
    /// </summary>
    public const double IceDensityGCm3 = 0.9;

    /// <summary>
    /// Limite rígido: o satélite mantém a forma esférica e só se desfaz quando a maré
    /// arranca material da superfície. É o menor dos dois — o caso otimista.
    /// </summary>
    /// <remarks>
    /// Repare no que a fórmula <em>não</em> pede. Escrita em densidades ela é
    /// <c>R_pai·(2ρ_pai/ρ_sat)^⅓</c> e parece precisar do raio do pai; trocando as
    /// densidades por GM e raio, o raio do pai se cancela junto com a constante
    /// gravitacional, e sobram o raio do satélite e a razão entre as massas. É a mesma
    /// equação, escrita com o que se conhece melhor: a massa de um planeta se mede com
    /// precisão de oito casas, e o raio dele depende de onde se decide que a atmosfera
    /// termina.
    /// </remarks>
    public static double RigidKm(
        double satelliteRadiusKm,
        double satelliteMuKm3S2,
        double parentMuKm3S2)
        => Scaled(satelliteRadiusKm, satelliteMuKm3S2, parentMuKm3S2, Math.Cbrt(2.0));

    /// <summary>
    /// Limite fluido: o satélite se deforma sob a maré, o que o alonga e o entrega mais
    /// cedo. É o maior dos dois, e o realista para corpo sem coesão.
    /// </summary>
    public static double FluidKm(
        double satelliteRadiusKm,
        double satelliteMuKm3S2,
        double parentMuKm3S2)
        => Scaled(satelliteRadiusKm, satelliteMuKm3S2, parentMuKm3S2, FluidCoefficient);

    /// <summary>
    /// Limite fluido para escombros de densidade suposta, que é o caso de um anel: não há
    /// satélite de que tomar raio e massa, e sim material do qual se conhece só de que é
    /// feito.
    /// </summary>
    /// <remarks>
    /// Aqui quem se cancela é o raio do <em>pai</em>: substituindo ρ_pai por
    /// GM/(G·⁴⁄₃πR³) na forma em densidades, o R³ do denominador anula o R da frente. O
    /// limite para gelo em torno de um planeta depende só da massa do planeta — dois
    /// planetas de mesma massa e tamanhos diferentes têm o mesmo anel possível, na mesma
    /// distância.
    /// </remarks>
    public static double FluidForDebrisKm(double parentMuKm3S2, double debrisDensityGCm3)
    {
        if (parentMuKm3S2 <= 0.0 || debrisDensityGCm3 <= 0.0)
        {
            return 0.0;
        }

        var debrisMassPerKm3 = debrisDensityGCm3 * 1.0e12;

        var cube = 3.0 * parentMuKm3S2
            / (4.0
                * Math.PI
                * BodyMass.GravitationalConstantKm3PerKgS2
                * debrisMassPerKm3);

        return FluidCoefficient * Math.Cbrt(cube);
    }

    /// <summary>
    /// O destino de um satélite que passa a <paramref name="periapsisKm"/> do pai.
    /// </summary>
    /// <remarks>
    /// A comparação é com o periápside, e não com o semi-eixo: o corpo se parte no ponto
    /// mais próximo, e uma órbita excêntrica que só mergulha na zona de Roche uma vez por
    /// volta já basta. Foi assim que o Shoemaker-Levy 9 virou um colar de vinte fragmentos
    /// dois anos antes de cair em Júpiter.
    /// </remarks>
    public static SatelliteFate FateAt(double periapsisKm, double rigidKm, double fluidKm)
    {
        if (periapsisKm <= 0.0 || fluidKm <= 0.0)
        {
            return SatelliteFate.Unknown;
        }

        if (periapsisKm < rigidKm)
        {
            return SatelliteFate.Disrupted;
        }

        return periapsisKm < fluidKm ? SatelliteFate.AtRisk : SatelliteFate.Stable;
    }

    private static double Scaled(
        double satelliteRadiusKm,
        double satelliteMuKm3S2,
        double parentMuKm3S2,
        double coefficient)
    {
        if (satelliteRadiusKm <= 0.0 || satelliteMuKm3S2 <= 0.0 || parentMuKm3S2 <= 0.0)
        {
            return 0.0;
        }

        return coefficient * satelliteRadiusKm * Math.Cbrt(parentMuKm3S2 / satelliteMuKm3S2);
    }
}
