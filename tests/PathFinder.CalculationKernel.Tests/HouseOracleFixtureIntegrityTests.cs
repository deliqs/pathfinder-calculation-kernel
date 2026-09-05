using System.Security.Cryptography;
using System.Text.Json;

namespace PathFinder.CalculationKernel.Tests;

public sealed class HouseOracleFixtureIntegrityTests
{
    [Fact]
    public void Fixtures_MatchTheirRecordedSha256Hashes()
    {
        var manifestPath = Path.Combine(
            AppContext.BaseDirectory,
            "TestData",
            "house-oracle-provenance.json");
        using var manifest = JsonDocument.Parse(File.ReadAllBytes(manifestPath));

        foreach (var fixture in manifest.RootElement.GetProperty("fixtures").EnumerateArray())
        {
            var fileName = fixture.GetProperty("fileName").GetString()!;
            var expectedHash = fixture.GetProperty("sha256").GetString();
            var path = Path.Combine(AppContext.BaseDirectory, "TestData", fileName);
            var actualHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

            Assert.Equal(expectedHash, actualHash);
        }
    }
}
