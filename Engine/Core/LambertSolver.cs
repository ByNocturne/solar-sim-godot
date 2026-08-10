using SolarSim.Engine.Models;

namespace SolarSim.Engine.Core;

/// <summary>
/// O problema de Lambert: dadas duas posições e um tempo de voo, quais velocidades
/// ligam uma à outra sob a gravidade de um corpo central.
/// </summary>
/// <remarks>
/// Só o ramo elíptico (e, opcionalmente, o hiperbólico quando o tempo é curto demais
/// para a elipse mínima). A parábola é o limite entre os dois e não precisa de caso
/// próprio — o iterador cai de um lado ou do outro.
/// <para>
/// Formulação por variável universal (Curtis / Vallado): itera em <c>z</c> até o tempo
/// de voo bater, e devolve as velocidades pelos coeficientes f e g.
/// </para>
/// </remarks>
public static class LambertSolver
{
    private const int MaxIterations = 60;

    private const double Tolerance = 1e-8;

    /// <summary>
    /// Velocidades na partida e na chegada que realizam a transferência.
    /// </summary>
    public readonly record struct Solution(
        Vector3D DepartureVelocityKmS,
        Vector3D ArrivalVelocityKmS);

    /// <summary>
    /// Resolve Lambert entre duas posições. Devolve nulo quando a geometria é degenerada
    /// ou o iterador não converge — o chamador trata como janela inviável.
    /// </summary>
    /// <param name="shortWay">
    /// <c>true</c> para o arco menor que 180°; <c>false</c> para o maior. As duas
    /// soluções existem na maioria das geometrias, e a de menor Δv depende da data.
    /// </param>
    public static Solution? TrySolve(
        in Vector3D position1Km,
        in Vector3D position2Km,
        double timeOfFlightSeconds,
        double muKm3S2,
        bool shortWay = true)
    {
        if (timeOfFlightSeconds <= 0.0 || muKm3S2 <= 0.0)
        {
            return null;
        }

        var r1 = position1Km.Magnitude;
        var r2 = position2Km.Magnitude;

        if (r1 <= 0.0 || r2 <= 0.0)
        {
            return null;
        }

        var cosDeltaNu = Math.Clamp(
            position1Km.Dot(position2Km) / (r1 * r2), -1.0, 1.0);

        // Ângulo orientado no sentido direto (prograde): se o produto vetorial aponta
        // para −Z, o arco curto no plano XY seria o retrógrado, e completa-se a volta.
        var deltaNu = Math.Acos(cosDeltaNu);
        var crossZ = position1Km.Cross(position2Km).Z;

        if (crossZ < 0.0)
        {
            deltaNu = AstroConstants.TwoPi - deltaNu;
        }

        if (!shortWay)
        {
            deltaNu = AstroConstants.TwoPi - deltaNu;
        }

        if (deltaNu < 1e-8 || Math.Abs(deltaNu - Math.PI) < 1e-8)
        {
            // 0° não liga nada; 180° exato anula A e a formulação clássica divide por
            // zero. O chamador pode afastar um pouco as posições.
            return null;
        }

        var sinDeltaNu = Math.Sin(deltaNu);
        var aParameter = sinDeltaNu
            * Math.Sqrt(r1 * r2 / Math.Max(1e-16, 1.0 - Math.Cos(deltaNu)));

        if (Math.Abs(aParameter) < 1e-14 || !double.IsFinite(aParameter))
        {
            return null;
        }

        // Chute: z = 0 é a parábola; para transferências longas sobe para o ramo elíptico.
        var z = 0.0;
        double y = 0.0;
        var converged = false;

        for (var iteration = 0; iteration < MaxIterations; iteration++)
        {
            var (c2, c3) = Stumpff(z);
            var sqrtC2 = Math.Sqrt(Math.Max(c2, 1e-16));

            y = r1 + r2 + aParameter * (z * c3 - 1.0) / sqrtC2;

            if (aParameter > 0.0 && y < 0.0)
            {
                // Fora do domínio elíptico deste A: empurra z para cima.
                z = 0.1 + z * 1.5;
                continue;
            }

            var chi = Math.Sqrt(Math.Max(y / Math.Max(c2, 1e-16), 0.0));
            var tof = (chi * chi * chi * c3 + aParameter * Math.Sqrt(y))
                / Math.Sqrt(muKm3S2);

            if (!double.IsFinite(tof))
            {
                return null;
            }

            var residual = tof - timeOfFlightSeconds;

            if (Math.Abs(residual) < Tolerance * Math.Max(timeOfFlightSeconds, 1.0))
            {
                converged = true;
                break;
            }

            // Derivada dt/dz por diferença central — mais estável que a forma analítica
            // perto de z = 0, onde as séries de Stumpff trocam de ramo.
            const double step = 1e-5;
            var (c2p, c3p) = Stumpff(z + step);
            var yp = r1 + r2 + aParameter * ((z + step) * c3p - 1.0) / Math.Sqrt(Math.Max(c2p, 1e-16));
            var chip = Math.Sqrt(Math.Max(yp / Math.Max(c2p, 1e-16), 0.0));
            var tofp = (chip * chip * chip * c3p + aParameter * Math.Sqrt(Math.Max(yp, 0.0)))
                / Math.Sqrt(muKm3S2);

            var dtdz = (tofp - tof) / step;

            if (Math.Abs(dtdz) < 1e-18 || !double.IsFinite(dtdz))
            {
                return null;
            }

            var delta = residual / dtdz;
            z -= Math.Clamp(delta, -1.0, 1.0);

            // Elipse: z ∈ (0, (2π)²). Hipérbole: z < 0. Trava o teto elíptico.
            z = Math.Clamp(z, -4.0 * Math.PI * Math.PI, 4.0 * Math.PI * Math.PI - 1e-3);
        }

        if (!converged || y <= 0.0)
        {
            return null;
        }

        var f = 1.0 - y / r1;
        var g = aParameter * Math.Sqrt(y / muKm3S2);
        var gDot = 1.0 - y / r2;

        if (Math.Abs(g) < 1e-18 || !double.IsFinite(g))
        {
            return null;
        }

        var v1 = (position2Km - f * position1Km) / g;
        var v2 = (gDot * position2Km - position1Km) / g;

        if (!IsFinite(v1) || !IsFinite(v2))
        {
            return null;
        }

        return new Solution(v1, v2);
    }

    /// <summary>
    /// Funções de Stumpff C₂(z) e C₃(z), com série de Taylor perto de zero para não
    /// perder dígitos no cancelamento trigonométrico.
    /// </summary>
    public static (double C2, double C3) Stumpff(double z)
    {
        if (z > 1e-6)
        {
            var sqrtZ = Math.Sqrt(z);
            return ((1.0 - Math.Cos(sqrtZ)) / z, (sqrtZ - Math.Sin(sqrtZ)) / (z * sqrtZ));
        }

        if (z < -1e-6)
        {
            var sqrtZ = Math.Sqrt(-z);
            return ((Math.Cosh(sqrtZ) - 1.0) / -z, (Math.Sinh(sqrtZ) - sqrtZ) / (-z * sqrtZ));
        }

        // Série: C₂ = 1/2! − z/4! + z²/6! − …; C₃ = 1/3! − z/5! + z²/7! − …
        return (
            0.5 - z / 24.0 + z * z / 720.0,
            1.0 / 6.0 - z / 120.0 + z * z / 5040.0);
    }

    private static bool IsFinite(in Vector3D v)
        => double.IsFinite(v.X) && double.IsFinite(v.Y) && double.IsFinite(v.Z);
}
