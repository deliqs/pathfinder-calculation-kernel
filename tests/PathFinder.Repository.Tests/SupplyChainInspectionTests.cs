using System.Text.Json;
using System.Xml.Linq;
using Xunit;

namespace PathFinder.Repository.Tests;

public sealed partial class RepositoryBoundaryTests
{
    [Fact]
    public void SupplyChainProofs_PinToolsAndExerciseThePackedKernel()
    {
        // Arrange
        var root = FindRepositoryRoot();

        // Act
        var violations = InspectSupplyChainProofs(root);

        // Assert
        Assert.True(
            violations.Count == 0,
            $"Supply-chain proof violations:{Environment.NewLine}{string.Join(Environment.NewLine, violations)}");
    }

    private static List<string> InspectSupplyChainProofs(string root)
    {
        var violations = new List<string>();
        InspectPackedKernelConsumers(root, violations);
        InspectSbomTool(root, violations);
        InspectSecurityWorkflows(root, violations);
        return violations;
    }

    private static void InspectPackedKernelConsumers(string root, List<string> violations)
    {
        const string packageName = "PathFinder.CalculationKernel";
        var catalogPath = Path.Combine(root, "Directory.Packages.props");
        var packageVersion = XDocument.Load(catalogPath)
            .Descendants("PackageVersion")
            .SingleOrDefault(element => element.Attribute("Include")?.Value == packageName)
            ?.Attribute("Version")?.Value;
        if (packageVersion != "1.0.0")
        {
            violations.Add($"Expected {packageName} package-consumption version 1.0.0; found {packageVersion ?? "missing"}");
        }

        var consumers = new[]
        {
            "tools/PathFinder.AccuracyBenchmark/PathFinder.AccuracyBenchmark.csproj",
            "tests/PathFinder.CalculationKernel.Tests/PathFinder.CalculationKernel.Tests.csproj"
        };
        foreach (var relativePath in consumers)
        {
            var project = XDocument.Load(Path.Combine(root, relativePath));
            var projectReference = project.Descendants("ProjectReference")
                .SingleOrDefault(element => element.Attribute("Include")?.Value.Contains(packageName, StringComparison.Ordinal) == true);
            var packageReference = project.Descendants("PackageReference")
                .SingleOrDefault(element => element.Attribute("Include")?.Value == packageName);

            if (EffectiveCondition(projectReference) != "'$(UsePackedKernel)' != 'true'")
            {
                violations.Add($"Source reference is not limited to normal builds: {relativePath}");
            }

            if (EffectiveCondition(packageReference) != "'$(UsePackedKernel)' == 'true'")
            {
                violations.Add($"Packed-kernel reference is not limited to package verification: {relativePath}");
            }
        }
    }

    private static string? EffectiveCondition(XElement? element) =>
        element?.Attribute("Condition")?.Value ?? element?.Parent?.Attribute("Condition")?.Value;

    private static void InspectSbomTool(string root, List<string> violations)
    {
        var manifestPath = Path.Combine(root, ".config", "dotnet-tools.json");
        if (!File.Exists(manifestPath))
        {
            violations.Add("Missing pinned .NET tool manifest for SBOM generation");
            return;
        }

        using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        if (!manifest.RootElement.GetProperty("tools").TryGetProperty(
                "microsoft.sbom.dotnettool",
                out var sbomTool))
        {
            violations.Add("Tool manifest omits Microsoft.Sbom.DotNetTool");
            return;
        }

        if (sbomTool.GetProperty("version").GetString() != "4.1.5")
        {
            violations.Add("Microsoft.Sbom.DotNetTool must be pinned to 4.1.5");
        }

        if (sbomTool.GetProperty("rollForward").GetBoolean())
        {
            violations.Add("Microsoft.Sbom.DotNetTool must not roll forward");
        }
    }

    private static void InspectSecurityWorkflows(string root, List<string> violations)
    {
        var requirements = new Dictionary<string, string[]>
        {
            ["codeql.yml"] =
            [
                "github/codeql-action/init@cdf488f595d80d6e07e03d4674febd5ab45fa938",
                "github/codeql-action/analyze@cdf488f595d80d6e07e03d4674febd5ab45fa938",
                "build-mode: manual",
                "  staging-disclosure:\n    if: ${{ github.event.repository.private }}",
                "CodeQL analysis is deferred while this staging repository is private",
                "  analyze:\n    if: ${{ !github.event.repository.private }}",
                "    permissions:\n      actions: read\n      contents: read\n      security-events: write",
                "security-events: write"
            ],
            ["dependency-review.yml"] =
            [
                "actions/dependency-review-action@a1d282b36b6f3519aa1f3fc636f609c47dddb294",
                "fail-on-severity: moderate"
            ],
            ["secret-scan.yml"] =
            [
                "trufflesecurity/trufflehog@0c952ace0f842f11c75775922d7400335cf60bc6",
                "version: 3.97.1",
                "--results=verified,unknown"
            ]
        };

        foreach (var requirement in requirements)
        {
            var relativePath = $".github/workflows/{requirement.Key}";
            var path = Path.Combine(root, relativePath);
            if (!File.Exists(path))
            {
                violations.Add($"Missing security workflow: {relativePath}");
                continue;
            }

            var contents = File.ReadAllText(path);
            foreach (var marker in requirement.Value.Where(marker => !contents.Contains(marker, StringComparison.Ordinal)))
            {
                violations.Add($"Security workflow {relativePath} omits: {marker}");
            }
        }
    }
}
