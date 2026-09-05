using NodaTime;
using PathFinder.CalculationKernel.Ephemeris;

namespace PathFinder.CalculationKernel.Search;

public enum StationExtremum
{
    Minimum,
    Maximum
}

/// <summary>
/// Finds a longitude station with the benchmark's kernel-derived ten-minute sampling and
/// three-point parabolic interpolation. This is a reproducibility contract, not a claim that the
/// algorithm is an independently sourced astronomical station reference.
/// </summary>
public sealed class StationFinder(IEphemeris ephemeris)
{
    private static readonly Duration SampleInterval = Duration.FromMinutes(10);

    public Instant? Find(
        Planet body,
        Instant searchStart,
        Duration searchWindow,
        StationExtremum extremum)
    {
        if (searchWindow < Duration.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(searchWindow));
        }

        var end = searchStart + searchWindow;
        var points = new List<StationSample>();
        double? previousLongitude = null;
        for (var instant = searchStart; instant <= end; instant += SampleInterval)
        {
            var longitude = ephemeris.GetLongitude(body, instant);
            if (previousLongitude is not null)
            {
                longitude = UnwrapNear(longitude, previousLongitude.Value);
            }

            points.Add(new StationSample(instant, longitude));
            previousLongitude = longitude;
        }

        var candidates = Enumerable.Range(1, Math.Max(0, points.Count - 2))
            .Where(index => IsRequestedExtremum(points, index, extremum))
            .ToArray();
        if (candidates.Length == 0)
        {
            return null;
        }

        var selected = candidates.Aggregate((best, current) =>
            IsBetter(points[current].Longitude, points[best].Longitude, extremum)
                ? current
                : best);
        var previous = points[selected - 1].Longitude;
        var center = points[selected].Longitude;
        var next = points[selected + 1].Longitude;
        var denominator = previous - 2 * center + next;
        var fraction = Math.Abs(denominator) < double.Epsilon
            ? 0
            : 0.5 * (previous - next) / denominator;

        return points[selected].Instant + Duration.FromMinutes(10 * fraction);
    }

    private static bool IsRequestedExtremum(
        IReadOnlyList<StationSample> points,
        int index,
        StationExtremum extremum)
    {
        var previous = points[index - 1].Longitude;
        var center = points[index].Longitude;
        var next = points[index + 1].Longitude;
        return extremum == StationExtremum.Minimum
            ? center <= previous && center <= next && (center < previous || center < next)
            : center >= previous && center >= next && (center > previous || center > next);
    }

    private static bool IsBetter(
        double candidate,
        double current,
        StationExtremum extremum) =>
        extremum == StationExtremum.Minimum ? candidate < current : candidate > current;

    private static double UnwrapNear(double longitude, double reference)
    {
        while (longitude - reference > 180) longitude -= 360;
        while (longitude - reference < -180) longitude += 360;
        return longitude;
    }

    private readonly record struct StationSample(Instant Instant, double Longitude);
}
