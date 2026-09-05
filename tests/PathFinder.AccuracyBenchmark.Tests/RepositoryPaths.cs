namespace PathFinder.AccuracyBenchmark.Tests;

internal static class RepositoryPaths
{
    internal static string File(params string[] segments) =>
        Path.Combine([Root(), .. segments]);

    private static string Root()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (System.IO.File.Exists(Path.Combine(current.FullName, "PathFinder.CalculationKernel.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not find the benchmark repository root.");
    }
}
