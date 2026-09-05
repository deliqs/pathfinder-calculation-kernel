using System.Xml.Linq;
using Xunit;

namespace PathFinder.Repository.Tests;

public sealed partial class RepositoryBoundaryTests
{
    [Fact]
    public void CertificationWorkflow_UsesIsolatedExactPackageAndSupplyChainProofs()
    {
        // Arrange
        var root = FindRepositoryRoot();

        // Act
        var violations = InspectCertificationWorkflow(root);

        // Assert
        Assert.True(
            violations.Count == 0,
            $"Certification workflow violations:{Environment.NewLine}{string.Join(Environment.NewLine, violations)}");
    }

    private static List<string> InspectCertificationWorkflow(string root)
    {
        var violations = new List<string>();
        InspectPackedPackageConfiguration(root, violations);
        InspectCertificationAction(root, violations);
        InspectCertificationCallers(root, violations);
        InspectAuditConfiguration(root, violations);
        return violations;
    }

    private static void InspectPackedPackageConfiguration(string root, List<string> violations)
    {
        const string packageName = "PathFinder.CalculationKernel";
        var configPath = Path.Combine(root, "NuGet.Packed.config");
        if (!File.Exists(configPath))
        {
            violations.Add("Missing NuGet.Packed.config for local package-consumption verification");
            return;
        }

        var config = XDocument.Load(configPath);
        var sources = config.Descendants("packageSources").Elements("add")
            .ToDictionary(
                element => element.Attribute("key")?.Value ?? string.Empty,
                element => element.Attribute("value")?.Value ?? string.Empty,
                StringComparer.Ordinal);
        if (!sources.TryGetValue("local-packages", out var localSource) || localSource != "./artifacts/packages")
        {
            violations.Add("Packed verification must use ./artifacts/packages as local-packages");
        }

        var mappings = config.Descendants("packageSourceMapping").Elements("packageSource")
            .ToDictionary(
                element => element.Attribute("key")?.Value ?? string.Empty,
                element => element.Elements("package")
                    .Select(package => package.Attribute("pattern")?.Value ?? string.Empty)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase),
                StringComparer.Ordinal);
        if (!mappings.TryGetValue("local-packages", out var localPatterns) ||
            !localPatterns.SetEquals([packageName]))
        {
            violations.Add($"Only {packageName} may resolve from local-packages");
        }

        if (mappings.TryGetValue("nuget.org", out var publicPatterns) &&
            publicPatterns.Contains(packageName))
        {
            violations.Add($"{packageName} must not resolve from nuget.org during packed verification");
        }

        foreach (var relativePath in new[]
                 {
                     "tools/PathFinder.AccuracyBenchmark/PathFinder.AccuracyBenchmark.csproj",
                     "tests/PathFinder.CalculationKernel.Tests/PathFinder.CalculationKernel.Tests.csproj"
                 })
        {
            var project = XDocument.Load(Path.Combine(root, relativePath));
            var packageReference = project.Descendants("PackageReference")
                .SingleOrDefault(element => element.Attribute("Include")?.Value == packageName);
            if (packageReference?.Attribute("VersionOverride")?.Value != "[$(Version)]")
            {
                violations.Add($"Packed verification must request the exact build version: {relativePath}");
            }
        }
    }

    private static void InspectCertificationAction(string root, List<string> violations)
    {
        var actionPath = Path.Combine(root, ".github", "actions", "certify", "action.yml");
        if (!File.Exists(actionPath))
        {
            violations.Add("Missing reusable certification action");
            return;
        }

        var action = File.ReadAllText(actionPath);
        foreach (var marker in new[]
                 {
                     "--locked-mode -warnaserror",
                     "--no-restore -c Release -warnaserror --no-incremental",
                     "dotnet test PathFinder.CalculationKernel.slnx --no-build -c Release",
                     "dotnet pack src/PathFinder.CalculationKernel/PathFinder.CalculationKernel.csproj",
                     "reproduce --offline",
                     "artifacts/reproduction/source-1",
                     "artifacts/reproduction/source-2",
                     "artifacts/reproduction/packed",
                     "cmp -s",
                     "sha256sum",
                     "benchmark/results/results.sha256",
                     "cmp -s benchmark/results/results.sha256",
                     "cmp -s benchmark/references/normalized.json",
                     "cmp -s benchmark/provenance/calculation-source-manifest.json",
                     "normalized-references.json",
                     "calculation-source-manifest.json",
                     "NuGet.Packed.config",
                     "UsePackedKernel=true",
                     "NUGET_PACKAGES=$PACKED_NUGET_CACHE",
                     "RestorePackagesPath=\"$PACKED_NUGET_CACHE\"",
                     "RESTORED_NUPKG=",
                     "cmp -s \"$PACKED_NUPKG\" \"$RESTORED_NUPKG\"",
                     "PACKED_INTERMEDIATE_ROOT=$(mktemp -d \"$RUNNER_TEMP/pathfinder-packed-obj.XXXXXX\")",
                     "PACKED_OUTPUT_ROOT=$(mktemp -d \"$RUNNER_TEMP/pathfinder-packed-bin.XXXXXX\")",
                     "BaseIntermediateOutputPath=\"$PACKED_INTERMEDIATE_ROOT/kernel-tests/\"",
                     "BaseIntermediateOutputPath=\"$PACKED_INTERMEDIATE_ROOT/benchmark/\"",
                     "BaseOutputPath=\"$PACKED_OUTPUT_ROOT/kernel-tests/\"",
                     "BaseOutputPath=\"$PACKED_OUTPUT_ROOT/benchmark/\"",
                     "DefaultItemExcludes='obj/**'",
                     "Verify source-mode assets remain usable",
                     "dotnet tool restore",
                     "dotnet sbom-tool generate",
                     "dotnet sbom-tool validate",
                     "-mi spdx:2.2",
                     "-o artifacts/release/sbom-validation.json",
                     "SHA256SUMS"
                 })
        {
            if (!action.Contains(marker, StringComparison.Ordinal))
            {
                violations.Add($"Certification action omits: {marker}");
            }
        }

        if (action.Contains("-gt 1970-01-01T00:00:00Z", StringComparison.Ordinal))
        {
            violations.Add("Certification action must not publish a fabricated SBOM generation timestamp");
        }

        if (action.Contains("BaseIntermediateOutputPath=obj/", StringComparison.Ordinal))
        {
            violations.Add("Packed intermediates must live outside project source trees");
        }
    }

    private static void InspectCertificationCallers(string root, List<string> violations)
    {
        foreach (var workflowName in new[] { "ci.yml", "release.yml" })
        {
            var workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", workflowName));
            if (!workflow.Contains("uses: ./.github/actions/certify", StringComparison.Ordinal))
            {
                violations.Add($"{workflowName} does not invoke the reusable certification action");
            }

            if (!workflow.Contains("artifacts/release", StringComparison.Ordinal))
            {
                violations.Add($"{workflowName} does not publish the complete certification bundle");
            }
        }
    }

    private static void InspectAuditConfiguration(string root, List<string> violations)
    {
        var properties = XDocument.Load(Path.Combine(root, "Directory.Build.props"));
        var expected = new Dictionary<string, string>
        {
            ["NuGetAudit"] = "true",
            ["NuGetAuditLevel"] = "low",
            ["NuGetAuditMode"] = "all"
        };
        foreach (var property in expected)
        {
            var actual = properties.Descendants(property.Key).SingleOrDefault()?.Value;
            if (actual != property.Value)
            {
                violations.Add($"Expected {property.Key}={property.Value}; found {actual ?? "missing"}");
            }
        }
    }
}
