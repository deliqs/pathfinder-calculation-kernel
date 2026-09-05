using PathFinder.AccuracyBenchmark.Commands;

namespace PathFinder.AccuracyBenchmark.Tests;

public sealed class CandidateOutputGuardTests
{
    [Fact]
    public void Validate_OutputInsideFrozenBenchmark_Throws()
    {
        var root = Directory.CreateTempSubdirectory("pathfinder-output-guard-");
        var frozen = Directory.CreateDirectory(Path.Combine(root.FullName, "benchmark"));
        var candidate = Path.Combine(frozen.FullName, "results", "candidate");

        try
        {
            Assert.Throws<InvalidOperationException>(() =>
                CandidateOutputGuard.Validate(candidate, frozen.FullName));
        }
        finally
        {
            Directory.Delete(root.FullName, recursive: true);
        }
    }

    [Fact]
    public void Validate_SymlinkAncestorCouldResolveInsideFrozenBenchmark_Throws()
    {
        var root = Directory.CreateTempSubdirectory("pathfinder-output-guard-");
        var frozen = Directory.CreateDirectory(Path.Combine(root.FullName, "benchmark"));
        var link = Path.Combine(root.FullName, "linked-benchmark");
        Directory.CreateSymbolicLink(link, frozen.FullName);

        try
        {
            Assert.Throws<InvalidOperationException>(() =>
                CandidateOutputGuard.Validate(Path.Combine(link, "candidate"), frozen.FullName));
        }
        finally
        {
            Directory.Delete(link);
            Directory.Delete(root.FullName, recursive: true);
        }
    }

    [Fact]
    public void Validate_ExistingOrSameDirectory_Throws()
    {
        var root = Directory.CreateTempSubdirectory("pathfinder-output-guard-");
        var frozen = Directory.CreateDirectory(Path.Combine(root.FullName, "frozen"));
        var existing = Directory.CreateDirectory(Path.Combine(root.FullName, "existing"));

        try
        {
            Assert.Throws<InvalidOperationException>(() =>
                CandidateOutputGuard.Validate(frozen.FullName, frozen.FullName));
            Assert.Throws<InvalidOperationException>(() =>
                CandidateOutputGuard.Validate(existing.FullName, frozen.FullName));
        }
        finally
        {
            Directory.Delete(root.FullName, recursive: true);
        }
    }

    [Fact]
    public void Validate_NewSiblingDirectory_ReturnsAbsolutePathWithoutCreatingIt()
    {
        var root = Directory.CreateTempSubdirectory("pathfinder-output-guard-");
        var frozen = Directory.CreateDirectory(Path.Combine(root.FullName, "frozen"));
        var candidate = Path.Combine(root.FullName, "candidate");

        try
        {
            var actual = CandidateOutputGuard.Validate(candidate, frozen.FullName);

            Assert.Equal(Path.GetFullPath(candidate), actual);
            Assert.False(Directory.Exists(candidate));
        }
        finally
        {
            Directory.Delete(root.FullName, recursive: true);
        }
    }
}
