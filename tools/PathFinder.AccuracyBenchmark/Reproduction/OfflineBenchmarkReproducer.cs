using System.Text;
using PathFinder.AccuracyBenchmark.Cases;
using PathFinder.AccuracyBenchmark.Commands;
using PathFinder.AccuracyBenchmark.References;
using PathFinder.AccuracyBenchmark.Serialization;

namespace PathFinder.AccuracyBenchmark.Reproduction;

public static class OfflineBenchmarkReproducer
{
    public const string ResultsFileName = "results.json";
    public const string ResultsHashFileName = "results.sha256";
    public const string NormalizedReferencesFileName = "normalized-references.json";
    public const string SourceManifestFileName = "calculation-source-manifest.json";

    public static ReproductionOutput Reproduce(string repositoryRoot, string outputDirectory)
    {
        var root = Path.GetFullPath(repositoryRoot);
        var benchmarkRoot = Path.Combine(root, "benchmark");
        var output = CandidateOutputGuard.Validate(outputDirectory, benchmarkRoot);
        var caseBytes = File.ReadAllBytes(Path.Combine(benchmarkRoot, "cases", "cases.json"));
        var cases = BenchmarkCaseManifestLoader.Load(caseBytes);
        var referencesRoot = Path.Combine(benchmarkRoot, "references");
        var referenceManifestBytes = File.ReadAllBytes(Path.Combine(
            referencesRoot,
            "manifests",
            "reference-manifest.json"));
        var verified = ReferenceManifestLoader.Load(referenceManifestBytes, referencesRoot, cases);
        var normalized = ReferenceNormalizer.Normalize(cases, verified);
        var normalizedBytes = CanonicalJson.Serialize(normalized);
        var sourceManifestBytes = CanonicalJson.Serialize(BenchmarkCalculator.CreateSourceManifest(root));
        var results = BenchmarkCalculator.Calculate(
            cases,
            verified,
            normalized,
            CanonicalJson.Sha256(sourceManifestBytes),
            CanonicalJson.Sha256(referenceManifestBytes));
        var resultsBytes = CanonicalJson.Serialize(results);
        var resultsHash = CanonicalJson.Sha256(resultsBytes);

        Directory.CreateDirectory(output);
        File.WriteAllBytes(Path.Combine(output, ResultsFileName), resultsBytes);
        File.WriteAllBytes(Path.Combine(output, NormalizedReferencesFileName), normalizedBytes);
        File.WriteAllBytes(Path.Combine(output, SourceManifestFileName), sourceManifestBytes);
        File.WriteAllText(
            Path.Combine(output, ResultsHashFileName),
            $"{resultsHash}  {ResultsFileName}\n",
            new UTF8Encoding(false));
        return new ReproductionOutput(
            output,
            resultsHash,
            CanonicalJson.Sha256(normalizedBytes),
            CanonicalJson.Sha256(sourceManifestBytes));
    }
}

public sealed record ReproductionOutput(
    string OutputDirectory,
    string ResultsSha256,
    string NormalizedReferencesSha256,
    string CalculationSourceManifestSha256);
