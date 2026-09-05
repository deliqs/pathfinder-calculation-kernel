namespace PathFinder.AccuracyBenchmark.Reproduction;

public sealed record NormalizedReferences(
    int SchemaVersion,
    string DatasetRevision,
    IReadOnlyList<NormalizedPositionReference> Positions,
    IReadOnlyList<NormalizedHouseReference> HouseCusps,
    IReadOnlyList<NormalizedTimingReference> Timings,
    NormalizedChironSeed ChironSeed);

public sealed record NormalizedPositionReference(
    string CaseId,
    string Body,
    string NominalTimeType,
    string NominalTimeValue,
    double NominalLongitudeDeg,
    string MatchedTimeType,
    string MatchedTimeValue,
    double MatchedLongitudeDeg,
    string TargetHeader);

public sealed record NormalizedHouseReference(
    string CaseId,
    string ReferenceSystem,
    int Cusp,
    double LongitudeDeg);

public sealed record NormalizedTimingReference(
    string CaseId,
    string ReferenceMethod,
    string ReferenceUtc);

public sealed record NormalizedChironSeed(
    string ReferenceId,
    string TargetHeader,
    string CenterHeader,
    string ReferenceFrame,
    string Units,
    string TimeScale,
    double JulianDate,
    double X,
    double Y,
    double Z,
    double Vx,
    double Vy,
    double Vz);

public sealed record CalculationSourceManifest(
    int SchemaVersion,
    string CalculationPackage,
    string CalculationRevision,
    IReadOnlyList<CalculationSourceFile> Files,
    IReadOnlyList<CalculationSourceProperty> Properties);

public sealed record CalculationSourceFile(string Path, string Sha256);

public sealed record CalculationSourceProperty(string Name, string Value);
