using SolarSim.Engine;
using SolarSim.Engine.Core;
using SolarSim.Engine.Models;

namespace SolarSim.Bridge;

/// <summary>
/// Retrato de um corpo em um instante, com tudo o que o inspetor mostra.
/// </summary>
/// <remarks>
/// Montado por consulta ao motor a cada atualização e descartado em seguida. É isso que
/// impede a interface de guardar cópia própria do estado da simulação e, com o tempo,
/// mostrar um número que já não é verdade.
/// </remarks>
public readonly record struct BodyReport
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    /// <summary>Nome do corpo em torno do qual este orbita. Nulo para a raiz.</summary>
    public string? ParentName { get; init; }

    /// <summary>Nome do corpo na origem, que é o que rotula a distância heliocêntrica.</summary>
    public required string RootName { get; init; }

    public double RadiusKm { get; init; }

    public double MuKm3S2 { get; init; }

    /// <summary>Distância ao corpo pai. Zero para a raiz.</summary>
    public double DistanceToParentKm { get; init; }

    public double DistanceToRootKm { get; init; }

    /// <summary>
    /// Velocidade relativa ao pai: a que os elementos orbitais descrevem. Para a Lua são
    /// cerca de 1 km/s, e não os 30 km/s com que ela acompanha a Terra em torno do Sol.
    /// </summary>
    public double SpeedRelativeToParentKmS { get; init; }

    public double SpeedRelativeToRootKmS { get; init; }

    /// <summary>
    /// Elementos da órbita como estão nesta data, com as taxas seculares já aplicadas —
    /// e não os de J2000. É por isso que o argumento do periápside no painel se mexe
    /// quando o tempo corre. Nulos para a raiz, que não orbita nada.
    /// </summary>
    public OrbitalElements? Elements { get; init; }

    /// <summary>
    /// Quanto o periápside gira por segundo, somando relatividade, achatamento do pai e
    /// o que o arquivo declarar. Zero quando a órbita não precessa.
    /// </summary>
    public double ApsidalPrecessionRadPerSecond { get; init; }

    /// <summary>
    /// Período orbital. Infinito quando a órbita é aberta, porque não há volta a
    /// completar.
    /// </summary>
    public double PeriodDays { get; init; }

    public double PeriapsisKm { get; init; }

    /// <summary>Infinito na órbita aberta.</summary>
    public double ApoapsisKm { get; init; }

    /// <summary>
    /// Raio da esfera de influência deste corpo. Zero para quem não tem massa e infinito
    /// para a raiz.
    /// </summary>
    public double SphereOfInfluenceKm { get; init; }

    /// <summary>
    /// Verdadeiro para um corpo acrescentado em tempo de execução, que é quem pode trocar
    /// de corpo pai e quem entra no arquivo salvo.
    /// </summary>
    public bool IsDynamic { get; init; }

    /// <summary>
    /// Ângulo entre o periápside e a posição atual, medido no foco. É o único elemento
    /// que muda com o tempo, e portanto o que mostra a órbita andando.
    /// </summary>
    public double TrueAnomalyRad { get; init; }

    public bool IsRoot => Elements is null;

    /// <summary>Consulta o motor e monta o retrato. Não altera nada: o relógio não anda.</summary>
    public static BodyReport For(SimEngine sim, string bodyId, double julianDate)
    {
        ArgumentNullException.ThrowIfNull(sim);

        var body = sim.BodyOf(bodyId);
        var root = sim.Root;

        var global = sim.StateAt(bodyId, julianDate);
        var local = sim.LocalStateAt(bodyId, julianDate);

        // Subtrair o estado da raiz em vez de assumir que ela está parada na origem:
        // hoje está, mas quem lê isto não precisa saber disso para confiar no número.
        var rootState = sim.StateAt(root.Id, julianDate);

        var report = new BodyReport
        {
            Id = body.Id,
            Name = body.Name,
            ParentName = body.ParentId is { } parentId ? sim.BodyOf(parentId).Name : null,
            RootName = root.Name,
            RadiusKm = body.RadiusKm,
            MuKm3S2 = body.MuKm3S2,
            DistanceToParentKm = local.PositionKm.Magnitude,
            DistanceToRootKm = (global.PositionKm - rootState.PositionKm).Magnitude,
            SpeedRelativeToParentKmS = local.VelocityKmS.Magnitude,
            SpeedRelativeToRootKmS = (global.VelocityKmS - rootState.VelocityKmS).Magnitude,
            SphereOfInfluenceKm = sim.SphereOfInfluenceKm(bodyId),
            IsDynamic = sim.IsDynamic(bodyId),
        };

        if (sim.ElementsAt(bodyId, julianDate) is not { } elements)
        {
            return report;
        }

        var mu = sim.GravitationalParameterOf(bodyId);

        // Deslocamento zero: os elementos consultados já se referem a esta data.
        var trueAnomaly = KeplerPropagator.TrueAnomalyAt(elements, mu, 0.0);

        return report with
        {
            Elements = elements,
            ApsidalPrecessionRadPerSecond =
                sim.SecularRatesOf(bodyId).ArgumentOfPeriapsisRadPerSecond,
            PeriodDays = elements.IsClosed
                ? KeplerPropagator.OrbitalPeriodDays(elements.SemiMajorAxisKm, mu)
                : double.PositiveInfinity,
            PeriapsisKm = elements.PeriapsisKm,
            ApoapsisKm = elements.ApoapsisKm,
            TrueAnomalyRad = AstroConstants.NormalizeAngle(trueAnomaly),
        };
    }
}
