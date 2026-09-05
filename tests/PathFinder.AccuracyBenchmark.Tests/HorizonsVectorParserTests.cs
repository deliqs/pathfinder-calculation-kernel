using PathFinder.AccuracyBenchmark.References;

namespace PathFinder.AccuracyBenchmark.Tests;

public sealed class HorizonsVectorParserTests
{
    private const string ValidResult = """
        Target body name: 2060 Chiron (1977 UB)           {source: JPL#171}
        Center body name: Sun (10)                        {source: DE441}
        Output units    : AU-D
        Calendar mode   : Gregorian
        Output type     : GEOMETRIC cartesian states
        Output format   : 2 (position and velocity)
        Reference frame : ICRF
                    JDTDB,            Calendar Date (TDB),                      X,                      Y,                      Z,                     VX,                     VY,                     VZ,
        $$SOE
        2451545.000000000, A.D. 2000-Jan-01 12:00:00.0000, -3.529597323721606E+00, -8.675401114502414E+00, -2.935904700117773E+00,  4.971227226758336E-03, -3.626418894486951E-03, -8.257960206970693E-04,
        $$EOE
        """;

    [Fact]
    public void Parse_CurrentJpl171Seed_ReturnsExactSixComponents()
    {
        var vector = HorizonsVectorParser.Parse(Payload(ValidResult), Expectation());

        Assert.Equal(-3.529597323721606, vector.X);
        Assert.Equal(-8.675401114502414, vector.Y);
        Assert.Equal(-2.935904700117773, vector.Z);
        Assert.Equal(0.004971227226758336, vector.Vx);
        Assert.Equal(-0.003626418894486951, vector.Vy);
        Assert.Equal(-0.0008257960206970693, vector.Vz);
        Assert.Equal(2451545.0, vector.JulianDateTdb);
    }

    [Theory]
    [InlineData("JPL#170", "ICRF", "AU-D")]
    [InlineData("JPL#171", "ECLIPTIC", "AU-D")]
    [InlineData("JPL#171", "ICRF", "KM-S")]
    public void Parse_WrongSolutionFrameOrUnits_Throws(string solution, string frame, string units)
    {
        var result = ValidResult
            .Replace("JPL#171", solution, StringComparison.Ordinal)
            .Replace("Reference frame : ICRF", $"Reference frame : {frame}", StringComparison.Ordinal)
            .Replace("Output units    : AU-D", $"Output units    : {units}", StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() =>
            HorizonsVectorParser.Parse(Payload(result), Expectation()));
    }

    private static HorizonsVectorExpectation Expectation() => new(
        "Target body name: 2060 Chiron (1977 UB) {source: JPL#171}",
        "Sun (10)",
        "ICRF",
        "AU-D",
        "TDB",
        2451545.0,
        "NASA/JPL Horizons API",
        "1.2");

    private static string Payload(string result) =>
        System.Text.Json.JsonSerializer.Serialize(new
        {
            signature = new { source = "NASA/JPL Horizons API", version = "1.2" },
            result
        });
}
