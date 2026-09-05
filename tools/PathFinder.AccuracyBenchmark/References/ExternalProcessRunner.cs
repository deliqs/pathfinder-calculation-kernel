using System.Diagnostics;

namespace PathFinder.AccuracyBenchmark.References;

public sealed record ExternalProcessOutput(int ExitCode, byte[] StandardOutput, byte[] StandardError);

public interface IExternalProcessRunner
{
    Task<ExternalProcessOutput> RunAsync(
        string executable,
        SwissInvocation invocation,
        CancellationToken cancellationToken);
}

public sealed class ExternalProcessRunner : IExternalProcessRunner
{
    public async Task<ExternalProcessOutput> RunAsync(
        string executable,
        SwissInvocation invocation,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in invocation.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (var variable in invocation.Environment)
        {
            startInfo.Environment[variable.Key] = variable.Value;
        }

        using var process = Process.Start(startInfo) ??
            throw new InvalidOperationException($"Could not start executable: {executable}");
        await using var standardOutput = new MemoryStream();
        await using var standardError = new MemoryStream();
        var outputTask = process.StandardOutput.BaseStream.CopyToAsync(standardOutput, cancellationToken);
        var errorTask = process.StandardError.BaseStream.CopyToAsync(standardError, cancellationToken);
        await Task.WhenAll(
            outputTask,
            errorTask,
            process.WaitForExitAsync(cancellationToken));
        return new ExternalProcessOutput(
            process.ExitCode,
            standardOutput.ToArray(),
            standardError.ToArray());
    }
}
