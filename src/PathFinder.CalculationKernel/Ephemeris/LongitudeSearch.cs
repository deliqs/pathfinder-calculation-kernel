using CosineKitty;
using NodaTime;

namespace PathFinder.CalculationKernel.Ephemeris;

internal static class LongitudeSearch
{
    internal static Instant FindSun(
        double target,
        Instant start,
        int daysForward,
        Func<Instant, AstroTime> toAstroTime)
    {
        var current = toAstroTime(start);
        double daysSearched = 0;
        while (daysSearched < daysForward)
        {
            var chunkDays = Math.Min(20.0, daysForward - daysSearched);
            var result = Astronomy.SearchSunLongitude(target, current, chunkDays);
            if (result is not null)
            {
                return ToInstant(result);
            }

            current = current.AddDays(chunkDays);
            daysSearched += chunkDays;
        }

        throw new InvalidOperationException(
            $"Could not find Sun at longitude {target} within {daysForward} days from {start}.");
    }

    internal static Instant FindPlanet(
        Planet planet,
        double target,
        Instant start,
        int daysForward,
        Func<Planet, Instant, EclipticPosition> getPosition)
    {
        var step = Duration.FromHours(planet == Planet.Moon ? 2 : 24);
        var end = start + Duration.FromDays(daysForward);
        var previousTime = start;
        var previous = getPosition(planet, previousTime);

        for (var current = start + step; current <= end; current += step)
        {
            var position = getPosition(planet, current);
            if (Crossed(previous.Longitude, position.Longitude, target))
            {
                return Refine(planet, target, previousTime, current, getPosition);
            }

            previousTime = current;
            previous = position;
        }

        throw new InvalidOperationException(
            $"Could not find {planet} at longitude {target} within {daysForward} days from {start}.");
    }

    private static bool Crossed(double previous, double current, double target)
    {
        var previousDifference = NormalizeDifference(target - previous);
        var currentDifference = NormalizeDifference(target - current);
        return previousDifference >= 0 && currentDifference <= 0 ||
               previousDifference <= 0 && currentDifference >= 0;
    }

    private static Instant Refine(
        Planet planet,
        double target,
        Instant start,
        Instant end,
        Func<Planet, Instant, EclipticPosition> getPosition)
    {
        var startDifference = NormalizeDifference(getPosition(planet, start).Longitude - target);
        for (var iteration = 0; iteration < 50; iteration++)
        {
            var middle = start + Duration.FromTicks((end - start).TotalTicks / 2);
            var difference = NormalizeDifference(getPosition(planet, middle).Longitude - target);
            if (Math.Abs(difference) < 0.0001)
            {
                return middle;
            }

            if (startDifference > 0 && difference > 0 || startDifference < 0 && difference < 0)
            {
                start = middle;
                startDifference = difference;
            }
            else
            {
                end = middle;
            }
        }

        return start + Duration.FromTicks((end - start).TotalTicks / 2);
    }

    private static double NormalizeDifference(double difference)
    {
        while (difference > 180) difference -= 360;
        while (difference < -180) difference += 360;
        return difference;
    }

    private static Instant ToInstant(AstroTime time)
    {
        var utc = time.ToUtcDateTime();
        return Instant.FromDateTimeUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc));
    }
}
