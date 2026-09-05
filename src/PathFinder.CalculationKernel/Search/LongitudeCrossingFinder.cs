using NodaTime;
using PathFinder.CalculationKernel.Ephemeris;

namespace PathFinder.CalculationKernel.Search;

public enum LongitudeMotion
{
    Direct,
    Retrograde
}

/// <summary>
/// Finds the first forward longitude crossing with explicit motion direction. The station-aware
/// split preserves crossings that occur twice inside one coarse ephemeris interval.
/// </summary>
public sealed class LongitudeCrossingFinder(IEphemeris ephemeris)
{
    private const double ExactToleranceDegrees = 0.0001;

    public Instant? FindFirst(
        Planet body,
        double targetLongitude,
        LongitudeMotion motion,
        Instant searchStart,
        Duration searchWindow)
    {
        if (searchWindow < Duration.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(searchWindow));
        }

        targetLongitude = LongitudeSearchMath.NormalizeDegrees(targetLongitude);
        if (searchWindow == Duration.Zero)
        {
            var isExact = Math.Abs(LongitudeSearchMath.DirectedDifference(
                ephemeris.GetLongitude(body, searchStart),
                targetLongitude)) < ExactToleranceDegrees;
            var currentMotion = ephemeris.GetDailyMotion(body, searchStart) < 0
                ? LongitudeMotion.Retrograde
                : LongitudeMotion.Direct;
            return isExact && currentMotion == motion ? searchStart : null;
        }

        var cache = new Dictionary<Instant, double>();
        double ReadLongitude(Instant instant)
        {
            if (!cache.TryGetValue(instant, out var longitude))
            {
                longitude = ephemeris.GetLongitude(body, instant);
                cache.Add(instant, longitude);
            }

            return longitude;
        }

        return LongitudeCrossingSearch.FindFirst(
            body,
            targetLongitude,
            motion,
            searchStart,
            searchStart + searchWindow,
            ReadLongitude);
    }
}
