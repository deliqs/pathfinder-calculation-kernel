using NodaTime;

namespace PathFinder.CalculationKernel.Ephemeris;

public readonly record struct AstronomyEngineTimeMetadata(
    string Convention,
    double DeltaTSeconds);

/// <summary>Exposes the time conversion used by the kernel's Astronomy Engine evaluations.</summary>
public static class AstronomyEngineTime
{
    public const string Convention = "AstronomyEngine-2.1.19:Espenak-Meeus";

    public static AstronomyEngineTimeMetadata GetMetadata(Instant utcMoment)
    {
        var time = AstronomyEngineEphemeris.ToAstroTime(utcMoment);
        return new AstronomyEngineTimeMetadata(Convention, (time.tt - time.ut) * 86400.0);
    }
}
