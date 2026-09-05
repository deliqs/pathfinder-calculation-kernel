using System.Text;
using NodaTime;
using PathFinder.AccuracyBenchmark.Cases;
using PathFinder.AccuracyBenchmark.References;
using PathFinder.AccuracyBenchmark.Serialization;

namespace PathFinder.AccuracyBenchmark.Tests;

public sealed class ReferenceManifestLoaderTests
{
    [Fact]
    public void Load_SeparateSwissVersionAndHouseOutputs_VerifiesEveryArtifact()
    {
        using var fixture = ReferenceFixture.Create();

        var verified = ReferenceManifestLoader.Load(fixture.ManifestBytes, fixture.Root, fixture.Cases);

        Assert.Equal("calculation-benchmark-4", verified.Manifest.DatasetRevision);
        Assert.Equal("Version: 2.10.03\n", Encoding.UTF8.GetString(
            verified.Read(verified.Manifest.Swiss.VersionOutput)));
        Assert.Equal("house  1       123.456789\n", Encoding.UTF8.GetString(
            verified.Read(verified.Manifest.Swiss.Requests[0].StandardOutput)));
        Assert.Equal("https://github.com/aloistr/swisseph", verified.Manifest.Swiss.SourceRepository);
        Assert.Equal("v2.10.03", verified.Manifest.Swiss.SourceTag);
        Assert.Equal("175e1fcb3108bcd5c0d146c803f51dcf23508012", verified.Manifest.Swiss.SourceCommit);
        Assert.Equal("make swetest", verified.Manifest.Swiss.BuildCommand);
        Assert.Matches("^[0-9a-f]{64}$", verified.Manifest.Swiss.ExecutableSha256);
        Assert.Equal("C", verified.Manifest.Swiss.Environment["LC_ALL"]);
        Assert.Equal(63, verified.ArtifactCount);
    }

