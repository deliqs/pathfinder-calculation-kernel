using System.Net;
using System.Text;
using System.Text.Json;
using PathFinder.AccuracyBenchmark.Cases;
using PathFinder.AccuracyBenchmark.References;

namespace PathFinder.AccuracyBenchmark.Tests;

public sealed class JplRefreshServiceTests
{
    [Fact]
    public async Task RefreshAsync_ValidatesAndArchivesEveryPositionSeedAndTimingResponse()
    {
        var frozen = FrozenBenchmarkDirectory();
        var root = CreateRefreshRoot(frozen);
        var output = Path.Combine(root.FullName, "candidate");
        var frozenBefore = TreeSnapshot.Capture(frozen);
        Assert.False(Directory.Exists(output));
        var cases = BenchmarkCaseManifestLoader.Load(File.ReadAllBytes(
            RepositoryPaths.File("benchmark", "cases", "cases.json")));
        var client = new RecordingJplClient(cases);

        try
        {
            var source = await JplRefreshService.RefreshAsync(
                output, frozen, cases, client, CancellationToken.None);

            Assert.Equal(frozenBefore, TreeSnapshot.Capture(frozen));
            Assert.StartsWith("..", Path.GetRelativePath(frozen, output), StringComparison.Ordinal);
            Assert.Equal(29, client.Requests.Count);
            Assert.Equal(29, source.Requests.Count);
            Assert.Equal(58, Directory.EnumerateFiles(
                Path.Combine(output, "jpl"), "*", SearchOption.TopDirectoryOnly).Count());
            Assert.All(source.Requests, request =>
            {
                Assert.True(File.Exists(Path.Combine(output, request.Response.Path)));
                Assert.True(File.Exists(Path.Combine(output, request.ResponseHeaders.Path)));
                Assert.Equal(request.Response.Sha256, Sha256Verifier.Hash(
                    File.ReadAllBytes(Path.Combine(output, request.Response.Path))));
            });
            Assert.True(File.Exists(Path.Combine(output, "jpl-reference-candidate.json")));
            Assert.True(File.Exists(Path.Combine(output, "jpl-normalized-candidate.json")));
            using var drift = JsonDocument.Parse(File.ReadAllBytes(
                Path.Combine(output, "jpl-drift-report.json")));
            Assert.Equal("jpl", drift.RootElement.GetProperty("provider").GetString());
            Assert.True(drift.RootElement.GetProperty("changed").GetBoolean());
            Assert.NotEqual(0, drift.RootElement.GetProperty("changedIds").GetArrayLength());
        }
        finally
        {
            Directory.Delete(root.FullName, recursive: true);
        }
    }

    [Fact]
    public async Task RefreshAsync_HttpError_DoesNotAcceptErrorBodyAsEvidence()
    {
        var frozen = FrozenBenchmarkDirectory();
        var root = CreateRefreshRoot(frozen);
        var output = Path.Combine(root.FullName, "candidate");
        var frozenBefore = TreeSnapshot.Capture(frozen);
        Assert.False(Directory.Exists(output));
        var cases = BenchmarkCaseManifestLoader.Load(File.ReadAllBytes(
            RepositoryPaths.File("benchmark", "cases", "cases.json")));
        var client = new ErrorJplClient();

        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => JplRefreshService.RefreshAsync(
                output,
                frozen,
                cases,
                client,
                CancellationToken.None));

            Assert.Equal(frozenBefore, TreeSnapshot.Capture(frozen));
            Assert.True(Directory.Exists(output));
            Assert.StartsWith("..", Path.GetRelativePath(frozen, output), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root.FullName, recursive: true);
        }
    }

    [Fact]
    public void Capture_FileBytesAndDirectoryTreeChange_ChangesSnapshot()
    {
        var root = Directory.CreateTempSubdirectory("pathfinder-tree-snapshot-");
        var file = Path.Combine(root.FullName, "evidence.txt");

        try
        {
            File.WriteAllText(file, "one");
            var before = TreeSnapshot.Capture(root.FullName);

            File.WriteAllText(file, "two");
            var bytesChanged = TreeSnapshot.Capture(root.FullName);
            Directory.CreateDirectory(Path.Combine(root.FullName, "added"));
            var treeChanged = TreeSnapshot.Capture(root.FullName);

            Assert.NotEqual(before, bytesChanged);
            Assert.NotEqual(bytesChanged, treeChanged);
        }
        finally
        {
            Directory.Delete(root.FullName, recursive: true);
        }
    }

    private static string FrozenBenchmarkDirectory() =>
        Directory.GetParent(Path.GetDirectoryName(
            RepositoryPaths.File("benchmark", "cases", "cases.json"))!)!.FullName;

    private static DirectoryInfo CreateRefreshRoot(string frozen) =>
        Directory.CreateDirectory(Path.Combine(
            Directory.GetParent(frozen)!.FullName,
            $".pathfinder-jpl-refresh-{Guid.NewGuid():N}"));

    private sealed class RecordingJplClient(BenchmarkCaseManifest cases) : IJplClient
    {
        private readonly IReadOnlyDictionary<string, HorizonsQuery> _positions =
            HorizonsQueryBuilder.Build(cases).ToDictionary(query => query.RequestUri.AbsoluteUri);
        private readonly IReadOnlyDictionary<string, HorizonsTimingQuery> _timings =
            HorizonsQueryBuilder.BuildTimings(cases).ToDictionary(query => query.RequestUri.AbsoluteUri);

        public List<Uri> Requests { get; } = [];

        public Task<JplHttpResponse> GetAsync(Uri requestUri, CancellationToken cancellationToken)
        {
            Requests.Add(requestUri);
            string payload;
            if (_positions.TryGetValue(requestUri.AbsoluteUri, out var position))
            {
                payload = position.Id == HorizonsQueryBuilder.ChironSeedReferenceId
                    ? HorizonsPayloads.Vector(position)
                    : HorizonsPayloads.Longitudes(position.Body, position.ExpectedTargetHeader,
                        position.Parameters["TIME_TYPE"].Trim('\''), position.ExpectedRowTimes);
            }
            else
            {
                var timing = _timings[requestUri.AbsoluteUri];
                payload = HorizonsPayloads.Longitudes(
                    timing.Body, timing.ExpectedTargetHeader, "UT", timing.ExpectedRowTimes);
            }

            return Task.FromResult(new JplHttpResponse(
                HttpStatusCode.OK,
                Encoding.UTF8.GetBytes(payload),
                [new("content-type", "application/json")]));
        }
    }

    private sealed class ErrorJplClient : IJplClient
    {
        public Task<JplHttpResponse> GetAsync(Uri requestUri, CancellationToken cancellationToken) =>
            Task.FromResult(new JplHttpResponse(
                HttpStatusCode.ServiceUnavailable,
                Encoding.UTF8.GetBytes("{\"error\":\"unavailable\"}"),
                []));
    }
}
