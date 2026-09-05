using CosineKitty;

namespace PathFinder.CalculationKernel.Ephemeris;

internal static class CalculatedPointEphemeris
{
    internal static EclipticPosition MeanNorthNode(AstroTime time)
    {
        var centuries = time.tt / 36525.0;
        var longitude = Normalize(125.04452 - 1934.136261 * centuries);
        return new EclipticPosition { Longitude = longitude };
    }

    internal static EclipticPosition MeanSouthNode(AstroTime time)
    {
        var north = MeanNorthNode(time);
        return new EclipticPosition { Longitude = Normalize(north.Longitude + 180.0) };
    }

    internal static EclipticPosition MeanLilith(AstroTime time)
    {
        var centuries = time.tt / 36525.0;
        var meanPerigee = 83.3532465 + 4069.0137287 * centuries;
        return new EclipticPosition { Longitude = Normalize(meanPerigee + 180.0) };
    }

    private static double Normalize(double degrees)
    {
        var normalized = degrees % 360.0;
        return normalized < 0 ? normalized + 360.0 : normalized;
    }
}
