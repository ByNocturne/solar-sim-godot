using SolarSim.Bridge;
using SolarSim.Engine.Core;
using SolarSim.Engine.Models;

namespace SolarSim.Tests;

/// <summary>
/// A curva perceptual e a transição entre os modos. É a aritmética que decide se o
/// Sistema Solar cabe na tela, e ela não precisa do Godot para ser verificada.
/// </summary>
public sealed class ScaleMapperTests
{
    private static readonly OrbitLevel Planetas =
        new(30.33 * AstroConstants.AstronomicalUnitKm, 420.0);

    [Fact]
    public void AMaiorOrbitaDoNivelCaiExatamenteNoRaioDeTela()
    {
        var mapa = Logaritmico();

        Assert.Equal(
            Planetas.ScreenRadiusPixels,
            mapa.OrbitRadiusPixels(Planetas.MaxDistanceKm, Planetas),
            tolerance: 1e-9);
    }

    [Fact]
    public void OrigemFicaNaOrigem()
    {
        Assert.Equal(0.0, Logaritmico().OrbitRadiusPixels(0.0, Planetas));
        Assert.Equal(Vector3D.Zero, Logaritmico().ToPixels(Vector3D.Zero, Planetas));
    }

    [Fact]
    public void CurvaEMonotonaCrescente()
    {
        var mapa = Logaritmico();
        var anterior = -1.0;

        for (var au = 0.1; au <= 31.0; au += 0.1)
        {
            var pixels = mapa.OrbitRadiusPixels(au * AstroConstants.AstronomicalUnitKm, Planetas);

            Assert.True(pixels > anterior, $"A curva recuou em {au:F1} UA.");
            anterior = pixels;
        }
    }

    /// <summary>
    /// O motivo de existir do modo logarítmico: em escala linear, os quatro planetas
    /// internos ocupam 3% do raio ocupado por Netuno e viram um borrão no centro.
    /// </summary>
    [Fact]
    public void ModoLogaritmicoAfastaOsPlanetasInternos()
    {
        var mercurioKm = 0.387 * AstroConstants.AstronomicalUnitKm;
        var netunoKm = 30.07 * AstroConstants.AstronomicalUnitKm;

        var noLinear = Fracao(Linear(), mercurioKm, netunoKm);
        var noLogaritmico = Fracao(Logaritmico(), mercurioKm, netunoKm);

        // Em escala linear, a órbita de Mercúrio ocupa pouco mais de 1% do raio da de
        // Netuno; a curva perceptual leva isso para perto de 10%.
        Assert.True(noLinear < 0.02, $"Mercurio nao estava esmagado no linear: {noLinear:P2}.");
        Assert.InRange(noLogaritmico, 0.05, 0.25);

        static double Fracao(ScaleMapper mapa, double dentroKm, double foraKm)
            => mapa.OrbitRadiusPixels(dentroKm, Planetas)
                / mapa.OrbitRadiusPixels(foraKm, Planetas);
    }

    [Fact]
    public void DirecaoEPreservadaEApenasOComprimentoMuda()
    {
        var mapa = Logaritmico();
        var local = new Vector3D(3.0e8, -4.0e8, 1.0e7);

        var escalado = mapa.ToPixels(local, Planetas);

        var cosseno = escalado.Normalized().Dot(local.Normalized());

        Assert.Equal(1.0, cosseno, tolerance: 1e-12);
        Assert.Equal(
            mapa.OrbitRadiusPixels(local.Magnitude, Planetas),
            escalado.Magnitude,
            tolerance: 1e-6);
    }

    [Fact]
    public void NivelDegeneradoCaiNoModoLinear()
    {
        var mapa = Logaritmico();
        var semFilhos = default(OrbitLevel);

        Assert.Equal(
            1.0e8 * mapa.PixelsPerKm,
            mapa.OrbitRadiusPixels(1.0e8, semFilhos),
            tolerance: 1e-9);
    }

    [Fact]
    public void TransicaoSaiDeUmModoEChegaExatamenteNoOutro()
    {
        var mapa = Logaritmico();

        mapa.ToggleMode();
        Assert.Equal(ScaleMode.Linear, mapa.Mode);

        for (var passo = 0; passo < 100; passo++)
        {
            mapa.Advance(ScaleMapper.TransitionSeconds / 20.0);
        }

        Assert.Equal(0.0, mapa.Blend);
        Assert.Equal(0.0, mapa.SmoothBlend);
    }

