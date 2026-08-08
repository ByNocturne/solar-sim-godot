using SolarSim.Engine.Core;

namespace SolarSim.Tests;

public sealed class TimeEngineTests
{
    [Fact]
    public void EpocaJ2000CorrespondeAoMeioDiaDe1JaneiroDe2000()
    {
        var epoca = new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        Assert.Equal(AstroConstants.J2000, TimeEngine.ToJulianDate(epoca), precision: 9);
    }

    [Theory]
    [InlineData(1969, 7, 20, 20, 17)]
    [InlineData(2000, 1, 1, 12, 0)]
    [InlineData(2026, 8, 8, 13, 36)]
    [InlineData(2149, 12, 31, 23, 59)]
    public void ConversaoDeIdaEVoltaPreservaOInstante(
        int ano, int mes, int dia, int hora, int minuto)
    {
        var original = new DateTime(ano, mes, dia, hora, minuto, 0, DateTimeKind.Utc);

        var recuperado = TimeEngine.ToUtc(TimeEngine.ToJulianDate(original));

        Assert.True(
            (recuperado - original).Duration() < TimeSpan.FromMilliseconds(1),
            $"Esperado {original:O}, obtido {recuperado:O}.");
    }

    [Fact]
    public void AvancoRespeitaMultiplicadorDeVelocidade()
    {
        var time = new TimeEngine { SpeedMultiplier = AstroConstants.SecondsPerDay };

        time.Advance(realSecondsElapsed: 1.0);

        Assert.Equal(1.0, time.DaysSinceEpoch, precision: 9);
    }

    [Fact]
    public void PausaCongelaOTempo()
    {
        var time = new TimeEngine { SpeedMultiplier = 1000.0, IsPaused = true };

        time.Advance(realSecondsElapsed: 5.0);

        Assert.Equal(AstroConstants.J2000, time.JulianDate);
    }

    [Fact]
    public void MultiplicadorNegativoFazOTempoAndarParaTras()
    {
        var time = new TimeEngine { SpeedMultiplier = -AstroConstants.SecondsPerDay };

        time.Advance(realSecondsElapsed: 2.0);

        Assert.Equal(-2.0, time.DaysSinceEpoch, precision: 9);
    }
}
