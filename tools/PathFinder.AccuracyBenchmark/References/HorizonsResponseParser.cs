using System.Globalization;
using System.Text.Json;

namespace PathFinder.AccuracyBenchmark.References;

public sealed record HorizonsResponseExpectation(
    string Body,
    string Target,
    string ExpectedTargetHeader,
    string Center,
    string EphemerisType,
    string Quantity,
    string TimeType,
    string CalendarType,
    string ApiSource,
    string ApiVersion,
    IReadOnlyList<string> RowTimes);

public sealed record HorizonsLongitudeRow(
    string Time,
    double JulianDate,
    double LongitudeDegrees,
    double LatitudeDegrees,
    string TargetHeader);

public static class HorizonsResponseParser
{
    public static IReadOnlyList<HorizonsLongitudeRow> Parse(
        string payload,
        HorizonsResponseExpectation expectation)
    {
        ValidateExpectation(expectation);
        using var document = ParseJson(payload);
        var root = document.RootElement;
        if (root.TryGetProperty("error", out var error) && error.ValueKind != JsonValueKind.Null)
        {
            throw new InvalidDataException($"Horizons returned an error payload: {error}");
        }

        var signature = RequiredObject(root, "signature");
        RequireEqual(RequiredString(signature, "source"), expectation.ApiSource, "API source");
        RequireEqual(RequiredString(signature, "version"), expectation.ApiVersion, "API version");
        var result = RequiredString(root, "result");
        if (result.Contains("Matching small-bodies", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Horizons target selection was ambiguous.");
        }

        var lines = result.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var targetHeader = RequiredLine(lines, "Target body name:");
        ValidateTarget(targetHeader, expectation);
        ValidateHeaders(lines, expectation);
        return ParseRows(lines, targetHeader, expectation.RowTimes);
    }

    private static void ValidateExpectation(HorizonsResponseExpectation expectation)
    {
        RequireEqual(expectation.Center, "500@399", "center contract");
        RequireEqual(expectation.EphemerisType, "OBSERVER", "ephemeris contract");
        RequireEqual(expectation.Quantity, "31", "quantity contract");
        RequireEqual(expectation.CalendarType, "GREGORIAN", "calendar contract");
        if (expectation.TimeType is not ("UT" or "TT"))
        {
            throw new InvalidDataException($"Unsupported Horizons time type: {expectation.TimeType}.");
        }
    }

    private static JsonDocument ParseJson(string payload)
    {
        try
        {
            return JsonDocument.Parse(payload);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Horizons response is not valid JSON.", exception);
        }
    }

    private static void ValidateTarget(string targetHeader, HorizonsResponseExpectation expectation)
    {
        if (!NormalizeWhitespace(targetHeader).Equals(
                NormalizeWhitespace(expectation.ExpectedTargetHeader),
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Horizons resolved target header does not match the frozen identity/solution. " +
                $"Expected '{expectation.ExpectedTargetHeader}', received '{NormalizeWhitespace(targetHeader)}'.");
        }

        if (!targetHeader.Contains(expectation.Body, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Horizons target body does not match {expectation.Body}: {targetHeader}");
        }

        var normalizedHeader = NormalizeWhitespace(targetHeader);
        if (expectation.Target.All(char.IsDigit) &&
            !normalizedHeader.Contains($"({expectation.Target})", StringComparison.Ordinal) &&
            !normalizedHeader.StartsWith($"Target body name: {expectation.Target} ", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Horizons target id does not match {expectation.Target}: {targetHeader}");
        }
    }

    private static string NormalizeWhitespace(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static void ValidateHeaders(string[] lines, HorizonsResponseExpectation expectation)
    {
        var centerHeader = RequiredLine(lines, "Center body name:");
        if (!centerHeader.Contains("Earth (399)", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Horizons center is not geocentric Earth: {centerHeader}");
        }

        if (!lines.Any(line => line.Contains("Calendar mode", StringComparison.Ordinal) &&
                               line.Contains("Gregorian", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidDataException("Horizons response does not confirm the Gregorian calendar.");
        }

        if (!lines.Any(line => line.Contains(
                "Atmos refraction: NO (AIRLESS)",
                StringComparison.Ordinal)))
        {
            throw new InvalidDataException("Horizons response does not confirm the airless apparent convention.");
        }

        var result = string.Join('\n', lines);
        if (!result.Contains(
                "Observer-centered IAU76/80 ecliptic-of-date longitude and latitude",
                StringComparison.Ordinal) ||
            !result.Contains("target centers' apparent position", StringComparison.Ordinal) ||
            !result.Contains("with light-time, gravitational deflection of", StringComparison.Ordinal) ||
            !result.Contains("light, and stellar aberrations.  Units: DEGREES", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Horizons response does not confirm apparent observer-centered ecliptic-of-date quantity 31.");
        }

        var tableHeader = lines.FirstOrDefault(line => line.Contains("ObsEcLon", StringComparison.Ordinal));
        if (tableHeader is null || !tableHeader.Contains($"Date__({expectation.TimeType})", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Horizons response does not contain quantity 31 on the {expectation.TimeType} time scale.");
        }
    }

    private static IReadOnlyList<HorizonsLongitudeRow> ParseRows(
        string[] lines,
        string targetHeader,
        IReadOnlyList<string> expectedTimes)
    {
        var start = Array.FindIndex(lines, line => line.Trim().Equals("$$SOE", StringComparison.Ordinal));
        var end = Array.FindIndex(lines, line => line.Trim().Equals("$$EOE", StringComparison.Ordinal));
        if (start < 0 || end <= start)
        {
            throw new InvalidDataException("Horizons response has no complete ephemeris data block.");
        }

        var rows = new List<HorizonsLongitudeRow>();
        foreach (var line in lines[(start + 1)..end].Where(line => !string.IsNullOrWhiteSpace(line)))
        {
            var fields = line.Split(',');
            if (fields.Length < 6)
            {
                throw new InvalidDataException($"Malformed Horizons CSV row: {line}");
            }

            rows.Add(new HorizonsLongitudeRow(
                fields[0].Trim(),
                ParseFinite(fields[1], "Julian date"),
                ParseFinite(fields[4], "ecliptic longitude"),
                ParseFinite(fields[5], "ecliptic latitude"),
                targetHeader.Trim()));
        }

        if (!rows.Select(row => row.Time).SequenceEqual(expectedTimes, StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                $"Horizons row times do not match the manifest. Expected [{string.Join(", ", expectedTimes)}], " +
                $"received [{string.Join(", ", rows.Select(row => row.Time))}].");
        }

        return rows;
    }

    private static double ParseFinite(string text, string field)
    {
        if (!double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ||
            !double.IsFinite(value))
        {
            throw new InvalidDataException($"Horizons {field} is not a finite invariant number: {text}");
        }

        return value;
    }

    private static JsonElement RequiredObject(JsonElement parent, string property) =>
        parent.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Object
            ? value
            : throw new InvalidDataException($"Horizons payload is missing object '{property}'.");

    private static string RequiredString(JsonElement parent, string property) =>
        parent.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()!
            : throw new InvalidDataException($"Horizons payload is missing string '{property}'.");

    private static string RequiredLine(IEnumerable<string> lines, string prefix) =>
        lines.FirstOrDefault(line => line.TrimStart().StartsWith(prefix, StringComparison.Ordinal)) ??
        throw new InvalidDataException($"Horizons response is missing '{prefix}' metadata.");

    private static void RequireEqual(string actual, string expected, string field)
    {
        if (!actual.Equals(expected, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Horizons {field} mismatch: expected '{expected}', received '{actual}'.");
        }
    }
}
