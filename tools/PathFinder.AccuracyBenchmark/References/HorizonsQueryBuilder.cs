using System.Globalization;
using NodaTime;
using NodaTime.Text;
using PathFinder.AccuracyBenchmark.Cases;
using PathFinder.CalculationKernel.Ephemeris;

namespace PathFinder.AccuracyBenchmark.References;

public sealed record HorizonsQuery(
    string Id,
    string Body,
    string ExpectedTargetHeader,
    IReadOnlyDictionary<string, string> Parameters,
    IReadOnlyList<string> ExpectedRowTimes,
    Uri RequestUri);

public sealed record HorizonsTimingQuery(
    string CaseId,
    string Body,
    string ExpectedTargetHeader,
    string Motion,
    double? TargetLongitudeDeg,
    string? Extremum,
    string SearchStartUtc,
    int SearchWindowDays,
    string Method,
    IReadOnlyDictionary<string, string> Parameters,
    IReadOnlyList<string> ExpectedRowTimes,
    Uri RequestUri);

public static class HorizonsQueryBuilder
{
    public const string Endpoint = "https://ssd.jpl.nasa.gov/api/horizons.api";
    public const string ApiSource = "NASA/JPL Horizons API";
    public const string ApiVersion = "1.2";
    public const string ChironSeedReferenceId = "jpl-horizons-chiron-seed-jpl171-j2000";

    public static IReadOnlyList<HorizonsQuery> Build(BenchmarkCaseManifest cases)
    {
        var queries = new List<HorizonsQuery>();
        foreach (var bodyGroup in cases.Positions.GroupBy(row => row.Body, StringComparer.Ordinal))
        {
            var rows = bodyGroup.ToArray();
            queries.Add(BuildPositionQuery(rows, "UT"));
            queries.Add(BuildPositionQuery(rows, "TT"));
        }

        queries.Add(BuildChironSeedQuery(cases));
        return queries;
    }

