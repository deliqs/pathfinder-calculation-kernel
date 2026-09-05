namespace PathFinder.CalculationKernel.Houses;

internal static class AstroMath
{
    internal static double NormalizeDegrees(double degrees)
    {
        var result = degrees % 360.0;
        if (result < 0)
        {
            result += 360.0;
        }

        return result;
    }

    internal static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;
    internal static double RadiansToDegrees(double radians) => radians * 180.0 / Math.PI;

    /// <summary>
    /// Computes the Right Ascension of the MC from MC ecliptic longitude.
    /// </summary>
    internal static double CalculateRamc(double mcLongitude, double obliquity)
    {
        var eps = DegreesToRadians(obliquity);
        var mc = DegreesToRadians(mcLongitude);
        var ra = Math.Atan2(Math.Sin(mc) * Math.Cos(eps), Math.Cos(mc));
        return NormalizeDegrees(RadiansToDegrees(ra));
    }
}
