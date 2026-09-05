using PathFinder.AccuracyBenchmark.References;

namespace PathFinder.AccuracyBenchmark.Tests;

public sealed class ReferenceIntegrityTests
{
    [Fact]
    public void Verify_TamperedContent_Throws()
    {
        var expected = Sha256Verifier.Hash(System.Text.Encoding.UTF8.GetBytes("original"));

        var error = Assert.Throws<InvalidDataException>(() =>
            Sha256Verifier.Verify(System.Text.Encoding.UTF8.GetBytes("tampered"), expected, "raw response"));

        Assert.Contains("raw response", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_ExactContent_ReturnsLowercaseHash()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("frozen");
        var hash = Sha256Verifier.Hash(bytes);

        var actual = Sha256Verifier.Verify(bytes, hash.ToUpperInvariant(), "raw response");

        Assert.Equal(hash, actual);
        Assert.Matches("^[0-9a-f]{64}$", actual);
    }
}
