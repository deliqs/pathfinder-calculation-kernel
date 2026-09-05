using System.Text;
using PathFinder.AccuracyBenchmark.Cases;
using PathFinder.AccuracyBenchmark.Serialization;

namespace PathFinder.AccuracyBenchmark.References;

public static class RefreshEvidenceWriter
{
    public static Task WriteJplAsync(
        string output,
        string benchmarkRoot,
        BenchmarkCaseManifest cases,
        JplReferenceSource candidate,
        CancellationToken cancellationToken)
    {
        var frozen = LoadFrozen(benchmarkRoot, cases);
        var normalized = NormalizeJpl(
            candidate,
            artifact => File.ReadAllBytes(Path.Combine(output, artifact.Path)),
            cases);
        var baseline = NormalizeJpl(
            frozen.Manifest.Jpl,
            artifact => frozen.Read(artifact),
            cases);
        return WriteAsync(output, "jpl", cases.DatasetRevision, normalized, baseline,
            normalized.Requests.ToDictionary(row => row.Id),
            baseline.Requests.ToDictionary(row => row.Id), cancellationToken);
    }

    public static Task WriteSwissAsync(
        string output,
        string benchmarkRoot,
        BenchmarkCaseManifest cases,
        SwissRefreshCandidate candidate,
        CancellationToken cancellationToken)
    {
        var frozen = LoadFrozen(benchmarkRoot, cases);
        var normalized = NormalizeSwiss(
            candidate.Requests,
            artifact => File.ReadAllBytes(Path.Combine(output, artifact.Path)),
            cases);
        var baseline = NormalizeSwiss(
            frozen.Manifest.Swiss.Requests,
            artifact => frozen.Read(artifact),
            cases);
        return WriteAsync(output, "swiss", cases.DatasetRevision, normalized, baseline,
            normalized.Cases.ToDictionary(row => row.CaseId),
            baseline.Cases.ToDictionary(row => row.CaseId), cancellationToken);
    }

    private static async Task WriteAsync<TDocument, TRow>(
        string output,
        string provider,
        string datasetRevision,
        TDocument candidate,
        TDocument baseline,
        IReadOnlyDictionary<string, TRow> candidateRows,
        IReadOnlyDictionary<string, TRow> baselineRows,
        CancellationToken cancellationToken)
    {
        var candidateBytes = CanonicalJson.Serialize(candidate);
        var baselineBytes = CanonicalJson.Serialize(baseline);
        var changedIds = candidateRows.Keys.Union(baselineRows.Keys, StringComparer.Ordinal)
            .Where(id => !candidateRows.TryGetValue(id, out var candidateRow) ||
                         !baselineRows.TryGetValue(id, out var baselineRow) ||
                         CanonicalJson.Sha256(CanonicalJson.Serialize(candidateRow)) !=
                         CanonicalJson.Sha256(CanonicalJson.Serialize(baselineRow)))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        var report = new RefreshDriftReport(
            1,
            provider,
            datasetRevision,
            CanonicalJson.Sha256(baselineBytes),
            CanonicalJson.Sha256(candidateBytes),
            changedIds.Length > 0,
            changedIds);
        await File.WriteAllBytesAsync(
            Path.Combine(output, $"{provider}-normalized-candidate.json"),
            candidateBytes,
            cancellationToken);
        await File.WriteAllBytesAsync(
            Path.Combine(output, $"{provider}-drift-report.json"),
            CanonicalJson.Serialize(report),
            cancellationToken);
    }

