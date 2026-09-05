using NodaTime;
using PathFinder.CalculationKernel.Ephemeris;
using PathFinder.CalculationKernel.Search;

namespace PathFinder.CalculationKernel.Tests;

public class LongitudeCrossingFinderTests
{
    [Theory]
    [InlineData(
        "2024-03-15T00:00:00Z",
        15,
        LongitudeMotion.Direct,
        "2024-03-21T23:56:07.96875Z")]
    [InlineData(
        "2024-04-01T00:00:00Z",
        20,
        LongitudeMotion.Retrograde,
        "2024-04-15T08:56:29.0625Z")]
    [InlineData(
        "2024-04-20T00:00:00Z",
        25,
        LongitudeMotion.Direct,
        "2024-05-05T18:37:58.125Z")]
    public void FindFirst_PublishedMercuryCrossing_MatchesCharacterizedInstant(
        string startText,
        int windowDays,
        LongitudeMotion motion,
        string expectedText)
    {
        var finder = new LongitudeCrossingFinder(new AstronomyEngineEphemeris());
        var start = NodaTime.Text.InstantPattern.ExtendedIso.Parse(startText).Value;
        var expected = NodaTime.Text.InstantPattern.ExtendedIso.Parse(expectedText).Value;

        var actual = finder.FindFirst(
            Planet.Mercury,
            20,
            motion,
            start,
            Duration.FromDays(windowDays));

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void FindFirst_StartAndEndOnTarget_ReturnsMatchingEndpoints()
    {
        var start = Instant.FromUtc(2025, 1, 1, 0, 0);
        var direct = LinearEphemeris(start, 10, 1);
        var retrograde = LinearEphemeris(start, 11, -1);

        var atStart = new LongitudeCrossingFinder(direct).FindFirst(
            Planet.Mercury,
            10,
            LongitudeMotion.Direct,
            start,
            Duration.FromDays(1));
        var atEnd = new LongitudeCrossingFinder(retrograde).FindFirst(
            Planet.Mercury,
            10,
            LongitudeMotion.Retrograde,
            start,
            Duration.FromDays(1));

        Assert.Equal(start, atStart);
        Assert.Equal(start + Duration.FromDays(1), atEnd);
    }

    [Fact]
    public void FindFirst_ZeroWindow_OnlyReturnsAnExactStartWithRequestedMotion()
    {
        var start = Instant.FromUtc(2025, 1, 1, 0, 0);
        var finder = new LongitudeCrossingFinder(LinearEphemeris(start, 20, 1));

        var exact = finder.FindFirst(
            Planet.Mercury,
            20,
            LongitudeMotion.Direct,
            start,
            Duration.Zero);
        var wrongMotion = finder.FindFirst(
            Planet.Mercury,
            20,
            LongitudeMotion.Retrograde,
            start,
            Duration.Zero);
        var absent = finder.FindFirst(
            Planet.Mercury,
            21,
            LongitudeMotion.Direct,
            start,
            Duration.Zero);

        Assert.Equal(start, exact);
        Assert.Null(wrongMotion);
        Assert.Null(absent);
    }

    [Fact]
    public void FindFirst_NegativeWindow_Throws()
    {
        var start = Instant.FromUtc(2025, 1, 1, 0, 0);
        var finder = new LongitudeCrossingFinder(LinearEphemeris(start, 20, 1));

        Assert.Throws<ArgumentOutOfRangeException>(() => finder.FindFirst(
            Planet.Mercury,
            20,
            LongitudeMotion.Direct,
            start,
            Duration.FromTicks(-1)));
    }

    [Fact]
    public void FindFirst_TwoCrossingsAroundStation_SelectsRequestedMotion()
    {
        var start = Instant.FromUtc(2025, 1, 1, 0, 0);
        var ephemeris = new FunctionEphemeris(instant =>
        {
            var hours = (instant - start).TotalHours;
            return 100 - Math.Pow(hours - 12, 2) / 144;
        });
        var finder = new LongitudeCrossingFinder(ephemeris);

        var direct = finder.FindFirst(
            Planet.Mercury,
            99.5,
            LongitudeMotion.Direct,
            start,
            Duration.FromDays(1));
        var retrograde = finder.FindFirst(
            Planet.Mercury,
            99.5,
            LongitudeMotion.Retrograde,
            start,
            Duration.FromDays(1));

        Assert.NotNull(direct);
        Assert.NotNull(retrograde);
        Assert.True(direct < start + Duration.FromHours(12));
        Assert.True(retrograde > start + Duration.FromHours(12));
    }

    [Fact]
    public void FindFirst_DirectCrossingThroughZero_UnwrapsCircularLongitude()
    {
        var start = Instant.FromUtc(2025, 1, 1, 0, 0);
        var finder = new LongitudeCrossingFinder(LinearEphemeris(start, 359, 2));

        var crossing = finder.FindFirst(
            Planet.Mercury,
            0,
            LongitudeMotion.Direct,
            start,
            Duration.FromDays(1));

        Assert.NotNull(crossing);
        Assert.InRange(Math.Abs((crossing.Value - (start + Duration.FromHours(12))).TotalSeconds), 0, 1);
    }

    private static IEphemeris LinearEphemeris(
        Instant epoch,
        double initialLongitude,
        double degreesPerDay) =>
        new FunctionEphemeris(instant => Normalize(
            initialLongitude + (instant - epoch).TotalDays * degreesPerDay));

    private static double Normalize(double value)
    {
        var result = value % 360;
        return result < 0 ? result + 360 : result;
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
            Enum.GetValues<Planet>().Select(planet => GetPlanetPosition(planet, utcMoment)).ToArray();

        public Instant FindPlanetAtLongitude(
            Planet planet,
            double targetLongitude,
            Instant searchStart,
            int searchDaysForward = 400) =>
            throw new NotSupportedException();
    }
}
