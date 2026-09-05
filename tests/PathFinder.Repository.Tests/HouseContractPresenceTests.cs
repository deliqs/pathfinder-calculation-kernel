using Xunit;

namespace PathFinder.Repository.Tests;

public sealed partial class RepositoryBoundaryTests
{
    [Fact]
    public void PublicRepository_AdmitsHouseCalculationContract()
    {
        var root = FindRepositoryRoot();
        var requiredFiles = new[]
        {
            "src/PathFinder.CalculationKernel/CalculationContracts.cs",
            "src/PathFinder.CalculationKernel/Houses/HouseCalculator.cs",
            "src/PathFinder.CalculationKernel/Houses/PlacidusHouses.cs",
            "src/PathFinder.CalculationKernel/Houses/KochHouses.cs",
            "src/PathFinder.CalculationKernel/Houses/RegiomontanusHouses.cs",
            "src/PathFinder.CalculationKernel/Houses/CampanusHouses.cs",
            "benchmark/references/raw/swiss/london-j2000.stdout.txt",
            "benchmark/references/raw/swiss/new-york-eclipse.stdout.txt",
            "benchmark/references/raw/swiss/sydney-historical.stdout.txt",
            "benchmark/references/raw/swiss/tromso-polar.stdout.txt",
            "benchmark/references/raw/swiss/version-identification.txt"
        };

        Assert.All(requiredFiles, relativePath => Assert.True(File.Exists(Path.Combine(root, relativePath))));
    }
}
