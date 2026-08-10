using SolarSim.Engine;
using SolarSim.Engine.Core;
using SolarSim.Engine.Data;
using SolarSim.Engine.Models;

namespace SolarSim.Tests;

/// <summary>
/// M19: Lambert, janelas de Δv e impulso. Preview só consulta; aplicar emenda um arco.
/// </summary>
public sealed class LambertTransferTests
{
    /// <summary>
    /// Transferência entre duas posições fora do alinhamento: a sonda que parte com a
    /// velocidade de Lambert chega à segunda posição no tempo pedido.
    /// </summary>
    [Fact]
    public void SolucaoLevaDeR1AR2NoTempoPedido()
    {
        var mu = AstroConstants.SunMuKm3S2;
        var r1 = new Vector3D(AstroConstants.AstronomicalUnitKm, 0.0, 0.0);
        var r2 = new Vector3D(
            0.2 * AstroConstants.AstronomicalUnitKm,
            1.4 * AstroConstants.AstronomicalUnitKm,
            0.0);
        var tofDays = 180.0;
        var tofSeconds = tofDays * AstroConstants.SecondsPerDay;

        var solution = LambertSolver.TrySolve(r1, r2, tofSeconds, mu, shortWay: true);

        Assert.NotNull(solution);

        var elements = OrbitDetermination.ElementsFrom(
            new StateVector(r1, solution!.Value.DepartureVelocityKmS),
            mu,
            daysSinceEpoch: 0.0);

        var arrived = KeplerPropagator.StateAt(elements, mu, tofDays);
        var miss = (arrived.PositionKm - r2).Magnitude;

        Assert.True(
            miss < 1_000.0,
            $"Chegou a {miss:N0} km de distância do alvo.");
    }

    /// <summary>
    /// Hohmann quase coplanar (170°): Δv de partida e chegada na ordem de km/s, e o
    /// tempo de meia elipse é a referência.
    /// </summary>
    [Fact]
    public void HohmannAproximadoTemDeltaVNaOrdemDeQuilometrosPorSegundo()
    {
        var mu = AstroConstants.SunMuKm3S2;
        var r1 = AstroConstants.AstronomicalUnitKm;
        var r2 = 1.524 * AstroConstants.AstronomicalUnitKm;
        var aTransfer = 0.5 * (r1 + r2);
        var tof = Math.PI * Math.Sqrt(aTransfer * aTransfer * aTransfer / mu);

        // 170° em vez de 180°: a formulação clássica tem singularidade no alinhamento.
        var angle = AstroConstants.DegreesToRadians(170.0);
        var position1 = new Vector3D(r1, 0.0, 0.0);
        var position2 = new Vector3D(r2 * Math.Cos(angle), r2 * Math.Sin(angle), 0.0);

        var solution = LambertSolver.TrySolve(position1, position2, tof, mu, shortWay: true);

        Assert.NotNull(solution);

        var vCircular1 = Math.Sqrt(mu / r1);
        var vCircular2 = Math.Sqrt(mu / r2);
        var departureDeltaV =
            (solution!.Value.DepartureVelocityKmS - new Vector3D(0.0, vCircular1, 0.0))
            .Magnitude;
        var arrivalDeltaV =
            (solution.Value.ArrivalVelocityKmS
                - new Vector3D(-vCircular2 * Math.Sin(angle), vCircular2 * Math.Cos(angle), 0.0))
            .Magnitude;

        Assert.InRange(departureDeltaV, 1.5, 6.0);
        Assert.InRange(arrivalDeltaV, 1.5, 6.0);
    }

    /// <summary>
    /// Terra→Marte no ToF Hohmann clássico: Δv total na ordem de alguns km/s, não de
    /// dezenas nem de metros.
    /// </summary>
    [Fact]
    public void TerraMarteNoTofHohmannTemDeltaVNaOrdemEsperada()
    {
        var sim = SolarSystem.NewEngine();

        // Varre um ano a partir de J2000: a geometria em J2000 puro não é a Hohmann.
        var samples = TransferPlanner.ScanWindows(
            sim,
            "earth",
            "mars",
            departureJdStart: AstroConstants.J2000,
            departureJdEnd: AstroConstants.J2000 + 800.0,
            departureStepDays: 20.0,
            timeOfFlightDaysMin: 150.0,
            timeOfFlightDaysMax: 320.0,
            timeOfFlightStepDays: 20.0);

        Assert.NotEmpty(samples);

        var melhor = samples[0];

        // Hohmann ideal ~5,6 km/s; janelas reais com inclinação e fase ficam um pouco
        // acima. Acima de 20 km/s seria geometria absurda ou solver quebrado.
        Assert.InRange(melhor.TotalDeltaVKmS, 5.0, 18.0);
    }

    [Fact]
    public void PreviewNaoAlteraOEstadoDoMotor()
    {
        var sim = SolarSystem.NewEngine();
        var antes = sim.StateAt("earth", AstroConstants.J2000);
        var dinamicosAntes = sim.DynamicBodyIds.Count;

        _ = TransferPlanner.BestPreview(
            sim, "earth", "mars", AstroConstants.J2000, 259.0);

        var depois = sim.StateAt("earth", AstroConstants.J2000);

        Assert.Equal(antes.PositionKm.X, depois.PositionKm.X);
        Assert.Equal(antes.VelocityKmS.X, depois.VelocityKmS.X);
        Assert.Equal(dinamicosAntes, sim.DynamicBodyIds.Count);
        Assert.Empty(sim.DynamicBodyIds);
    }

