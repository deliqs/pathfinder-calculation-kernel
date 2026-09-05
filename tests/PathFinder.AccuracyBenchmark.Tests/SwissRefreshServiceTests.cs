using System.Text;
using System.Text.Json;
using PathFinder.AccuracyBenchmark.Cases;
using PathFinder.AccuracyBenchmark.References;

namespace PathFinder.AccuracyBenchmark.Tests;

public sealed class SwissRefreshServiceTests
{
    [Fact]
    public async Task RefreshAsync_WritesExactSeparateOutputsAndManifestProvenance()
    {
        var frozen = FrozenBenchmarkDirectory();
        var root = CreateRefreshRoot(frozen);
        var output = Path.Combine(root, "candidate");
        var executable = Path.Combine(root, "swetest");
        var frozenBefore = TreeSnapshot.Capture(frozen);
        Assert.False(Directory.Exists(output));
        Directory.CreateDirectory(root);
        await File.WriteAllBytesAsync(executable, Encoding.UTF8.GetBytes("independent executable"));
        var cases = BenchmarkCaseManifestLoader.Load(File.ReadAllBytes(
            RepositoryPaths.File("benchmark", "cases", "cases.json")));
        var runner = new RecordingProcessRunner();

        try
        {
            var candidate = await SwissRefreshService.RefreshAsync(
                executable, output, frozen, cases, runner, CancellationToken.None);

            Assert.Equal(frozenBefore, TreeSnapshot.Capture(frozen));
            Assert.StartsWith("..", Path.GetRelativePath(frozen, output), StringComparison.Ordinal);
            Assert.Equal(5, runner.Invocations.Count);
            Assert.Equal(["-h"], runner.Invocations[0].Arguments);
            Assert.All(runner.Invocations.Skip(1), invocation =>
                Assert.Equal("-fPl", invocation.Arguments[^2]));
            Assert.Equal("not-verified-by-runner", candidate.SourceProvenanceStatus);
            Assert.Null(candidate.SourceRepository);
            Assert.Null(candidate.SourceTag);
            Assert.Null(candidate.SourceCommit);
            Assert.Null(candidate.BuildTarget);
            Assert.Null(candidate.BuildCommand);
            Assert.Equal(Sha256Verifier.Hash(Encoding.UTF8.GetBytes("independent executable")),
                candidate.ExecutableSha256);
            Assert.Equal(4, candidate.Requests.Count);
            Assert.Equal(Encoding.UTF8.GetBytes("Version: 2.10.03\n"), await File.ReadAllBytesAsync(
                Path.Combine(output, candidate.VersionOutput.Path)));
            Assert.True(File.Exists(Path.Combine(output, "swiss-reference-candidate.json")));
            Assert.True(File.Exists(Path.Combine(output, "swiss-normalized-candidate.json")));
            using var drift = JsonDocument.Parse(File.ReadAllBytes(
                Path.Combine(output, "swiss-drift-report.json")));
            Assert.Equal("swiss", drift.RootElement.GetProperty("provider").GetString());
            Assert.True(drift.RootElement.GetProperty("changed").GetBoolean());
            Assert.NotEqual(0, drift.RootElement.GetProperty("changedIds").GetArrayLength());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task RefreshAsync_ProcessFailure_LeavesFrozenBenchmarkByteIdentical()
    {
        var frozen = FrozenBenchmarkDirectory();
        var root = CreateRefreshRoot(frozen);
        var output = Path.Combine(root, "candidate");
        var executable = Path.Combine(root, "swetest");
        var frozenBefore = TreeSnapshot.Capture(frozen);
        Assert.False(Directory.Exists(output));
        Directory.CreateDirectory(root);
        await File.WriteAllBytesAsync(executable, Encoding.UTF8.GetBytes("independent executable"));
        var cases = BenchmarkCaseManifestLoader.Load(File.ReadAllBytes(
            RepositoryPaths.File("benchmark", "cases", "cases.json")));
        var runner = new FailingProcessRunner();

        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => SwissRefreshService.RefreshAsync(
                executable, output, frozen, cases, runner, CancellationToken.None));

            Assert.Equal(frozenBefore, TreeSnapshot.Capture(frozen));
            Assert.True(File.Exists(Path.Combine(output, "swiss", "version-identification.txt")));
            Assert.StartsWith("..", Path.GetRelativePath(frozen, output), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string FrozenBenchmarkDirectory() =>
        Directory.GetParent(Path.GetDirectoryName(
            RepositoryPaths.File("benchmark", "cases", "cases.json"))!)!.FullName;

    private static string CreateRefreshRoot(string frozen) =>
        Directory.CreateDirectory(Path.Combine(
            Directory.GetParent(frozen)!.FullName,
            $".pathfinder-swiss-refresh-{Guid.NewGuid():N}")).FullName;

    private sealed class RecordingProcessRunner : IExternalProcessRunner
    {
        public byte[] VersionBytes { get; } = Encoding.UTF8.GetBytes(
            "Swiss Ephemeris test application\nVersion: 2.10.03\n" +
            "-ut input date is Universal Time (UT1)\nother help text\n");

        public List<SwissInvocation> Invocations { get; } = [];

        public Task<ExternalProcessOutput> RunAsync(
            string executable,
            SwissInvocation invocation,
            CancellationToken cancellationToken)
        {
            Invocations.Add(invocation);
            var bytes = invocation.Arguments.SequenceEqual(["-h"], StringComparer.Ordinal)
                ? VersionBytes
                : Encoding.UTF8.GetBytes(HouseOutput());
            return Task.FromResult(new ExternalProcessOutput(0, bytes, []));
        }

        private static string HouseOutput() => string.Join('\n', Enumerable.Range(1, 12)
            .Select(cusp => FormattableString.Invariant(
                $"house {cusp,2}         {cusp * 20.0:0.0000000}"))) + "\n";
    }

    private sealed class FailingProcessRunner : IExternalProcessRunner
    {
        private int _invocationCount;

        public Task<ExternalProcessOutput> RunAsync(
            string executable,
            SwissInvocation invocation,
            CancellationToken cancellationToken)
        {
            _invocationCount++;
            return Task.FromResult(_invocationCount == 1
                ? new ExternalProcessOutput(
                    0,
                    Encoding.UTF8.GetBytes("Swiss Ephemeris test application\nVersion: 2.10.03\n"),
                    [])
                : new ExternalProcessOutput(
                    2,
                    [],
                    Encoding.UTF8.GetBytes("swetest failed")));
        }
    }
}
