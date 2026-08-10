using SolarSim.Engine.Models;

namespace SolarSim.Engine.Core;

/// <summary>
/// Coordenadas horizontais locais: azimute a partir do norte em direção leste, elevação
/// a partir do horizonte. Tudo em radianos.
/// </summary>
public readonly record struct HorizontalCoords(
    double AzimuthRad,
    double ElevationRad)
{
    public bool AboveHorizon => ElevationRad >= 0.0;
}

/// <summary>
/// Céu local a partir de posições eclípticas do motor. Planetário: só direções — a
/// distância AU não entra no desenho; quem renderiza coloca o marcador em R×direção.
/// </summary>
public static class LocalSky
{
    /// <summary>Site padrão da Fase 5 (graus).</summary>
    public const double DefaultLatitudeDeg = 40.0;

    public const double DefaultLongitudeDeg = 0.0;

    /// <summary>
    /// Posição do observador relativa ao centro da Terra, em km, no referencial eclíptico
    /// do motor (mesmo de <see cref="SimEngine.PositionAt"/>).
    /// </summary>
    public static Vector3D ObserverFromEarthCenterKm(
        double latitudeRad,
        double longitudeRad,
        double julianDate,
        double earthRadiusKm,
        double rotationPeriodSeconds,
        double obliquityRad,
        double primeMeridianAtJ2000Rad = 0.0)
    {
        if (earthRadiusKm <= 0.0 || rotationPeriodSeconds <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                earthRadiusKm <= 0.0 ? nameof(earthRadiusKm) : nameof(rotationPeriodSeconds));
        }

        var lst = LocalSiderealAngleRad(
            julianDate,
            longitudeRad,
            rotationPeriodSeconds,
            primeMeridianAtJ2000Rad);

        var cosLat = Math.Cos(latitudeRad);
        var xEq = earthRadiusKm * cosLat * Math.Cos(lst);
        var yEq = earthRadiusKm * cosLat * Math.Sin(lst);
        var zEq = earthRadiusKm * Math.Sin(latitudeRad);

        return EquatorialToEcliptic(new Vector3D(xEq, yEq, zEq), obliquityRad);
    }

    /// <summary>
    /// Azimute/elevação de um corpo visto do site. Posições absolutas no referencial do motor.
    /// </summary>
    public static HorizontalCoords Look(
        Vector3D bodyEclipticKm,
        Vector3D earthEclipticKm,
        Vector3D observerFromEarthCenterKm,
        double obliquityRad)
    {
        var topocentric = bodyEclipticKm - (earthEclipticKm + observerFromEarthCenterKm);
        var magnitude = topocentric.Magnitude;
        if (magnitude == 0.0)
        {
            return new HorizontalCoords(0.0, -Math.PI / 2.0);
        }

        var direction = topocentric / magnitude;
        var zenith = observerFromEarthCenterKm.Normalized();
        var northPole = EarthNorthPoleEcliptic(obliquityRad);
        var east = EastUnit(zenith, northPole);
        var north = zenith.Cross(east).Normalized();

        var up = direction.Dot(zenith);
        var e = direction.Dot(east);
        var n = direction.Dot(north);

        var horizontal = Math.Sqrt(e * e + n * n);
        var elevation = Math.Atan2(up, horizontal);
        var azimuth = AstroConstants.NormalizeAngle(Math.Atan2(e, n));

        return new HorizontalCoords(azimuth, elevation);
    }

    /// <summary>
    /// Direção unitária no referencial local ENU do desenho: +X leste, +Y zenite, +Z norte.
    /// O host Godot multiplica por R fixo — nunca por distância real.
    /// </summary>
    public static Vector3D DirectionEnu(HorizontalCoords coords)
    {
        var cosEl = Math.Cos(coords.ElevationRad);
        return new Vector3D(
            cosEl * Math.Sin(coords.AzimuthRad),
            Math.Sin(coords.ElevationRad),
            cosEl * Math.Cos(coords.AzimuthRad));
    }

    public static double LocalSiderealAngleRad(
        double julianDate,
        double longitudeRad,
        double rotationPeriodSeconds,
        double primeMeridianAtJ2000Rad = 0.0)
    {
        var turns = (julianDate - AstroConstants.J2000) * AstroConstants.SecondsPerDay
            / rotationPeriodSeconds;
        var spin = AstroConstants.NormalizeAngle(AstroConstants.TwoPi * turns + primeMeridianAtJ2000Rad);
        return AstroConstants.NormalizeAngle(spin + longitudeRad);
    }

    public static Vector3D EquatorialToEcliptic(Vector3D equatorial, double obliquityRad)
    {
        var cos = Math.Cos(obliquityRad);
        var sin = Math.Sin(obliquityRad);
        return new Vector3D(
            equatorial.X,
            equatorial.Y * cos + equatorial.Z * sin,
            -equatorial.Y * sin + equatorial.Z * cos);
    }

    public static Vector3D EarthNorthPoleEcliptic(double obliquityRad)
        => EquatorialToEcliptic(new Vector3D(0.0, 0.0, 1.0), obliquityRad);

    private static Vector3D EastUnit(Vector3D zenith, Vector3D northPole)
    {
        var east = northPole.Cross(zenith);
        var mag = east.Magnitude;
        if (mag < 1e-12)
        {
            east = new Vector3D(0.0, 0.0, 1.0).Cross(zenith);
            mag = east.Magnitude;
            if (mag < 1e-12)
            {
                return new Vector3D(1.0, 0.0, 0.0);
            }
        }

        return east / mag;
    }
}
