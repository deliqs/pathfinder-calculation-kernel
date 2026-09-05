namespace PathFinder.CalculationKernel.Houses;

/// <remarks>
/// Implements Regiomontanus house circles through the north and south points of the horizon.
/// Written from this geometric definition with a numeric oracle as the only reference.
/// </remarks>
internal static class RegiomontanusHouses
{
    private const double Obliquity = 23.4393;

    internal static IReadOnlyList<HouseCusp> Calculate(
        double ascLongitude,
        double mcLongitude,
        double latitude,
        Func<double, double, List<HouseCusp>> porphyryFallback)
    {
        if (Math.Abs(latitude) > 66.0)
        {
            return porphyryFallback(ascLongitude, mcLongitude);
        }

        mcLongitude = AstroMath.NormalizeDegrees(mcLongitude);
        var ramc = AstroMath.CalculateRamc(mcLongitude, Obliquity);
        var north = HouseCircleGeometry.NorthHorizonPoint(latitude);
        var cusps = new double[12];
        cusps[0] = ascLongitude;
        cusps[1] = CuspLongitude(north, 120.0, ramc);
        cusps[2] = CuspLongitude(north, 150.0, ramc);
        cusps[3] = AstroMath.NormalizeDegrees(mcLongitude + 180.0);
        cusps[6] = AstroMath.NormalizeDegrees(ascLongitude + 180.0);
        cusps[9] = mcLongitude;
        cusps[10] = CuspLongitude(north, 30.0, ramc);
        cusps[11] = CuspLongitude(north, 60.0, ramc);
        return QuadrantCusps.CompleteOpposites(cusps);
    }

    private static double CuspLongitude(HouseCircleGeometry.Vector north, double offset, double ramc)
        => HouseCircleGeometry.IntersectionLongitudeOnMcArc(
            north, HouseCircleGeometry.EquatorPoint(offset), ramc, Obliquity);
}
