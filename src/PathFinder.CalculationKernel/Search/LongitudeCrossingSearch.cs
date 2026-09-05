using NodaTime;

namespace PathFinder.CalculationKernel.Search;

internal static class LongitudeCrossingSearch
{
    private const double ExactToleranceDegrees = 0.0001;

    internal static Instant? FindFirst(
        Planet body,
        double target,
        LongitudeMotion motion,
        Instant start,
        Instant end,
        Func<Instant, double> readLongitude)
    {
        var step = Duration.FromHours(GetStepHours(body));
        var samples = BuildSamples(start, end, step, readLongitude);
        var stations = FindStations(samples, step, readLongitude);
        for (var index = 0; index + 1 < samples.Count; index++)
        {
            var sample = samples[index];
            var next = samples[index + 1];
            if (stations.TryGetValue(index, out var station))
            {
                var before = FindCrossing(sample, station, target, motion, readLongitude);
                if (before is not null)
                {
                    return before;
                }

                var after = FindCrossing(station, next, target, motion, readLongitude);
                if (after is not null)
                {
                    return after;
                }
            }
            else
            {
                var crossing = FindCrossing(sample, next, target, motion, readLongitude);
                if (crossing is not null)
                {
                    return crossing;
                }
            }
        }

        return null;
    }

    private static List<LongitudeSample> BuildSamples(
        Instant start,
        Instant end,
        Duration step,
        Func<Instant, double> readLongitude)
    {
        var samples = new List<LongitudeSample> { new(start, readLongitude(start)) };
        var current = start;
        while (current < end)
        {
            var next = current + step;
            if (next > end) next = end;
            samples.Add(new LongitudeSample(next, readLongitude(next)));
            current = next;
        }

        return samples;
    }

    private static IReadOnlyDictionary<int, LongitudeSample> FindStations(
        IReadOnlyList<LongitudeSample> samples,
        Duration step,
        Func<Instant, double> readLongitude)
    {
        var stations = new Dictionary<int, LongitudeSample>();
        var before = new LongitudeSample(samples[0].Instant - step, readLongitude(samples[0].Instant - step));
        var after = new LongitudeSample(samples[^1].Instant + step, readLongitude(samples[^1].Instant + step));
        for (var index = 0; index + 1 < samples.Count; index++)
        {
            var previous = index == 0 ? before : samples[index - 1];
            var current = samples[index];
            var next = samples[index + 1];
            var following = index + 2 < samples.Count ? samples[index + 2] : after;
            var incoming = LongitudeSearchMath.NormalizeDifference(
                current.Longitude - previous.Longitude);
            var outgoing = LongitudeSearchMath.NormalizeDifference(
                following.Longitude - next.Longitude);
            if (incoming * outgoing < 0)
            {
                stations[index] = RefineStation(current, next, incoming > 0, readLongitude);
            }
        }

        return stations;
    }

    private static LongitudeSample RefineStation(
        LongitudeSample start,
        LongitudeSample end,
        bool maximum,
        Func<Instant, double> readLongitude)
    {
        var lower = start.Instant;
        var upper = end.Instant;
        for (var iteration = 0; iteration < 30; iteration++)
        {
            var third = (upper - lower) / 3;
            var lowerThird = lower + third;
            var upperThird = upper - third;
            var lowerValue = LongitudeSearchMath.UnwrapNear(readLongitude(lowerThird), start.Longitude);
            var upperValue = LongitudeSearchMath.UnwrapNear(readLongitude(upperThird), start.Longitude);
            if (maximum ? lowerValue < upperValue : lowerValue > upperValue)
            {
                lower = lowerThird;
            }
            else
            {
                upper = upperThird;
            }
        }

        var instant = lower + (upper - lower) * 0.5;
        return new LongitudeSample(instant, readLongitude(instant));
    }

    private static Instant? FindCrossing(
        LongitudeSample start,
        LongitudeSample end,
        double target,
        LongitudeMotion requestedMotion,
        Func<Instant, double> readLongitude)
    {
        var delta = LongitudeSearchMath.NormalizeDifference(end.Longitude - start.Longitude);
        var motion = delta < 0 ? LongitudeMotion.Retrograde : LongitudeMotion.Direct;
        if (motion != requestedMotion)
        {
            return null;
        }

        if (Math.Abs(LongitudeSearchMath.DirectedDifference(start.Longitude, target)) <
            ExactToleranceDegrees)
        {
            return start.Instant;
        }

        if (Math.Abs(LongitudeSearchMath.DirectedDifference(end.Longitude, target)) <
            ExactToleranceDegrees)
        {
            return end.Instant;
        }

        var startDifference = LongitudeSearchMath.DirectedDifference(start.Longitude, target);
        var endDifference = LongitudeSearchMath.DirectedDifference(end.Longitude, target);
        if (startDifference * endDifference >= 0 ||
            Math.Abs(startDifference) >= 15 ||
            Math.Abs(endDifference) >= 15)
        {
            return null;
        }

        return RefineCrossing(start, end, target, readLongitude);
    }

    private static Instant RefineCrossing(
        LongitudeSample start,
        LongitudeSample end,
        double target,
        Func<Instant, double> readLongitude)
    {
        var lower = start.Instant;
        var upper = end.Instant;
        var lowerDifference = LongitudeSearchMath.DirectedDifference(start.Longitude, target);
        for (var iteration = 0; iteration < 30; iteration++)
        {
            var midpoint = lower + (upper - lower) * 0.5;
            var difference = LongitudeSearchMath.DirectedDifference(readLongitude(midpoint), target);
            if (Math.Abs(difference) < ExactToleranceDegrees)
            {
                return midpoint;
            }

            if (lowerDifference * difference <= 0)
            {
                upper = midpoint;
            }
            else
            {
                lower = midpoint;
                lowerDifference = difference;
            }
        }

        return lower + (upper - lower) * 0.5;
    }

    private static double GetStepHours(Planet planet) => planet switch
    {
        Planet.Moon => 12,
        Planet.Sun or Planet.Mercury or Planet.Venus or Planet.Mars => 24,
        _ => 72
    };

    private readonly record struct LongitudeSample(Instant Instant, double Longitude);
}

internal static class LongitudeSearchMath
{
    internal static double UnwrapNear(double longitude, double reference) =>
        reference + NormalizeDifference(longitude - reference);

    internal static double DirectedDifference(double longitude, double target) =>
        NormalizeDifference(NormalizeDegrees(longitude - target));

    internal static double NormalizeDifference(double difference)
    {
        if (difference > 180) difference -= 360;
        if (difference < -180) difference += 360;
        return difference;
    }

    internal static double NormalizeDegrees(double degrees)
    {
        var normalized = degrees % 360;
        return normalized < 0 ? normalized + 360 : normalized;
    }
}