    [Fact]
    public void ImpulsoEmendaUmArcoNovoNaSonda()
    {
        var sim = SolarSystem.NewEngine();
        var jd = AstroConstants.J2000;

        // Sonda heliocêntrica na posição/velocidade da Terra.
        var terra = sim.StateAt("earth", jd) - sim.StateAt("sun", jd);

        sim.AddFromState(
            new CelestialBodyData
            {
                Id = "sonda",
                Name = "Sonda",
                ParentId = "sun",
                Kind = BodyKind.Spacecraft,
                MuKm3S2 = 0.0,
                RadiusKm = 0.0,
            },
            terra,
            jd);

        Assert.Single(sim.TrajectoryOf("sonda"));

        var preview = TransferPlanner.BestPreview(sim, "earth", "mars", jd, 259.0);

        Assert.NotNull(preview);

        var deltaV = preview!.Value.DepartureVelocityKmS - terra.VelocityKmS;
        var posicaoAntes = sim.StateAt("sonda", jd).PositionKm;

        sim.ApplyImpulse("sonda", deltaV, jd);

        var arcos = sim.TrajectoryOf("sonda");

        Assert.Equal(2, arcos.Count);
        Assert.Equal(jd, arcos[1].StartJulianDate);
        Assert.Equal("sun", arcos[1].ParentId);

        // Posição contínua; velocidade muda.
        var depois = sim.StateAt("sonda", jd);

        Assert.Equal(posicaoAntes.X, depois.PositionKm.X, tolerance: 1e-6);
        Assert.Equal(
            preview.Value.DepartureVelocityKmS.Magnitude,
            depois.VelocityKmS.Magnitude,
            tolerance: 1e-6);
    }

    /// <summary>
    /// Depois do impulso de partida, a sonda chega perto de Marte no fim do ToF — a
    /// prova de que a transferência não é só um número na tela.
    /// </summary>
    [Fact]
    public void DepoisDoImpulsoASondaEncontraMarteNoFimDoVoo()
    {
        var sim = SolarSystem.NewEngine();

        var samples = TransferPlanner.ScanWindows(
            sim,
            "earth",
            "mars",
            AstroConstants.J2000,
            AstroConstants.J2000 + 800.0,
            20.0,
            150.0,
            320.0,
            20.0);

        var melhorAmostra = samples[0];
        var preview = TransferPlanner.BestPreview(
            sim,
            "earth",
            "mars",
            melhorAmostra.DepartureJulianDate,
            melhorAmostra.TimeOfFlightDays);

        Assert.NotNull(preview);

        var jd = preview!.Value.DepartureJulianDate;
        var terra = sim.StateAt("earth", jd) - sim.StateAt("sun", jd);

        sim.AddFromState(
            new CelestialBodyData
            {
                Id = "sonda",
                Name = "Sonda",
                ParentId = "sun",
                Kind = BodyKind.Spacecraft,
                MuKm3S2 = 0.0,
                RadiusKm = 0.0,
            },
            terra,
            jd);

        sim.ApplyImpulse(
            "sonda",
            preview.Value.DepartureVelocityKmS - terra.VelocityKmS,
            jd);

        var chegada = preview.Value.ArrivalJulianDate;
        var sonda = sim.StateAt("sonda", chegada).PositionKm;
        var marte = sim.StateAt("mars", chegada).PositionKm;
        var erro = (sonda - marte).Magnitude;

        // Lambert liga as posições heliocêntricas; o erro residual é numérico do solver.
        Assert.True(
            erro < 500_000.0,
            $"Miss de {erro:N0} km na chegada — esperado bem abaixo de 0,5 milhão de km.");
    }

    [Fact]
    public void ImpulsoEmCorpoDoArquivoERecusado()
    {
        var sim = SolarSystem.NewEngine();

        var erro = Assert.Throws<SystemDataException>(
            () => sim.ApplyImpulse("earth", new Vector3D(1.0, 0.0, 0.0), AstroConstants.J2000));

        Assert.Contains("dinâmico", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TempoReversoDesfazOImpulso()
    {
        var sim = SolarSystem.NewEngine();
        var jd = AstroConstants.J2000;

        // Longe de qualquer planeta, para o Advance ao voltar no tempo não tentar
        // reatribuir a sonda a uma esfera de influência.
        var estado = new StateVector(
            new Vector3D(3.0 * AstroConstants.AstronomicalUnitKm, 0.0, 0.0),
            new Vector3D(0.0, 15.0, 0.0));

        sim.AddFromState(
            new CelestialBodyData
            {
                Id = "sonda",
                Name = "Sonda",
                ParentId = "sun",
                Kind = BodyKind.Spacecraft,
                MuKm3S2 = 0.0,
                RadiusKm = 0.0,
            },
            estado,
            jd);

        sim.ApplyImpulse("sonda", new Vector3D(0.5, 0.0, 0.0), jd);
        Assert.Equal(2, sim.TrajectoryOf("sonda").Count);

        sim.Time.JumpTo(jd - 1.0);
        sim.Advance(0.0);

        Assert.Single(sim.TrajectoryOf("sonda"));
    }
}
