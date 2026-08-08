using SolarSim.Engine;
using SolarSim.Engine.Core;
using SolarSim.Engine.Data;
using SolarSim.Engine.Models;

namespace SolarSim.Tests;

public sealed class SimEngineTests
{
    [Fact]
    public void RaizPermaneceNaOrigem()
    {
        var sim = SolarSystem.NewEngine();

        Assert.Equal(Vector3D.Zero, sim.PositionAt("sun", AstroConstants.J2000));
        Assert.Equal(Vector3D.Zero, sim.PositionAt("sun", AstroConstants.J2000 + 10_000.0));
    }

    [Fact]
    public void TerraFicaAProximadamenteUmaUnidadeAstronomicaDoSol()
    {
        var sim = SolarSystem.NewEngine();

        var distancia = sim.PositionAt("earth", AstroConstants.J2000).Magnitude;

        Assert.InRange(
            distancia / AstroConstants.AstronomicalUnitKm,
            0.98,
            1.02);
    }

    [Fact]
    public void AvancoPublicaSnapshotComTodosOsCorpos()
    {
        var sim = SolarSystem.NewEngine();
        SystemStateSnapshot? recebido = null;
        sim.SystemUpdated += snapshot => recebido = snapshot;

        sim.Time.SpeedMultiplier = AstroConstants.SecondsPerDay;
        sim.Advance(realSecondsElapsed: 1.0);

        Assert.NotNull(recebido);
        Assert.Equal(sim.Bodies.Count, recebido!.Value.Bodies.Count);
        Assert.Equal(AstroConstants.J2000 + 1.0, recebido.Value.JulianDate, precision: 9);
    }

    [Fact]
    public void CorpoDesconhecidoFalhaComMensagemClara()
    {
        var sim = SolarSystem.NewEngine();

        var erro = Assert.Throws<KeyNotFoundException>(
            () => sim.PositionAt("plutao", AstroConstants.J2000));

        Assert.Contains("plutao", erro.Message, StringComparison.Ordinal);
    }
}
