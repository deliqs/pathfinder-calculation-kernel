using PathFinder.AccuracyBenchmark.Cases;
using PathFinder.AccuracyBenchmark.Commands;
using PathFinder.AccuracyBenchmark.References;
using PathFinder.AccuracyBenchmark.Reproduction;

namespace PathFinder.AccuracyBenchmark;

public static class Program
{
    public static async Task<int> Main(string[] arguments)
    {
        try
        {
            var command = CommandLine.Parse(arguments);
            return command switch
            {
                RefreshSwissCommand swiss => await RefreshSwissAsync(swiss, CancellationToken.None),
                RefreshJplCommand jpl => await RefreshJplAsync(jpl, CancellationToken.None),
                ReproduceCommand reproduce => Reproduce(reproduce),
                _ => throw new InvalidOperationException($"Unsupported command type: {command.GetType().Name}")
            };
        }
        catch (CommandLineException exception)
        {
            await Console.Error.WriteLineAsync(exception.Message);
            return 2;
        }
        catch (Exception exception)
        {
            await Console.Error.WriteLineAsync(exception.Message);
            return 1;
        }
    }

    private static int Reproduce(ReproduceCommand command)
    {
        var output = OfflineBenchmarkReproducer.Reproduce(
            Environment.CurrentDirectory,
            command.OutputDirectory);
        Console.Out.WriteLine(
            $"Offline results written to {output.OutputDirectory} ({output.ResultsSha256})");
        return 0;
    }

    private static async Task<int> RefreshJplAsync(
        RefreshJplCommand command,
        CancellationToken cancellationToken)
    {
        var root = Environment.CurrentDirectory;
        var cases = BenchmarkCaseManifestLoader.Load(await File.ReadAllBytesAsync(
            Path.Combine(root, "benchmark", "cases", "cases.json"), cancellationToken));
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "PathFinder-Calculation-Benchmark/0.1 (+https://github.com/deliqs/pathfinder-calculation-kernel)");
        await JplRefreshService.RefreshAsync(
            command.OutputDirectory,
            Path.Combine(root, "benchmark"),
            cases,
            new JplClient(httpClient),
            cancellationToken);
        await Console.Out.WriteLineAsync($"JPL candidate written to {Path.GetFullPath(command.OutputDirectory)}");
        return 0;
    }

    private static async Task<int> RefreshSwissAsync(
        RefreshSwissCommand command,
        CancellationToken cancellationToken)
    {
        var root = Environment.CurrentDirectory;
        var cases = BenchmarkCaseManifestLoader.Load(await File.ReadAllBytesAsync(
            Path.Combine(root, "benchmark", "cases", "cases.json"), cancellationToken));
        await SwissRefreshService.RefreshAsync(
            command.SwetestPath,
            command.OutputDirectory,
            Path.Combine(root, "benchmark"),
            cases,
            new ExternalProcessRunner(),
            cancellationToken);
        await Console.Out.WriteLineAsync($"Swiss candidate written to {Path.GetFullPath(command.OutputDirectory)}");
        return 0;
    }
}
