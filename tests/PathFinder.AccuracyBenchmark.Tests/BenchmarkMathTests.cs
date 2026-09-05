using PathFinder.AccuracyBenchmark.Calculation;

namespace PathFinder.AccuracyBenchmark.Tests;

public sealed class BenchmarkMathTests
{
    [Theory]
    [InlineData(359.999, 0.001, 0.002)]
    [InlineData(0.001, 359.999, 0.002)]
    [InlineData(10, 190, 180)]
    [InlineData(42, 42, 0)]
    public void CircularDistanceDegrees_WrapsAtZero(
        double first,
        double second,
        double expected)
    {
        var actual = BenchmarkMath.CircularDistanceDegrees(first, second);

        Assert.Equal(expected, actual, precision: 12);
    }

    [Fact]
    public void Median_EvenCount_AveragesMiddleValues()
    {
        var actual = BenchmarkMath.Median([9, 1, 7, 3]);

        Assert.Equal(5, actual);
    }

    [Fact]
    public void Maximum_ReturnsLargestValueRegardlessOfOrder()
    {
        var actual = BenchmarkMath.Maximum([0.5, 74.75, 2.4, 13.2]);

        Assert.Equal(74.75, actual);
    }

    [Fact]
    public void Passed_IncludesToleranceBoundary()
    {
        Assert.True(BenchmarkMath.Passed(60, 60));
        Assert.False(BenchmarkMath.Passed(60.0000001, 60));
    }
}
