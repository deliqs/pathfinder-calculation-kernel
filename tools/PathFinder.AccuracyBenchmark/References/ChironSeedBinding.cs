using System.Globalization;
using PathFinder.CalculationKernel.Provenance;

namespace PathFinder.AccuracyBenchmark.References;

public static class ChironSeedBinding
{
    public static void Verify(HorizonsVectorRow vector)
    {
        var metadata = CalculationKernelMetadata.SourceManifestInput
            .ToDictionary(property => property.Name, property => property.Value, StringComparer.Ordinal);
        RequireEqual(vector.X, metadata, "chironSeedX");
        RequireEqual(vector.Y, metadata, "chironSeedY");
        RequireEqual(vector.Z, metadata, "chironSeedZ");
        RequireEqual(vector.Vx, metadata, "chironSeedVx");
        RequireEqual(vector.Vy, metadata, "chironSeedVy");
        RequireEqual(vector.Vz, metadata, "chironSeedVz");
        if (metadata["chironSeedReferenceId"] != HorizonsQueryBuilder.ChironSeedReferenceId)
        {
            throw new InvalidDataException("Chiron seed reference id is not bound to the kernel metadata.");
        }
    }

    private static void RequireEqual(
        double actual,
        IReadOnlyDictionary<string, string> metadata,
        string name)
    {
        var expected = double.Parse(metadata[name], CultureInfo.InvariantCulture);
        if (actual != expected)
        {
            throw new InvalidDataException(
                $"Chiron seed component {name} differs from the calculation kernel metadata.");
        }
    }
}
