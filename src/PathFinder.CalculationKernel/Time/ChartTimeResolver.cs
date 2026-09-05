using NodaTime;
using NodaTime.TimeZones;

namespace PathFinder.CalculationKernel.Time;

/// <summary>Resolves civil chart times with PathFinder's established lenient DST policy.</summary>
public static class ChartTimeResolver
{
    public static ChartTimeResolution ResolveLocal(
        string role,
        LocalDateTime localDateTime,
        string timezoneId)
    {
        var zone = DateTimeZoneProviders.Tzdb[timezoneId];
        var mapping = zone.MapLocal(localDateTime);
        var zoned = zone.AtLeniently(localDateTime);
        IReadOnlyList<TimeResolutionWarning> warnings = mapping.Count switch
        {
            0 =>
            [
                CreateWarning(
                    TimeResolutionWarning.DstGapAdjusted,
                    "shiftedForward",
                    localDateTime,
                    zoned,
                    mapping)
            ],
            2 =>
            [
                CreateWarning(
                    TimeResolutionWarning.DstOverlapResolved,
                    "earlierOccurrence",
                    localDateTime,
                    zoned,
                    mapping)
            ],
            _ => []
        };

        return Create(role, localDateTime, zoned, zone.Id, warnings);
    }

    public static ChartTimeResolution FromInstant(string role, Instant instant, string timezoneId)
    {
        var zone = DateTimeZoneProviders.Tzdb[timezoneId];
        return Create(role, null, instant.InZone(zone), zone.Id, []);
    }

    private static ChartTimeResolution Create(
        string role,
        LocalDateTime? input,
        ZonedDateTime zoned,
        string timezoneId,
        IReadOnlyList<TimeResolutionWarning> warnings) => new()
    {
        Role = role,
        InputLocalDateTime = input,
        ResolvedLocalDateTime = zoned.LocalDateTime,
        ResolvedTimeZoneId = timezoneId,
        AppliedOffset = zoned.Offset,
        UtcInstant = zoned.ToInstant(),
        CalculationInstant = zoned.ToInstant(),
        TzdbVersion = DateTimeZoneProviders.Tzdb.VersionId,
        Warnings = warnings
    };

    private static TimeResolutionWarning CreateWarning(
        string code,
        string resolution,
        LocalDateTime requested,
        ZonedDateTime selected,
        ZoneLocalMapping mapping) => new()
    {
        Code = code,
        Resolution = resolution,
        RequestedLocalDateTime = requested,
        ResolvedLocalDateTime = selected.LocalDateTime,
        SelectedOffset = selected.Offset,
        CandidateOffsets = [mapping.EarlyInterval.WallOffset, mapping.LateInterval.WallOffset]
    };
}
