using PathFinder.CalculationKernel.Houses;

namespace PathFinder.CalculationKernel.Tests;

public class CampanusHousesInvariantTests
{
    private readonly HouseCalculator _calculator = new();

    [Theory]
    [MemberData(nameof(OracleRows))]
    public void CalculateHousesFromMc_PreservesCampanusInvariants(QuadrantHouseOracleTests.OracleRow row)
    {
        var houses = _calculator.CalculateHousesFromMc(row.Mc, row.Latitude, HouseSystem.Campanus);
        HouseCuspAssertions.AssertStandardInvariants(_calculator, houses, row.Mc, row.Latitude);
    }

    [Fact]
    public void HorizonCircle_IntersectsAtTheAscendant()
    {
        const double mc = 99.0;
        const double latitude = 51.5074;
        var ramc = AstroMath.CalculateRamc(mc, 23.4393);
        var longitude = HouseCircleGeometry.IntersectionLongitudeOnMcArc(
            HouseCircleGeometry.NorthHorizonPoint(latitude),
            HouseCircleGeometry.PrimeVerticalPoint(latitude, 90.0),
            ramc,
            23.4393);

        Assert.True(HouseCuspAssertions.CircularDifference(
                longitude, _calculator.CalculateAscendantFromMc(mc, latitude).Longitude)
            <= HouseCuspAssertions.Tolerance);
    }

    [Theory]
    [InlineData(66.0)]
    [InlineData(-66.0)]
    public void CampanusAtThreshold_IsComputedRatherThanPorphyry(double latitude)
        => AssertComputedAtThreshold(latitude);

    [Theory]
    [InlineData(66.5)]
    [InlineData(-66.5)]
    public void CampanusBeyondThreshold_FallsBackToPorphyry(double latitude)
        => AssertFallback(latitude);

    public static IEnumerable<object[]> OracleRows() => QuadrantHouseOracleTests.McOracleRows()
        .Where(row => ((QuadrantHouseOracleTests.OracleRow)row[0]).System == HouseSystem.Campanus);

    private void AssertComputedAtThreshold(double latitude)
    {
        var computed = _calculator.CalculateHousesFromMc(99.0, latitude, HouseSystem.Campanus);
        var porphyry = _calculator.CalculateHousesFromMc(99.0, latitude, HouseSystem.Porphyry);
        HouseCuspAssertions.Differs(computed, porphyry);
    }

    private void AssertFallback(double latitude)
    {
        var actual = _calculator.CalculateHousesFromMc(99.0, latitude, HouseSystem.Campanus);
        var expected = _calculator.CalculateHousesFromMc(99.0, latitude, HouseSystem.Porphyry);
        HouseCuspAssertions.MatchesExactly(actual, expected);
    }
}
