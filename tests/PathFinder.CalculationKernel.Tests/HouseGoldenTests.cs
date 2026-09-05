using NodaTime;
using NodaTime.Text;
using PathFinder.CalculationKernel.Houses;

namespace PathFinder.CalculationKernel.Tests;

public class HouseGoldenTests
{
    private readonly HouseCalculator _calculator = new();

    [Fact]
    public void CalculateHouses_PublishedBenchmarkCases_MatchFortyEightCharacterizedCusps()
    {
        // Characterized from the published PathFinder benchmark inputs. Swiss values and
        // tolerances are independently archived by the benchmark runner, not used as this oracle.
        Assert.Equal(48, PublishedCases.Sum(testCase => testCase.Longitudes.Count));

        foreach (var testCase in PublishedCases)
        {
            var instant = InstantPattern.ExtendedIso.Parse(testCase.Utc).Value;
            var location = new GeoLocation
            {
                Latitude = testCase.Latitude,
                Longitude = testCase.Longitude,
                TimezoneId = "UTC"
            };

            var cusps = _calculator.CalculateHouses(instant, location, HouseSystem.Placidus);

            Assert.Equal(12, cusps.Count);
            for (var index = 0; index < cusps.Count; index++)
            {
                Assert.Equal(index + 1, cusps[index].HouseNumber);
                Assert.Equal(testCase.Longitudes[index], cusps[index].CuspPosition.Longitude, 9);
            }
        }
    }

    [Fact]
    public void CalculateHouses_PolarPlacidusCase_EqualsPorphyryFallback()
    {
        var instant = Instant.FromUtc(2024, 6, 21, 12, 0);
        var tromso = new GeoLocation
        {
            Latitude = 69.6492,
            Longitude = 18.9553,
            TimezoneId = "Europe/Oslo"
        };

        var placidus = _calculator.CalculateHouses(instant, tromso, HouseSystem.Placidus);
        var porphyry = _calculator.CalculateHouses(instant, tromso, HouseSystem.Porphyry);

        Assert.Equal(
            porphyry.Select(cusp => cusp.CuspPosition.Longitude),
            placidus.Select(cusp => cusp.CuspPosition.Longitude));
    }

    [Theory]
    [InlineData(HouseSystem.Placidus)]
    [InlineData(HouseSystem.WholeSign)]
    [InlineData(HouseSystem.Equal)]
    [InlineData(HouseSystem.Koch)]
    [InlineData(HouseSystem.Regiomontanus)]
    [InlineData(HouseSystem.Campanus)]
    [InlineData(HouseSystem.Porphyry)]
    public void CalculateHouses_SupportedSystem_ReturnsTwelveNumberedCusps(HouseSystem system)
    {
        var location = new GeoLocation
        {
            Latitude = 51.5074,
            Longitude = -0.1278,
            TimezoneId = "Europe/London"
        };

        var cusps = _calculator.CalculateHouses(
            Instant.FromUtc(2000, 1, 1, 0, 0),
            location,
            system);

        Assert.Equal(Enumerable.Range(1, 12), cusps.Select(cusp => cusp.HouseNumber));
        Assert.All(cusps, cusp => Assert.InRange(cusp.CuspPosition.Longitude, 0, 360));
    }

    private static IReadOnlyList<HouseCase> PublishedCases =>
    [
        new(
            "2000-01-01T00:00:00Z",
            51.5074,
            -0.1278,
            [186.93906153606568, 211.62739612330878, 242.47795329737852,
             279.04201845564137, 314.6361509343626, 343.9166598989988,
             6.939061536065651, 31.627396123308813, 62.47795329737852,
             99.04201845564138, 134.6361509343626, 163.91665989899877]),
        new(
            "2024-04-08T18:00:00Z",
            40.7128,
            -74.006,
            [135.48789278034957, 156.95269406902958, 183.3723099918528,
             215.78590126217858, 251.84740458323773, 286.0263345085102,
             315.48789278034957, 336.9526940690296, 3.3723099918528305,
             35.78590126217858, 71.84740458323772, 106.02633450851022]),
        new(
            "1950-01-01T00:00:00Z",
            -33.8688,
            151.2093,
            [344.2270512557373, 10.29093161453461, 40.269594699897524,
             72.73369994543663, 105.37145790250065, 136.2135532246735,
             164.22705125573725, 190.2909316145346, 220.26959469989754,
             252.73369994543663, 285.37145790250065, 316.2135532246735]),
        new(
            "2024-06-21T12:00:00Z",
            69.6492,
            18.9553,
            [189.59291617262681, 222.28038968110926, 254.9678631895917,
             287.65533669807417, 314.9678631895917, 342.2803896811093,
             9.592916172626815, 42.280389681109256, 74.9678631895917,
             107.65533669807414, 134.9678631895917, 162.28038968110926])
    ];

    private sealed record HouseCase(
        string Utc,
        double Latitude,
        double Longitude,
        IReadOnlyList<double> Longitudes);
}
