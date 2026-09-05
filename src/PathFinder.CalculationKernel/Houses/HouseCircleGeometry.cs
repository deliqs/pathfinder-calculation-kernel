namespace PathFinder.CalculationKernel.Houses;

/// <remarks>
/// Implements the local-frame geometry for house circles through the north and south points
/// of the horizon and their ecliptic intersections. Written from this geometric definition
/// with a numeric oracle as the only reference.
/// </remarks>
internal static class HouseCircleGeometry
{
    internal static Vector NorthHorizonPoint(double latitude)
    {
        var phi = AstroMath.DegreesToRadians(latitude);
        return new Vector(-Math.Sin(phi), 0.0, Math.Cos(phi));
    }

    internal static Vector EquatorPoint(double eastwardOffset)
    {
        var offset = AstroMath.DegreesToRadians(eastwardOffset);
        return new Vector(Math.Cos(offset), Math.Sin(offset), 0.0);
    }

    internal static Vector PrimeVerticalPoint(double latitude, double distanceFromZenith)
    {
        var phi = AstroMath.DegreesToRadians(latitude);
        var distance = AstroMath.DegreesToRadians(distanceFromZenith);
        var zenith = new Vector(Math.Cos(phi), 0.0, Math.Sin(phi));
        var east = new Vector(0.0, 1.0, 0.0);
        return Math.Cos(distance) * zenith + Math.Sin(distance) * east;
    }

    internal static double IntersectionLongitudeOnMcArc(
        Vector northHorizon,
        Vector thirdPoint,
        double ramc,
        double obliquity)
    {
        var circlePole = Vector.Normalize(Vector.Cross(northHorizon, thirdPoint));
        var eclipticPole = LocalEclipticNorthPole(ramc, obliquity);
        var first = Vector.Normalize(Vector.Cross(circlePole, eclipticPole));
        var second = -first;
        var mc = MidheavenLongitude(ramc, obliquity);
        var firstLongitude = EclipticLongitude(first, ramc, obliquity);
        return IsOnMcArc(firstLongitude, mc)
            ? firstLongitude
            : EclipticLongitude(second, ramc, obliquity);
    }

    private static Vector LocalEclipticNorthPole(double ramc, double obliquity)
    {
        var epsilon = AstroMath.DegreesToRadians(obliquity);
        var absolutePole = new Vector(0.0, -Math.Sin(epsilon), Math.Cos(epsilon));
        return RotateAboutZ(absolutePole, -ramc);
    }

    private static double MidheavenLongitude(double ramc, double obliquity)
    {
        var rightAscension = AstroMath.DegreesToRadians(ramc);
        var epsilon = AstroMath.DegreesToRadians(obliquity);
        return AstroMath.NormalizeDegrees(AstroMath.RadiansToDegrees(
            Math.Atan2(Math.Sin(rightAscension), Math.Cos(rightAscension) * Math.Cos(epsilon))));
    }

    private static double EclipticLongitude(Vector local, double ramc, double obliquity)
    {
        var absolute = RotateAboutZ(local, ramc);
        var epsilon = AstroMath.DegreesToRadians(obliquity);
        return AstroMath.NormalizeDegrees(AstroMath.RadiansToDegrees(Math.Atan2(
            absolute.Y * Math.Cos(epsilon) + absolute.Z * Math.Sin(epsilon), absolute.X)));
    }

    private static bool IsOnMcArc(double longitude, double mc)
    {
        var distance = AstroMath.NormalizeDegrees(longitude - mc);
        return distance > 0.0 && distance < 180.0;
    }

    private static Vector RotateAboutZ(Vector vector, double degrees)
    {
        var radians = AstroMath.DegreesToRadians(degrees);
        var cosine = Math.Cos(radians);
        var sine = Math.Sin(radians);
        return new Vector(
            vector.X * cosine - vector.Y * sine,
            vector.X * sine + vector.Y * cosine,
            vector.Z);
    }

    internal readonly record struct Vector(double X, double Y, double Z)
    {
        public static Vector operator +(Vector left, Vector right)
            => new(left.X + right.X, left.Y + right.Y, left.Z + right.Z);

        public static Vector operator -(Vector vector) => new(-vector.X, -vector.Y, -vector.Z);
        public static Vector operator *(double scalar, Vector vector)
            => new(scalar * vector.X, scalar * vector.Y, scalar * vector.Z);

        internal static Vector Cross(Vector left, Vector right) => new(
            left.Y * right.Z - left.Z * right.Y,
            left.Z * right.X - left.X * right.Z,
            left.X * right.Y - left.Y * right.X);

        internal static Vector Normalize(Vector vector)
        {
            var length = Math.Sqrt(vector.X * vector.X + vector.Y * vector.Y + vector.Z * vector.Z);
            return new Vector(vector.X / length, vector.Y / length, vector.Z / length);
        }
    }
}
