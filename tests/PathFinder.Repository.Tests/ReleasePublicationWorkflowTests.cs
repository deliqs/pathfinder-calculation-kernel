using System.Text.RegularExpressions;
using Xunit;

namespace PathFinder.Repository.Tests;

public sealed partial class RepositoryBoundaryTests
{
    [Fact]
    public void ReleaseWorkflow_PublishesCertifiedPackageFromImmutableTags()
    {
        var root = FindRepositoryRoot();
        var violations = InspectReleasePublication(root);
        Assert.True(
            violations.Count == 0,
            $"Release publication workflow violations:{Environment.NewLine}{string.Join(Environment.NewLine, violations)}");
    }

    private static List<string> InspectReleasePublication(string root)
    {
        var violations = new List<string>();
        var workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "release.yml"));
        foreach (var marker in new[]
                 {
                     "tags:",
                     "'v*'",
                     "--verify-tag",
                     "dotnet nuget push",
                     "gh release create",
                     "sha256sum -c",
                     "Directory.Build.props",
                     "id-token: write",
                     "uses: NuGet/login@8d196754b4036150537f80ac539e15c2f1028841",
                     "steps.nuget-login.outputs.NUGET_API_KEY",
                     "startsWith(github.ref, 'refs/tags/v')",
                     "${GITHUB_REF_NAME#v}"
                 })
        {
            if (!workflow.Contains(marker, StringComparison.Ordinal))
            {
                violations.Add($"release.yml omits: {marker}");
            }
        }

        if (workflow.Contains("secrets.NUGET_API_KEY", StringComparison.Ordinal))
        {
            violations.Add("release.yml must not use secrets.NUGET_API_KEY");
        }

        if (workflow.Contains("permissions:\n  contents: write", StringComparison.Ordinal))
        {
            violations.Add("release.yml must not grant top-level contents: write");
        }

        if (!HasTagGuardedJobLevelContentsWrite(workflow))
        {
            violations.Add(
                "release.yml must grant job-level contents: write under a job guarded by startsWith(github.ref, 'refs/tags/v')");
        }

        InspectNugetPushBoundToTagGuardedPublishJob(workflow, violations);
        return violations;
    }

    private static void InspectNugetPushBoundToTagGuardedPublishJob(string workflow, List<string> violations)
    {
        const string header = "\n  publish:\n";
        const string guard = "if: startsWith(github.ref, 'refs/tags/v')";
        const string push = "dotnet nuget push";
        var headerIndex = workflow.IndexOf(header, StringComparison.Ordinal);
        if (headerIndex < 0)
        {
            violations.Add("release.yml omits the publish: job header");
            return;
        }

        var guardIndex = workflow.IndexOf(guard, headerIndex, StringComparison.Ordinal);
        if (guardIndex < 0)
        {
            violations.Add("release.yml publish job is not guarded by startsWith(github.ref, 'refs/tags/v')");
            return;
        }

        var firstPush = workflow.IndexOf(push, StringComparison.Ordinal);
        if (firstPush >= 0 && firstPush < headerIndex)
        {
            violations.Add("dotnet nuget push must not appear before the publish: job header");
        }

        if (firstPush >= 0 && firstPush < guardIndex)
        {
            violations.Add("dotnet nuget push must lie after the publish job's startsWith(github.ref, 'refs/tags/v') guard");
        }

        var idToken = workflow.IndexOf("id-token: write", StringComparison.Ordinal);
        if (idToken < 0 || idToken < headerIndex)
        {
            violations.Add("id-token: write must lie inside the tag-guarded publish job");
        }
    }

    private static bool HasTagGuardedJobLevelContentsWrite(string workflow)
    {
        var jobsIndex = workflow.IndexOf("\njobs:\n", StringComparison.Ordinal);
        if (jobsIndex < 0)
        {
            return false;
        }

        var jobBlocks = Regex.Split(workflow[(jobsIndex + 1)..], @"\n  (?=[A-Za-z0-9_-]+:)");
        return jobBlocks.Any(block =>
            block.Contains("startsWith(github.ref, 'refs/tags/v')", StringComparison.Ordinal) &&
            block.Contains("permissions:\n      contents: write", StringComparison.Ordinal));
    }
}
