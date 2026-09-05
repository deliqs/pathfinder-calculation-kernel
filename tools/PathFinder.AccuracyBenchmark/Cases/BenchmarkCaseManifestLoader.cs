using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using NodaTime.Text;

namespace PathFinder.AccuracyBenchmark.Cases;

public static class BenchmarkCaseManifestLoader
{
    private static readonly HashSet<string> Bodies =
    [
        "Sun", "Moon", "Mercury", "Venus", "Mars", "Jupiter", "Saturn", "Uranus",
        "Neptune", "Pluto", "Chiron"
    ];

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static BenchmarkCaseManifest Load(ReadOnlySpan<byte> bytes)
    {
        RawManifest raw;
        try
        {
            raw = JsonSerializer.Deserialize<RawManifest>(bytes, Options) ??
                throw new InvalidDataException("Case manifest is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Case manifest does not match its closed schema.", exception);
        }

        Require(raw.SchemaVersion == 2, "schemaVersion must be 2");
        RequireText(raw.DatasetRevision, "datasetRevision");
        Require(raw.Positions is not null, "positions are required");
        var positions = ExpandPositions(raw.Positions!);
        var houses = raw.Houses ?? throw new InvalidDataException("houses are required");
        var timings = raw.Timings ?? throw new InvalidDataException("timings are required");
        var historical = raw.HistoricalTimes ?? throw new InvalidDataException("historicalTimes are required");
        ValidateHouses(houses);
        ValidateTimings(timings);
        ValidateHistorical(historical);
        EnsureUniqueIds(positions.Select(row => row.Id)
            .Concat(houses.Select(row => row.Id))
            .Concat(timings.Select(row => row.Id))
            .Concat(historical.Select(row => row.Id)));
        return new BenchmarkCaseManifest(
            raw.SchemaVersion,
            raw.DatasetRevision!,
            positions,
            houses,
            timings,
            historical);
    }

    private static IReadOnlyList<PositionCase> ExpandPositions(RawPositionSet positions)
    {
        Require(positions.Bodies is not null, "position bodies are required");
        Require(positions.Epochs is not null, "position epochs are required");
        var result = new List<PositionCase>();
        foreach (var body in positions.Bodies!)
        {
            Require(Bodies.Contains(body.Body ?? string.Empty), $"unsupported position body '{body.Body}'");
            RequireText(body.HorizonsCommand, $"{body.Body}.horizonsCommand");
            RequireText(body.HorizonsTargetId, $"{body.Body}.horizonsTargetId");
            RequireText(body.HorizonsTargetHeader, $"{body.Body}.horizonsTargetHeader");
            RequirePositive(body.ToleranceArcsec, $"{body.Body}.toleranceArcsec");
            foreach (var epoch in positions.Epochs!)
            {
                RequireText(epoch.Id, "position epoch id");
                RequireInstant(epoch.Utc, $"{epoch.Id}.utc");
                RequireText(epoch.HorizonsUt, $"{epoch.Id}.horizonsUt");
                var parsedUtc = InstantPattern.ExtendedIso.Parse(epoch.Utc!);
                var expectedHorizonsUt = parsedUtc.Value.ToDateTimeUtc().ToString(
                    "yyyy-MMM-dd HH:mm:ss.fff",
                    CultureInfo.InvariantCulture);
                Require(epoch.HorizonsUt == expectedHorizonsUt,
                    $"{epoch.Id}.horizonsUt must exactly derive from utc as {expectedHorizonsUt}");
                result.Add(new PositionCase(
                    $"{body.Body!.ToLowerInvariant()}-{epoch.Id}",
                    body.Body,
                    body.HorizonsCommand!,
                    body.HorizonsTargetId!,
                    body.HorizonsTargetHeader!,
                    epoch.Utc!,
                    epoch.HorizonsUt!,
                    body.ToleranceArcsec));
            }
        }

        return result;
    }

    private static void ValidateHouses(IReadOnlyList<HouseCase> houses)
    {
        foreach (var row in houses)
        {
            RequireText(row.Id, "house id");
            RequireInstant(row.Utc, $"{row.Id}.utc");
            RequireFinite(row.Latitude, $"{row.Id}.latitude");
            Require(row.Latitude is >= -90 and <= 90, $"{row.Id}.latitude is outside [-90,90]");
            RequireFinite(row.EastPositiveLongitude, $"{row.Id}.eastPositiveLongitude");
            Require(row.EastPositiveLongitude is >= -180 and <= 180,
                $"{row.Id}.eastPositiveLongitude is outside [-180,180]");
            ValidateHouseSystems(row);
            RequirePositive(row.ToleranceArcsec, $"{row.Id}.toleranceArcsec");
        }
    }

