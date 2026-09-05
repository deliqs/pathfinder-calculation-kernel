using NodaTime;

namespace PathFinder.CalculationKernel;

public enum Planet
{
    Sun,
    Moon,
    Mercury,
    Venus,
    Mars,
    Jupiter,
    Saturn,
    Uranus,
    Neptune,
    Pluto,
    Chiron,
    NorthNode,
    SouthNode,
    Lilith
}

public enum ZodiacSign
{
    Aries,
    Taurus,
    Gemini,
    Cancer,
    Leo,
    Virgo,
    Libra,
    Scorpio,
    Sagittarius,
    Capricorn,
    Aquarius,
    Pisces
}

public enum HouseSystem
{
    Placidus,
    WholeSign,
    Equal,
    Koch,
    Regiomontanus,
    Campanus,
    Porphyry
}

public readonly record struct EclipticPosition
{
    public double Longitude { get; init; }
    public double Latitude { get; init; }
    public double Distance { get; init; }
    public ZodiacSign Sign => (ZodiacSign)(int)(Longitude / 30.0);
    public double DegreeInSign => Longitude % 30.0;
}

public sealed record PlanetPosition
{
    public required Planet Planet { get; init; }
    public required EclipticPosition Position { get; init; }
    public double DailyMotion { get; init; }
    public bool IsRetrograde => DailyMotion < 0;
}

public sealed record GeoLocation
{
    public required double Latitude { get; init; }
    public required double Longitude { get; init; }
    public required string TimezoneId { get; init; }
}

public sealed record HouseCusp
{
    public required int HouseNumber { get; init; }
    public required EclipticPosition CuspPosition { get; init; }
}

public sealed record ChartTimeResolution
{
    public required string Role { get; init; }
    public LocalDateTime? InputLocalDateTime { get; init; }
    public required LocalDateTime ResolvedLocalDateTime { get; init; }
    public required string ResolvedTimeZoneId { get; init; }
    public required Offset AppliedOffset { get; init; }
    public required Instant UtcInstant { get; init; }
    public required Instant CalculationInstant { get; init; }
    public required string TzdbVersion { get; init; }
    public required IReadOnlyList<TimeResolutionWarning> Warnings { get; init; }
}

public sealed record TimeResolutionWarning
{
    public const string DstGapAdjusted = "dst_gap_adjusted";
    public const string DstOverlapResolved = "dst_overlap_resolved";

    public required string Code { get; init; }
    public required string Resolution { get; init; }
    public required LocalDateTime RequestedLocalDateTime { get; init; }
    public required LocalDateTime ResolvedLocalDateTime { get; init; }
    public required Offset SelectedOffset { get; init; }
    public required IReadOnlyList<Offset> CandidateOffsets { get; init; }
}
