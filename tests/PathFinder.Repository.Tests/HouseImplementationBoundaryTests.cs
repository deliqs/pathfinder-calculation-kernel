using Xunit;

namespace PathFinder.Repository.Tests;

public sealed class HouseImplementationBoundaryTests
{
    [Fact]
    public void ProductSource_DoesNotContainRestrictedHouseImplementationTerms()
    {
        var root = FindRepositoryRoot();
        var restrictedTerms = new[]
        {
            "swe" + "house",
            "Asc" + "1",
            "Asc" + "2",
            "Obl" + "Asc",
            "oblique " + "ascension"
        };
        var violations = Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(Path.Combine(root, "tools"), "*.cs", SearchOption.AllDirectories))
            .Where(path => restrictedTerms.Any(term =>
                File.ReadAllText(path).Contains(term, StringComparison.OrdinalIgnoreCase)))
            .Select(path => Path.GetRelativePath(root, path))
            .ToArray();

        Assert.Empty(violations);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PathFinder.CalculationKernel.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
