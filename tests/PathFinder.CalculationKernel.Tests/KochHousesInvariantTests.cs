using PathFinder.CalculationKernel.Houses;

namespace PathFinder.CalculationKernel.Tests;

public class KochHousesInvariantTests
{
    private readonly HouseCalculator _calculator = new();

    [Theory]
    [MemberData(nameof(OracleRows))]
    public void CalculateHousesFromMc_PreservesKochInvariants(QuadrantHouseOracleTests.OracleRow row)
    {
        var houses = _calculator.CalculateHousesFromMc(row.Mc, row.Latitude, HouseSystem.Koch);
        HouseCuspAssertions.AssertStandardInvariants(_calculator, houses, row.Mc, row.Latitude);
    }

    [Theory]
    [InlineData(60.0)]
    [InlineData(-60.0)]
    public void KochAtThreshold_IsComputedRatherThanPorphyry(double latitude)
        => AssertComputedAtThreshold(latitude);

    [Theory]
    [InlineData(60.5)]
    [InlineData(-60.5)]
    public void KochBeyondThreshold_FallsBackToPorphyry(double latitude)
        => AssertFallback(latitude);

    [Fact]
    public void DiurnalSemiArcBoundaries_RiseAtTheMcAndIc()
    {
        const double mc = 99.0;
        const double latitude = 51.5074;
        const double obliquity = 23.4393;
        var ramc = AstroMath.CalculateRamc(mc, obliquity);
        var declination = Math.Asin(Math.Sin(AstroMath.DegreesToRadians(obliquity))
            * Math.Sin(AstroMath.DegreesToRadians(mc)));
        var ascensionalDifference = Math.Asin(Math.Tan(AstroMath.DegreesToRadians(latitude))
            * Math.Tan(declination));
        var semiArc = 90.0 + AstroMath.RadiansToDegrees(ascensionalDifference);

        var risingMc = AscendantAtRamc(ramc - semiArc, latitude, obliquity);
        var risingIc = AscendantAtRamc(ramc + semiArc, latitude, obliquity);

        Assert.True(HouseCuspAssertions.CircularDifference(risingMc, mc) <= HouseCuspAssertions.Tolerance);
        Assert.True(HouseCuspAssertions.CircularDifference(
            risingIc, AstroMath.NormalizeDegrees(mc + 180.0)) <= HouseCuspAssertions.Tolerance);
    }

    public static IEnumerable<object[]> OracleRows() => QuadrantHouseOracleTests.McOracleRows()
        .Where(row => ((QuadrantHouseOracleTests.OracleRow)row[0]).System == HouseSystem.Koch);

    private void AssertComputedAtThreshold(double latitude)
    {
        var computed = _calculator.CalculateHousesFromMc(99.0, latitude, HouseSystem.Koch);
        var porphyry = _calculator.CalculateHousesFromMc(99.0, latitude, HouseSystem.Porphyry);
        HouseCuspAssertions.Differs(computed, porphyry);
    }

    private void AssertFallback(double latitude)
    {
        var actual = _calculator.CalculateHousesFromMc(99.0, latitude, HouseSystem.Koch);
        var expected = _calculator.CalculateHousesFromMc(99.0, latitude, HouseSystem.Porphyry);
        HouseCuspAssertions.MatchesExactly(actual, expected);
    }

    private double AscendantAtRamc(double ramc, double latitude, double obliquity)
    {
        var ramcRadians = AstroMath.DegreesToRadians(ramc);
        var obliquityRadians = AstroMath.DegreesToRadians(obliquity);
        var mc = AstroMath.NormalizeDegrees(AstroMath.RadiansToDegrees(Math.Atan2(
            Math.Sin(ramcRadians), Math.Cos(ramcRadians) * Math.Cos(obliquityRadians))));
        return _calculator.CalculateAscendantFromMc(mc, latitude).Longitude;
    }
}
