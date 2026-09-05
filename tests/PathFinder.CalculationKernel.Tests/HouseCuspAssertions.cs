using PathFinder.CalculationKernel.Houses;

namespace PathFinder.CalculationKernel.Tests;

internal static class HouseCuspAssertions
{
    internal const double Tolerance = 0.01;

    internal static double CircularDifference(double first, double second)
    {
        var difference = Math.Abs(first - second);
        return Math.Min(difference, 360.0 - difference);
    }

    internal static void MatchOracle(IReadOnlyList<HouseCusp> houses, IReadOnlyList<double> expected)
    {
        Assert.Equal(12, houses.Count);
        for (var index = 0; index < 12; index++)
        {
            Assert.True(CircularDifference(houses[index].CuspPosition.Longitude, expected[index]) <= Tolerance,
                $"House {index + 1} should match the oracle.");
        }
    }

    internal static void AssertStandardInvariants(
        HouseCalculator calculator,
        IReadOnlyList<HouseCusp> houses,
        double mc,
        double latitude)
    {
        Assert.Equal(12, houses.Count);
        Assert.All(houses, cusp => Assert.True(double.IsFinite(cusp.CuspPosition.Longitude)
            && cusp.CuspPosition.Longitude >= 0.0
            && cusp.CuspPosition.Longitude < 360.0));
        for (var index = 0; index < 12; index++)
        {
            Assert.Equal(index + 1, houses[index].HouseNumber);
        }

        Assert.True(CircularDifference(houses[0].CuspPosition.Longitude,
                calculator.CalculateAscendantFromMc(mc, latitude).Longitude)
            <= Tolerance);
        Assert.True(CircularDifference(houses[9].CuspPosition.Longitude, mc) <= Tolerance);
        for (var index = 0; index < 6; index++)
        {
            Assert.InRange(CircularDifference(houses[index].CuspPosition.Longitude,
                houses[index + 6].CuspPosition.Longitude), 180.0 - Tolerance, 180.0 + Tolerance);
        }

        AssertDirectedOrder(mc, houses[10], houses[11], houses[0], houses[1], houses[2], houses[3]);
    }

    internal static void Differs(IReadOnlyList<HouseCusp> actual, IReadOnlyList<HouseCusp> porphyry)
        => Assert.Contains(actual.Select((cusp, index) => CircularDifference(
            cusp.CuspPosition.Longitude, porphyry[index].CuspPosition.Longitude)),
            difference => difference > Tolerance);

    internal static void MatchesExactly(IReadOnlyList<HouseCusp> actual, IReadOnlyList<HouseCusp> expected)
        => Assert.Equal(actual.Select(cusp => cusp.CuspPosition.Longitude),
            expected.Select(cusp => cusp.CuspPosition.Longitude));

    private static void AssertDirectedOrder(double mc, params HouseCusp[] cusps)
    {
        var previous = mc;
        foreach (var cusp in cusps)
        {
            var distance = AstroMath.NormalizeDegrees(cusp.CuspPosition.Longitude - previous);
            Assert.InRange(distance, double.Epsilon, 180.0 - double.Epsilon);
            previous = cusp.CuspPosition.Longitude;
        }
    }
}
