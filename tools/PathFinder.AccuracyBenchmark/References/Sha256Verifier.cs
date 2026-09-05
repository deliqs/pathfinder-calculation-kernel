using System.Security.Cryptography;

namespace PathFinder.AccuracyBenchmark.References;

public static class Sha256Verifier
{
    public static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes));

    public static string Verify(ReadOnlySpan<byte> bytes, string expectedHash, string artifactName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedHash);
        var actualHash = Hash(bytes);
        if (!actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"SHA-256 mismatch for {artifactName}: expected {expectedHash.ToLowerInvariant()}, actual {actualHash}.");
        }

        return actualHash;
    }
}