    [Theory]
    [InlineData("raw/swiss/version-identification.txt")]
    [InlineData("raw/swiss/london-j2000.stdout.txt")]
    [InlineData("raw/jpl/sun-positions-ut.response.json")]
    [InlineData("raw/jpl/sun-positions-ut.headers.txt")]
    public void Load_TamperedRawArtifact_Throws(string relativePath)
    {
        using var fixture = ReferenceFixture.Create();
        File.AppendAllText(Path.Combine(fixture.Root, relativePath), "tampered", Encoding.UTF8);

        var error = Assert.Throws<InvalidDataException>(() =>
            ReferenceManifestLoader.Load(fixture.ManifestBytes, fixture.Root, fixture.Cases));

        Assert.Contains(relativePath, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_ArtifactPathEscapesReferenceRoot_Throws()
    {
        using var fixture = ReferenceFixture.Create();
        var manifest = fixture.Manifest with
        {
            Swiss = fixture.Manifest.Swiss with
            {
                VersionOutput = fixture.Manifest.Swiss.VersionOutput with { Path = "../outside.txt" }
            }
        };

        Assert.Throws<InvalidDataException>(() =>
            ReferenceManifestLoader.Load(CanonicalJson.Serialize(manifest), fixture.Root, fixture.Cases));
    }

    [Fact]
    public void Load_JplCenterParameterTamperedWithInternallyConsistentManifest_Throws()
    {
        using var fixture = ReferenceFixture.Create();
        var request = fixture.Manifest.Jpl.Requests[0];
        var parameters = request.Parameters.ToDictionary(pair => pair.Key, pair => pair.Value);
        parameters["CENTER"] = "'500@10'";
        var manifest = fixture.Manifest with
        {
            Jpl = fixture.Manifest.Jpl with
            {
                Requests = [request with { Parameters = parameters }, .. fixture.Manifest.Jpl.Requests.Skip(1)]
            }
        };

        var error = Assert.Throws<InvalidDataException>(() => ReferenceManifestLoader.Load(
            CanonicalJson.Serialize(manifest), fixture.Root, fixture.Cases));

        Assert.Contains("parameters", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Load_FrozenSwissSourceOrExecutableIdentityChanged_Throws(bool changeSourceCommit)
    {
        using var fixture = ReferenceFixture.Create();
        var swiss = changeSourceCommit
            ? fixture.Manifest.Swiss with { SourceCommit = new string('b', 40) }
            : fixture.Manifest.Swiss with { ExecutableSha256 = new string('b', 64) };
        var manifest = fixture.Manifest with { Swiss = swiss };

        Assert.Throws<InvalidDataException>(() => ReferenceManifestLoader.Load(
            CanonicalJson.Serialize(manifest), fixture.Root, fixture.Cases));
    }

    [Theory]
    [InlineData("jpl-count")]
    [InlineData("jpl-id")]
    [InlineData("jpl-body")]
    [InlineData("jpl-purpose")]
    [InlineData("jpl-row-time")]
    [InlineData("swiss-case")]
    [InlineData("swiss-coordinate")]
    [InlineData("swiss-arguments")]
    [InlineData("tzdb-provider")]
    public void Load_FrozenRequestOrDependencyContractChanged_Throws(string change)
    {
        using var fixture = ReferenceFixture.Create();
        var jpl = fixture.Manifest.Jpl.Requests.ToArray();
        var swiss = fixture.Manifest.Swiss.Requests.ToArray();
        var manifest = change switch
        {
            "jpl-count" => fixture.Manifest with
            {
                Jpl = fixture.Manifest.Jpl with { Requests = jpl[..^1] }
            },
            "jpl-id" => WithFirstJpl(fixture.Manifest, jpl[0] with { Id = "different" }),
            "jpl-body" => WithFirstJpl(fixture.Manifest, jpl[0] with { Body = "Moon" }),
            "jpl-purpose" => WithFirstJpl(fixture.Manifest, jpl[0] with { Purpose = "different" }),
            "jpl-row-time" => WithFirstJpl(fixture.Manifest, jpl[0] with { ExpectedRowTimes = ["different"] }),
            "swiss-case" => WithFirstSwiss(fixture.Manifest, swiss[0] with { CaseId = "different" }),
            "swiss-coordinate" => WithFirstSwiss(
                fixture.Manifest,
                swiss[0] with { Latitude = swiss[0].Latitude + 1 }),
            "swiss-arguments" => WithFirstSwiss(
                fixture.Manifest,
                swiss[0] with { Arguments = ["-different"] }),
            "tzdb-provider" => fixture.Manifest with
            {
                Tzdb = fixture.Manifest.Tzdb with { ProviderVersion = "different" }
            },
            _ => throw new InvalidOperationException(change)
        };

        Assert.Throws<InvalidDataException>(() => ReferenceManifestLoader.Load(
            CanonicalJson.Serialize(manifest), fixture.Root, fixture.Cases));
    }

    private static ReferenceManifest WithFirstJpl(
        ReferenceManifest manifest,
        JplReferenceRequest first) => manifest with
    {
        Jpl = manifest.Jpl with { Requests = [first, .. manifest.Jpl.Requests.Skip(1)] }
    };

    private static ReferenceManifest WithFirstSwiss(
        ReferenceManifest manifest,
        SwissReferenceRequest first) => manifest with
    {
        Swiss = manifest.Swiss with { Requests = [first, .. manifest.Swiss.Requests.Skip(1)] }
    };

    private sealed class ReferenceFixture : IDisposable
    {
        private ReferenceFixture(
            string root,
            ReferenceManifest manifest,
            BenchmarkCaseManifest cases)
        {
            Root = root;
            Manifest = manifest;
            Cases = cases;
            ManifestBytes = CanonicalJson.Serialize(manifest);
        }

        public string Root { get; }
        public ReferenceManifest Manifest { get; }
        public BenchmarkCaseManifest Cases { get; }
        public byte[] ManifestBytes { get; }

        public static ReferenceFixture Create()
        {
            var root = Path.Combine(Path.GetTempPath(), $"pathfinder-reference-{Guid.NewGuid():N}");
            var cases = BenchmarkCaseManifestLoader.Load(File.ReadAllBytes(
                RepositoryPaths.File("benchmark", "cases", "cases.json")));
            var jplRequests = HorizonsQueryBuilder.Build(cases)
                .Select(query => CreateJpl(
                    root,
                    query.Id,
                    query.Body,
                    Purpose(query.Id),
                    query.Parameters,
                    query.ExpectedTargetHeader,
                    query.ExpectedRowTimes))
                .Concat(HorizonsQueryBuilder.BuildTimings(cases).Select(query => CreateJpl(
                    root,
                    $"timing-{query.CaseId}",
                    query.Body,
                    $"timing:{query.Method}",
                    query.Parameters,
                    query.ExpectedTargetHeader,
                    query.ExpectedRowTimes)))
                .ToArray();
            var version = Write(
                root,
                "raw/swiss/version-identification.txt",
                "Version: 2.10.03\n");
            var swissRequests = cases.Houses.Select(row =>
            {
                var invocation = SwissInvocationBuilder.BuildHouse(row);
                var output = Write(
                    root,
                    $"raw/swiss/{row.Id}.stdout.txt",
                    "house  1       123.456789\n");
                return new SwissReferenceRequest(
                    row.Id,
                    invocation.UtJulianDate!.Value,
                    row.EastPositiveLongitude,
                    row.Latitude,
                    row.SwissHouseSystemCode,
                    invocation.Arguments,
                    output);
            }).ToArray();
            var manifest = new ReferenceManifest(
                2,
                "calculation-benchmark-4",
                new JplReferenceSource(
                    HorizonsQueryBuilder.ApiSource,
                    HorizonsQueryBuilder.ApiVersion,
                    HorizonsQueryBuilder.Endpoint,
                    jplRequests),
                new SwissReferenceSource(
                    "2.10.03",
                    "user-supplied executable; not distributed by this repository",
                    "https://github.com/aloistr/swisseph",
                    "v2.10.03",
                    "175e1fcb3108bcd5c0d146c803f51dcf23508012",
                    "swetest",
                    "make swetest",
                    "8cb956985f8619174377a8aaa17245e5035838f867a1ec3ac284adf4821a8cf0",
                    new SortedDictionary<string, string> { ["LC_ALL"] = "C", ["TZ"] = "UTC" },
                    ["-h"],
                    version,
                    swissRequests),
                new TzdbReferenceSource(
                    "3.3.3",
                    DateTimeZoneProviders.Tzdb.VersionId,
                    "IANA TZDB distributed by Noda Time",
                    "compatibility; not an independent timezone-engine comparison"));
            return new ReferenceFixture(root, manifest, cases);
        }

        public void Dispose() => Directory.Delete(Root, recursive: true);

        private static RawArtifact Write(string root, string relativePath, string content)
        {
            var bytes = Encoding.UTF8.GetBytes(content);
            var path = Path.Combine(root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, bytes);
            return new RawArtifact(relativePath, Sha256Verifier.Hash(bytes));
        }

        private static JplReferenceRequest CreateJpl(
            string root,
            string id,
            string body,
            string purpose,
            IReadOnlyDictionary<string, string> parameters,
            string targetHeader,
            IReadOnlyList<string> rowTimes) => new(
            id,
            body,
            purpose,
            parameters,
            targetHeader,
            rowTimes,
            Write(root, $"raw/jpl/{id}.response.json", "{\"result\":\"raw\"}\n"),
            Write(root, $"raw/jpl/{id}.headers.txt", "content-type: application/json\n"));

        private static string Purpose(string id) => id switch
        {
            HorizonsQueryBuilder.ChironSeedReferenceId => "chiron-seed-vector",
            _ when id.EndsWith("-positions-ut", StringComparison.Ordinal) => "positions-nominal-ut",
            _ when id.EndsWith("-positions-tt", StringComparison.Ordinal) => "positions-matched-tt",
            _ => throw new InvalidOperationException()
        };
    }
}
