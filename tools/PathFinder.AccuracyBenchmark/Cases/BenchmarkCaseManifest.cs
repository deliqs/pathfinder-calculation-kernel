namespace PathFinder.AccuracyBenchmark.Cases;

public sealed record BenchmarkCaseManifest(
    int SchemaVersion,
    string DatasetRevision,
    IReadOnlyList<PositionCase> Positions,
    IReadOnlyList<HouseCase> Houses,
    IReadOnlyList<TimingCase> Timings,
    IReadOnlyList<HistoricalTimeCase> HistoricalTimes);

public sealed record PositionCase(
    string Id,
    string Body,
    string HorizonsCommand,
    string HorizonsTargetId,
    string HorizonsTargetHeader,
    string Utc,
    string HorizonsUt,
    double ToleranceArcsec)
{
    public string HorizonsTarget => HorizonsCommand;
}

public sealed record HouseCase(
    string Id,
    string Utc,
    double Latitude,
    double EastPositiveLongitude,
    string RequestedSystem,
    string ReferenceSystem,
    string SwissHouseSystemCode,
    double ToleranceArcsec);

public sealed record TimingCase(
    string Id,
    string Kind,
    string Body,
    string Motion,
    double? TargetLongitudeDeg,
    string? Extremum,
    string SearchStartUtc,
    int SearchWindowDays,
    string Method,
    double ToleranceMinutes);

public sealed record HistoricalTimeCase(
    string Id,
    string RequestedLocal,
    string ZoneId,
    string ResolutionMethod,
    bool CompatibilityCase);
