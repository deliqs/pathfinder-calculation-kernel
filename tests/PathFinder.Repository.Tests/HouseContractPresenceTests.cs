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
            "benchmark/references/raw/swiss/london-j2000-koch.stdout.txt",
            "benchmark/references/raw/swiss/london-j2000-regiomontanus.stdout.txt",
            "benchmark/references/raw/swiss/london-j2000-campanus.stdout.txt",
            "benchmark/references/raw/swiss/new-york-eclipse-koch.stdout.txt",
            "benchmark/references/raw/swiss/new-york-eclipse-regiomontanus.stdout.txt",
            "benchmark/references/raw/swiss/new-york-eclipse-campanus.stdout.txt",
            "benchmark/references/raw/swiss/sydney-historical-koch.stdout.txt",
            "benchmark/references/raw/swiss/sydney-historical-regiomontanus.stdout.txt",
            "benchmark/references/raw/swiss/sydney-historical-campanus.stdout.txt",
            "benchmark/references/raw/swiss/tromso-polar-koch.stdout.txt",
            "benchmark/references/raw/swiss/tromso-polar-regiomontanus.stdout.txt",
            "benchmark/references/raw/swiss/tromso-polar-campanus.stdout.txt",
            "benchmark/references/raw/swiss/gothenburg-koch-midsummer.stdout.txt",
            "benchmark/references/raw/swiss/reykjavik-regiomontanus-midsummer.stdout.txt",
            "benchmark/references/raw/swiss/reykjavik-campanus-midsummer.stdout.txt",
            "benchmark/references/raw/swiss/version-identification.txt"
        };

        Assert.All(requiredFiles, relativePath => Assert.True(File.Exists(Path.Combine(root, relativePath))));
    }
}
