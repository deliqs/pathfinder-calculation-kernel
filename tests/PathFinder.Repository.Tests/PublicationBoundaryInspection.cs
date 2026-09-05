using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace PathFinder.Repository.Tests;

public sealed partial class RepositoryBoundaryTests
{
    private static List<string> InspectPackageCatalog(
        string root,
        string catalogPath,
        string kernelLockPath)
    {
        var violations = new List<string>();
        if (!File.Exists(catalogPath))
        {
            violations.Add("Missing Directory.Packages.props");
        }
        else
        {
            var actualVersions = XDocument.Load(catalogPath)
                .Descendants("PackageVersion")
                .ToDictionary(
                    element => element.Attribute("Include")!.Value,
                    element => element.Attribute("Version")!.Value,
                    StringComparer.OrdinalIgnoreCase);

            foreach (var expected in ApprovedPackageVersions)
            {
                if (!actualVersions.TryGetValue(expected.Key, out var actual) || actual != expected.Value)
                {
                    violations.Add($"Expected {expected.Key} {expected.Value}; found {actual ?? "missing"}");
                }
            }

            foreach (var package in actualVersions.Keys.Except(ApprovedPackageVersions.Keys, StringComparer.OrdinalIgnoreCase))
            {
                violations.Add($"Unapproved centrally managed package: {package}");
            }
        }

        var buildPropertiesPath = Path.Combine(root, "Directory.Build.props");
        var buildProperties = XDocument.Load(buildPropertiesPath);
        var version = buildProperties.Descendants("Version").SingleOrDefault()?.Value;
        if (version != "0.1.0")
        {
            violations.Add($"Expected assembly and package version 0.1.0; found {version ?? "missing"}");
        }

        if (!File.Exists(kernelLockPath))
        {
            violations.Add($"Missing runtime lock file: {Relative(root, kernelLockPath)}");
            return violations;
        }

        using var lockDocument = JsonDocument.Parse(File.ReadAllText(kernelLockPath));
        var dependencies = lockDocument.RootElement.GetProperty("dependencies").EnumerateObject().Single().Value;
        var lockedPackages = dependencies.EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value, StringComparer.OrdinalIgnoreCase);

        foreach (var dependency in lockedPackages)
        {
            if (dependency.Value.GetProperty("type").GetString() != "Direct")
            {
                continue;
            }

            if (!ApprovedPackageVersions.TryGetValue(dependency.Key, out var expected))
            {
                violations.Add($"Unapproved direct dependency in lock file: {dependency.Key}");
                continue;
            }

            var resolved = dependency.Value.GetProperty("resolved").GetString();
            if (resolved != expected)
            {
                violations.Add($"Locked {dependency.Key} version {resolved} does not equal {expected}");
            }
        }

        InspectRuntimeClosure(root, lockedPackages, violations);

