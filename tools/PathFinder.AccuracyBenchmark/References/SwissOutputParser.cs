using System.Globalization;
using System.Text.RegularExpressions;

namespace PathFinder.AccuracyBenchmark.References;

public sealed record SwissOutputExpectation(
    string CaseId,
    string HouseSystemCode,
    double UtJulianDate,
    double EastPositiveLongitude,
    double Latitude);

public sealed record SwissHouseRow(string CaseId, int Cusp, double LongitudeDegrees);

public static partial class SwissOutputParser
{
    public static IReadOnlyList<SwissHouseRow> Parse(
        string exactStandardOutput,
        SwissOutputExpectation expectation)
    {
        ValidateExpectation(expectation);
        var rows = new List<SwissHouseRow>();
        foreach (var line in exactStandardOutput.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var match = HouseLine().Match(line);
            if (!match.Success)
            {
                continue;
            }

            var cusp = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            var longitude = double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            if (!double.IsFinite(longitude) || longitude is < 0 or >= 360)
            {
                throw new InvalidDataException($"Swiss cusp {cusp} is outside [0, 360): {longitude}.");
            }

            rows.Add(new SwissHouseRow(expectation.CaseId, cusp, longitude));
        }

        if (rows.Count != 12 || !rows.Select(row => row.Cusp).SequenceEqual(Enumerable.Range(1, 12)))
        {
            throw new InvalidDataException("Swiss output must contain exactly one ordered row for each cusp 1-12.");
        }

        return rows;
    }

    public static string ParseVersion(string exactVersionOutput, string expectedVersion)
    {
        var matches = VersionLine().Matches(exactVersionOutput);
        if (matches.Count != 1 ||
            !matches[0].Groups[1].Value.Equals(expectedVersion, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Swiss version output must identify required version {expectedVersion} exactly once.");
        }

        return matches[0].Groups[1].Value;
    }

    private static void ValidateExpectation(SwissOutputExpectation expectation)
    {
        if (expectation.HouseSystemCode is not ("P" or "K" or "R" or "C" or "O"))
        {
            throw new InvalidDataException($"Unsupported Swiss house-system code: {expectation.HouseSystemCode}.");
        }

        if (!double.IsFinite(expectation.UtJulianDate) ||
            !double.IsFinite(expectation.EastPositiveLongitude) ||
            !double.IsFinite(expectation.Latitude))
        {
            throw new InvalidDataException("Swiss invocation coordinates and Julian date must be finite.");
        }
    }

    [GeneratedRegex(@"^\s*house\s+(\d{1,2})\s+([+-]?\d+(?:\.\d+)?)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex HouseLine();

    [GeneratedRegex(@"(?m)^\s*Version:\s*(\d+\.\d+\.\d+)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex VersionLine();
}
