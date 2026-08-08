using SolarSim.Engine.Core;
using SolarSim.Engine.Models;

namespace SolarSim.Engine.Data;

/// <summary>
/// Sol e Terra em código, para a fatia vertical do M1. Substituído pela leitura de
/// <c>Data/solar_system_j2000.json</c> no M3.
/// </summary>
/// <remarks>
/// Elementos da Terra conforme a tabela de elementos keplerianos aproximados do JPL
/// para a época J2000.0, referidos à eclíptica. A tabela publica longitude média (L) e
/// longitude do periélio (varpi), de onde saem o argumento do periélio
/// (omega = varpi - Omega) e a anomalia média (M0 = L - varpi).
/// </remarks>
public sealed class HardcodedBodyRepository : IBodyRepository
{
    public IReadOnlyList<CelestialBodyData> LoadBodies() =>
    [
        new CelestialBodyData
        {
            Id = "sun",
            Name = "Sol",
            ParentId = null,
            MuKm3S2 = AstroConstants.SunMuKm3S2,
            RadiusKm = 695_700.0,
            Elements = null,
            ColorRgb = 0xFFD9_66,
        },
        new CelestialBodyData
        {
            Id = "earth",
            Name = "Terra",
            ParentId = "sun",
            MuKm3S2 = AstroConstants.EarthMuKm3S2,
            RadiusKm = 6_371.0,
            Elements = OrbitalElements.FromAuAndDegrees(
                semiMajorAxisAu: 1.00000261,
                eccentricity: 0.01671123,
                inclinationDeg: -0.00001531,
                longitudeOfAscendingNodeDeg: 0.0,
                argumentOfPeriapsisDeg: 102.93768193,
                meanAnomalyAtEpochDeg: -2.47311027),
            ColorRgb = 0x4A_90D9,
        },
    ];
}
