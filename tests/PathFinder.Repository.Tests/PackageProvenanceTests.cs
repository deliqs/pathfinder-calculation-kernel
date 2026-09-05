using System.Diagnostics;
using Xunit;

namespace PathFinder.Repository.Tests;

public sealed partial class RepositoryBoundaryTests
{
    [Fact]
    public async Task PackedKernel_ContainsOnlyMappedSourcePathsAndExactRepositoryCommit()
    {
        // Arrange
        var root = FindRepositoryRoot();
        var isolatedRoot = Path.Combine(root, "artifacts", $"package-provenance-{Guid.NewGuid():N}");

        try
        {
            // Act
            var packResult = await PackKernelAsync(root, isolatedRoot);
            var expectedCommit = ReadRepositoryCommit(root);
            var violations = PackageProvenanceInspector.Inspect(
                root,
                isolatedRoot,
                expectedCommit,
                packResult.PackagePath,
                packResult.SymbolPackagePath);

            // Assert
            Assert.True(
                violations.Count == 0,
                $"Package provenance violations:{Environment.NewLine}{string.Join(Environment.NewLine, violations)}");
        }
        finally
        {
            if (Directory.Exists(isolatedRoot))
            {
                Directory.Delete(isolatedRoot, recursive: true);
            }
        }
    }

    private static async Task<PackResult> PackKernelAsync(string root, string isolatedRoot)
    {
        var packageDirectory = Path.Combine(isolatedRoot, "packages");
        var intermediateDirectory = Path.Combine(isolatedRoot, "obj");
        var outputDirectory = Path.Combine(isolatedRoot, "bin");
        Directory.CreateDirectory(packageDirectory);

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = root,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };
        foreach (var argument in new[]
                 {
                     "pack",
                     "src/PathFinder.CalculationKernel/PathFinder.CalculationKernel.csproj",
                     "-c", "Release",
                     "-warnaserror",
                     "--output", packageDirectory,
                     "-p:RestoreLockedMode=true",
                     $"-p:BaseIntermediateOutputPath={intermediateDirectory}{Path.DirectorySeparatorChar}",
                     $"-p:MSBuildProjectExtensionsPath={intermediateDirectory}{Path.DirectorySeparatorChar}",
                     $"-p:BaseOutputPath={outputDirectory}{Path.DirectorySeparatorChar}",
                     "-p:DefaultItemExcludes=obj/**"
                 })
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start dotnet pack");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var output = $"{await standardOutput}{Environment.NewLine}{await standardError}";
        Assert.True(process.ExitCode == 0, $"Isolated dotnet pack failed:{Environment.NewLine}{output}");

        return new PackResult(
            Directory.GetFiles(packageDirectory, "*.nupkg").Single(path => !path.EndsWith(".snupkg", StringComparison.Ordinal)),
            Directory.GetFiles(packageDirectory, "*.snupkg").Single());
    }

    private static string ReadRepositoryCommit(string root)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = root,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("rev-parse");
        startInfo.ArgumentList.Add("HEAD");

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start git rev-parse HEAD");
        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, $"git rev-parse HEAD failed:{Environment.NewLine}{standardError}");

        return standardOutput.Trim();
    }

    private sealed record PackResult(string PackagePath, string SymbolPackagePath);
}
