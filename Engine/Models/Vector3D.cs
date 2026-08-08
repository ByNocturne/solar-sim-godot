namespace SolarSim.Engine.Models;

/// <summary>
/// Vetor cartesiano em precisão dupla. Componentes em km no uso do motor.
/// </summary>
public readonly record struct Vector3D(double X, double Y, double Z)
{
    public static Vector3D Zero => default;

    public double Magnitude => Math.Sqrt(X * X + Y * Y + Z * Z);

    public double MagnitudeSquared => X * X + Y * Y + Z * Z;

    public static Vector3D operator +(Vector3D a, Vector3D b)
        => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    public static Vector3D operator -(Vector3D a, Vector3D b)
        => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    public static Vector3D operator -(Vector3D v) => new(-v.X, -v.Y, -v.Z);

    public static Vector3D operator *(Vector3D v, double scalar)
        => new(v.X * scalar, v.Y * scalar, v.Z * scalar);

    public static Vector3D operator *(double scalar, Vector3D v) => v * scalar;

    public static Vector3D operator /(Vector3D v, double scalar)
        => new(v.X / scalar, v.Y / scalar, v.Z / scalar);

    public double Dot(Vector3D other) => X * other.X + Y * other.Y + Z * other.Z;

    public Vector3D Cross(Vector3D other) => new(
        Y * other.Z - Z * other.Y,
        Z * other.X - X * other.Z,
        X * other.Y - Y * other.X);

    public Vector3D Normalized()
    {
        var magnitude = Magnitude;
        return magnitude == 0.0 ? Zero : this / magnitude;
    }
}
