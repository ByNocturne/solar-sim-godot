namespace SolarSim.Engine.Models;

/// <summary>
/// Posição e velocidade de um corpo em um instante. Posição em km, velocidade em km/s,
/// ambas relativas ao corpo pai.
/// </summary>
/// <remarks>
/// É o tipo que permite existir uma nave: um corpo cuja órbita não vem de elementos
/// tabelados, e sim de onde ele está e para onde está indo.
/// </remarks>
public readonly record struct StateVector(Vector3D PositionKm, Vector3D VelocityKmS)
{
    /// <summary>
    /// Compõe o estado do pai com o estado local do filho. É a operação que leva a
    /// posição relativa à órbita para o referencial global, e a velocidade junto.
    /// </summary>
    public static StateVector operator +(StateVector parent, StateVector local)
        => new(parent.PositionKm + local.PositionKm, parent.VelocityKmS + local.VelocityKmS);

    /// <summary>
    /// O caminho de volta: o estado de um corpo visto de outro. É o que a emenda de
    /// cônicas mede no instante em que troca o corpo pai.
    /// </summary>
    public static StateVector operator -(StateVector state, StateVector reference)
        => new(
            state.PositionKm - reference.PositionKm,
            state.VelocityKmS - reference.VelocityKmS);

    public double DistanceKm => PositionKm.Magnitude;

    public double SpeedKmS => VelocityKmS.Magnitude;

    /// <summary>
    /// Momento angular específico. Constante ao longo de uma órbita kepleriana, o que
    /// faz dele um bom detector de erro no propagador.
    /// </summary>
    public Vector3D SpecificAngularMomentum => PositionKm.Cross(VelocityKmS);

    /// <summary>
    /// Energia orbital específica pela equação vis-viva. Também constante ao longo da
    /// órbita: negativa para órbitas fechadas, positiva para hiperbólicas.
    /// </summary>
    public double SpecificEnergy(double muKm3S2)
        => (SpeedKmS * SpeedKmS / 2.0) - (muKm3S2 / DistanceKm);
}
