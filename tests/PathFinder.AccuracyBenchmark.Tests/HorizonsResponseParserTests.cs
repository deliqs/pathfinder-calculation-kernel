using PathFinder.AccuracyBenchmark.References;

namespace PathFinder.AccuracyBenchmark.Tests;

public sealed class HorizonsResponseParserTests
{
    private const string ValidResult = """
        JPL/HORIZONS                     Sun (10)
        Target body name: Sun (10)                       {source: DE441}
        Center body name: Earth (399)                    {source: DE441}
        Time scale used: UT
        Calendar mode   : Gregorian
        Atmos refraction: NO (AIRLESS)
         Date__(UT)__HR:MN:SC.fff, Date_________JDUT, , ,    ObsEcLon,   ObsEcLat,
        $$SOE
         2050-Jan-01 00:00:00.000, 2469807.500000000, , , 280.7483818,  0.0001127,
        $$EOE
         Observer-centered IAU76/80 ecliptic-of-date longitude and latitude of the
        target centers' apparent position, with light-time, gravitational deflection of
        light, and stellar aberrations.  Units: DEGREES
        """;

    [Fact]
    public void Parse_ValidPayload_ReturnsValidatedLongitude()
    {
        var payload = Payload("NASA/JPL Horizons API", "1.2", ValidResult);
        var expectation = Expectation("Sun", "10", "UT", "2050-Jan-01 00:00:00.000");

        var rows = HorizonsResponseParser.Parse(payload, expectation);

        var row = Assert.Single(rows);
        Assert.Equal(280.7483818, row.LongitudeDegrees, precision: 10);
        Assert.Equal(2469807.5, row.JulianDate, precision: 10);
        Assert.Contains("DE441", row.TargetHeader, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{\"signature\":{\"source\":\"NASA/JPL Horizons API\",\"version\":\"1.2\"},\"error\":\"invalid command\"}")]
    [InlineData("{\"signature\":{\"source\":\"NASA/JPL Horizons API\",\"version\":\"1.2\"},\"result\":\"Matching small-bodies: 2\"}")]
    [InlineData("<html>temporarily unavailable</html>")]
    public void Parse_Http200ErrorPayload_Throws(string payload)
    {
        var error = Assert.Throws<InvalidDataException>(() =>
            HorizonsResponseParser.Parse(payload, Expectation("Sun", "10", "UT", "2050-Jan-01 00:00:00.000")));

        Assert.NotEmpty(error.Message);
    }

    [Theory]
    [InlineData("Other source", "1.2")]
    [InlineData("NASA/JPL Horizons API", "9.9")]
    public void Parse_WrongSignature_Throws(string source, string version)
    {
        var payload = Payload(source, version, ValidResult);

        Assert.Throws<InvalidDataException>(() =>
            HorizonsResponseParser.Parse(payload, Expectation("Sun", "10", "UT", "2050-Jan-01 00:00:00.000")));
    }

    [Fact]
    public void Parse_WrongTarget_Throws()
    {
        var payload = Payload("NASA/JPL Horizons API", "1.2", ValidResult);

        Assert.Throws<InvalidDataException>(() =>
            HorizonsResponseParser.Parse(payload, Expectation("Moon", "301", "UT", "2050-Jan-01 00:00:00.000")));
    }

    [Fact]
    public void Parse_WrongTimeScaleOrRowTime_Throws()
    {
        var payload = Payload("NASA/JPL Horizons API", "1.2", ValidResult);

        Assert.Throws<InvalidDataException>(() =>
            HorizonsResponseParser.Parse(payload, Expectation("Sun", "10", "TT", "2050-Jan-01 00:01:33.000")));
    }

    [Theory]
    [InlineData("Atmos refraction: YES (REFRACTED)")]
    [InlineData("Observer-centered J2000 longitude and latitude")]
    [InlineData("target centers' astrometric position")]
    public void Parse_MissingAirlessApparentEclipticOfDateContract_Throws(string replacement)
    {
        var result = replacement.StartsWith("Atmos", StringComparison.Ordinal)
            ? ValidResult.Replace("Atmos refraction: NO (AIRLESS)", replacement, StringComparison.Ordinal)
            : replacement.Contains("J2000", StringComparison.Ordinal)
                ? ValidResult.Replace(
                    "Observer-centered IAU76/80 ecliptic-of-date longitude and latitude",
                    replacement,
                    StringComparison.Ordinal)
                : ValidResult.Replace("target centers' apparent position", replacement, StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => HorizonsResponseParser.Parse(
            Payload("NASA/JPL Horizons API", "1.2", result),
            Expectation("Sun", "10", "UT", "2050-Jan-01 00:00:00.000")));
    }

    [Theory]
    [InlineData("JPL#171", true)]
    [InlineData("JPL#170", false)]
    public void Parse_ChironRequiresExactResolvedIdentityAndSolution(string solution, bool valid)
    {
        var result = ValidResult
            .Replace("Sun (10)", "2060 Chiron (1977 UB)", StringComparison.Ordinal)
            .Replace("{source: DE441}", $"{{source: {solution}}}", StringComparison.Ordinal);
        var payload = Payload("NASA/JPL Horizons API", "1.2", result);
        var expectation = new HorizonsResponseExpectation(
            "Chiron",
            "2060",
            "Target body name: 2060 Chiron (1977 UB) {source: JPL#171}",
            "500@399",
            "OBSERVER",
            "31",
            "UT",
            "GREGORIAN",
            "NASA/JPL Horizons API",
            "1.2",
            ["2050-Jan-01 00:00:00.000"]);

        if (valid)
        {
            Assert.Single(HorizonsResponseParser.Parse(payload, expectation));
        }
        else
        {
            Assert.Throws<InvalidDataException>(() => HorizonsResponseParser.Parse(payload, expectation));
        }
    }

    private static HorizonsResponseExpectation Expectation(
        string body,
        string target,
        string timeType,
        string rowTime) => new(
            body,
            target,
            $"Target body name: {body} ({target}) {{source: DE441}}",
            "500@399",
            "OBSERVER",
            "31",
            timeType,
            "GREGORIAN",
            "NASA/JPL Horizons API",
            "1.2",
            [rowTime]);

    private static string Payload(string source, string version, string result) =>
        System.Text.Json.JsonSerializer.Serialize(new
        {
            signature = new { source, version },
            result
        });
}