    private static JplRefreshNormalized NormalizeJpl(
        JplReferenceSource source,
        Func<RawArtifact, byte[]> read,
        BenchmarkCaseManifest cases)
    {
        var requests = source.Requests.Select(request =>
        {
            var payload = Encoding.UTF8.GetString(read(request.Response));
            if (request.Purpose == "chiron-seed-vector")
            {
                var vector = HorizonsVectorParser.Parse(payload, new HorizonsVectorExpectation(
                    request.ExpectedTargetHeader, "Sun (10)", "ICRF", "AU-D", "TDB", 2451545.0,
                    source.ApiSource, source.ApiVersion));
                return new JplRefreshNormalizedRequest(
                    request.Id,
                    request.Purpose,
                    [],
                    new RefreshVectorRow(
                        vector.JulianDateTdb, vector.X, vector.Y, vector.Z,
                        vector.Vx, vector.Vy, vector.Vz));
            }

            var body = cases.Positions.First(row => row.Body == request.Body);
            var rows = HorizonsResponseParser.Parse(payload, new HorizonsResponseExpectation(
                request.Body,
                body.HorizonsTargetId,
                request.ExpectedTargetHeader,
                "500@399",
                "OBSERVER",
                "31",
                request.Parameters["TIME_TYPE"].Trim('\''),
                "GREGORIAN",
                source.ApiSource,
                source.ApiVersion,
                request.ExpectedRowTimes));
            return new JplRefreshNormalizedRequest(
                request.Id,
                request.Purpose,
                rows.Select(row => new RefreshLongitudeRow(
                    row.Time, row.JulianDate, row.LongitudeDegrees, row.LatitudeDegrees)).ToArray(),
                null);
        }).ToArray();
        return new JplRefreshNormalized(1, cases.DatasetRevision, requests);
    }

    private static SwissRefreshNormalized NormalizeSwiss(
        IReadOnlyList<SwissReferenceRequest> requests,
        Func<RawArtifact, byte[]> read,
        BenchmarkCaseManifest cases)
    {
        var result = requests.Select(request =>
        {
            var row = cases.Houses.Single(value => value.Id == request.CaseId);
            var cusps = SwissOutputParser.Parse(
                Encoding.UTF8.GetString(read(request.StandardOutput)),
                new SwissOutputExpectation(
                    request.CaseId,
                    request.HouseSystemCode,
                    request.UtJulianDate,
                    request.EastPositiveLongitude,
                    request.Latitude));
            return new SwissRefreshNormalizedCase(
                request.CaseId,
                row.ReferenceSystem,
                cusps.Select(cusp => new RefreshHouseRow(cusp.Cusp, cusp.LongitudeDegrees)).ToArray());
        }).ToArray();
        return new SwissRefreshNormalized(1, cases.DatasetRevision, result);
    }

    private static VerifiedReferenceManifest LoadFrozen(
        string benchmarkRoot,
        BenchmarkCaseManifest cases)
    {
        var referencesRoot = Path.Combine(benchmarkRoot, "references");
        return ReferenceManifestLoader.Load(
            File.ReadAllBytes(Path.Combine(referencesRoot, "manifests", "reference-manifest.json")),
            referencesRoot,
            cases);
    }
}

public sealed record JplRefreshNormalized(
    int SchemaVersion,
    string DatasetRevision,
    IReadOnlyList<JplRefreshNormalizedRequest> Requests);

public sealed record JplRefreshNormalizedRequest(
    string Id,
    string Purpose,
    IReadOnlyList<RefreshLongitudeRow> Rows,
    RefreshVectorRow? Vector);

public sealed record RefreshLongitudeRow(
    string Time,
    double JulianDate,
    double LongitudeDeg,
    double LatitudeDeg);

public sealed record RefreshVectorRow(
    double JulianDate,
    double X,
    double Y,
    double Z,
    double Vx,
    double Vy,
    double Vz);

public sealed record SwissRefreshNormalized(
    int SchemaVersion,
    string DatasetRevision,
    IReadOnlyList<SwissRefreshNormalizedCase> Cases);

public sealed record SwissRefreshNormalizedCase(
    string CaseId,
    string ReferenceSystem,
    IReadOnlyList<RefreshHouseRow> Cusps);

public sealed record RefreshHouseRow(int Cusp, double LongitudeDeg);

public sealed record RefreshDriftReport(
    int SchemaVersion,
    string Provider,
    string DatasetRevision,
    string BaselineNormalizedSha256,
    string CandidateNormalizedSha256,
    bool Changed,
    IReadOnlyList<string> ChangedIds);
