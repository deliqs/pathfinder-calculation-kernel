namespace PathFinder.AccuracyBenchmark.References;

public sealed record ReferenceManifest(
    int SchemaVersion,
    string DatasetRevision,
    JplReferenceSource Jpl,
    SwissReferenceSource Swiss,
    TzdbReferenceSource Tzdb);

public sealed record RawArtifact(string Path, string Sha256);

public sealed record JplReferenceSource(
    string ApiSource,
    string ApiVersion,
    string Endpoint,
    IReadOnlyList<JplReferenceRequest> Requests);

public sealed record JplReferenceRequest(
    string Id,
    string Body,
    string Purpose,
    IReadOnlyDictionary<string, string> Parameters,
    string ExpectedTargetHeader,
    IReadOnlyList<string> ExpectedRowTimes,
    RawArtifact Response,
    RawArtifact ResponseHeaders);

public sealed record SwissReferenceSource(
    string RequiredVersion,
    string Distribution,
    string SourceRepository,
    string SourceTag,
    string SourceCommit,
    string BuildTarget,
    string BuildCommand,
    string ExecutableSha256,
    IReadOnlyDictionary<string, string> Environment,
    IReadOnlyList<string> VersionInvocation,
    RawArtifact VersionOutput,
    IReadOnlyList<SwissReferenceRequest> Requests);

public sealed record SwissReferenceRequest(
    string CaseId,
    double UtJulianDate,
    double EastPositiveLongitude,
    double Latitude,
    string HouseSystemCode,
    IReadOnlyList<string> Arguments,
    RawArtifact StandardOutput);

public sealed record TzdbReferenceSource(
    string NodaTimePackageVersion,
    string ProviderVersion,
    string Source,
    string CaseSemantics);

public sealed class VerifiedReferenceManifest
{
    private readonly IReadOnlyDictionary<string, byte[]> _artifacts;

    internal VerifiedReferenceManifest(ReferenceManifest manifest, IReadOnlyDictionary<string, byte[]> artifacts)
    {
        Manifest = manifest;
        _artifacts = artifacts;
    }

    public ReferenceManifest Manifest { get; }
    public int ArtifactCount => _artifacts.Count;

    public byte[] Read(RawArtifact artifact) =>
        _artifacts.TryGetValue(artifact.Path, out var bytes)
            ? bytes.ToArray()
            : throw new InvalidDataException($"Reference artifact was not verified: {artifact.Path}");
}
