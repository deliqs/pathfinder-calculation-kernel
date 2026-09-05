namespace PathFinder.CalculationKernel.Houses;

/// <remarks>
/// Implements Koch cusps as ascendants at thirds of the MC's diurnal semi-arc.
/// Written from this geometric definition with a numeric oracle as the only reference.
/// </remarks>
internal static class KochHouses
{
    private const double Obliquity = 23.4393;

    internal static IReadOnlyList<HouseCusp> Calculate(
        double ascLongitude,
        double mcLongitude,
        double latitude,
        Func<double, double, double> calculateAscendant,
        Func<double, double, List<HouseCusp>> porphyryFallback)
    {
        if (Math.Abs(latitude) > 60.0)
        {
            return porphyryFallback(ascLongitude, mcLongitude);
        }

        mcLongitude = AstroMath.NormalizeDegrees(mcLongitude);
        var ramc = AstroMath.CalculateRamc(mcLongitude, Obliquity);
        var semiArc = DiurnalSemiArc(mcLongitude, latitude);
        var cusps = new double[12];
        cusps[0] = ascLongitude;
        cusps[1] = calculateAscendant(ramc + semiArc / 3.0, latitude);
        cusps[2] = calculateAscendant(ramc + 2.0 * semiArc / 3.0, latitude);
        cusps[3] = AstroMath.NormalizeDegrees(mcLongitude + 180.0);
        cusps[6] = AstroMath.NormalizeDegrees(ascLongitude + 180.0);
        cusps[9] = mcLongitude;
        cusps[10] = calculateAscendant(ramc - 2.0 * semiArc / 3.0, latitude);
        cusps[11] = calculateAscendant(ramc - semiArc / 3.0, latitude);
        return QuadrantCusps.CompleteOpposites(cusps);
    }

    private static double DiurnalSemiArc(double mcLongitude, double latitude)
    {
        var epsilon = AstroMath.DegreesToRadians(Obliquity);
        var mc = AstroMath.DegreesToRadians(mcLongitude);
        var phi = AstroMath.DegreesToRadians(latitude);
        var declination = Math.Asin(Math.Sin(epsilon) * Math.Sin(mc));
        var ascensionalDifference = Math.Asin(Math.Tan(phi) * Math.Tan(declination));
        return 90.0 + AstroMath.RadiansToDegrees(ascensionalDifference);
    }
}
