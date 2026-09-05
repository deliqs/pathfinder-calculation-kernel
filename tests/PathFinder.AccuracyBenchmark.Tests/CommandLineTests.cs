using PathFinder.AccuracyBenchmark.Commands;

namespace PathFinder.AccuracyBenchmark.Tests;

public sealed class CommandLineTests
{
    [Fact]
    public void Parse_ReproduceOffline_RequiresExplicitOutput()
    {
        var command = Assert.IsType<ReproduceCommand>(CommandLine.Parse(
            ["reproduce", "--offline", "--output", "candidate"]));

        Assert.True(command.Offline);
        Assert.Equal("candidate", command.OutputDirectory);
        Assert.Throws<CommandLineException>(() => CommandLine.Parse(["reproduce", "--offline"]));
        Assert.Throws<CommandLineException>(() =>
            CommandLine.Parse(["reproduce", "--output", "candidate"]));
    }

    [Fact]
    public void Parse_RefreshJpl_RequiresExplicitCandidateOutput()
    {
        var command = Assert.IsType<RefreshJplCommand>(CommandLine.Parse(
            ["refresh-jpl", "--output", "candidate"]));

        Assert.Equal("candidate", command.OutputDirectory);
        Assert.Throws<CommandLineException>(() => CommandLine.Parse(["refresh-jpl"]));
    }

    [Fact]
    public void Parse_RefreshSwiss_RequiresUserSuppliedExecutableAndCandidateOutput()
    {
        var command = Assert.IsType<RefreshSwissCommand>(CommandLine.Parse(
            ["refresh-swiss", "--swetest", "/tmp/swetest", "--output", "candidate"]));

        Assert.Equal("/tmp/swetest", command.SwetestPath);
        Assert.Equal("candidate", command.OutputDirectory);
        Assert.Throws<CommandLineException>(() =>
            CommandLine.Parse(["refresh-swiss", "--output", "candidate"]));
    }

    [Fact]
    public void Parse_UnknownOrDuplicateOption_FailsClosed()
    {
        Assert.Throws<CommandLineException>(() => CommandLine.Parse(["unknown"]));
        Assert.Throws<CommandLineException>(() => CommandLine.Parse(
            ["refresh-jpl", "--output", "one", "--output", "two"]));
    }
}
