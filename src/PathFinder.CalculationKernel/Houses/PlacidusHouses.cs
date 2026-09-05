namespace PathFinder.CalculationKernel.Houses;

internal static class PlacidusHouses
{
    private const double Obliquity = 23.4393;

    internal static List<HouseCusp> Calculate(
        double ascendant,
        double midheaven,
        double latitude,
        Func<double, double, List<HouseCusp>> porphyryFallback)
    {
        if (Math.Abs(latitude) > 60.0)
        {
            return porphyryFallback(ascendant, midheaven);
        }

        var cusps = porphyryFallback(ascendant, midheaven)
            .Select(cusp => cusp.CuspPosition.Longitude)
            .ToArray();
        var epsilon = AstroMath.DegreesToRadians(Obliquity);
        var phi = AstroMath.DegreesToRadians(latitude);
        int[] refinedIndexes = [1, 2, 10, 11];

        for (var iteration = 0; iteration < 50; iteration++)
        {
            var converged = true;
            var ramc = AstroMath.DegreesToRadians(AstroMath.CalculateRamc(cusps[9], Obliquity));
            foreach (var index in refinedIndexes)
            {
                var longitude = index switch
                {
                    10 => SolveCusp(ramc, 1.0 / 3.0, phi, epsilon, true, cusps[index]),
                    11 => SolveCusp(ramc, 2.0 / 3.0, phi, epsilon, true, cusps[index]),
                    1 => SolveCusp(ramc, 1.0 / 3.0, phi, epsilon, false, cusps[index]),
                    2 => SolveCusp(ramc, 2.0 / 3.0, phi, epsilon, false, cusps[index]),
                    _ => cusps[index]
                };

                if (Math.Abs(AstroMath.NormalizeDegrees(longitude - cusps[index])) > 0.001)
                {
                    converged = false;
                }

                cusps[index] = AstroMath.NormalizeDegrees(longitude);
            }

            cusps[4] = AstroMath.NormalizeDegrees(cusps[10] + 180.0);
            cusps[5] = AstroMath.NormalizeDegrees(cusps[11] + 180.0);
            cusps[7] = AstroMath.NormalizeDegrees(cusps[1] + 180.0);
            cusps[8] = AstroMath.NormalizeDegrees(cusps[2] + 180.0);
            if (converged)
            {
                break;
            }
        }

        return cusps.Select((longitude, index) => new HouseCusp
        {
            HouseNumber = index + 1,
            CuspPosition = new EclipticPosition { Longitude = longitude }
        }).ToList();
    }

    private static double SolveCusp(
        double ramc,
        double fraction,
        double latitude,
        double epsilon,
        bool diurnal,
        double initialGuess)
    {
        var longitude = AstroMath.DegreesToRadians(initialGuess);
        for (var iteration = 0; iteration < 50; iteration++)
        {
            var declination = Math.Asin(Math.Sin(epsilon) * Math.Sin(longitude));
            var ascensionalDifference = Math.Asin(Math.Tan(declination) * Math.Tan(latitude));
            var diurnalArc = AstroMath.DegreesToRadians(90.0) + ascensionalDifference;
            var nocturnalArc = AstroMath.DegreesToRadians(90.0) - ascensionalDifference;
            var targetRightAscension = diurnal
                ? ramc + fraction * diurnalArc
                : ramc + diurnalArc + fraction * nocturnalArc;
            var next = RightAscensionToLongitude(
                AstroMath.RadiansToDegrees(NormalizeRadians(targetRightAscension)),
                AstroMath.RadiansToDegrees(epsilon));
            var nextRadians = AstroMath.DegreesToRadians(next);
            if (Math.Abs(AstroMath.RadiansToDegrees(nextRadians - longitude)) < 0.0001)
            {
                return next;
            }

            longitude = nextRadians;
        }

        return AstroMath.RadiansToDegrees(longitude);
    }

    private static double RightAscensionToLongitude(double rightAscensionDegrees, double epsilonDegrees)
    {
        var rightAscension = AstroMath.DegreesToRadians(rightAscensionDegrees);
        var epsilon = AstroMath.DegreesToRadians(epsilonDegrees);
        return AstroMath.NormalizeDegrees(AstroMath.RadiansToDegrees(
            Math.Atan2(Math.Sin(rightAscension) / Math.Cos(epsilon), Math.Cos(rightAscension))));
    }

    private static double NormalizeRadians(double radians)
    {
        var result = radians % (2.0 * Math.PI);
        return result < 0 ? result + 2.0 * Math.PI : result;
    }
}
