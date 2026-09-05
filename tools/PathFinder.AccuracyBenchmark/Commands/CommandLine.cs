namespace PathFinder.AccuracyBenchmark.Commands;

public abstract record BenchmarkCommand(string OutputDirectory);

public sealed record ReproduceCommand(bool Offline, string OutputDirectory)
    : BenchmarkCommand(OutputDirectory);

public sealed record RefreshJplCommand(string OutputDirectory)
    : BenchmarkCommand(OutputDirectory);

public sealed record RefreshSwissCommand(string SwetestPath, string OutputDirectory)
    : BenchmarkCommand(OutputDirectory);

public sealed class CommandLineException(string message) : Exception(message);

public static class CommandLine
{
    public static BenchmarkCommand Parse(IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            throw new CommandLineException(Usage);
        }

        var commandArguments = arguments.Skip(1).ToArray();
        return arguments[0] switch
        {
            "reproduce" => ParseReproduce(commandArguments),
            "refresh-jpl" => ParseJpl(commandArguments),
            "refresh-swiss" => ParseSwiss(commandArguments),
            _ => throw new CommandLineException($"Unknown command '{arguments[0]}'.{Environment.NewLine}{Usage}")
        };
    }

    public static string Usage =>
        "Commands:" + Environment.NewLine +
        "  reproduce --offline --output <new-directory>" + Environment.NewLine +
        "  refresh-jpl --output <new-candidate-directory>" + Environment.NewLine +
        "  refresh-swiss --swetest <path> --output <new-candidate-directory>";

    private static ReproduceCommand ParseReproduce(IReadOnlyList<string> arguments)
    {
        var options = ParseOptions(arguments, "--offline", "--output");
        if (!options.Flags.Contains("--offline"))
        {
            throw new CommandLineException("Reproduction must explicitly use --offline.");
        }

        return new ReproduceCommand(true, RequiredValue(options, "--output"));
    }

    private static RefreshJplCommand ParseJpl(IReadOnlyList<string> arguments)
    {
        var options = ParseOptions(arguments, "--output");
        return new RefreshJplCommand(RequiredValue(options, "--output"));
    }

    private static RefreshSwissCommand ParseSwiss(IReadOnlyList<string> arguments)
    {
        var options = ParseOptions(arguments, "--swetest", "--output");
        return new RefreshSwissCommand(
            RequiredValue(options, "--swetest"),
            RequiredValue(options, "--output"));
    }

    private static ParsedOptions ParseOptions(IReadOnlyList<string> arguments, params string[] allowed)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var flags = new HashSet<string>(StringComparer.Ordinal);
        var allowedSet = allowed.ToHashSet(StringComparer.Ordinal);
        for (var index = 0; index < arguments.Count; index++)
        {
            var option = arguments[index];
            if (!allowedSet.Contains(option) || values.ContainsKey(option) || flags.Contains(option))
            {
                throw new CommandLineException($"Unknown or duplicate option '{option}'.");
            }

            if (option == "--offline")
            {
                flags.Add(option);
                continue;
            }

            if (++index >= arguments.Count || arguments[index].StartsWith("--", StringComparison.Ordinal))
            {
                throw new CommandLineException($"Option '{option}' requires a value.");
            }

            values.Add(option, arguments[index]);
        }

        return new ParsedOptions(values, flags);
    }

    private static string RequiredValue(ParsedOptions options, string name) =>
        options.Values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new CommandLineException($"Required option '{name}' was not supplied.");

    private sealed record ParsedOptions(
        IReadOnlyDictionary<string, string> Values,
        IReadOnlySet<string> Flags);
}
