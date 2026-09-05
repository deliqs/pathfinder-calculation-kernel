using PathFinder.AccuracyBenchmark.References;

namespace PathFinder.AccuracyBenchmark.Tests;

public sealed class SwissOutputParserTests
{
    [Fact]
    public void Parse_ExactInvocationAndTwelveCusps_ReturnsNormalizedRows()
    {
        const string output = """
            house  1       186.936667747
            house  2       211.624946545
            house  3       242.475284683
            house  4       279.038859132
            house  5       314.632874146
            house  6       343.913697785
            house  7         6.936667747
            house  8        31.624946545
            house  9        62.475284683
            house 10        99.038859132
            house 11       134.632874146
            house 12       163.913697785
            """;
        var expectation = new SwissOutputExpectation(
            "london-j2000",
            "P",
            2451544.5,
            -0.1278,
            51.5074);

        var actual = SwissOutputParser.Parse(output, expectation);

        Assert.Equal(12, actual.Count);
        Assert.Equal(186.936667747, actual[0].LongitudeDegrees, precision: 9);
        Assert.Equal(12, actual[^1].Cusp);
    }

    [Theory]
    [InlineData("K")]
    [InlineData("R")]
    [InlineData("C")]
    [InlineData("O")]
    public void Parse_SupportedHouseSystemCode_ReturnsTwelveCusps(string code)
    {
        const string output = """
            house  1       10.0
            house  2       20.0
            house  3       30.0
            house  4       40.0
            house  5       50.0
            house  6       60.0
            house  7       70.0
            house  8       80.0
            house  9       90.0
            house 10      100.0
            house 11      110.0
            house 12      120.0
            """;
        var expectation = new SwissOutputExpectation("case", code, 2451544.5, 0, 0);

        var actual = SwissOutputParser.Parse(output, expectation);

        Assert.Equal(12, actual.Count);
        Assert.Equal(Enumerable.Range(1, 12), actual.Select(row => row.Cusp));
    }

    [Theory]
    [InlineData("house 1 10")]
    [InlineData("fatal error: ephemeris path missing")]
    public void Parse_MalformedOrIncompleteOutput_Throws(string output)
    {
        var expectation = new SwissOutputExpectation(
            "case",
            "P",
            2451544.5,
            0,
            0);

        Assert.Throws<InvalidDataException>(() => SwissOutputParser.Parse(output, expectation));
    }

    [Theory]
    [InlineData("Swiss Ephemeris test application\nVersion: 2.10.03\n", "2.10.03")]
    [InlineData("Version: 2.10.02\n", "2.10.03")]
    [InlineData("no version here\n", "2.10.03")]
    public void ParseVersion_ValidatesSeparateUnmodifiedVersionOutput(
        string versionOutput,
        string expectedVersion)
    {
        if (versionOutput.Contains(expectedVersion, StringComparison.Ordinal))
        {
            Assert.Equal(expectedVersion, SwissOutputParser.ParseVersion(versionOutput, expectedVersion));
        }
        else
        {
            Assert.Throws<InvalidDataException>(() =>
                SwissOutputParser.ParseVersion(versionOutput, expectedVersion));
        }
    }

    [Fact]
    public void ParseVersion_MoreThanOneVersionIdentification_Throws()
    {
        const string output = "Version: 2.10.03\nhelp\nVersion: 2.10.03\n";

        Assert.Throws<InvalidDataException>(() =>
            SwissOutputParser.ParseVersion(output, "2.10.03"));
    }
}
