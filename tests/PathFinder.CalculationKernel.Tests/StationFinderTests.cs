using NodaTime;
using NodaTime.Text;
using PathFinder.CalculationKernel.Ephemeris;
using PathFinder.CalculationKernel.Search;

namespace PathFinder.CalculationKernel.Tests;

public class StationFinderTests
{
    [Theory]
    [InlineData(
        Planet.Mercury,
        "2024-04-22T00:00:00Z",
        6,
        StationExtremum.Minimum,
        "2024-04-25T12:51:45.916906191Z")]
    [InlineData(
        Planet.Saturn,
        "2024-06-26T00:00:00Z",
        7,
        StationExtremum.Maximum,
        "2024-06-29T19:06:54.17098049Z")]
    [InlineData(
        Planet.Pluto,
        "2024-04-29T00:00:00Z",
        6,
        StationExtremum.Maximum,
        "2024-05-02T17:30:15.478278664Z")]
    public void Find_PublishedBenchmarkStation_MatchesTenMinuteInterpolation(
        Planet body,
        string startText,
        int windowDays,
        StationExtremum extremum,
        string expectedText)
    {
        var finder = new StationFinder(new AstronomyEngineEphemeris());
        var start = InstantPattern.ExtendedIso.Parse(startText).Value;
        var expected = InstantPattern.ExtendedIso.Parse(expectedText).Value;

        var actual = finder.Find(
            body,
            start,
            Duration.FromDays(windowDays),
            extremum);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Find_ParabolicMinimumBetweenSamples_InterpolatesTheExtremum()
    {
        var start = Instant.FromUtc(2025, 1, 1, 0, 0);
        var expected = start + Duration.FromMinutes(63);
        var finder = new StationFinder(new FunctionEphemeris(instant =>
        {
            var minutes = (instant - expected).TotalMinutes;
            return 20 + minutes * minutes / 10_000;
        }));

        var actual = finder.Find(
            Planet.Mercury,
            start,
            Duration.FromHours(2),
            StationExtremum.Minimum);

        Assert.NotNull(actual);
        Assert.InRange(Math.Abs((actual.Value - expected).TotalMilliseconds), 0, 1);
    }

    [Fact]
    public void Find_CircularMaximum_UnwrapsSamplesAcrossZeroDegrees()
    {
        var start = Instant.FromUtc(2025, 1, 1, 0, 0);
        var expected = start + Duration.FromMinutes(60);
        var finder = new StationFinder(new FunctionEphemeris(instant =>
        {
            var minutes = (instant - expected).TotalMinutes;
            return Normalize(0.1 - minutes * minutes / 20_000);
        }));

        var actual = finder.Find(
            Planet.Mercury,
            start,
            Duration.FromHours(2),
            StationExtremum.Maximum);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Find_MonotonicSeries_ReturnsNull()
    {
        var start = Instant.FromUtc(2025, 1, 1, 0, 0);
        var finder = new StationFinder(new FunctionEphemeris(instant =>
            10 + (instant - start).TotalDays));

        var result = finder.Find(
            Planet.Mercury,
            start,
            Duration.FromHours(1),
            StationExtremum.Maximum);

        Assert.Null(result);
    }

    [Fact]
    public void Find_ZeroWindow_ReturnsNull()
    {
        var start = Instant.FromUtc(2025, 1, 1, 0, 0);
        var finder = new StationFinder(new FunctionEphemeris(_ => 10));

        var result = finder.Find(
            Planet.Mercury,
            start,
            Duration.Zero,
            StationExtremum.Minimum);

        Assert.Null(result);
    }

    [Fact]
    public void Find_NegativeWindow_Throws()
    {
        var start = Instant.FromUtc(2025, 1, 1, 0, 0);
        var finder = new StationFinder(new FunctionEphemeris(_ => 10));

        Assert.Throws<ArgumentOutOfRangeException>(() => finder.Find(
            Planet.Mercury,
            start,
            Duration.FromTicks(-1),
            StationExtremum.Minimum));
    }

    private static double Normalize(double longitude)
    {
        var normalized = longitude % 360;
        return normalized < 0 ? normalized + 360 : normalized;
    }

    private sealed class FunctionEphemeris(Func<Instant, double> longitude) : IEphemeris
    {
        public double GetLongitude(Planet planet, Instant utcMoment) => longitude(utcMoment);

        public PlanetPosition GetPlanetPosition(Planet planet, Instant utcMoment) => new()
        {
            Planet = planet,
            Position = new EclipticPosition { Longitude = longitude(utcMoment) },
            DailyMotion = GetDailyMotion(planet, utcMoment)
        };

        public double GetDailyMotion(Planet planet, Instant utcMoment)
        {
            var difference = longitude(utcMoment + Duration.FromDays(1)) - longitude(utcMoment);
            if (difference > 180) difference -= 360;
            if (difference < -180) difference += 360;
            return difference;
        }

        public IReadOnlyList<PlanetPosition> GetAllPlanetPositions(Instant utcMoment) =>
            Enum.GetValues<Planet>().Select(body => GetPlanetPosition(body, utcMoment)).ToArray();

        public Instant FindPlanetAtLongitude(
            Planet planet,
            double targetLongitude,
            Instant searchStart,
            int searchDaysForward = 400) =>
            throw new NotSupportedException();
    }
}