        return violations;
    }

    private static void InspectRuntimeClosure(
        string root,
        IReadOnlyDictionary<string, JsonElement> lockedPackages,
        List<string> violations)
    {
        var projectPath = Path.Combine(root, "src", "PathFinder.CalculationKernel", "PathFinder.CalculationKernel.csproj");
        var project = XDocument.Load(projectPath);
        var runtimeRoots = project.Descendants("PackageReference")
            .Where(reference => !string.Equals(reference.Element("PrivateAssets")?.Value, "all", StringComparison.OrdinalIgnoreCase))
            .Select(reference => reference.Attribute("Include")!.Value);
        var remaining = new Queue<string>(runtimeRoots);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (remaining.TryDequeue(out var package))
        {
            if (!visited.Add(package))
            {
                continue;
            }

            if (!ApprovedRuntimePackages.Contains(package))
            {
                violations.Add($"Unapproved runtime dependency in lock graph: {package}");
            }

            if (!lockedPackages.TryGetValue(package, out var locked) ||
                !locked.TryGetProperty("dependencies", out var children))
            {
                continue;
            }

            foreach (var child in children.EnumerateObject())
            {
                remaining.Enqueue(child.Name);
            }
        }
    }

    private static List<string> InspectPublicationFiles(string root)
    {
        var violations = new List<string>();
        var requiredFiles = new[]
        {
            "AGENTS.md",
            "CITATION.cff",
            "LICENSE",
            "README.md",
            "SECURITY.md",
            "THIRD_PARTY_NOTICES.md",
            "LICENSES/AstronomyEngine-MIT.txt",
            "LICENSES/NodaTime-Apache-2.0.txt",
            "LICENSES/NodaTime-NOTICE.txt",
            ".github/CODEOWNERS",
            ".github/dependabot.yml",
            ".github/workflows/ci.yml",
            ".github/workflows/release.yml",
            ".github/workflows/refresh-references.yml"
        };

        foreach (var relativePath in requiredFiles)
        {
            if (!File.Exists(Path.Combine(root, relativePath)))
            {
                violations.Add($"Missing publication file: {relativePath}");
            }
        }

        var noticesPath = Path.Combine(root, "THIRD_PARTY_NOTICES.md");
        if (File.Exists(noticesPath))
        {
            var notices = File.ReadAllText(noticesPath);
            foreach (var requiredTerm in new[]
                     { "Astronomy Engine", "Noda Time", "JPL", "IANA", "Swiss Ephemeris", "pyswisseph" })
            {
                if (!notices.Contains(requiredTerm, StringComparison.Ordinal))
                {
                    violations.Add($"THIRD_PARTY_NOTICES.md omits {requiredTerm}");
                }
            }

            foreach (var requiredSource in new[]
                     {
                         "https://www.gnu.org/licenses/agpl-3.0.html",
                         "https://www.gnu.org/licenses/gpl-faq.html#WhatCaseIsOutputGPL"
                     })
            {
                if (!notices.Contains(requiredSource, StringComparison.Ordinal))
                {
                    violations.Add($"THIRD_PARTY_NOTICES.md omits licensing source {requiredSource}");
                }
            }

            if (notices.Contains("redistribution status must be reviewed", StringComparison.OrdinalIgnoreCase))
            {
                violations.Add("THIRD_PARTY_NOTICES.md leaves Swiss output redistribution unresolved");
            }

            foreach (var requiredSwissDescription in new[]
                     {
                         "four archived Swiss stdout files",
                         "one minimal derived version-identification line"
                     })
            {
                if (!notices.Contains(requiredSwissDescription, StringComparison.Ordinal))
                {
                    violations.Add($"THIRD_PARTY_NOTICES.md omits Swiss artifact description: {requiredSwissDescription}");
                }
            }

            if (notices.Contains("five archived Swiss stdout files", StringComparison.OrdinalIgnoreCase))
            {
                violations.Add("THIRD_PARTY_NOTICES.md misclassifies the derived Swiss version identification as raw stdout");
            }
        }

        var kernelProjectPath = Path.Combine(root, "src", "PathFinder.CalculationKernel", "PathFinder.CalculationKernel.csproj");
        if (File.Exists(kernelProjectPath))
        {
            var packedFiles = XDocument.Load(kernelProjectPath)
                .Descendants("None")
                .Where(element => element.Attribute("Pack")?.Value == "true")
                .Select(element => element.Attribute("Include")?.Value)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var required in new[] { "../../LICENSE", "../../LICENSES/*", "../../THIRD_PARTY_NOTICES.md" })
            {
                if (!packedFiles.Contains(required))
                {
                    violations.Add($"Kernel package omits notice material: {required}");
                }
            }
        }

        InspectWorkflows(root, violations);
        return violations;
    }

    private static void InspectWorkflows(string root, List<string> violations)
    {
        var actionPattern = new Regex(@"uses:\s+[^\s@]+@(?<revision>[^\s#]+)", RegexOptions.CultureInvariant);
        var workflowsDirectory = Path.Combine(root, ".github", "workflows");
        if (!Directory.Exists(workflowsDirectory))
        {
            return;
        }

        foreach (var workflow in Directory.EnumerateFiles(workflowsDirectory, "*.yml"))
        {
            var contents = File.ReadAllText(workflow);
            if (!contents.Contains("permissions:\n  contents: read", StringComparison.Ordinal))
            {
                violations.Add($"Workflow lacks read-only default permissions: {Relative(root, workflow)}");
            }

            if (contents.Contains("pull_request_target", StringComparison.Ordinal))
            {
                violations.Add($"Workflow uses pull_request_target: {Relative(root, workflow)}");
            }

            foreach (Match match in actionPattern.Matches(contents))
            {
                if (!Regex.IsMatch(match.Groups["revision"].Value, "^[0-9a-f]{40}$", RegexOptions.CultureInvariant))
                {
                    violations.Add($"Action is not pinned to a full commit: {match.Value}");
                }
            }
        }

        var refreshPath = Path.Combine(workflowsDirectory, "refresh-references.yml");
        if (!File.Exists(refreshPath))
        {
            return;
        }

        var refresh = File.ReadAllText(refreshPath);
        foreach (var forbidden in new[] { "contents: write", "packages: write", "git push", "nuget push", "gh release" })
        {
            if (refresh.Contains(forbidden, StringComparison.OrdinalIgnoreCase))
            {
                violations.Add($"Refresh workflow contains forbidden capability: {forbidden}");
            }
        }
    }
}
