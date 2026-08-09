using System.Globalization;
using System.Text;
using SolarSim.Engine.Models;

namespace SolarSim.Tests;

/// <summary>
/// Compara os corpos do catálogo com efemérides do JPL Horizons em duas datas distantes
/// da época, no mesmo referencial eclíptico J2000 centrado no Sol usado no M2.
/// </summary>
/// <remarks>
/// A tolerância aqui é bem maior que a dos planetas, e a razão não é o propagador: são os
/// corpos. Um asteroide do cinturão principal sente Júpiter muito mais que a Terra sente,
/// e um cometa sente também a força não gravitacional da sublimação, que o modelo de dois
/// corpos não tem. O que este teste fixa é que os elementos do arquivo descrevem mesmo
/// aqueles corpos — um sinal trocado ou uma coluna deslocada erraria por ordens de
/// grandeza, não por um por cento.
///
/// Erros medidos hoje, em 2026, que é a data mais distante: Ceres 0,03%, Plutão 0,21%,
/// Vesta 0,28%, Halley 0,35%, Eros 0,38% e Héctor 1,05%. A ordem é a esperada, e diz
/// quem sente Júpiter: o troiano, que está preso a ele, é o pior caso por uma margem
/// larga, e o cinturão principal fica uma ordem de grandeza melhor.
/// </remarks>
public sealed class MinorBodyRegressionTests
{
    private const double RadialToleranceFraction = 0.015;

    private static readonly Referencia[] Referencias =
    [
        new("ceres", 2456293.5,
            new Vector3D(1.765767503524832e6, 3.975608240608669e8, 1.216465556519902e7)),
        new("ceres", 2461041.5,
            new Vector3D(3.811728039516315e8, 1.926780353254012e8, -6.412224331556011e7)),
        new("vesta", 2456293.5,
            new Vector3D(4.929899503432468e7, 3.807214670513474e8, -1.741016514115344e7)),
        new("vesta", 2461041.5,
            new Vector3D(1.646672628555179e8, -2.850203157647729e8, -1.158945712623374e7)),
        new("eros", 2456293.5,
            new Vector3D(1.814680822782608e8, -1.935811941237848e8, 7.775456408433847e6)),
        new("eros", 2461041.5,
            new Vector3D(2.365054254577367e7, 1.745915852372736e8, 2.254265443077363e7)),
        new("hektor", 2456293.5,
            new Vector3D(-6.462609904746010e8, 4.073742177952880e8, 6.508553344623509e7)),
        new("hektor", 2461041.5,
            new Vector3D(-7.666148780220928e8, 5.348697996793127e7, -5.754531770379354e7)),
        new("pluto", 2456293.5,
            new Vector3D(7.643038457552747e8, -4.771498950933451e9, 2.895975478821502e8)),
        new("pluto", 2461041.5,
            new Vector3D(2.876454611935341e9, -4.435932980402722e9, -3.571668914301288e8)),
        new("halley", 2456293.5,
            new Vector3D(-3.049367490216856e9, 3.665096162046016e9, -1.442713865696919e9)),
        new("halley", 2461041.5,
            new Vector3D(-2.917189260832731e9, 4.103229382979976e9, -1.479132076090842e9)),
    ];

    [Fact]
    public void DistanciaRadialDosCorposMenoresConfereComOJpl()
    {
        var sim = SolarSystem.NewEngineWithCatalog();
        var relatorio = new StringBuilder();
        var maiorErro = 0.0;

        foreach (var referencia in Referencias)
        {
            var calculado = sim.StateAt(referencia.BodyId, referencia.JulianDateTdb);

            var raioEsperado = referencia.PositionKm.Magnitude;
            var erroRelativo = Math.Abs(calculado.DistanceKm - raioEsperado) / raioEsperado;
            maiorErro = Math.Max(maiorErro, erroRelativo);

            relatorio.AppendLine(CultureInfo.InvariantCulture,
                $"{referencia.BodyId,-8} JD {referencia.JulianDateTdb,12:F1}  "
                    + $"esperado {raioEsperado,18:N0} km  "
                    + $"obtido {calculado.DistanceKm,18:N0} km  erro {erroRelativo:P4}");
        }

        Assert.True(
            maiorErro < RadialToleranceFraction,
            $"Erro radial maximo de {maiorErro:P4}, acima da tolerancia de "
                + $"{RadialToleranceFraction:P2}.{Environment.NewLine}{relatorio}");
    }

    private sealed record Referencia(string BodyId, double JulianDateTdb, Vector3D PositionKm);
}
