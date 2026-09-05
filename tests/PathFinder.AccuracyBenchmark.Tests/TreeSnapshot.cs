using System.Security.Cryptography;
using System.Text;

namespace PathFinder.AccuracyBenchmark.Tests;

internal sealed record TreeSnapshot(string CanonicalListing)
{
    internal static TreeSnapshot Capture(string directory)
    {
        var root = Path.GetFullPath(directory);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException(root);
        }

        var listing = new StringBuilder();
        foreach (var child in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories)
                     .Select(path => RelativePath(root, path))
                     .OrderBy(path => path, StringComparer.Ordinal))
        {
            listing.Append("directory\t").Append(child).Append('\n');
        }

        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                     .OrderBy(path => RelativePath(root, path), StringComparer.Ordinal))
        {
            var relativePath = RelativePath(root, file);
            using var stream = File.OpenRead(file);
            listing.Append("file\t")
                .Append(relativePath).Append('\t')
                .Append(stream.Length).Append('\t')
                .Append(Convert.ToHexStringLower(SHA256.HashData(stream)))
                .Append('\n');
        }

        return new TreeSnapshot(listing.ToString());
    }

    private static string RelativePath(string root, string path) =>
        Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');
}