    [Fact]
    public void DuranteATransicaoAPosicaoFicaEntreOsDoisModos()
    {
        var distanciaKm = 5.2 * AstroConstants.AstronomicalUnitKm;

        var linear = Linear().OrbitRadiusPixels(distanciaKm, Planetas);
        var log = Logaritmico().OrbitRadiusPixels(distanciaKm, Planetas);

        var mapa = Logaritmico();
        mapa.ToggleMode();
        mapa.Advance(ScaleMapper.TransitionSeconds / 2.0);

        var meio = mapa.OrbitRadiusPixels(distanciaKm, Planetas);

        Assert.InRange(meio, Math.Min(linear, log), Math.Max(linear, log));
    }

    /// <summary>
    /// Sem derivada nula nas pontas, a troca de modo dá um solavanco perceptível. O teste
    /// olha para o primeiro passo da transição: ele tem que ser bem menor que o passo do
    /// meio.
    /// </summary>
    [Fact]
    public void TransicaoComecaDevagar()
    {
        var mapa = Logaritmico();
        mapa.ToggleMode();

        var passo = ScaleMapper.TransitionSeconds / 20.0;

        mapa.Advance(passo);
        var primeiro = Math.Abs(1.0 - mapa.SmoothBlend);

        var anterior = mapa.SmoothBlend;
        for (var index = 0; index < 9; index++)
        {
            anterior = mapa.SmoothBlend;
            mapa.Advance(passo);
        }

        var noMeio = Math.Abs(anterior - mapa.SmoothBlend);

        Assert.True(primeiro < noMeio / 4.0, "A transicao arranca com solavanco.");
    }

    [Fact]
    public void MudarAEscalaInvalidaOCacheDasOrbitas()
    {
        var mapa = Logaritmico();
        var inicial = mapa.Revision;

        mapa.PixelsPerKm *= 2.0;
        Assert.NotEqual(inicial, mapa.Revision);

        var depoisDoZoom = mapa.Revision;
        mapa.ToggleMode();
        mapa.Advance(0.1);

        Assert.NotEqual(depoisDoZoom, mapa.Revision);
    }

    [Fact]
    public void EscreverOMesmoValorNaoInvalidaOCache()
    {
        var mapa = Logaritmico();
        var inicial = mapa.Revision;

        mapa.PixelsPerKm = mapa.PixelsPerKm;

        Assert.Equal(inicial, mapa.Revision);
    }

    [Theory]
    [InlineData(695_700.0, 1737.4)]
    [InlineData(69_911.0, 6371.0)]
    [InlineData(6371.0, 2439.7)]
    public void CorpoMaiorTemRaioVisualMaior(double maiorKm, double menorKm)
    {
        var mapa = Logaritmico();

        Assert.True(mapa.BodyRadiusPixels(maiorKm) > mapa.BodyRadiusPixels(menorKm));
    }

    [Fact]
    public void RaioVisualNaoDependeDaEscalaDeDistancia()
    {
        var mapa = Logaritmico();
        var antes = mapa.BodyRadiusPixels(6371.0);

        mapa.PixelsPerKm *= 1000.0;
        mapa.ToggleMode();
        mapa.Advance(ScaleMapper.TransitionSeconds);

        Assert.Equal(antes, mapa.BodyRadiusPixels(6371.0));
    }

    [Fact]
    public void RaioVisualFicaEntreOsLimitesUteis()
    {
        var mapa = Logaritmico();

        foreach (var raioKm in new[] { 0.0, 1.0, 1737.4, 6371.0, 69_911.0, 695_700.0, 1.0e9 })
        {
            Assert.InRange(mapa.BodyRadiusPixels(raioKm), 2.5, 15.0);
        }
    }

    private static ScaleMapper Logaritmico() => new();

    private static ScaleMapper Linear()
    {
        var mapa = new ScaleMapper();
        mapa.ToggleMode();
        mapa.Advance(ScaleMapper.TransitionSeconds);

        return mapa;
    }
}
