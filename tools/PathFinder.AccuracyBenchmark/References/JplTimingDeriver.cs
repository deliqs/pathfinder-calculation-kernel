using System.Globalization;
using NodaTime;
using PathFinder.AccuracyBenchmark.Cases;

namespace PathFinder.AccuracyBenchmark.References;

public sealed record JplTimingReference(
    string CaseId,
    Instant ReferenceInstant,
    string ReferenceMethod);

public static class JplTimingDeriver
{
    public static JplTimingReference Derive(
        TimingCase timing,
        IReadOnlyList<HorizonsLongitudeRow> rows)
    {
        ValidateMethod(timing);
        if (rows.Count < 2)
        {
            throw new InvalidDataException($"Timing reference {timing.Id} has too few JPL rows.");
        }

        var samples = Unwrap(rows);
        return timing.Kind switch
        {
            "longitude-crossing" => DeriveCrossing(timing, samples),
            "station" => DeriveStation(timing, samples),
            _ => throw new InvalidDataException($"Unsupported timing kind: {timing.Kind}")
        };
    }

    private static JplTimingReference DeriveCrossing(
        TimingCase timing,
        IReadOnlyList<TimingSample> samples)
    {
        var target = timing.TargetLongitudeDeg!.Value;
        for (var index = 0; index < samples.Count - 1; index++)
        {
            var first = samples[index];
            var second = samples[index + 1];
            var equivalentTarget = UnwrapNear(target, first.Longitude);
            var requestedDirection = timing.Motion == "direct"
                ? second.Longitude > first.Longitude
                : second.Longitude < first.Longitude;
            var bracketed = timing.Motion == "direct"
                ? equivalentTarget >= first.Longitude && equivalentTarget <= second.Longitude
                : equivalentTarget <= first.Longitude && equivalentTarget >= second.Longitude;
            if (!requestedDirection || !bracketed)
            {
                continue;
            }

            var fraction = (equivalentTarget - first.Longitude) /
                (second.Longitude - first.Longitude);
            return new JplTimingReference(
                timing.Id,
                Interpolate(first.Instant, second.Instant, fraction),
                "jpl-quantity31-hourly-linear-crossing");
        }

        throw new InvalidDataException(
            $"Timing reference {timing.Id} has no {timing.Motion} crossing in its JPL rows.");
    }

    private static JplTimingReference DeriveStation(
        TimingCase timing,
        IReadOnlyList<TimingSample> samples)
    {
        var maximum = timing.Extremum == "maximum";
        if (maximum != (timing.Motion == "retrograde"))
        {
            throw new InvalidDataException(
                $"Timing reference {timing.Id} extremum does not enter {timing.Motion} motion.");
        }

        var candidates = Enumerable.Range(1, samples.Count - 2)
            .Where(index => IsExtremum(samples, index, maximum) &&
                            EntersMotion(samples, index, timing.Motion))
            .ToArray();
        if (candidates.Length == 0)
        {
            throw new InvalidDataException(
                $"Timing reference {timing.Id} has no {timing.Extremum} station in its JPL rows.");
        }

        var selected = candidates.Aggregate((best, current) =>
            maximum
                ? samples[current].Longitude > samples[best].Longitude ? current : best
                : samples[current].Longitude < samples[best].Longitude ? current : best);
        var previous = samples[selected - 1];
        var center = samples[selected];
        var next = samples[selected + 1];
        var denominator = previous.Longitude - 2 * center.Longitude + next.Longitude;
        var fraction = Math.Abs(denominator) < double.Epsilon
            ? 0
            : 0.5 * (previous.Longitude - next.Longitude) / denominator;
        var intervalTicks = (next.Instant - center.Instant).BclCompatibleTicks;
        var instant = center.Instant + Duration.FromTicks(checked((long)Math.Round(
            intervalTicks * fraction,
            MidpointRounding.AwayFromZero)));
        return new JplTimingReference(
            timing.Id,
            instant,
            "jpl-quantity31-10-minute-parabolic-extremum");
    }

    private static IReadOnlyList<TimingSample> Unwrap(IReadOnlyList<HorizonsLongitudeRow> rows)
    {
        var result = new List<TimingSample>(rows.Count);
        double? previous = null;
        foreach (var row in rows)
        {
            var longitude = previous is null
                ? row.LongitudeDegrees
                : UnwrapNear(row.LongitudeDegrees, previous.Value);
            result.Add(new TimingSample(ParseInstant(row.Time), longitude));
            previous = longitude;
        }

        return result;
    }

    private static bool IsExtremum(
        IReadOnlyList<TimingSample> rows,
        int index,
        bool maximum)
    {
        var previous = rows[index - 1].Longitude;
        var center = rows[index].Longitude;
        var next = rows[index + 1].Longitude;
        return maximum
            ? center >= previous && center >= next && (center > previous || center > next)
            : center <= previous && center <= next && (center < previous || center < next);
    }

    private static bool EntersMotion(
        IReadOnlyList<TimingSample> rows,
        int index,
        string motion) => motion == "direct"
        ? rows[index + 1].Longitude > rows[index].Longitude
        : rows[index + 1].Longitude < rows[index].Longitude;

    private static void ValidateMethod(TimingCase timing)
    {
        var expected = timing.Kind == "longitude-crossing"
            ? "kernel-longitude-crossing"
            : timing.Kind == "station"
                ? "kernel-station-parabolic-10-minute"
                : throw new InvalidDataException($"Unsupported timing kind: {timing.Kind}");
        if (timing.Method != expected)
        {
            throw new InvalidDataException(
                $"Timing reference {timing.Id} method does not match {timing.Kind}.");
        }
    }

    private static Instant ParseInstant(string value)
    {
        if (!DateTimeOffset.TryParseExact(
                value,
                "yyyy-MMM-dd HH:mm:ss.fff",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            throw new InvalidDataException($"JPL timing row has an invalid calendar value: {value}");
        }

        return Instant.FromDateTimeOffset(parsed);
    }

    private static Instant Interpolate(Instant first, Instant second, double fraction) =>
        first + Duration.FromTicks(checked((long)Math.Round(
            (second - first).BclCompatibleTicks * fraction,
            MidpointRounding.AwayFromZero)));

    private static double UnwrapNear(double longitude, double reference)
    {
        while (longitude - reference > 180) longitude -= 360;
        while (longitude - reference < -180) longitude += 360;
        return longitude;
    }

    private readonly record struct TimingSample(Instant Instant, double Longitude);
}
