using System.Reflection;
using NodaTime;
using PathFinder.CalculationKernel.Houses;

namespace PathFinder.CalculationKernel.Tests;

public class HouseCalculatorPublicSurfaceTests
{
    private const double LongitudeToleranceDegrees = 1e-9;

    private readonly HouseCalculator _calculator = new();

    [Fact]
    public void McDerivedMethods_ArePublicInstanceSurface()
    {
        AssertPublicInstanceMethod(nameof(HouseCalculator.CalculateAscendantFromMc));
        AssertPublicInstanceMethod(nameof(HouseCalculator.CalculateHousesFromMc));
    }

    [Theory]
    [InlineData(51.5074, -0.1278, "Europe/London")]
    [InlineData(40.7128, -74.006, "America/New_York")]
    [InlineData(-33.8688, 151.2093, "Australia/Sydney")]
    public void CalculateFromMc_MatchesTimeBasedCalculation(
        double latitude,
        double longitude,
        string timezoneId)
    {
        var instant = Instant.FromUtc(2000, 1, 1, 12, 0);
        var location = new GeoLocation
        {
            Latitude = latitude,
            Longitude = longitude,
            TimezoneId = timezoneId
        };

        var midheaven = _calculator.CalculateMidheaven(instant, location).Longitude;
        var fromMc = _calculator.CalculateAscendantFromMc(midheaven, location.Latitude);
        var fromTime = _calculator.CalculateAscendant(instant, location);

        Assert.True(
            HouseCuspAssertions.CircularDifference(fromTime.Longitude, fromMc.Longitude)
            <= LongitudeToleranceDegrees,
            $"Ascendant from MC should match CalculateAscendant within {LongitudeToleranceDegrees}°.");

        foreach (var system in Enum.GetValues<HouseSystem>())
        {
            AssertHousesMatch(
                _calculator.CalculateHouses(instant, location, system),
                _calculator.CalculateHousesFromMc(midheaven, location.Latitude, system),
                system);
        }
    }

    private static void AssertPublicInstanceMethod(string name)
    {
        var method = typeof(HouseCalculator).GetMethod(name, BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(method);
        Assert.True(method!.IsPublic);
        Assert.False(method.IsStatic);
    }

    private static void AssertHousesMatch(
        IReadOnlyList<HouseCusp> fromTime,
        IReadOnlyList<HouseCusp> fromMc,
        HouseSystem system)
    {
        Assert.Equal(fromTime.Count, fromMc.Count);
        for (var index = 0; index < fromTime.Count; index++)
        {
            Assert.Equal(fromTime[index].HouseNumber, fromMc[index].HouseNumber);
            Assert.True(
                HouseCuspAssertions.CircularDifference(
                    fromTime[index].CuspPosition.Longitude,
                    fromMc[index].CuspPosition.Longitude) <= LongitudeToleranceDegrees,
                $"{system} house {fromTime[index].HouseNumber} from MC should match CalculateHouses.");
        }
    }
}
