using SolarSim.Engine.Models;

namespace SolarSim.Engine.Core;

/// <summary>
/// Assinaturas químicas simplificadas: desequilíbrio O₂+CH₄ e escudo de ozônio.
/// </summary>
public static class BiosignatureEvaluator
{
    public const double OxygenThreshold = 0.01;
    public const double MethaneThreshold = 1.0e-6;
    public const double OzoneThreshold = 1.0e-7;

    public static bool HasOxygenMethaneDisequilibrium(AtmosphereProfile? atmosphere)
    {
        if (atmosphere is null)
        {
            return false;
        }

        return atmosphere.MoleFractionO2 >= OxygenThreshold
            && atmosphere.MoleFractionCH4 >= MethaneThreshold;
    }

    public static bool HasOzoneShield(AtmosphereProfile? atmosphere)
        => atmosphere is not null && atmosphere.MoleFractionO3 >= OzoneThreshold;
}
