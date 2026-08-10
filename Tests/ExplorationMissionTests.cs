using SolarSim.Engine.Application;
using SolarSim.Engine.Exploration;

namespace SolarSim.Tests;

public sealed class ExplorationMissionTests
{
    [Fact]
    public void Lua_SemEscudoDeRadiacao_FalhaNoPlanejamento()
    {
        var campaign = NewCampaign();
        var hazard = campaign.Assess("moon");
        Assert.True(hazard.RequiredRadiationShield >= 1, "Lua deveria exigir proteção radiológica");

        var bad = new MissionLoadout
        {
            ThermalShield = hazard.RequiredThermalShield,
            RadiationShield = 0,
            EvaHours = hazard.MaxSafeEvaHours,
            Propellant = 50.0,
        };

        var mission = campaign.Plan("moon");
        var result = mission.Depart(bad);
        Assert.False(result.Ok);
        Assert.Equal(MissionFailureReason.InsufficientRadiationShield, result.Failure);
    }

    [Fact]
    public void Lua_ComCargaAdequada_AmostraEVolta()
    {
        var campaign = NewCampaign();
        var hazard = campaign.Assess("moon");
        var loadout = MissionLoadout.ForHazard(hazard, evaHours: 2.0);

        // Garante propelente no inventário.
        campaign.Inventory.Add(ResourceKind.Propellant, 100.0);
        var result = campaign.Run("moon", loadout, ResourceKind.WaterIce, sampleEvaHours: 2.0);

        Assert.True(result.Ok, result.Message);
        Assert.True(campaign.Inventory.Get(ResourceKind.WaterIce) > 0.0);
    }

    [Fact]
    public void MarteELua_ExigemCargasDiferentes()
    {
        var campaign = NewCampaign();
        var moon = campaign.Assess("moon");
        var mars = campaign.Assess("mars");

        Assert.False(
            moon.RequiredThermalShield == mars.RequiredThermalShield
                && moon.RequiredRadiationShield == mars.RequiredRadiationShield
                && Math.Abs(moon.MaxSafeEvaHours - mars.MaxSafeEvaHours) < 1e-9
                && Math.Abs(moon.TransitPropellantOneWay - mars.TransitPropellantOneWay) < 1e-9,
            "Marte e Lua deveriam diferir no perfil de risco");
    }

    [Fact]
    public void Composicao_BennuTemMaisOrganicosQueLua()
    {
        var campaign = NewCampaign();
        var moon = campaign.CompositionOf("moon");
        var bennu = campaign.CompositionOf("bennu");

        Assert.True(bennu.Deposits.ContainsKey(ResourceKind.Organics));
        Assert.False(moon.Deposits.ContainsKey(ResourceKind.Organics));
        Assert.True(
            bennu.Deposits[ResourceKind.Organics].Abundance
                > moon.Deposits.GetValueOrDefault(ResourceKind.Organics).Abundance);
    }

    [Fact]
    public void Campanha_RefinaGeloParaProximaIda()
    {
        var campaign = NewCampaign(startingPropellant: 0.0);
        campaign.Inventory.Add(ResourceKind.Propellant, 40.0);

        var moon = campaign.Assess("moon");
        var loadout = MissionLoadout.ForHazard(moon, 2.0);
        var first = campaign.Run("moon", loadout, ResourceKind.WaterIce, 2.0);
        Assert.True(first.Ok, first.Message);

        var ice = campaign.Inventory.Get(ResourceKind.WaterIce);
        Assert.True(ice > 0.0);

        var beforeProp = campaign.Inventory.Get(ResourceKind.Propellant);
        var produced = campaign.RefineIce(ice);
        Assert.True(produced > 0.0);
        Assert.True(campaign.Inventory.Get(ResourceKind.Propellant) > beforeProp);
        Assert.Equal(0.0, campaign.Inventory.Get(ResourceKind.WaterIce));
    }

    [Fact]
    public void Partida_SemPropelenteNoInventario_FalhaSemQueimarMissao()
    {
        var campaign = NewCampaign(startingPropellant: 0.5);
        var hazard = campaign.Assess("moon");
        var loadout = MissionLoadout.ForHazard(hazard, 2.0);
        Assert.True(loadout.Propellant > 0.5);

        var result = campaign.Run("moon", loadout);
        Assert.False(result.Ok);
        Assert.Equal(MissionFailureReason.InsufficientPropellant, result.Failure);
        Assert.Equal(0.5, campaign.Inventory.Get(ResourceKind.Propellant));
    }

    private static ExplorationCampaign NewCampaign(double startingPropellant = 12.0)
    {
        var session = SimSession.FromDataRoot(SolarSystem.RepoRootPath);
        var compositions = CompositionLoader.FromFile(SolarSystem.CompositionPath);
        return new ExplorationCampaign(session, compositions, startingPropellant);
    }
}
