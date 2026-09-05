using System.Text.Json;
using PathFinder.AccuracyBenchmark.Reproduction;

namespace PathFinder.AccuracyBenchmark.Tests;

public sealed class PublishedBenchmarkTests
{
    [Fact]
    public void Reproduce_FrozenInputs_MatchesPublishedArtifactsAndResultsSchema()
    {
        var output = RepositoryPaths.File($"pathfinder-published-proof-{Guid.NewGuid():N}");
        try
        {
            OfflineBenchmarkReproducer.Reproduce(RepositoryPaths.File(), output);
            AssertPublishedBytes(output, "results.json", "benchmark", "results", "results.json");
            AssertPublishedBytes(output, "results.sha256", "benchmark", "results", "results.sha256");
            AssertPublishedBytes(
                output,
                "normalized-references.json",
                "benchmark",
                "references",
                "normalized.json");
            AssertPublishedBytes(
                output,
                "calculation-source-manifest.json",
                "benchmark",
                "provenance",
                "calculation-source-manifest.json");
            using var schema = JsonDocument.Parse(File.ReadAllBytes(
                RepositoryPaths.File("benchmark", "schemas", "results.schema.json")));
            using var generated = JsonDocument.Parse(File.ReadAllBytes(
                Path.Combine(output, "results.json")));
            using var published = JsonDocument.Parse(File.ReadAllBytes(
                RepositoryPaths.File("benchmark", "results", "results.json")));
            TestJsonSchemaValidator.Validate(generated.RootElement, schema.RootElement);
            TestJsonSchemaValidator.Validate(published.RootElement, schema.RootElement);
            using var sourceManifest = JsonDocument.Parse(File.ReadAllBytes(
                RepositoryPaths.File("benchmark", "provenance", "calculation-source-manifest.json")));
            var sourceFiles = sourceManifest.RootElement.GetProperty("files").EnumerateArray().ToArray();
            Assert.NotEmpty(sourceFiles);
            Assert.All(sourceFiles, file => Assert.Equal(
                file.GetProperty("sha256").GetString(),
                PathFinder.AccuracyBenchmark.References.Sha256Verifier.Hash(File.ReadAllBytes(
                    RepositoryPaths.File(file.GetProperty("path").GetString()!.Split('/'))))));
        }
        finally
        {
            if (Directory.Exists(output))
            {
                Directory.Delete(output, true);
            }
        }
    }

    private static void AssertPublishedBytes(
        string output,
        string generatedName,
        params string[] publishedSegments) => Assert.Equal(
        File.ReadAllBytes(RepositoryPaths.File(publishedSegments)),
        File.ReadAllBytes(Path.Combine(output, generatedName)));
}
