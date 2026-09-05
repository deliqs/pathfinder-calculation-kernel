using PathFinder.AccuracyBenchmark.Cases;
using PathFinder.AccuracyBenchmark.References;

namespace PathFinder.AccuracyBenchmark.Tests;

public sealed class SwissInvocationBuilderTests
{
    [Fact]
    public void BuildVersion_UsesExactHelpCommandAndDeterministicEnvironment()
    {
        var invocation = SwissInvocationBuilder.BuildVersion();

        Assert.Equal(["-h"], invocation.Arguments);
        Assert.Equal("C", invocation.Environment["LC_ALL"]);
        Assert.Equal("UTC", invocation.Environment["TZ"]);
    }

    [Fact]
    public void BuildHouse_ConsumesUtcLocationAndHouseSystem()
    {
        var row = new HouseCase(
            "london-j2000",
            "2000-01-01T00:00:00Z",
            51.5074,
            -0.1278,
            "Placidus",
            "Placidus",
            "P",
            3600);

        var invocation = SwissInvocationBuilder.BuildHouse(row);

        Assert.Equal(2451544.5, invocation.UtJulianDate);
        Assert.Equal(
            ["-bj2451544.5", "-ut", "-p", "-house-0.1278,51.5074,P", "-fPl", "-head"],
            invocation.Arguments);
    }

    [Fact]
    public void BuildHouse_PorphyrySouthernEastPositive_UsesManifestValuesUnchanged()
    {
        var row = new HouseCase(
            "southern-polar",
            "2024-06-21T12:00:00Z",
            -69.6492,
            18.9553,
            "Placidus",
            "Porphyry",
            "O",
            3600);

        var invocation = SwissInvocationBuilder.BuildHouse(row);

        Assert.Equal(2460483.0, invocation.UtJulianDate);
        Assert.Contains("-house18.9553,-69.6492,O", invocation.Arguments);
    }
}
