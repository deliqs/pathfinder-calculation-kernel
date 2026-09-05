namespace PathFinder.AccuracyBenchmark.Commands;

public static class CandidateOutputGuard
{
    public static string Validate(string outputDirectory, string frozenReferencesDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(frozenReferencesDirectory);
        var candidate = Path.GetFullPath(outputDirectory);
        var frozen = Path.GetFullPath(frozenReferencesDirectory);
        RejectLinkedAncestors(candidate, frozen);
        var relativeToFrozen = Path.GetRelativePath(frozen, candidate);
        var insideFrozen = relativeToFrozen == "." ||
            (!relativeToFrozen.Equals("..", StringComparison.Ordinal) &&
             !relativeToFrozen.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

        if (insideFrozen)
        {
            throw new InvalidOperationException(
                $"Candidate output cannot be inside the frozen reference directory: {candidate}");
        }

        if (Directory.Exists(candidate) || File.Exists(candidate))
        {
            throw new InvalidOperationException(
                $"Candidate output must be a new path so a refresh cannot overwrite evidence: {candidate}");
        }

        return candidate;
    }

    private static void RejectLinkedAncestors(string candidate, string frozen)
    {
        for (var directory = new DirectoryInfo(Path.GetDirectoryName(candidate)!);
             directory is not null;
             directory = directory.Parent)
        {
            if (!directory.Exists)
            {
                continue;
            }

            if (!Contains(directory.FullName, frozen) &&
                (directory.LinkTarget is not null ||
                directory.Attributes.HasFlag(FileAttributes.ReparsePoint))
               )
            {
                throw new InvalidOperationException(
                    $"Candidate output cannot traverse a symbolic link or reparse point: {directory.FullName}");
            }
        }
    }

    private static bool Contains(string parent, string child)
    {
        var relative = Path.GetRelativePath(parent, child);
        return relative == "." ||
            (!relative.Equals("..", StringComparison.Ordinal) &&
             !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
    }
}
