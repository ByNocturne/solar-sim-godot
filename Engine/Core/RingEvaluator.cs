using SolarSim.Engine.Models;

namespace SolarSim.Engine.Core;

/// <summary>
/// Onde um corpo poderia sustentar um anel, e se poderia.
/// </summary>
/// <remarks>
/// Um anel é o que sobra quando há material onde ele não consegue se juntar. Fora do
/// limite de Roche os fragmentos se atraem mais do que a maré os separa, e em algumas
/// órbitas viram uma lua; dentro dele a maré vence sempre, e o material fica espalhado
/// pela órbita indefinidamente. É por isso que Saturno tem anéis <em>e</em> luas, com a
/// fronteira entre uns e outras caindo quase exatamente no limite de Roche.
///
/// A parte de Roche é física. O resto é heurística, e vale a pena dizer o que ela não é:
/// não há aqui evolução de disco, ressonância com lua pastora nem tempo de vida do anel.
/// A pergunta respondida é a mais fraca das possíveis — "há espaço para um anel, e há de
/// que fazê-lo?" —, e ainda assim ela separa certo os corpos do Sistema Solar.
/// </remarks>
public static class RingEvaluator
{
    /// <summary>
    /// Distância ao Sol além da qual o gelo de água sobrevive, em UA. É a linha de gelo do
    /// disco protoplanetário: aquém dela a água está em vapor e os corpos se formam secos,
    /// além dela o gelo é o material mais abundante que existe.
    /// </summary>
    /// <remarks>
    /// O valor exato depende do modelo de disco, e 2,7 UA é o convencional. A validação
    /// dele aqui é que a linha cai entre Vesta e Ceres — os dois maiores do cinturão, um
    /// basáltico e seco, o outro com gelo de água —, que é onde a mineralogia diz que ela
    /// deve cair.
    /// </remarks>
    public const double IceLineAu = 2.7;

    /// <summary>
    /// Avalia um corpo como hospedeiro de anel.
    /// </summary>
    /// <param name="muKm3S2">GM do corpo.</param>
    /// <param name="radiusKm">Raio médio: o piso do anel, porque abaixo dele é superfície.</param>
    /// <param name="orbitsStar">
    /// Se o corpo orbita a raiz do sistema. Um satélite é recusado de saída — não porque
    /// um anel em torno de uma lua seja impossível, mas porque a vizinhança de uma lua é
    /// governada pela maré do planeta, e este modelo de dois corpos não tem o que dizer
    /// sobre ela. É limite de escopo, e não resultado.
    /// </param>
    /// <param name="semiMajorAxisAu">
    /// Semi-eixo maior da órbita heliocêntrica, para a linha de gelo. É o semi-eixo e não
    /// a distância de hoje: ter anel é propriedade do corpo, não do mês.
    /// </param>
    public static RingZone Evaluate(
        double muKm3S2,
        double radiusKm,
        bool orbitsStar,
        double semiMajorAxisAu)
    {
        var outerKm = RocheLimit.FluidForDebrisKm(muKm3S2, RocheLimit.IceDensityGCm3);

        var hasRoom = outerKm > radiusKm && radiusKm > 0.0;
        var hasIcyDebris = semiMajorAxisAu >= IceLineAu;

        return new RingZone
        {
            InnerRadiusKm = radiusKm,
            OuterRadiusKm = outerKm,
            HasRoom = hasRoom,
            HasIcyDebris = hasIcyDebris,
            IsPlausible = hasRoom && hasIcyDebris && orbitsStar,
        };
    }
}
