using System.Text;
using PathFinder.AccuracyBenchmark.Cases;
using PathFinder.AccuracyBenchmark.Commands;
using PathFinder.AccuracyBenchmark.Serialization;

namespace PathFinder.AccuracyBenchmark.References;

public static class SwissRefreshService
{
    private const string RequiredVersion = "2.10.03";
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static async Task<SwissRefreshCandidate> RefreshAsync(
        string swetestPath,
        string outputDirectory,
        string frozenReferencesDirectory,
        BenchmarkCaseManifest cases,
        IExternalProcessRunner processRunner,
        CancellationToken cancellationToken)
    {
        var executable = Path.GetFullPath(swetestPath);
        if (!File.Exists(executable))
        {
            throw new FileNotFoundException("The user-supplied swetest executable does not exist.", executable);
        }

        var output = CandidateOutputGuard.Validate(outputDirectory, frozenReferencesDirectory);
        Directory.CreateDirectory(Path.Combine(output, "swiss"));
        var versionInvocation = SwissInvocationBuilder.BuildVersion();
        var versionOutput = await RunCheckedAsync(
            executable, versionInvocation, processRunner, cancellationToken);
        var observedVersion = SwissOutputParser.ParseVersion(Decode(versionOutput), RequiredVersion);
        var versionIdentification = Encoding.UTF8.GetBytes($"Version: {observedVersion}\n");
        var versionArtifact = await WriteArtifactAsync(
            output, "swiss/version-identification.txt", versionIdentification, cancellationToken);

        var requests = new List<SwissReferenceRequest>();
        foreach (var row in cases.Houses)
        {
            var invocation = SwissInvocationBuilder.BuildHouse(row);
            var standardOutput = await RunCheckedAsync(
                executable, invocation, processRunner, cancellationToken);
            var expectation = new SwissOutputExpectation(
                row.Id,
                row.SwissHouseSystemCode,
                invocation.UtJulianDate!.Value,
                row.EastPositiveLongitude,
                row.Latitude);
            SwissOutputParser.Parse(Decode(standardOutput), expectation);
            var artifact = await WriteArtifactAsync(
                output, $"swiss/{row.Id}.stdout.txt", standardOutput, cancellationToken);
            requests.Add(new SwissReferenceRequest(
                row.Id,
                invocation.UtJulianDate.Value,
                row.EastPositiveLongitude,
                row.Latitude,
                row.SwissHouseSystemCode,
                invocation.Arguments,
                artifact));
        }

        var source = new SwissRefreshCandidate(
            RequiredVersion,
            "user-supplied executable; not distributed by this repository",
            "not-verified-by-runner",
            null,
            null,
            null,
            null,
            null,
            Sha256Verifier.Hash(await File.ReadAllBytesAsync(executable, cancellationToken)),
            versionInvocation.Environment,
            versionInvocation.Arguments,
            versionArtifact,
            requests);
        await File.WriteAllBytesAsync(
            Path.Combine(output, "swiss-reference-candidate.json"),
            CanonicalJson.Serialize(source),
            cancellationToken);
        await RefreshEvidenceWriter.WriteSwissAsync(
            output, frozenReferencesDirectory, cases, source, cancellationToken);
        return source;
    }

    private static async Task<byte[]> RunCheckedAsync(
        string executable,
        SwissInvocation invocation,
        IExternalProcessRunner runner,
        CancellationToken cancellationToken)
    {
        var output = await runner.RunAsync(executable, invocation, cancellationToken);
        if (output.ExitCode != 0)
        {
            throw new InvalidDataException(
                $"swetest exited with code {output.ExitCode}: {Decode(output.StandardError)}");
        }

        return output.StandardOutput;
    }

    private static async Task<RawArtifact> WriteArtifactAsync(
        string output,
        string relativePath,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        await File.WriteAllBytesAsync(Path.Combine(output, relativePath), bytes, cancellationToken);
        return new RawArtifact(relativePath, Sha256Verifier.Hash(bytes));
    }

    private static string Decode(byte[] bytes)
    {
        try
        {
            return StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException("swetest output is not valid UTF-8.", exception);
        }
    }
}
