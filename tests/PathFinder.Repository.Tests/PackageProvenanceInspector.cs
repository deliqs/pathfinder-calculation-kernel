using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace PathFinder.Repository.Tests;

internal static partial class PackageProvenanceInspector
{
    private const string VirtualRoot = "/_/";
    private const string RepositoryUrl = "https://github.com/deliqs/pathfinder-calculation-kernel";
    private const string RawSourcePrefix = "https://raw.githubusercontent.com/deliqs/pathfinder-calculation-kernel/";
    private static readonly Guid SourceLinkKind = new("CC110556-A091-4D38-9FEC-25AB9A351A6A");
    private static readonly Guid Sha256Kind = new("8829D00F-11B8-4213-878B-770E8597AC16");
    private static readonly Guid Sha1Kind = new("FF1816EC-AA5E-4D10-87F7-6F4963833460");

    public static List<string> Inspect(
        string root,
        string isolatedRoot,
        string expectedCommit,
        string packagePath,
        string symbolPackagePath)
    {
        var violations = new List<string>();
        var forbiddenRoots = new[]
        {
            root,
            isolatedRoot,
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        };

        using var package = ZipFile.OpenRead(packagePath);
        using var symbolPackage = ZipFile.OpenRead(symbolPackagePath);
        InspectArchive(package, Path.GetFileName(packagePath), forbiddenRoots, violations);
        InspectArchive(symbolPackage, Path.GetFileName(symbolPackagePath), forbiddenRoots, violations);

        var assemblyBytes = ReadEntry(package, "lib/net10.0/PathFinder.CalculationKernel.dll");
        var pdbBytes = ReadEntry(symbolPackage, "lib/net10.0/PathFinder.CalculationKernel.pdb");
        InspectBinary("packaged DLL", assemblyBytes, forbiddenRoots, violations);
        InspectBinary("packaged PDB", pdbBytes, forbiddenRoots, violations);
        InspectCodeView(assemblyBytes, forbiddenRoots, violations);

        InspectNuspec(package, expectedCommit, violations);
        InspectPortablePdb(root, pdbBytes, expectedCommit, forbiddenRoots, violations);
        return violations;
    }

