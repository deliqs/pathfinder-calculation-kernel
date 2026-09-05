using System.Text.Json;
using System.Text.Json.Serialization;
using PathFinder.AccuracyBenchmark.Cases;

namespace PathFinder.AccuracyBenchmark.References;

public static class ReferenceManifestLoader
{
    private const string SwissDistribution =
        "user-supplied executable; not distributed by this repository";
    private const string TzdbSource = "IANA TZDB distributed by Noda Time";
    private const string TzdbSemantics =
        "compatibility; not an independent timezone-engine comparison";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static VerifiedReferenceManifest Load(
        ReadOnlySpan<byte> bytes,
        string referencesRoot,
        BenchmarkCaseManifest cases)
    {
        ReferenceManifest manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<ReferenceManifest>(bytes, Options) ??
                throw new InvalidDataException("Reference manifest is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Reference manifest does not match its closed schema.", exception);
        }

        ValidateManifest(manifest);
        ReferenceManifestContractValidator.Validate(manifest, cases);
        var root = Path.GetFullPath(referencesRoot);
        var artifacts = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var artifact in EnumerateArtifacts(manifest))
        {
            ValidateArtifact(root, artifact, artifacts);
        }

        return new VerifiedReferenceManifest(manifest, artifacts);
    }

    private static void ValidateManifest(ReferenceManifest manifest)
    {
        Require(manifest.SchemaVersion == 2, "schemaVersion must be 2");
        RequireText(manifest.DatasetRevision, "datasetRevision");
        Require(manifest.Jpl is not null, "jpl is required");
        Require(manifest.Swiss is not null, "swiss is required");
        Require(manifest.Tzdb is not null, "tzdb is required");
        ValidateJpl(manifest.Jpl!);
        ValidateSwiss(manifest.Swiss!);
        ValidateTzdb(manifest.Tzdb!);
    }

    private static void ValidateJpl(JplReferenceSource source)
    {
        Require(source.ApiSource == HorizonsQueryBuilder.ApiSource, "JPL API source is invalid");
        Require(source.ApiVersion == HorizonsQueryBuilder.ApiVersion, "JPL API version is invalid");
        Require(source.Endpoint == HorizonsQueryBuilder.Endpoint, "JPL endpoint is invalid");
        Require(source.Requests is not null && source.Requests.Count > 0, "JPL requests are required");
        foreach (var request in source.Requests!)
        {
            RequireText(request.Id, "JPL request id");
            RequireText(request.Body, $"{request.Id}.body");
            RequireText(request.Purpose, $"{request.Id}.purpose");
            Require(request.Parameters is not null && request.Parameters.Count > 0,
                $"{request.Id}.parameters are required");
            RequireText(request.ExpectedTargetHeader, $"{request.Id}.expectedTargetHeader");
            Require(request.ExpectedRowTimes is not null && request.ExpectedRowTimes.Count > 0,
                $"{request.Id}.expectedRowTimes are required");
        }

        RequireUnique(source.Requests!.Select(request => request.Id), "JPL request ids");
    }

    private static void ValidateSwiss(SwissReferenceSource source)
    {
        Require(source.RequiredVersion == "2.10.03", "Swiss requiredVersion must be 2.10.03");
        Require(source.Distribution == SwissDistribution, "Swiss distribution boundary is invalid");
        Require(source.SourceRepository == "https://github.com/aloistr/swisseph",
            "Swiss sourceRepository is invalid");
        Require(source.SourceTag == "v2.10.03", "Swiss sourceTag is invalid");
        Require(source.SourceCommit == "175e1fcb3108bcd5c0d146c803f51dcf23508012",
            "Swiss sourceCommit does not match the frozen source");
        Require(source.BuildTarget == "swetest", "Swiss buildTarget must be swetest");
        Require(source.BuildCommand == "make swetest", "Swiss buildCommand must be make swetest");
        Require(source.ExecutableSha256 ==
                "8cb956985f8619174377a8aaa17245e5035838f867a1ec3ac284adf4821a8cf0",
            "Swiss executableSha256 does not match the reviewed frozen build");
        Require(source.Environment is not null && source.Environment.Count == 2 &&
                source.Environment.TryGetValue("LC_ALL", out var locale) && locale == "C" &&
                source.Environment.TryGetValue("TZ", out var timezone) && timezone == "UTC",
            "Swiss environment must be exactly LC_ALL=C and TZ=UTC");
        Require(source.VersionInvocation is not null &&
                source.VersionInvocation.SequenceEqual(["-h"], StringComparer.Ordinal),
            "Swiss versionInvocation must be exactly [-h]");
        Require(source.Requests is not null && source.Requests.Count > 0, "Swiss requests are required");
        foreach (var request in source.Requests!)
        {
            RequireText(request.CaseId, "Swiss caseId");
            Require(double.IsFinite(request.UtJulianDate), $"{request.CaseId}.utJulianDate must be finite");
            Require(double.IsFinite(request.EastPositiveLongitude),
                $"{request.CaseId}.eastPositiveLongitude must be finite");
            Require(double.IsFinite(request.Latitude), $"{request.CaseId}.latitude must be finite");
            Require(request.HouseSystemCode is "P" or "O", $"{request.CaseId}.houseSystemCode is invalid");
            Require(request.Arguments is not null && request.Arguments.Count > 0,
                $"{request.CaseId}.arguments are required");
            var expected = SwissInvocationBuilder.BuildHouse(new Cases.HouseCase(
                request.CaseId,
                JulianDateToUtc(request.UtJulianDate),
                request.Latitude,
                request.EastPositiveLongitude,
                request.HouseSystemCode == "P" ? "Placidus" : "Placidus",
                request.HouseSystemCode == "P" ? "Placidus" : "Porphyry",
                request.HouseSystemCode,
                1));
            Require(request.Arguments!.SequenceEqual(expected.Arguments, StringComparer.Ordinal),
                $"{request.CaseId}.arguments do not match its recorded input fields");
        }

        RequireUnique(source.Requests!.Select(request => request.CaseId), "Swiss case ids");
    }

    private static void ValidateTzdb(TzdbReferenceSource source)
    {
        Require(source.NodaTimePackageVersion == "3.3.3", "Noda Time package version is invalid");
        RequireText(source.ProviderVersion, "TZDB providerVersion");
        Require(source.Source == TzdbSource, "TZDB source is invalid");
        Require(source.CaseSemantics == TzdbSemantics, "TZDB case semantics are invalid");
    }

    private static IEnumerable<RawArtifact> EnumerateArtifacts(ReferenceManifest manifest)
    {
        foreach (var request in manifest.Jpl.Requests)
        {
            yield return request.Response;
            yield return request.ResponseHeaders;
        }

        yield return manifest.Swiss.VersionOutput;
        foreach (var request in manifest.Swiss.Requests)
        {
            yield return request.StandardOutput;
        }
    }

    private static void ValidateArtifact(
        string root,
        RawArtifact? artifact,
        IDictionary<string, byte[]> artifacts)
    {
        if (artifact is null)
        {
            throw new InvalidDataException("raw artifact is required");
        }
        RequireText(artifact.Path, "raw artifact path");
        Require(artifact.Sha256 is { Length: 64 } &&
                artifact.Sha256.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f'),
            $"{artifact.Path}.sha256 must be 64 lowercase hexadecimal characters");
        Require(!Path.IsPathRooted(artifact.Path), $"Reference artifact path must be relative: {artifact.Path}");

        var fullPath = Path.GetFullPath(Path.Combine(root, artifact.Path));
        var relative = Path.GetRelativePath(root, fullPath);
        Require(relative != ".." && !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal),
            $"Reference artifact path escapes the reference root: {artifact.Path}");
        Require(!artifacts.ContainsKey(artifact.Path), $"Reference artifact path is duplicated: {artifact.Path}");
        Require(File.Exists(fullPath), $"Reference artifact does not exist: {artifact.Path}");

        var bytes = File.ReadAllBytes(fullPath);
        Sha256Verifier.Verify(bytes, artifact.Sha256, artifact.Path);
        artifacts.Add(artifact.Path, bytes);
    }

    private static void RequireUnique(IEnumerable<string> values, string field) =>
        Require(values.Distinct(StringComparer.Ordinal).Count() == values.Count(), $"{field} must be unique");

    private static bool IsLowerHex(string value) =>
        value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static string JulianDateToUtc(double value)
    {
        var ticks = checked((long)Math.Round(
            (value - 2440587.5) * NodaTime.NodaConstants.TicksPerDay,
            MidpointRounding.AwayFromZero));
        return NodaTime.Instant.FromUnixTimeTicks(ticks).ToString();
    }

    private static void RequireText(string? value, string field) =>
        Require(!string.IsNullOrWhiteSpace(value), $"{field} is required");

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidDataException(message);
        }
    }
}
