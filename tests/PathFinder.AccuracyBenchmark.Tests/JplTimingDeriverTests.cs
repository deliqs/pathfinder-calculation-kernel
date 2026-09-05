using NodaTime;
using PathFinder.AccuracyBenchmark.Cases;
using PathFinder.AccuracyBenchmark.References;

namespace PathFinder.AccuracyBenchmark.Tests;

public sealed class JplTimingDeriverTests
{
    [Fact]
    public void Derive_DirectCrossingAcrossZero_InterpolatesFirstMatchingInterval()
    {
        var row = TimingCase("longitude-crossing", "direct", 0, null);
        var rows = new[]
        {
            Longitude("2024-Jan-01 00:00:00.000", 359),
            Longitude("2024-Jan-01 01:00:00.000", 1),
            Longitude("2024-Jan-01 02:00:00.000", 2)
        };

        var result = JplTimingDeriver.Derive(row, rows);

        Assert.Equal(Instant.FromUtc(2024, 1, 1, 0, 30), result.ReferenceInstant);
        Assert.Equal("jpl-quantity31-hourly-linear-crossing", result.ReferenceMethod);
    }

    [Fact]
    public void Derive_RetrogradeCrossing_UsesRequestedMotion()
    {
        var row = TimingCase("longitude-crossing", "retrograde", 20, null);
        var rows = new[]
        {
            Longitude("2024-Jan-01 00:00:00.000", 21),
            Longitude("2024-Jan-01 01:00:00.000", 19)
        };

        var result = JplTimingDeriver.Derive(row, rows);

        Assert.Equal(Instant.FromUtc(2024, 1, 1, 0, 30), result.ReferenceInstant);
    }

    [Fact]
    public void Derive_StationUnwrapsLongitudeAndUsesParabolicExtremum()
    {
        var row = TimingCase("station", "retrograde", null, "maximum");
        var rows = new[]
        {
            Longitude("2024-Jan-01 00:00:00.000", 359.9),
            Longitude("2024-Jan-01 00:10:00.000", 0),
            Longitude("2024-Jan-01 00:20:00.000", 359.9)
        };

        var result = JplTimingDeriver.Derive(row, rows);

        Assert.Equal(Instant.FromUtc(2024, 1, 1, 0, 10), result.ReferenceInstant);
        Assert.Equal("jpl-quantity31-10-minute-parabolic-extremum", result.ReferenceMethod);
    }

    [Fact]
    public void Derive_StationExtremumDoesNotEnterRequestedMotion_Throws()
    {
        var row = TimingCase("station", "direct", null, "maximum");
        var rows = new[]
        {
            Longitude("2024-Jan-01 00:00:00.000", 10),
            Longitude("2024-Jan-01 00:10:00.000", 11),
            Longitude("2024-Jan-01 00:20:00.000", 10)
        };

        Assert.Throws<InvalidDataException>(() => JplTimingDeriver.Derive(row, rows));
    }

    private static TimingCase TimingCase(
        string kind,
        string motion,
        double? target,
        string? extremum) => new(
        "timing",
        kind,
        "Mercury",
        motion,
        target,
        extremum,
        "2024-01-01T00:00:00Z",
        1,
        kind == "station" ? "kernel-station-parabolic-10-minute" : "kernel-longitude-crossing",
        90);

    private static HorizonsLongitudeRow Longitude(string time, double value) =>
        new(time, 0, value, 0, "target");
}
