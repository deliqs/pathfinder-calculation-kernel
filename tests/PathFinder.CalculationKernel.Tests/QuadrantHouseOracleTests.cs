using System.Globalization;
using NodaTime;
using PathFinder.CalculationKernel.Houses;

namespace PathFinder.CalculationKernel.Tests;

public class QuadrantHouseOracleTests
{
    private const double Tolerance = 0.01;
    private readonly HouseCalculator _calculator = new();

    public static IEnumerable<object[]> McOracleRows() =>
        ReadRows("house-cusps-swiss-oracle.csv", hasAscendant: false)
            .Concat(ReadRows("house-cusps-swiss-holdout.csv", hasAscendant: false))
            .Select(row => new object[] { row });

    public static IEnumerable<object[]> LondonOracleRows() =>
        ReadRows("house-cusps-swiss-oracle-london2000.csv", hasAscendant: true)
            .Select(row => new object[] { row });

    [Theory]
    [MemberData(nameof(McOracleRows))]
    public void CalculateHousesFromMc_MatchesSwissEphemeris(OracleRow row)
    {
        var houses = _calculator.CalculateHousesFromMc(row.Mc, row.Latitude, row.System);

        if (UsesPolarFallback(row))
        {
            var porphyry = _calculator.CalculateHousesFromMc(row.Mc, row.Latitude, HouseSystem.Porphyry);
            HouseCuspAssertions.MatchesExactly(houses, porphyry);
            return;
        }

        HouseCuspAssertions.MatchOracle(houses, row.Cusps);
    }

    [Theory]
    [InlineData(HouseSystem.Koch, 12.3, 15.0)]
    [InlineData(HouseSystem.Koch, 222.75, -58.0)]
    [InlineData(HouseSystem.Regiomontanus, 12.3, 15.0)]
    [InlineData(HouseSystem.Regiomontanus, 222.75, -58.0)]
    [InlineData(HouseSystem.Campanus, 12.3, 15.0)]
    [InlineData(HouseSystem.Campanus, 222.75, -58.0)]
    public void CalculateHousesFromMc_IsEquivalentForMcModulo360(
        HouseSystem system,
        double mc,
        double latitude)
    {
        var expected = _calculator.CalculateHousesFromMc(mc, latitude, system);
        var increased = _calculator.CalculateHousesFromMc(mc + 360.0, latitude, system);
        var decreased = _calculator.CalculateHousesFromMc(mc - 360.0, latitude, system);

        for (var index = 0; index < 12; index++)
        {
            Assert.True(HouseCuspAssertions.CircularDifference(
                    increased[index].CuspPosition.Longitude, expected[index].CuspPosition.Longitude)
                <= 1e-9);
            Assert.True(HouseCuspAssertions.CircularDifference(
                    decreased[index].CuspPosition.Longitude, expected[index].CuspPosition.Longitude)
                <= 1e-9);
        }
    }

    [Theory]
    [MemberData(nameof(LondonOracleRows))]
    public void CalculateHouses_London2000_MatchesSwissEphemeris(OracleRow row)
    {
        var location = new GeoLocation
        {
            Latitude = 51.5074,
            Longitude = -0.1278,
            TimezoneId = "Europe/London"
        };
        var instant = Instant.FromUtc(2000, 1, 1, 0, 0);
        var houses = _calculator.CalculateHouses(instant, location, row.System);

        Assert.True(HouseCuspAssertions.CircularDifference(
                _calculator.CalculateAscendant(instant, location).Longitude, row.Ascendant!.Value)
            <= Tolerance);
        Assert.True(HouseCuspAssertions.CircularDifference(
                _calculator.CalculateMidheaven(instant, location).Longitude, row.Mc)
            <= Tolerance);
        HouseCuspAssertions.MatchOracle(houses, row.Cusps);
    }

    private static IEnumerable<OracleRow> ReadRows(string fileName, bool hasAscendant)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", fileName);
        foreach (var line in File.ReadLines(path).Skip(1))
        {
            var values = line.Split(',', StringSplitOptions.TrimEntries);
            const int cuspOffset = 3;
            yield return new OracleRow(
                Enum.Parse<HouseSystem>(values[0]),
                hasAscendant ? Parse(values[1]) : null,
                Parse(values[hasAscendant ? 2 : 1]),
                hasAscendant ? 51.5074 : Parse(values[2]),
                values.Skip(cuspOffset).Select(Parse).ToArray());
        }
    }

    private static double Parse(string value) => double.Parse(value, CultureInfo.InvariantCulture);

    private static bool UsesPolarFallback(OracleRow row) => row.System switch
    {
        HouseSystem.Koch => Math.Abs(row.Latitude) > 60.0,
        HouseSystem.Regiomontanus or HouseSystem.Campanus => Math.Abs(row.Latitude) > 66.0,
        _ => false
    };

    public sealed record OracleRow(
        HouseSystem System,
        double? Ascendant,
        double Mc,
        double Latitude,
        IReadOnlyList<double> Cusps);
}
