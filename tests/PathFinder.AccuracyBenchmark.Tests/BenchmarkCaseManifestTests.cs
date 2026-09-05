using System.Text.Json;
using PathFinder.AccuracyBenchmark.Cases;

namespace PathFinder.AccuracyBenchmark.Tests;

public sealed class BenchmarkCaseManifestTests
{
    [Fact]
    public void Load_FrozenCases_AreIndependentInputsWithCompleteCoverage()
    {
        var path = RepositoryPaths.File("benchmark", "cases", "cases.json");

        var manifest = BenchmarkCaseManifestLoader.Load(File.ReadAllBytes(path));

        Assert.Equal(2, manifest.SchemaVersion);
        Assert.Equal("calculation-benchmark-4", manifest.DatasetRevision);
        Assert.Equal(44, manifest.Positions.Count);
        Assert.Equal(4, manifest.Houses.Count);
        Assert.Equal(6, manifest.Timings.Count);
        Assert.Equal(3, manifest.HistoricalTimes.Count);
        Assert.Equal(11, manifest.Positions.Select(row => row.Body).Distinct().Count());
        Assert.All(manifest.Positions, row => Assert.False(string.IsNullOrWhiteSpace(row.HorizonsTarget)));
    }

    [Fact]
    public void FrozenCases_ContainNoExpectedOrGeneratedResultProperties()
    {
        var path = RepositoryPaths.File("benchmark", "cases", "cases.json");
        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        var forbidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "expected",
            "referenceUtc",
            "referenceLongitudeDeg",
            "jplLongitudeDeg",
            "swissLongitudeDeg",
            "pathfinderUtc",
            "pathfinderLongitudeDeg",
            "error",
            "passed"
        };

        var properties = DescendantPropertyNames(document.RootElement).ToArray();

        Assert.DoesNotContain(properties, forbidden.Contains);
    }

    [Fact]
    public void Load_TimingCases_RequireBodyTargetMotionWindowAndMethodFields()
    {
        var json = """
            {
              "schemaVersion": 2,
              "datasetRevision": "test",
              "positions": { "bodies": [], "epochs": [] },
              "houses": [],
              "timings": [{
                "id": "missing-motion",
                "kind": "longitude-crossing",
                "body": "Mercury",
                "targetLongitudeDeg": 20,
                "searchStartUtc": "2024-03-15T00:00:00Z",
                "searchWindowDays": 15,
                "method": "longitude-crossing"
              }],
              "historicalTimes": []
            }
            """;

        var error = Assert.Throws<InvalidDataException>(() =>
            BenchmarkCaseManifestLoader.Load(System.Text.Encoding.UTF8.GetBytes(json)));

        Assert.Contains("motion", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_UnknownProperty_FailsClosed()
    {
        var json = """
            {
              "schemaVersion": 2,
              "datasetRevision": "test",
              "positions": { "bodies": [], "epochs": [] },
              "houses": [],
              "timings": [],
              "historicalTimes": [],
              "expectedResults": []
            }
            """;

        Assert.Throws<InvalidDataException>(() =>
            BenchmarkCaseManifestLoader.Load(System.Text.Encoding.UTF8.GetBytes(json)));
    }

    [Fact]
    public void Load_ChironUsesResolvableExactNumericSmallBodyCommand()
    {
        var manifest = BenchmarkCaseManifestLoader.Load(File.ReadAllBytes(
            RepositoryPaths.File("benchmark", "cases", "cases.json")));

        var chiron = manifest.Positions.First(row => row.Body == "Chiron");

        Assert.Equal("2060;", chiron.HorizonsCommand);
        Assert.Equal("2060", chiron.HorizonsTargetId);
        Assert.Equal(
            "Target body name: 2060 Chiron (1977 UB) {source: JPL#171}",
            chiron.HorizonsTargetHeader);
    }

    [Fact]
    public void Load_FrozenPlanetaryTargetsPinCurrentResolvedJplSources()
    {
        var manifest = BenchmarkCaseManifestLoader.Load(File.ReadAllBytes(
            RepositoryPaths.File("benchmark", "cases", "cases.json")));
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Mars"] = "Target body name: Mars (499) {source: mar099}",
            ["Jupiter"] = "Target body name: Jupiter (599) {source: jup365_merged}",
            ["Saturn"] = "Target body name: Saturn (699) {source: sat441l}",
            ["Uranus"] = "Target body name: Uranus (799) {source: ura184_merged}",
            ["Neptune"] = "Target body name: Neptune (899) {source: nep098_merged}",
            ["Pluto"] = "Target body name: Pluto (999) {source: plu060_merged}"
        };

        foreach (var pair in expected)
        {
            Assert.Equal(pair.Value, manifest.Positions.First(row => row.Body == pair.Key).HorizonsTargetHeader);
        }
    }

    [Fact]
    public void Load_HorizonsNominalCalendarDoesNotExactlyMatchUtc_Throws()
    {
        var json = File.ReadAllText(RepositoryPaths.File("benchmark", "cases", "cases.json"))
            .Replace(
                "1950-Jan-01 00:00:00.000",
                "1950-Jan-01 00:00:01.000",
                StringComparison.Ordinal);

        var error = Assert.Throws<InvalidDataException>(() =>
            BenchmarkCaseManifestLoader.Load(System.Text.Encoding.UTF8.GetBytes(json)));

        Assert.Contains("horizonsUt", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("kernel-longitude-crossing", "unspecified-crossing")]
    [InlineData("kernel-station-parabolic-10-minute", "unspecified-station")]
    public void Load_TimingMethodDoesNotMatchFrozenKindContract_Throws(
        string expected,
        string replacement)
    {
        var json = File.ReadAllText(RepositoryPaths.File("benchmark", "cases", "cases.json"))
            .Replace(expected, replacement, StringComparison.Ordinal);

        var error = Assert.Throws<InvalidDataException>(() =>
            BenchmarkCaseManifestLoader.Load(System.Text.Encoding.UTF8.GetBytes(json)));

        Assert.Contains("method", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_StationMotionDoesNotMatchExtremum_Throws()
    {
        var json = File.ReadAllText(RepositoryPaths.File("benchmark", "cases", "cases.json"))
            .Replace(
                "\"motion\": \"direct\",\n      \"targetLongitudeDeg\": null,\n      \"extremum\": \"minimum\"",
                "\"motion\": \"retrograde\",\n      \"targetLongitudeDeg\": null,\n      \"extremum\": \"minimum\"",
                StringComparison.Ordinal);

        var error = Assert.Throws<InvalidDataException>(() =>
            BenchmarkCaseManifestLoader.Load(System.Text.Encoding.UTF8.GetBytes(json)));

        Assert.Contains("extremum", error.Message, StringComparison.Ordinal);
    }

    private static IEnumerable<string> DescendantPropertyNames(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                yield return property.Name;
                foreach (var nested in DescendantPropertyNames(property.Value))
                {
                    yield return nested;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                foreach (var nested in DescendantPropertyNames(item))
                {
                    yield return nested;
                }
            }
        }
    }
}
