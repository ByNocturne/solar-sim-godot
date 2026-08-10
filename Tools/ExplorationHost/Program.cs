using System.Runtime.CompilerServices;
using SolarSim.Engine.Application;
using SolarSim.Engine.Exploration;

namespace SolarSim.ExplorationHost;

/// <summary>
/// Host mínimo fora do Godot: demonstra M20–M22 (SimSession + exploração cuidadosa).
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        var dataRoot = args.Length > 0 ? args[0] : FindRepoRoot();
        var session = SimSession.FromDataRoot(dataRoot);
        var compositions = CompositionLoader.FromFile(
            Path.Combine(dataRoot, CompositionLoader.DefaultRelativePath));
        var campaign = new ExplorationCampaign(session, compositions, startingPropellant: 20.0);

        Console.WriteLine("Solar Sim — Exploration Host (sem Godot)");
        Console.WriteLine($"Corpos carregados: {session.Bodies.Count}");
        Console.WriteLine();

        RunDemo(campaign, "moon");
        Console.WriteLine();
        RunDemo(campaign, "mars");
        Console.WriteLine();

        var ice = campaign.Inventory.Get(ResourceKind.WaterIce);
        if (ice > 0.0)
        {
            var made = campaign.RefineIce(ice);
            Console.WriteLine($"ISRU: {ice:0.###} gelo → {made:0.###} propelente.");
        }

        Console.WriteLine();
        Console.WriteLine("Inventário final:");
        foreach (var (kind, amount) in campaign.Inventory.Amounts.OrderBy(kv => kv.Key.ToString()))
        {
            Console.WriteLine($"  {kind}: {amount:0.###}");
        }

        return 0;
    }

    private static void RunDemo(ExplorationCampaign campaign, string bodyId)
    {
        var hazard = campaign.Assess(bodyId);
        Console.WriteLine($"=== {hazard.Name} ({bodyId}) ===");
        foreach (var note in hazard.HazardNotes)
        {
            Console.WriteLine($"  risco: {note}");
        }

        Console.WriteLine(
            $"  exige térmico={hazard.RequiredThermalShield}, "
                + $"radiação={hazard.RequiredRadiationShield}, "
                + $"EVA≤{hazard.MaxSafeEvaHours:0.#} h");

        var careless = MissionLoadout.Minimal;
        var bad = campaign.Plan(bodyId).Depart(careless);
        Console.WriteLine($"  carga mínima: {(bad.Ok ? "ok" : "FALHOU")} — {bad.Message}");

        var loadout = MissionLoadout.ForHazard(hazard, evaHours: Math.Min(2.0, hazard.MaxSafeEvaHours));
        campaign.Inventory.Add(ResourceKind.Propellant, loadout.Propellant);
        var prefer = bodyId == "mars" ? ResourceKind.WaterIce : ResourceKind.Regolith;
        var result = campaign.Run(bodyId, loadout, prefer, sampleEvaHours: Math.Min(2.0, loadout.EvaHours));
        Console.WriteLine($"  ida preparada: {(result.Ok ? "ok" : "FALHOU")} — {result.Message}");
    }

    private static string FindRepoRoot([CallerFilePath] string thisFile = "")
    {
        var dir = Path.GetDirectoryName(thisFile)!;
        // Tools/ExplorationHost -> repo root
        return Path.GetFullPath(Path.Combine(dir, "..", ".."));
    }
}
