using NodaTime.Text;
using PathFinder.CalculationKernel.Time;

namespace PathFinder.CalculationKernel.Tests;

public class ChartTimeResolverTests
{
    [Theory]
    [InlineData(
        "2011-12-30T12:00:00",
        "Pacific/Apia",
        "2011-12-31T12:00:00",
        "+14",
        "2011-12-30T22:00:00Z",
        TimeResolutionWarning.DstGapAdjusted)]
    [InlineData(
        "1941-07-01T12:00:00",
        "Europe/London",
        "1941-07-01T12:00:00",
        "+02",
        "1941-07-01T10:00:00Z",
        null)]
    [InlineData(
        "1850-01-01T12:00:00",
        "Europe/Amsterdam",
        "1850-01-01T12:00:00",
        "+00:17:30",
        "1850-01-01T11:42:30Z",
        null)]
    public void ResolveLocal_PublishedHistoricalCase_MatchesPinnedTzdbCompatibility(
        string requestedText,
        string zoneId,
        string expectedResolvedText,
        string expectedOffset,
        string expectedUtcText,
        string? expectedWarning)
    {
        var requested = LocalDateTimePattern.ExtendedIso.Parse(requestedText).Value;
        var expectedResolved = LocalDateTimePattern.ExtendedIso.Parse(expectedResolvedText).Value;
        var expectedUtc = InstantPattern.ExtendedIso.Parse(expectedUtcText).Value;

        var result = ChartTimeResolver.ResolveLocal("benchmark", requested, zoneId);

        Assert.Equal(expectedResolved, result.ResolvedLocalDateTime);
        Assert.Equal(expectedOffset, result.AppliedOffset.ToString());
        Assert.Equal(expectedUtc, result.UtcInstant);
        Assert.Equal(
            expectedWarning is null ? [] : [expectedWarning],
            result.Warnings.Select(warning => warning.Code));
        Assert.Equal("TZDB: 2026c (mapping: 48.2)", result.TzdbVersion);
    }
}
