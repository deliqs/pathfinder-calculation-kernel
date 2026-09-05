using System.Globalization;
using System.Reflection;
using PathFinder.CalculationKernel.Ephemeris;
using PathFinder.CalculationKernel.Provenance;

namespace PathFinder.CalculationKernel.Tests;

public class CalculationKernelMetadataTests
{
    [Fact]
    public void SourceManifestInput_IsOrderedDeterministicAndCommitIndependent()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var first = CalculationKernelMetadata.SourceManifestInput.ToArray();
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-EG");
            var second = CalculationKernelMetadata.SourceManifestInput.ToArray();

            Assert.Equal(first, second);
            Assert.Equal(first.OrderBy(item => item.Name, StringComparer.Ordinal), first);
            Assert.Equal("4", CalculationKernelMetadata.CalculationRevision);
            Assert.Contains(first, item => item is
                { Name: "runtimeDependency:CosineKitty.AstronomyEngine", Value: "2.1.19" });
            Assert.Contains(first, item => item is
                { Name: "runtimeDependency:NodaTime", Value: "3.3.3" });
            Assert.DoesNotContain(first, item =>
                item.Name.Contains("commit", StringComparison.OrdinalIgnoreCase) ||
                item.Value.Contains("commit", StringComparison.OrdinalIgnoreCase) ||
                item.Name.Contains("output", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void SourceManifestInput_RecordsLoadBearingCalculationConventions()
    {
        var metadata = CalculationKernelMetadata.SourceManifestInput
            .ToDictionary(item => item.Name, item => item.Value, StringComparer.Ordinal);

        Assert.Equal("14", metadata["bodyCount"]);
        Assert.Equal("mean", metadata["lunarNodeModel"]);
        Assert.Equal("mean-apogee", metadata["lilithModel"]);
        Assert.Equal("gravity-simulation:jpl-j2000-state", metadata["chironModel"]);
        Assert.Equal("23.4393", metadata["houseObliquityDegrees"]);
        Assert.Equal("truncated-gmst", metadata["siderealTimeModel"]);
        Assert.Equal("porphyry-above-60-degrees", metadata["placidusPolarFallback"]);
        Assert.Equal("porphyry-above-60-degrees", metadata["kochPolarFallback"]);
        Assert.Equal("porphyry-above-66-degrees", metadata["regiomontanusPolarFallback"]);
        Assert.Equal("porphyry-above-66-degrees", metadata["campanusPolarFallback"]);
        Assert.Equal("10", metadata["stationSampleMinutes"]);
    }

    [Fact]
    public void SourceManifestInput_IdentifiesVerifiedCurrentChironSeedWithoutCircularHash()
    {
        var metadata = CalculationKernelMetadata.SourceManifestInput
            .ToDictionary(item => item.Name, item => item.Value, StringComparer.Ordinal);

        Assert.Equal("current-jpl-solution-verified", metadata["chironSeedProvenanceStatus"]);
        Assert.Equal("jpl-horizons-chiron-seed-jpl171-j2000", metadata["chironSeedReferenceId"]);
        Assert.Equal("COMMAND='2060;'; JPL#171", metadata["chironSeedTarget"]);
        Assert.Equal("-3.529597323721606", metadata["chironSeedX"]);
        Assert.Equal("-8.675401114502414", metadata["chironSeedY"]);
        Assert.Equal("-2.935904700117773", metadata["chironSeedZ"]);
        Assert.Equal("0.004971227226758336", metadata["chironSeedVx"]);
        Assert.Equal("-0.003626418894486951", metadata["chironSeedVy"]);
        Assert.Equal("-0.0008257960206970693", metadata["chironSeedVz"]);
    }

    [Theory]
    [InlineData("InitX", -3.529597323721606)]
    [InlineData("InitY", -8.675401114502414)]
    [InlineData("InitZ", -2.935904700117773)]
    [InlineData("InitVx", 0.004971227226758336)]
    [InlineData("InitVy", -0.003626418894486951)]
    [InlineData("InitVz", -0.0008257960206970693)]
    public void ChironSeedComponent_MatchesCurrentJpl171RawVector(string fieldName, double expected)
    {
        var field = typeof(ChironCalculator).GetField(
            fieldName,
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(field);
        Assert.Equal(expected, Assert.IsType<double>(field.GetRawConstantValue()));
    }
}
