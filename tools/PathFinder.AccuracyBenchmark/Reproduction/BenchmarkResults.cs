namespace PathFinder.AccuracyBenchmark.Reproduction;

public sealed record BenchmarkResults(
    int SchemaVersion,
    string DatasetRevision,
    BenchmarkProvenance Provenance,
    BenchmarkSummary Summary,
    IReadOnlyList<PositionResult> Positions,
    IReadOnlyList<HouseCuspResult> HouseCusps,
    IReadOnlyList<TimingResult> Timings,
    IReadOnlyList<HistoricalTimeResult> HistoricalTimes);

public sealed record BenchmarkProvenance(
    string CalculationPackage,
    string CalculationRevision,
    string CalculationSourceManifestSha256,
    string ReferenceManifestSha256,
    string AstronomyEngineVersion,
    string NodaTimeVersion,
    string TzdbVersion,
    string JplApiSource,
    string JplApiVersion,
    string SwissEphemerisVersion,
    string SwissSourceCommit,
    string SwissExecutableSha256,
    string HorizonsUtSemantics,
    string Horizons1950InputSemantics,
    string SwissUtSemantics,
    double Dut1AssumptionSeconds);

public sealed record BenchmarkSummary(
    int PositionRows,
    int PositionPassed,
    double MedianNominalUtcPositionErrorArcsec,
    double MaximumNominalUtcPositionErrorArcsec,
    double MedianMatchedTtPositionErrorArcsec,
    double MaximumMatchedTtPositionErrorArcsec,
    int HouseCuspRows,
    int HouseCuspsPassed,
    double MaximumHouseCuspErrorArcsec,
    int TimingRows,
    int TimingsPassed,
    double MaximumTimingErrorMinutes,
    int HistoricalTimeRows,
    int HistoricalTimesExecuted);

public sealed record PositionResult(
    string CaseId,
    string Body,
    string Utc,
    double PathfinderLongitudeDeg,
    double JplNominalUtcLongitudeDeg,
    double JplMatchedTtLongitudeDeg,
    double NominalUtcErrorArcsec,
    double MatchedTtErrorArcsec,
    double PathfinderDeltaTSeconds,
    string HorizonsNominalTimeType,
    string HorizonsNominalTimeValue,
    string HorizonsMatchedTimeType,
    string HorizonsMatchedTimeValue,
    double ToleranceArcsec,
    bool Passed);

public sealed record HouseCuspResult(
    string CaseId,
    string Utc,
    double Latitude,
    double EastPositiveLongitude,
    string RequestedSystem,
    string ReferenceSystem,
    int Cusp,
    double PathfinderLongitudeDeg,
    double SwissLongitudeDeg,
    double AbsoluteErrorArcsec,
    double ToleranceArcsec,
    bool Passed);

public sealed record TimingResult(
    string CaseId,
    string Kind,
    string Body,
    string Motion,
    double? TargetLongitudeDeg,
    string? Extremum,
    string SearchStartUtc,
    int SearchWindowDays,
    string PathfinderMethod,
    string ReferenceMethod,
    string ReferenceUtc,
    string PathfinderUtc,
    double AbsoluteErrorMinutes,
    double ToleranceMinutes,
    bool Passed);

public sealed record HistoricalTimeResult(
    string CaseId,
    string RequestedLocal,
    string ZoneId,
    string ResolutionMethod,
    bool CompatibilityCase,
    string ResolvedLocal,
    string UtcInstant,
    string SelectedOffset,
    IReadOnlyList<string> WarningCodes,
    string TzdbVersion,
    bool Executed);
