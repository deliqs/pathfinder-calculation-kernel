using System.Xml.Linq;
using Xunit;

namespace PathFinder.Repository.Tests;

public sealed partial class RepositoryBoundaryTests
{
    private static readonly IReadOnlyDictionary<string, string> ApprovedPackageVersions =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["CosineKitty.AstronomyEngine"] = "2.1.19",
            ["Microsoft.NET.Test.Sdk"] = "18.9.0",
            ["Microsoft.SourceLink.GitHub"] = "10.0.400",
            ["NodaTime"] = "3.3.3",
            ["PathFinder.CalculationKernel"] = "1.0.0",
            ["xunit"] = "2.9.3",
            ["xunit.runner.visualstudio"] = "4.0.0"
        };

    private static readonly string[] PrivateNamespaceFragments =
    [
        "PathFinder.Api",
        "PathFinder.AppHost",
        "PathFinder.Data",
        "PathFinder.Infrastructure",
        "PathFinder.Interpretation",
        "PathFinder.PublicApi"
    ];

    private static readonly HashSet<string> ApprovedRuntimePackages = new(StringComparer.OrdinalIgnoreCase)
    {
        "CosineKitty.AstronomyEngine",
        "NodaTime"
    };

    [Fact]
    public void PublicProjects_UseOnlyApprovedDependenciesAndNamespaces()
    {
        // Arrange
        var root = FindRepositoryRoot();
        var kernelProject = Path.Combine(
            root,
            "src",
            "PathFinder.CalculationKernel",
            "PathFinder.CalculationKernel.csproj");

        // Act
        var violations = InspectRepository(root, kernelProject);

        // Assert
        Assert.True(
            violations.Count == 0,
            $"Public repository boundary violations:{Environment.NewLine}{string.Join(Environment.NewLine, violations)}");
    }

    [Fact]
    public void PackageCatalog_PinsApprovedVersionsAndRuntimeLockGraph()
    {
        // Arrange
        var root = FindRepositoryRoot();
        var catalogPath = Path.Combine(root, "Directory.Packages.props");
        var kernelLockPath = Path.Combine(
            root,
            "src",
            "PathFinder.CalculationKernel",
            "packages.lock.json");

        // Act
        var violations = InspectPackageCatalog(root, catalogPath, kernelLockPath);

        // Assert
        Assert.True(
            violations.Count == 0,
            $"Package catalog violations:{Environment.NewLine}{string.Join(Environment.NewLine, violations)}");
    }

    [Fact]
    public void PublicationFiles_KeepThirdPartyAndWorkflowBoundariesExplicit()
    {
        // Arrange
        var root = FindRepositoryRoot();

        // Act
        var violations = InspectPublicationFiles(root);

        // Assert
        Assert.True(
            violations.Count == 0,
            $"Publication boundary violations:{Environment.NewLine}{string.Join(Environment.NewLine, violations)}");
    }

    [Fact]
    public void FindRepositoryRoot_GitWorktreeMarkerFile_ReturnsMarkedDirectory()
    {
        // Arrange
        var root = Directory.CreateTempSubdirectory("pathfinder-repository-root-");
        var nested = Directory.CreateDirectory(Path.Combine(root.FullName, "tests", "bin"));
        File.WriteAllText(Path.Combine(root.FullName, ".git"), "gitdir: ../worktrees/example");

        try
        {
            // Act
            var actual = FindRepositoryRoot(nested);

            // Assert
            Assert.Equal(root.FullName, actual);
        }
        finally
        {
            Directory.Delete(root.FullName, recursive: true);
        }
    }

    [Fact]
    public void PublicProjects_PrivateNamespacesInProductTests_ReportViolations()
    {
        // Arrange
        var root = Directory.CreateTempSubdirectory("pathfinder-boundary-scan-");
        var kernelDirectory = Directory.CreateDirectory(Path.Combine(root.FullName, "src", "PathFinder.CalculationKernel"));
        var kernelProject = Path.Combine(kernelDirectory.FullName, "PathFinder.CalculationKernel.csproj");
        File.WriteAllText(kernelProject, "<Project />");
        var kernelTests = Directory.CreateDirectory(Path.Combine(root.FullName, "tests", "PathFinder.CalculationKernel.Tests"));
        var benchmarkTests = Directory.CreateDirectory(Path.Combine(root.FullName, "tests", "PathFinder.AccuracyBenchmark.Tests"));
        File.WriteAllText(Path.Combine(kernelTests.FullName, "Forbidden.cs"), "using PathFinder.Data;");
        File.WriteAllText(Path.Combine(benchmarkTests.FullName, "Forbidden.cs"), "using PathFinder.Api;");

        try
        {
            // Act
            var violations = InspectRepository(root.FullName, kernelProject);

            // Assert
            Assert.Contains(violations, violation =>
                violation.Contains("PathFinder.Data", StringComparison.Ordinal) &&
                violation.Contains("tests/PathFinder.CalculationKernel.Tests/Forbidden.cs", StringComparison.Ordinal));
            Assert.Contains(violations, violation =>
                violation.Contains("PathFinder.Api", StringComparison.Ordinal) &&
                violation.Contains("tests/PathFinder.AccuracyBenchmark.Tests/Forbidden.cs", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(root.FullName, recursive: true);
        }
    }

    private static List<string> InspectRepository(string root, string kernelProject)
    {
        var violations = new List<string>();
        if (!File.Exists(kernelProject))
        {
            violations.Add($"Missing authoritative kernel project: {Relative(root, kernelProject)}");
            return violations;
        }

        foreach (var project in Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories))
        {
            var document = XDocument.Load(project);
            foreach (var reference in document.Descendants("PackageReference"))
            {
                var package = reference.Attribute("Include")?.Value;
                if (package is not null && !ApprovedPackageVersions.ContainsKey(package))
                {
                    violations.Add($"Unapproved package {package}: {Relative(root, project)}");
                }

                if (reference.Attribute("Version") is not null)
                {
                    violations.Add($"Package version must be central: {Relative(root, project)} -> {package}");
                }

                var relativeProject = Relative(root, project);
                var isProductProject = relativeProject.StartsWith("src/", StringComparison.Ordinal) ||
                    relativeProject.StartsWith("tools/", StringComparison.Ordinal);
                var condition = reference.Attribute("Condition")?.Value ?? reference.Parent?.Attribute("Condition")?.Value;
                var isPackedKernelVerificationReference =
                    relativeProject == "tools/PathFinder.AccuracyBenchmark/PathFinder.AccuracyBenchmark.csproj" &&
                    package == "PathFinder.CalculationKernel" &&
                    condition == "'$(UsePackedKernel)' == 'true'";
                var privateAssets = reference.Element("PrivateAssets")?.Value;
                if (isProductProject &&
                    package is not ("CosineKitty.AstronomyEngine" or "Microsoft.SourceLink.GitHub" or "NodaTime") &&
                    !isPackedKernelVerificationReference)
                {
                    violations.Add($"Product project references non-product package {package}: {Relative(root, project)}");
                }

                if (isProductProject && privateAssets is not null &&
                    !(package == "Microsoft.SourceLink.GitHub" && privateAssets.Equals("all", StringComparison.OrdinalIgnoreCase)))
                {
                    violations.Add($"Only Source Link may be a private product dependency: {Relative(root, project)} -> {package}");
                }
            }

            foreach (var reference in document.Descendants("ProjectReference"))
            {
                var include = reference.Attribute("Include")?.Value;
                if (include is null)
                {
                    continue;
                }

                var target = Path.GetFullPath(include, Path.GetDirectoryName(project)!);
                if (!target.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                {
                    violations.Add($"Project reference leaves repository: {Relative(root, project)} -> {include}");
                }
            }
        }

        var sourceDirectories = new[]
        {
            Path.Combine(root, "src"),
            Path.Combine(root, "tools"),
            Path.Combine(root, "tests", "PathFinder.CalculationKernel.Tests"),
            Path.Combine(root, "tests", "PathFinder.AccuracyBenchmark.Tests")
        };
        foreach (var path in sourceDirectories)
        {
            if (!Directory.Exists(path))
            {
                continue;
            }

            foreach (var source in Directory.EnumerateFiles(path, "*.cs", SearchOption.AllDirectories))
            {
                var contents = File.ReadAllText(source);
                foreach (var fragment in PrivateNamespaceFragments.Where(contents.Contains))
                {
                    violations.Add($"Private namespace {fragment}: {Relative(root, source)}");
                }
            }
        }

        return violations;
    }

    private static string FindRepositoryRoot() =>
        FindRepositoryRoot(new DirectoryInfo(AppContext.BaseDirectory));

    private static string FindRepositoryRoot(DirectoryInfo? directory)
    {
        while (directory is not null)
        {
            var marker = Path.Combine(directory.FullName, ".git");
            if (Directory.Exists(marker) || File.Exists(marker))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }

    private static string Relative(string root, string path) =>
        Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');
}
