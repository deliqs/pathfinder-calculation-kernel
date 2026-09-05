using System.Text.Json;
using PathFinder.AccuracyBenchmark.Reproduction;

namespace PathFinder.AccuracyBenchmark.Tests;

public sealed class OfflineBenchmarkReproducerTests
{
    [Fact]
    public void Reproduce_FrozenBundle_WritesCompleteCanonicalOutputs()
    {
        var output = NewOutputDirectory();
        try
        {
            OfflineBenchmarkReproducer.Reproduce(RepositoryPaths.File(), output);

            var resultsPath = Path.Combine(output, "results.json");
            var normalizedPath = Path.Combine(output, "normalized-references.json");
            var hashPath = Path.Combine(output, "results.sha256");
            Assert.True(File.Exists(resultsPath));
            Assert.True(File.Exists(normalizedPath));
            Assert.True(File.Exists(hashPath));
            using var document = JsonDocument.Parse(File.ReadAllBytes(resultsPath));
            Assert.Equal(44, document.RootElement.GetProperty("positions").GetArrayLength());
            Assert.Equal(48, document.RootElement.GetProperty("houseCusps").GetArrayLength());
            Assert.Equal(6, document.RootElement.GetProperty("timings").GetArrayLength());
            Assert.Equal(3, document.RootElement.GetProperty("historicalTimes").GetArrayLength());
            Assert.Equal(
                $"{Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(resultsPath)))}  results.json\n",
                File.ReadAllText(hashPath));
        }
        finally
        {
            DeleteOutput(output);
        }
    }

    [Fact]
    public void Reproduce_Twice_ProducesByteIdenticalOutputs()
    {
        var first = NewOutputDirectory();
        var second = NewOutputDirectory();
        try
        {
            OfflineBenchmarkReproducer.Reproduce(RepositoryPaths.File(), first);
            OfflineBenchmarkReproducer.Reproduce(RepositoryPaths.File(), second);

            foreach (var file in new[]
                     {
                         "results.json",
                         "normalized-references.json",
                         "calculation-source-manifest.json",
                         "results.sha256"
                     })
            {
                Assert.Equal(
                    File.ReadAllBytes(Path.Combine(first, file)),
                    File.ReadAllBytes(Path.Combine(second, file)));
            }
        }
        finally
        {
            DeleteOutput(first);
            DeleteOutput(second);
        }
    }

    private static string NewOutputDirectory()
    {
        var path = RepositoryPaths.File($"pathfinder-offline-{Guid.NewGuid():N}");
        Assert.False(Directory.Exists(path));
        return path;
    }

    private static void DeleteOutput(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, true);
        }
    }
}