    private static void ValidateHouseSystems(HouseCase row)
    {
        Require(row.RequestedSystem is "Placidus" or "Koch" or "Regiomontanus" or "Campanus",
            $"{row.Id}.requestedSystem must be Placidus, Koch, Regiomontanus, or Campanus");
        Require(
            row.ReferenceSystem is "Placidus" or "Koch" or "Regiomontanus" or "Campanus" or "Porphyry",
            $"{row.Id}.referenceSystem must be Placidus, Koch, Regiomontanus, Campanus, or Porphyry");
        Require(row.SwissHouseSystemCode is "P" or "K" or "R" or "C" or "O",
            $"{row.Id}.swissHouseSystemCode must be P, K, R, C, or O");
        if (row.ReferenceSystem == "Porphyry")
        {
            Require(row.SwissHouseSystemCode == "O",
                $"{row.Id}: a Porphyry reference must use code O");
            return;
        }

        Require(row.SwissHouseSystemCode == OwnHouseSystemCode(row.ReferenceSystem),
            $"{row.Id}: a non-Porphyry reference must use its own code");
    }

    private static string OwnHouseSystemCode(string system) => system switch
    {
        "Placidus" => "P",
        "Koch" => "K",
        "Regiomontanus" => "R",
        "Campanus" => "C",
        _ => throw new InvalidDataException($"unsupported house system '{system}'")
    };

    private static void ValidateTimings(IReadOnlyList<TimingCase> timings)
    {
        foreach (var row in timings)
        {
            RequireText(row.Id, "timing id");
            Require(row.Kind is "longitude-crossing" or "station", $"{row.Id}.kind is invalid");
            Require(Bodies.Contains(row.Body ?? string.Empty), $"{row.Id}.body is invalid");
            Require(row.Motion is "direct" or "retrograde", $"{row.Id}.motion is required and must be valid");
            RequireInstant(row.SearchStartUtc, $"{row.Id}.searchStartUtc");
            Require(row.SearchWindowDays > 0, $"{row.Id}.searchWindowDays must be positive");
            RequireText(row.Method, $"{row.Id}.method");
            RequirePositive(row.ToleranceMinutes, $"{row.Id}.toleranceMinutes");
            if (row.Kind == "longitude-crossing")
            {
                Require(row.Method == "kernel-longitude-crossing",
                    $"{row.Id}.method must be kernel-longitude-crossing");
                Require(row.TargetLongitudeDeg is >= 0 and < 360,
                    $"{row.Id}.targetLongitudeDeg is required in [0,360)");
                Require(row.Extremum is null, $"{row.Id}.extremum is only valid for stations");
            }
            else
            {
                Require(row.Method == "kernel-station-parabolic-10-minute",
                    $"{row.Id}.method must be kernel-station-parabolic-10-minute");
                Require(row.TargetLongitudeDeg is null, $"{row.Id}.targetLongitudeDeg is only valid for crossings");
                Require(row.Extremum is "minimum" or "maximum", $"{row.Id}.extremum is required");
                Require(row.Extremum == (row.Motion == "direct" ? "minimum" : "maximum"),
                    $"{row.Id}.extremum does not enter its requested post-station motion");
            }
        }
    }

    private static void ValidateHistorical(IReadOnlyList<HistoricalTimeCase> rows)
    {
        foreach (var row in rows)
        {
            RequireText(row.Id, "historical id");
            Require(LocalDateTimePattern.ExtendedIso.Parse(row.RequestedLocal ?? string.Empty).Success,
                $"{row.Id}.requestedLocal must be an ISO local date-time");
            RequireText(row.ZoneId, $"{row.Id}.zoneId");
            Require(row.ResolutionMethod == "nodatime-in-zone-leniently", $"{row.Id}.resolutionMethod is invalid");
            Require(row.CompatibilityCase, $"{row.Id} must be labeled as a compatibility case");
        }
    }

    private static void EnsureUniqueIds(IEnumerable<string> ids)
    {
        var duplicates = ids.GroupBy(id => id, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        Require(duplicates.Length == 0, $"case ids must be unique: {string.Join(", ", duplicates)}");
    }

    private static void RequireInstant(string? value, string field) =>
        Require(InstantPattern.ExtendedIso.Parse(value ?? string.Empty).Success, $"{field} must be an ISO UTC instant");

    private static void RequireText(string? value, string field) =>
        Require(!string.IsNullOrWhiteSpace(value), $"{field} is required");

    private static void RequirePositive(double value, string field)
    {
        RequireFinite(value, field);
        Require(value > 0, $"{field} must be positive");
    }

    private static void RequireFinite(double value, string field) =>
        Require(double.IsFinite(value), $"{field} must be finite");

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidDataException(message);
        }
    }

    private sealed record RawManifest(
        int SchemaVersion,
        string? DatasetRevision,
        RawPositionSet? Positions,
        IReadOnlyList<HouseCase>? Houses,
        IReadOnlyList<TimingCase>? Timings,
        IReadOnlyList<HistoricalTimeCase>? HistoricalTimes);

    private sealed record RawPositionSet(
        IReadOnlyList<RawPositionBody>? Bodies,
        IReadOnlyList<RawPositionEpoch>? Epochs);

    private sealed record RawPositionBody(
        string? Body,
        string? HorizonsCommand,
        string? HorizonsTargetId,
        string? HorizonsTargetHeader,
        double ToleranceArcsec);

    private sealed record RawPositionEpoch(string? Id, string? Utc, string? HorizonsUt);
}
