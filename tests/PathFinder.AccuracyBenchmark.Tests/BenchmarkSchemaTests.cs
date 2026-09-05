using System.Text.Json;

namespace PathFinder.AccuracyBenchmark.Tests;

public sealed class BenchmarkSchemaTests
{
    [Theory]
    [InlineData("cases.schema.json", "PathFinder calculation benchmark cases")]
    [InlineData("reference-manifest.schema.json", "PathFinder benchmark reference manifest")]
    [InlineData("results.schema.json", "PathFinder calculation benchmark results")]
    public void Schemas_AreDraft202012AndClosedAtTheRoot(string file, string title)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(
            RepositoryPaths.File("benchmark", "schemas", file)));
        var root = document.RootElement;

        Assert.Equal("https://json-schema.org/draft/2020-12/schema", root.GetProperty("$schema").GetString());
        Assert.Equal(title, root.GetProperty("title").GetString());
        Assert.False(root.GetProperty("additionalProperties").GetBoolean());
    }

    [Fact]
    public void ResultsSchema_RequiresBothPositionTimeComparisonsAndDeltaTMetadata()
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(
            RepositoryPaths.File("benchmark", "schemas", "results.schema.json")));
        var position = document.RootElement.GetProperty("$defs").GetProperty("positionResult");
        var required = position.GetProperty("required").EnumerateArray()
            .Select(value => value.GetString())
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("nominalUtcErrorArcsec", required);
        Assert.Contains("matchedTtErrorArcsec", required);
        Assert.Contains("pathfinderDeltaTSeconds", required);
        Assert.Contains("horizonsNominalTimeType", required);
        Assert.Contains("horizonsMatchedTimeType", required);
    }

    [Fact]
    public void ResultsSchema_ClosesEveryNestedObjectAndRequiresEveryProperty()
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(
            RepositoryPaths.File("benchmark", "schemas", "results.schema.json")));
        var root = document.RootElement;
        var objects = new[]
        {
            root.GetProperty("properties").GetProperty("provenance"),
            root.GetProperty("properties").GetProperty("summary"),
            root.GetProperty("$defs").GetProperty("positionResult"),
            root.GetProperty("$defs").GetProperty("houseResult"),
            root.GetProperty("$defs").GetProperty("timingResult"),
            root.GetProperty("$defs").GetProperty("historicalResult")
        };

        foreach (var schema in objects)
        {
            Assert.False(schema.GetProperty("additionalProperties").GetBoolean());
            var required = schema.GetProperty("required").EnumerateArray()
                .Select(value => value.GetString()!)
                .ToHashSet(StringComparer.Ordinal);
            var properties = schema.GetProperty("properties").EnumerateObject()
                .Select(property => property.Name)
                .ToHashSet(StringComparer.Ordinal);
            Assert.Equal(properties, required);
        }
    }

    [Fact]
    public void ResultsSchema_DeclaresCanonicalPublicationPrecision()
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(
            RepositoryPaths.File("benchmark", "schemas", "results.schema.json")));
        var root = document.RootElement;
        var summary = root.GetProperty("properties").GetProperty("summary").GetProperty("properties");
        var position = root.GetProperty("$defs").GetProperty("positionResult").GetProperty("properties");
        var house = root.GetProperty("$defs").GetProperty("houseResult").GetProperty("properties");
        var timing = root.GetProperty("$defs").GetProperty("timingResult").GetProperty("properties");

        Assert.Equal(0.0000001m, MultipleOf(position, "pathfinderLongitudeDeg"));
        Assert.Equal(0.001m, MultipleOf(position, "nominalUtcErrorArcsec"));
        Assert.Equal(0.001m, MultipleOf(position, "matchedTtErrorArcsec"));
        Assert.Equal(0.001m, MultipleOf(position, "pathfinderDeltaTSeconds"));
        Assert.Equal(0.0000001m, MultipleOf(house, "pathfinderLongitudeDeg"));
        Assert.Equal(0.001m, MultipleOf(house, "absoluteErrorArcsec"));
        Assert.Equal(0.001m, MultipleOf(timing, "absoluteErrorMinutes"));
        Assert.Equal(0.001m, MultipleOf(summary, "medianNominalUtcPositionErrorArcsec"));
        Assert.Equal(0.001m, MultipleOf(summary, "maximumTimingErrorMinutes"));
    }

    [Fact]
    public void SchemaValidator_RejectsNumberOutsideDeclaredPublicationPrecision()
    {
        using var instance = JsonDocument.Parse("1.0001");
        using var schema = JsonDocument.Parse("""{"type":"number","multipleOf":0.001}""");

        Assert.Throws<InvalidDataException>(() =>
            TestJsonSchemaValidator.Validate(instance.RootElement, schema.RootElement));
    }

    [Fact]
    public void ReferenceSchema_BindsPurposeAndExpectedRowsToEachJplArtifact()
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(
            RepositoryPaths.File("benchmark", "schemas", "reference-manifest.schema.json")));
        var request = document.RootElement.GetProperty("$defs").GetProperty("jplRequest");
        var required = request.GetProperty("required").EnumerateArray()
            .Select(value => value.GetString())
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("purpose", required);
        Assert.Contains("expectedRowTimes", required);
    }

    [Fact]
    public void ReferenceSchema_RecordsSwissBuildExecutableAndEnvironmentProvenance()
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(
            RepositoryPaths.File("benchmark", "schemas", "reference-manifest.schema.json")));
        var swiss = document.RootElement.GetProperty("properties").GetProperty("swiss");
        var required = swiss.GetProperty("required").EnumerateArray()
            .Select(value => value.GetString())
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("sourceRepository", required);
        Assert.Contains("sourceTag", required);
        Assert.Contains("sourceCommit", required);
        Assert.Contains("buildTarget", required);
        Assert.Contains("buildCommand", required);
        Assert.Contains("executableSha256", required);
        Assert.Contains("environment", required);
        Assert.Equal(
            "175e1fcb3108bcd5c0d146c803f51dcf23508012",
            swiss.GetProperty("properties").GetProperty("sourceCommit").GetProperty("const").GetString());
        Assert.Equal(
            "8cb956985f8619174377a8aaa17245e5035838f867a1ec3ac284adf4821a8cf0",
            swiss.GetProperty("properties").GetProperty("executableSha256").GetProperty("const").GetString());
    }

    private static decimal MultipleOf(JsonElement properties, string name) =>
        properties.GetProperty(name).GetProperty("multipleOf").GetDecimal();
}