    public static IReadOnlyList<HorizonsTimingQuery> BuildTimings(BenchmarkCaseManifest cases)
    {
        var bodies = cases.Positions
            .GroupBy(row => row.Body, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var queries = new List<HorizonsTimingQuery>();
        foreach (var row in cases.Timings)
        {
            if (!bodies.TryGetValue(row.Body, out var body))
            {
                throw new InvalidDataException($"Timing body has no Horizons identity: {row.Body}");
            }

            var parsed = InstantPattern.ExtendedIso.Parse(row.SearchStartUtc);
            if (!parsed.Success)
            {
                throw new InvalidDataException($"Timing start is not parseable: {row.SearchStartUtc}");
            }

            var stop = parsed.Value + Duration.FromDays(row.SearchWindowDays);
            var stepMinutes = row.Kind == "station" ? 10 : 60;
            var parameters = Common(body.HorizonsCommand);
            parameters.Add("ANG_FORMAT", "'DEG'");
            parameters.Add("APPARENT", "'AIRLESS'");
            parameters.Add("CAL_FORMAT", "'BOTH'");
            parameters.Add("CAL_TYPE", "'GREGORIAN'");
            parameters.Add("CENTER", "'500@399'");
            parameters.Add("CSV_FORMAT", "'YES'");
            parameters.Add("EPHEM_TYPE", "'OBSERVER'");
            parameters.Add("EXTRA_PREC", "'YES'");
            parameters.Add("QUANTITIES", "'31'");
            parameters.Add("REF_SYSTEM", "'ICRF'");
            parameters.Add("START_TIME", $"'{FormatRangeTime(parsed.Value)}'");
            parameters.Add("STEP_SIZE", stepMinutes == 10 ? "'10 m'" : "'1 h'");
            parameters.Add("STOP_TIME", $"'{FormatRangeTime(stop)}'");
            parameters.Add("TIME_DIGITS", "'FRACSEC'");
            parameters.Add("TIME_TYPE", "'UT'");
            queries.Add(new HorizonsTimingQuery(
                row.Id,
                row.Body,
                body.HorizonsTargetHeader,
                row.Motion,
                row.TargetLongitudeDeg,
                row.Extremum,
                row.SearchStartUtc,
                row.SearchWindowDays,
                row.Method,
                parameters,
                GenerateExpectedTimes(parsed.Value, stop, stepMinutes),
                CreateUri(parameters)));
        }

        return queries;
    }

    private static HorizonsQuery BuildPositionQuery(IReadOnlyList<PositionCase> rows, string timeType)
    {
        var first = rows[0];
        var times = timeType == "UT"
            ? rows.Select(row => row.HorizonsUt).ToArray()
            : rows.Select(row => FormatMatchedTt(row.Utc)).ToArray();
        var parameters = Common(first.HorizonsCommand);
        parameters.Add("ANG_FORMAT", "'DEG'");
        parameters.Add("APPARENT", "'AIRLESS'");
        parameters.Add("CAL_FORMAT", "'BOTH'");
        parameters.Add("CAL_TYPE", "'GREGORIAN'");
        parameters.Add("CENTER", "'500@399'");
        parameters.Add("CSV_FORMAT", "'YES'");
        parameters.Add("EPHEM_TYPE", "'OBSERVER'");
        parameters.Add("EXTRA_PREC", "'YES'");
        parameters.Add("QUANTITIES", "'31'");
        parameters.Add("REF_SYSTEM", "'ICRF'");
        parameters.Add("TIME_DIGITS", "'FRACSEC'");
        parameters.Add("TIME_TYPE", $"'{timeType}'");
        parameters.Add("TLIST", string.Join(' ', times.Select(value => $"'{value}'")));
        parameters.Add("TLIST_TYPE", "'CAL'");
        return Create(
            $"{first.Body.ToLowerInvariant()}-positions-{timeType.ToLowerInvariant()}",
            first.Body,
            first.HorizonsTargetHeader,
            parameters,
            times);
    }

    private static HorizonsQuery BuildChironSeedQuery(BenchmarkCaseManifest cases)
    {
        var chiron = cases.Positions.First(row => row.Body == "Chiron");
        var parameters = Common(chiron.HorizonsCommand);
        parameters.Add("CAL_TYPE", "'GREGORIAN'");
        parameters.Add("CENTER", "'500@10'");
        parameters.Add("CSV_FORMAT", "'YES'");
        parameters.Add("EPHEM_TYPE", "'VECTORS'");
        parameters.Add("OUT_UNITS", "'AU-D'");
        parameters.Add("REF_PLANE", "'FRAME'");
        parameters.Add("REF_SYSTEM", "'ICRF'");
        parameters.Add("TIME_DIGITS", "'FRACSEC'");
        parameters.Add("TIME_TYPE", "'TDB'");
        parameters.Add("TLIST", "'2451545.0'");
        parameters.Add("TLIST_TYPE", "'JD'");
        parameters.Add("VEC_CORR", "'NONE'");
        parameters.Add("VEC_TABLE", "'2'");
        return Create(
            ChironSeedReferenceId,
            "Chiron",
            chiron.HorizonsTargetHeader,
            parameters,
            ["2451545.000000000"]);
    }

    private static SortedDictionary<string, string> Common(string command) =>
        new(StringComparer.Ordinal)
        {
            ["COMMAND"] = $"'{command}'",
            ["MAKE_EPHEM"] = "'YES'",
            ["OBJ_DATA"] = "'YES'",
            ["format"] = "json"
        };

    private static HorizonsQuery Create(
        string id,
        string body,
        string targetHeader,
        SortedDictionary<string, string> parameters,
        IReadOnlyList<string> times)
    {
        return new HorizonsQuery(id, body, targetHeader, parameters, times, CreateUri(parameters));
    }

    private static Uri CreateUri(IReadOnlyDictionary<string, string> parameters)
    {
        var query = string.Join('&', parameters.Select(pair =>
            $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
        return new Uri($"{Endpoint}?{query}");
    }

    private static string FormatRangeTime(Instant value) =>
        value.ToDateTimeUtc().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    private static IReadOnlyList<string> GenerateExpectedTimes(Instant start, Instant stop, int stepMinutes)
    {
        var result = new List<string>();
        for (var current = start; current <= stop; current += Duration.FromMinutes(stepMinutes))
        {
            result.Add(current.ToDateTimeUtc().ToString(
                "yyyy-MMM-dd HH:mm:ss.fff",
                CultureInfo.InvariantCulture));
        }

        return result;
    }

    private static string FormatMatchedTt(string utcText)
    {
        var parsed = InstantPattern.ExtendedIso.Parse(utcText);
        if (!parsed.Success)
        {
            throw new InvalidDataException($"Position UTC is not parseable: {utcText}");
        }

        var metadata = AstronomyEngineTime.GetMetadata(parsed.Value);
        var matched = parsed.Value + Duration.FromSeconds(metadata.DeltaTSeconds);
        return matched.ToDateTimeUtc().ToString("yyyy-MMM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
    }
}
