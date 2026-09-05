using System.Collections.ObjectModel;

namespace PathFinder.CalculationKernel.Provenance;

public sealed record SourceManifestProperty(string Name, string Value);

/// <summary>
/// Stable inputs for a source manifest. The immutable release pins the source commit externally;
/// keeping it out of this list avoids a circular build/output identity.
/// </summary>
public static class CalculationKernelMetadata
{
    public const string CalculationRevision = "4";

    private static readonly ReadOnlyCollection<SourceManifestProperty> ManifestInput =
        Array.AsReadOnly(new[]
        {
            new SourceManifestProperty("bodyCount", "14"),
            new SourceManifestProperty("calculationRevision", CalculationRevision),
            new SourceManifestProperty("chironModel", "gravity-simulation:jpl-j2000-state"),
            new SourceManifestProperty("chironSeedCenter", "500@10"),
            new SourceManifestProperty("chironSeedEpoch", "2000-01-01T12:00:00 TDB"),
            new SourceManifestProperty("chironSeedProvenanceStatus", "current-jpl-solution-verified"),
            new SourceManifestProperty("chironSeedReferencePlane", "FRAME:ICRF/J2000"),
            new SourceManifestProperty("chironSeedReferenceId", "jpl-horizons-chiron-seed-jpl171-j2000"),
            new SourceManifestProperty("chironSeedTarget", "COMMAND='2060;'; JPL#171"),
            new SourceManifestProperty("chironSeedUnits", "AU-D"),
            new SourceManifestProperty("chironSeedVectorCorrection", "NONE"),
            new SourceManifestProperty("chironSeedVx", "0.004971227226758336"),
            new SourceManifestProperty("chironSeedVy", "-0.003626418894486951"),
            new SourceManifestProperty("chironSeedVz", "-0.0008257960206970693"),
            new SourceManifestProperty("chironSeedX", "-3.529597323721606"),
            new SourceManifestProperty("chironSeedY", "-8.675401114502414"),
            new SourceManifestProperty("chironSeedZ", "-2.935904700117773"),
            new SourceManifestProperty("campanusPolarFallback", "porphyry-above-66-degrees"),
            new SourceManifestProperty("houseObliquityDegrees", "23.4393"),
            new SourceManifestProperty("kochPolarFallback", "porphyry-above-60-degrees"),
            new SourceManifestProperty("lilithModel", "mean-apogee"),
            new SourceManifestProperty("lunarNodeModel", "mean"),
            new SourceManifestProperty("placidusPolarFallback", "porphyry-above-60-degrees"),
            new SourceManifestProperty("regiomontanusPolarFallback", "porphyry-above-66-degrees"),
            new SourceManifestProperty("runtimeDependency:CosineKitty.AstronomyEngine", "2.1.19"),
            new SourceManifestProperty("runtimeDependency:NodaTime", "3.3.3"),
            new SourceManifestProperty("siderealTimeModel", "truncated-gmst"),
            new SourceManifestProperty("stationSampleMinutes", "10")
        }.OrderBy(item => item.Name, StringComparer.Ordinal).ToArray());

    public static IReadOnlyList<SourceManifestProperty> SourceManifestInput => ManifestInput;
}