    private static void InspectArchive(
        ZipArchive archive,
        string archiveName,
        IReadOnlyList<string> forbiddenRoots,
        List<string> violations)
    {
        foreach (var entry in archive.Entries)
        {
            if (entry.FullName.StartsWith("/", StringComparison.Ordinal) ||
                entry.FullName.Contains("../", StringComparison.Ordinal) ||
                WindowsRootedPath().IsMatch(entry.FullName))
            {
                violations.Add($"{archiveName} contains a rooted package entry: {entry.FullName}");
            }

            if (entry.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase) ||
                entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) ||
                entry.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
                entry.FullName.EndsWith(".psmdcp", StringComparison.OrdinalIgnoreCase) ||
                entry.FullName.EndsWith(".rels", StringComparison.OrdinalIgnoreCase))
            {
                InspectText($"{archiveName}:{entry.FullName}", Encoding.UTF8.GetString(ReadEntry(entry)), forbiddenRoots, violations);
            }
        }
    }

    private static void InspectCodeView(
        byte[] assemblyBytes,
        IReadOnlyList<string> forbiddenRoots,
        List<string> violations)
    {
        using var reader = new PEReader(new MemoryStream(assemblyBytes));
        var codeViewEntries = reader.ReadDebugDirectory().Where(entry => entry.Type == DebugDirectoryEntryType.CodeView).ToList();
        if (codeViewEntries.Count != 1)
        {
            violations.Add($"Packaged DLL must contain exactly one CodeView entry; found {codeViewEntries.Count}");
            return;
        }

        var codeViewPath = reader.ReadCodeViewDebugDirectoryData(codeViewEntries[0]).Path;
        InspectText("CodeView path", codeViewPath, forbiddenRoots, violations);
        if (!codeViewPath.StartsWith(VirtualRoot, StringComparison.Ordinal))
        {
            violations.Add($"CodeView path is not mapped below {VirtualRoot}: {codeViewPath}");
        }
    }

    private static void InspectNuspec(ZipArchive package, string expectedCommit, List<string> violations)
    {
        var nuspecEntry = package.Entries.Single(entry => entry.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));
        using var nuspecStream = nuspecEntry.Open();
        var document = XDocument.Load(nuspecStream);
        var repository = document.Descendants().Single(element => element.Name.LocalName == "repository");
        var url = repository.Attribute("url")?.Value;
        var commit = repository.Attribute("commit")?.Value ?? string.Empty;
        if (url != RepositoryUrl)
        {
            violations.Add($"Unexpected nuspec repository URL: {url ?? "missing"}");
        }

        if (commit != expectedCommit || !CommitHash().IsMatch(commit))
        {
            violations.Add($"Nuspec repository commit {commit} does not equal HEAD {expectedCommit}");
        }
    }

    private static void InspectPortablePdb(
        string root,
        byte[] pdbBytes,
        string expectedCommit,
        IReadOnlyList<string> forbiddenRoots,
        List<string> violations)
    {
        using var provider = MetadataReaderProvider.FromPortablePdbStream(new MemoryStream(pdbBytes));
        var reader = provider.GetMetadataReader();
        var sourceLinkValues = reader.CustomDebugInformation
            .Select(handle => reader.GetCustomDebugInformation(handle))
            .Where(information => reader.GetGuid(information.Kind) == SourceLinkKind)
            .Select(information => Encoding.UTF8.GetString(reader.GetBlobBytes(information.Value)))
            .ToList();
        if (sourceLinkValues.Count != 1)
        {
            violations.Add($"Portable PDB must contain exactly one SourceLink mapping; found {sourceLinkValues.Count}");
            return;
        }

        var sourceLink = sourceLinkValues[0];
        InspectText("SourceLink mapping", sourceLink, forbiddenRoots, violations);
        using var sourceLinkDocument = JsonDocument.Parse(sourceLink);
        var mappings = sourceLinkDocument.RootElement.GetProperty("documents").EnumerateObject().ToList();
        var expectedUrl = $"{RawSourcePrefix}{expectedCommit}/*";
        if (mappings.Count != 1 || mappings[0].Name != $"{VirtualRoot}*" || mappings[0].Value.GetString() != expectedUrl)
        {
            violations.Add($"SourceLink must map {VirtualRoot}* to the exact repository commit {expectedCommit}");
        }

        foreach (var handle in reader.Documents)
        {
            var document = reader.GetDocument(handle);
            var documentName = reader.GetString(document.Name).Replace('\\', '/');
            InspectText("PDB document", documentName, forbiddenRoots, violations);
            if (!documentName.StartsWith(VirtualRoot, StringComparison.Ordinal))
            {
                violations.Add($"PDB document is not mapped below {VirtualRoot}: {documentName}");
                continue;
            }

            var localPath = Path.Combine(root, documentName[VirtualRoot.Length..].Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(localPath))
            {
                violations.Add($"Mapped PDB document does not resolve to repository source: {documentName}");
                continue;
            }

            var expectedHash = reader.GetBlobBytes(document.Hash);
            var actualHash = HashDocument(File.ReadAllBytes(localPath), reader.GetGuid(document.HashAlgorithm));
            if (!actualHash.SequenceEqual(expectedHash))
            {
                violations.Add($"PDB checksum does not match mapped repository source: {documentName}");
            }
        }
    }

    private static byte[] HashDocument(byte[] bytes, Guid algorithm) =>
        algorithm == Sha256Kind
            ? SHA256.HashData(bytes)
            : algorithm == Sha1Kind
                ? SHA1.HashData(bytes)
                : throw new InvalidDataException($"Unsupported PDB document hash algorithm: {algorithm}");

    private static void InspectBinary(
        string description,
        byte[] bytes,
        IReadOnlyList<string> forbiddenRoots,
        List<string> violations) =>
        InspectText(description, Encoding.Latin1.GetString(bytes), forbiddenRoots, violations);

    private static void InspectText(
        string description,
        string text,
        IReadOnlyList<string> forbiddenRoots,
        List<string> violations)
    {
        var normalized = text.Replace('\\', '/');
        foreach (var forbiddenRoot in forbiddenRoots.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            if (normalized.Contains(forbiddenRoot.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase))
            {
                violations.Add($"{description} contains machine-specific root {forbiddenRoot}");
            }
        }

        var machinePath = MachineSpecificPath().Match(normalized);
        if (machinePath.Success)
        {
            violations.Add($"{description} contains an absolute user path: {machinePath.Value}");
        }
    }

    private static byte[] ReadEntry(ZipArchive archive, string entryName) =>
        ReadEntry(archive.GetEntry(entryName) ?? throw new InvalidDataException($"Package entry is missing: {entryName}"));

    private static byte[] ReadEntry(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    [GeneratedRegex("^[A-Za-z]:[\\\\/]")]
    private static partial Regex WindowsRootedPath();

    [GeneratedRegex("(?i)(?<![A-Z])(?:[A-Z]:/(?!/)|/(?:Users|home|private/var|var/folders)/)[^\\x00\\r\\n\\\"']+")]
    private static partial Regex MachineSpecificPath();

    [GeneratedRegex("^[0-9a-f]{40}$")]
    private static partial Regex CommitHash();
}
