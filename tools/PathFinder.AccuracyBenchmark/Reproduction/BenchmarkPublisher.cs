using PathFinder.AccuracyBenchmark.Calculation;

namespace PathFinder.AccuracyBenchmark.Reproduction;

internal static class BenchmarkPublisher
{
    internal static IReadOnlyList<PositionResult> Positions(IEnumerable<PositionResult> values) =>
        values.Select(value => value with
        {
            PathfinderLongitudeDeg = PublicationPrecision.LongitudeDegrees(value.PathfinderLongitudeDeg),
            NominalUtcErrorArcsec = PublicationPrecision.AngularErrorArcseconds(value.NominalUtcErrorArcsec),
            MatchedTtErrorArcsec = PublicationPrecision.AngularErrorArcseconds(value.MatchedTtErrorArcsec),
            PathfinderDeltaTSeconds = PublicationPrecision.DeltaTSeconds(value.PathfinderDeltaTSeconds)
        }).ToArray();

    internal static IReadOnlyList<HouseCuspResult> HouseCusps(IEnumerable<HouseCuspResult> values) =>
        values.Select(value => value with
        {
            PathfinderLongitudeDeg = PublicationPrecision.LongitudeDegrees(value.PathfinderLongitudeDeg),
            AbsoluteErrorArcsec = PublicationPrecision.AngularErrorArcseconds(value.AbsoluteErrorArcsec)
        }).ToArray();

    internal static IReadOnlyList<TimingResult> Timings(IEnumerable<TimingResult> values) =>
        values.Select(value => value with
        {
            AbsoluteErrorMinutes = PublicationPrecision.TimingErrorMinutes(value.AbsoluteErrorMinutes)
        }).ToArray();
}
