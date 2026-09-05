namespace PathFinder.AccuracyBenchmark.References;

public sealed record SwissRefreshCandidate(
    string RequiredVersion,
    string Distribution,
    string SourceProvenanceStatus,
    string? SourceRepository,
    string? SourceTag,
    string? SourceCommit,
    string? BuildTarget,
    string? BuildCommand,
    string ExecutableSha256,
    IReadOnlyDictionary<string, string> Environment,
    IReadOnlyList<string> VersionInvocation,
    RawArtifact VersionOutput,
    IReadOnlyList<SwissReferenceRequest> Requests);
