using System.Globalization;
using NodaTime;
using NodaTime.Text;
using PathFinder.AccuracyBenchmark.Cases;

namespace PathFinder.AccuracyBenchmark.References;

public sealed record SwissInvocation(
    IReadOnlyList<string> Arguments,
    IReadOnlyDictionary<string, string> Environment,
    double? UtJulianDate);

public static class SwissInvocationBuilder
{
    private const double UnixEpochJulianDate = 2440587.5;

    public static SwissInvocation BuildVersion() => new(
        ["-h"],
        DeterministicEnvironment(),
        null);

    public static SwissInvocation BuildHouse(HouseCase row)
    {
        var parsed = InstantPattern.ExtendedIso.Parse(row.Utc);
        if (!parsed.Success)
        {
            throw new InvalidDataException($"House case UTC is not parseable: {row.Utc}");
        }

        var julianDate = UnixEpochJulianDate +
            parsed.Value.ToUnixTimeTicks() / (double)NodaConstants.TicksPerDay;
        var house = $"-house{Format(row.EastPositiveLongitude)},{Format(row.Latitude)}," +
            row.SwissHouseSystemCode;
        return new SwissInvocation(
            [$"-bj{Format(julianDate)}", "-ut", "-p", house, "-fPl", "-head"],
            DeterministicEnvironment(),
            julianDate);
    }

    private static IReadOnlyDictionary<string, string> DeterministicEnvironment() =>
        new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["LC_ALL"] = "C",
            ["TZ"] = "UTC"
        };

    private static string Format(double value) =>
        value.ToString("0.################", CultureInfo.InvariantCulture);
}
