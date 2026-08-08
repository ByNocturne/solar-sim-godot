using SolarSim.Engine.Models;

namespace SolarSim.Engine.Core;

/// <summary>
/// O problema inverso: dado onde um corpo está e para onde vai, quais são os elementos
/// da órbita que ele descreve.
/// </summary>
/// <remarks>
/// É o que permite existir uma nave. Um planeta vem de elementos tabelados, mas uma
/// nave vem de um vetor de estado — o ponto em que ela foi solta e a velocidade que
/// levava — e é esta conversão que transforma isso em uma órbita que o propagador sabe
/// percorrer. Serve também à emenda de cônicas, onde o estado relativo ao novo pai vira
/// os elementos do arco seguinte.
/// </remarks>
public static class OrbitDetermination
{
    /// <summary>
    /// Elementos da órbita que passa por este estado, com a anomalia média já recuada
    /// para a época J2000.0.
    /// </summary>
    /// <param name="state">Posição e velocidade relativas ao corpo central.</param>
    /// <param name="parentMuKm3S2">
    /// Parâmetro gravitacional efetivo da órbita, na mesma convenção do propagador.
    /// </param>
    /// <param name="daysSinceEpoch">Instante a que o estado se refere.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Se o estado for degenerado — em cima do foco, sem momento angular — ou se a
    /// cônica resultante for parabólica demais para o semi-eixo maior descrevê-la.
    /// </exception>
    public static OrbitalElements ElementsFrom(
        in StateVector state,
        double parentMuKm3S2,
        double daysSinceEpoch)
    {
        var position = state.PositionKm;
        var velocity = state.VelocityKmS;

        var radius = position.Magnitude;
        var speedSquared = velocity.MagnitudeSquared;

        if (radius == 0.0 || double.IsNaN(radius) || parentMuKm3S2 <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(state),
                "Estado degenerado: um corpo em cima do foco, ou sem corpo central, não "
                    + "descreve órbita alguma.");
        }

        var angularMomentum = position.Cross(velocity);
        var angularMomentumMagnitude = angularMomentum.Magnitude;

        if (angularMomentumMagnitude == 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(state),
                "Momento angular nulo: a trajetória é uma queda radial, que é uma reta e "
                    + "não uma cônica com plano definido.");
        }

        // Vetor excentricidade: aponta para o periápside e tem módulo igual a e.
        var eccentricityVector =
            ((speedSquared - parentMuKm3S2 / radius) * position
                - position.Dot(velocity) * velocity)
            / parentMuKm3S2;

        var eccentricity = eccentricityVector.Magnitude;
        OrbitalElements.RequireRepresentableEccentricity(eccentricity);

        // Vis-viva resolvida para o semi-eixo maior. Sai negativo na hipérbole, que é
        // exatamente a convenção que o propagador espera.
        var semiMajorAxis = 1.0 / (2.0 / radius - speedSquared / parentMuKm3S2);

        // Linha dos nodos: a interseção do plano orbital com o plano de referência.
        var node = new Vector3D(-angularMomentum.Y, angularMomentum.X, 0.0);

        // Por atan2 do seno sobre o cosseno, e não por acos do cosseno: o acos perde
        // metade dos dígitos quando a inclinação é quase nula, que é o caso da maioria
        // dos corpos do Sistema Solar.
        var inclination = Math.Atan2(node.Magnitude, angularMomentum.Z);

        var (longitudeOfAscendingNode, argumentOfPeriapsis, trueAnomaly) = Angles(
            position,
            velocity,
            angularMomentum,
            node,
            eccentricityVector,
            eccentricity);

        var meanAnomaly = KeplerPropagator.MeanAnomalyFromTrue(trueAnomaly, eccentricity);

        // Recuar até a época é o que torna os elementos independentes do instante em que
        // o estado foi medido: a partir daqui eles valem para qualquer data.
        var meanMotion = KeplerPropagator.MeanMotionRadPerSecond(semiMajorAxis, parentMuKm3S2);
        var meanAnomalyAtEpoch =
            meanAnomaly - meanMotion * daysSinceEpoch * AstroConstants.SecondsPerDay;

        // Os ângulos saem de atan2, ou seja, em (-pi, pi]. A forma canônica do projeto é
        // [0, 2pi), a mesma em que a inclinação já sai por construção.
        return new OrbitalElements(
            semiMajorAxis,
            eccentricity,
            inclination,
            AstroConstants.NormalizeAngle(longitudeOfAscendingNode),
            AstroConstants.NormalizeAngle(argumentOfPeriapsis),
            eccentricity < 1.0
                ? AstroConstants.NormalizeAngle(meanAnomalyAtEpoch)
                : meanAnomalyAtEpoch);
    }

    /// <summary>
    /// Os três ângulos que dependem de direções: nodo ascendente, argumento do
    /// periápside e anomalia verdadeira.
    /// </summary>
    /// <remarks>
    /// Duas direções somem em casos particulares: a do nodo, quando a órbita está no
    /// plano de referência, e a do periápside, quando a órbita é circular. Os ramos
    /// abaixo existem só para evitar dividir zero por zero — não para melhorar a
    /// precisão perto da degeneração. Perto dela cada ângulo isolado é mal condicionado,
    /// mas as somas que a propagação usa, Omega + omega e omega + nu, continuam bem
    /// determinadas, e os erros se cancelam no caminho de volta.
    /// </remarks>
    private static (double Node, double Argument, double TrueAnomaly) Angles(
        Vector3D position,
        Vector3D velocity,
        Vector3D angularMomentum,
        Vector3D node,
        Vector3D eccentricityVector,
        double eccentricity)
    {
        var isEquatorial = node.MagnitudeSquared == 0.0;
        var isCircular = eccentricity == 0.0;

        // Sentido de percurso, que é o que distingue a órbita equatorial direta da
        // retrógrada quando não há linha de nodos para orientar os ângulos.
        var direction = angularMomentum.Z >= 0.0 ? 1.0 : -1.0;

        if (isEquatorial && isCircular)
        {
            return (0.0, 0.0, Math.Atan2(direction * position.Y, position.X));
        }

        if (isEquatorial)
        {
            var longitudeOfPeriapsis = Math.Atan2(
                direction * eccentricityVector.Y, eccentricityVector.X);

            return (
                0.0,
                longitudeOfPeriapsis,
                AngleInPlane(position, eccentricityVector, angularMomentum));
        }

        var ascendingNode = Math.Atan2(node.Y, node.X);

        if (isCircular)
        {
            // Sem periápside, o ângulo é contado a partir do nodo: é o argumento de
            // latitude, que a propagação reconstrói somando omega igual a zero.
            return (ascendingNode, 0.0, AngleInPlane(position, node, angularMomentum));
        }

        return (
            ascendingNode,
            AngleInPlane(eccentricityVector, node, angularMomentum),
            AngleInPlane(position, eccentricityVector, angularMomentum));
    }

    /// <summary>
    /// Ângulo de <paramref name="from"/> até <paramref name="target"/>, medido no plano
    /// orbital e no sentido do movimento. Por atan2, e não por acos: acos perde precisão
    /// justamente perto de zero e de pi, e não distingue os dois lados.
    /// </summary>
    private static double AngleInPlane(Vector3D target, Vector3D from, Vector3D normal)
    {
        var ahead = normal.Cross(from);

        return Math.Atan2(
            target.Dot(ahead) / normal.Magnitude,
            target.Dot(from));
    }
}
