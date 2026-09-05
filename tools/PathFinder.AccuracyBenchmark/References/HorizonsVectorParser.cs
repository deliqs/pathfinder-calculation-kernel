using System.Globalization;
using System.Text.Json;

namespace PathFinder.AccuracyBenchmark.References;

public sealed record HorizonsVectorExpectation(
    string ExpectedTargetHeader,
    string Center,
    string ReferenceFrame,
    string Units,
    string TimeType,
    double JulianDateTdb,
    string ApiSource,
    string ApiVersion);

public sealed record HorizonsVectorRow(
    double JulianDateTdb,
    double X,
    double Y,
    double Z,
    double Vx,
    double Vy,
    double Vz);

public static class HorizonsVectorParser
{
    public static HorizonsVectorRow Parse(string payload, HorizonsVectorExpectation expectation)
    {
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
        RequireNormalizedLine(lines, "Target body name:", expectation.ExpectedTargetHeader);
        RequireContainingLine(lines, "Center body name:", expectation.Center);
        RequireContainingLine(lines, "Output units", expectation.Units);
        RequireContainingLine(lines, "Calendar mode", "Gregorian");
        RequireContainingLine(lines, "Output type", "GEOMETRIC cartesian states");
        RequireContainingLine(lines, "Output format", "2 (position and velocity)");
        RequireContainingLine(lines, "Reference frame", expectation.ReferenceFrame);

        var tableHeader = RequiredLine(lines, "JDTDB");
        if (!tableHeader.Contains($"Calendar Date ({expectation.TimeType})", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Horizons vector table is not on {expectation.TimeType}.");
        }

        var row = ParseOnlyRow(lines);
        if (row.JulianDateTdb != expectation.JulianDateTdb)
        {
            throw new InvalidDataException(
                $"Horizons vector epoch mismatch: expected {expectation.JulianDateTdb:R}, " +
                $"received {row.JulianDateTdb:R}.");
        }

        return row;
    }

    private static HorizonsVectorRow ParseOnlyRow(string[] lines)
    {
        var start = Array.FindIndex(lines, line => line.Trim().Equals("$$SOE", StringComparison.Ordinal));
        var end = Array.FindIndex(lines, line => line.Trim().Equals("$$EOE", StringComparison.Ordinal));
        if (start < 0 || end <= start)
        {
            throw new InvalidDataException("Horizons response has no complete vector data block.");
        }

        var dataRows = lines[(start + 1)..end].Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
        if (dataRows.Length != 1)
        {
            throw new InvalidDataException($"Horizons seed response must contain exactly one row, found {dataRows.Length}.");
        }

        var fields = dataRows[0].Split(',');
        if (fields.Length < 8)
        {
            throw new InvalidDataException($"Malformed Horizons vector CSV row: {dataRows[0]}");
        }

        return new HorizonsVectorRow(
            ParseFinite(fields[0], "Julian date TDB"),
            ParseFinite(fields[2], "X"),
            ParseFinite(fields[3], "Y"),
            ParseFinite(fields[4], "Z"),
            ParseFinite(fields[5], "VX"),
            ParseFinite(fields[6], "VY"),
            ParseFinite(fields[7], "VZ"));
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

    private static void RequireNormalizedLine(string[] lines, string prefix, string expected)
    {
        var actual = NormalizeWhitespace(RequiredLine(lines, prefix));
        if (!actual.Equals(NormalizeWhitespace(expected), StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Horizons resolved target header mismatch: expected '{expected}', received '{actual}'.");
        }
    }

    private static void RequireContainingLine(string[] lines, string prefix, string expected)
    {
        var line = RequiredLine(lines, prefix);
        if (!line.Contains(expected, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Horizons '{prefix}' metadata does not contain '{expected}': {line}");
        }
    }

    private static string RequiredLine(IEnumerable<string> lines, string prefix) =>
        lines.FirstOrDefault(line => line.TrimStart().StartsWith(prefix, StringComparison.Ordinal)) ??
        throw new InvalidDataException($"Horizons response is missing '{prefix}' metadata.");

    private static string NormalizeWhitespace(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

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

    private static void RequireEqual(string actual, string expected, string field)
    {
        if (!actual.Equals(expected, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Horizons {field} mismatch: expected '{expected}', received '{actual}'.");
        }
    }
}
