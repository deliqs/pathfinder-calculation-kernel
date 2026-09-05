namespace PathFinder.CalculationKernel.Houses;

/// <remarks>
/// Completes quadrant house cusps by placing houses 4–9 opposite houses 10–3.
/// Written from this geometric definition with a numeric oracle as the only reference.
/// </remarks>
internal static class QuadrantCusps
{
    internal static IReadOnlyList<HouseCusp> CompleteOpposites(double[] cusps)
    {
        cusps[4] = AstroMath.NormalizeDegrees(cusps[10] + 180.0);
        cusps[5] = AstroMath.NormalizeDegrees(cusps[11] + 180.0);
        cusps[7] = AstroMath.NormalizeDegrees(cusps[1] + 180.0);
        cusps[8] = AstroMath.NormalizeDegrees(cusps[2] + 180.0);

        return cusps.Select((longitude, index) => new HouseCusp
        {
            HouseNumber = index + 1,
            CuspPosition = new() { Longitude = longitude }
        }).ToList();
    }
}
