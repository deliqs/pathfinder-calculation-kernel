using NodaTime;
using NodaTime.Text;
using PathFinder.AccuracyBenchmark.Calculation;
using PathFinder.AccuracyBenchmark.Cases;
using PathFinder.AccuracyBenchmark.References;
using PathFinder.CalculationKernel;
using PathFinder.CalculationKernel.Ephemeris;
using PathFinder.CalculationKernel.Houses;
using PathFinder.CalculationKernel.Provenance;
using PathFinder.CalculationKernel.Search;
using PathFinder.CalculationKernel.Time;

namespace PathFinder.AccuracyBenchmark.Reproduction;

public static class BenchmarkCalculator
{
    public static BenchmarkResults Calculate(
        BenchmarkCaseManifest cases,
        VerifiedReferenceManifest references,
        NormalizedReferences normalized,
        string sourceManifestSha256,
        string referenceManifestSha256)
    {
        var ephemeris = new AstronomyEngineEphemeris();
        var positions = CalculatePositions(cases, normalized, ephemeris);
        var houses = CalculateHouses(cases, normalized);
        var timings = CalculateTimings(cases, normalized, ephemeris);
        var historical = CalculateHistorical(cases);
        return new BenchmarkResults(
            2,
            cases.DatasetRevision,
            CreateProvenance(references, sourceManifestSha256, referenceManifestSha256),
            CreateSummary(positions, houses, timings, historical),
            BenchmarkPublisher.Positions(positions),
            BenchmarkPublisher.HouseCusps(houses),
            BenchmarkPublisher.Timings(timings),
            historical);
    }

    public static CalculationSourceManifest CreateSourceManifest(string repositoryRoot)
    {
        var sourceRoot = Path.Combine(repositoryRoot, "src", "PathFinder.CalculationKernel");
        var files = Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !ContainsDirectory(path, "bin") && !ContainsDirectory(path, "obj"))
            .Select(path => new CalculationSourceFile(
                Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/'),
                Sha256Verifier.Hash(File.ReadAllBytes(path))))
            .OrderBy(row => row.Path, StringComparer.Ordinal)
            .ToArray();
        if (files.Length == 0)
        {
            throw new InvalidDataException("Calculation source manifest contains no C# source files.");
        }

        return new CalculationSourceManifest(
            1,
            "PathFinder.CalculationKernel",
            CalculationKernelMetadata.CalculationRevision,
            files,
            CalculationKernelMetadata.SourceManifestInput
                .Select(row => new CalculationSourceProperty(row.Name, row.Value))
                .ToArray());
    }

    private static IReadOnlyList<PositionResult> CalculatePositions(
        BenchmarkCaseManifest cases,
        NormalizedReferences normalized,
        AstronomyEngineEphemeris ephemeris) => cases.Positions.Select(row =>
    {
        var reference = normalized.Positions.Single(value => value.CaseId == row.Id);
        var instant = ParseInstant(row.Utc);
        var longitude = ephemeris.GetLongitude(Enum.Parse<Planet>(row.Body), instant);
        var nominalError = BenchmarkMath.CircularDistanceDegrees(
            longitude, reference.NominalLongitudeDeg) * 3600;
        var matchedError = BenchmarkMath.CircularDistanceDegrees(
            longitude, reference.MatchedLongitudeDeg) * 3600;
        return new PositionResult(
            row.Id,
            row.Body,
            row.Utc,
            longitude,
            reference.NominalLongitudeDeg,
            reference.MatchedLongitudeDeg,
            nominalError,
            matchedError,
            AstronomyEngineTime.GetMetadata(instant).DeltaTSeconds,
            reference.NominalTimeType,
            reference.NominalTimeValue,
            reference.MatchedTimeType,
            reference.MatchedTimeValue,
            row.ToleranceArcsec,
            BenchmarkMath.Passed(nominalError, row.ToleranceArcsec) &&
            BenchmarkMath.Passed(matchedError, row.ToleranceArcsec));
    }).ToArray();

    private static IReadOnlyList<HouseCuspResult> CalculateHouses(
        BenchmarkCaseManifest cases,
        NormalizedReferences normalized)
    {
        var calculator = new HouseCalculator();
        var result = new List<HouseCuspResult>();
        foreach (var row in cases.Houses)
        {
            var location = new GeoLocation
            {
                Latitude = row.Latitude,
                Longitude = row.EastPositiveLongitude,
                TimezoneId = "UTC"
            };
            var calculated = calculator.CalculateHouses(
                ParseInstant(row.Utc),
                location,
                Enum.Parse<HouseSystem>(row.RequestedSystem));
            var reference = normalized.HouseCusps.Where(value => value.CaseId == row.Id).ToArray();
            foreach (var cusp in calculated.OrderBy(value => value.HouseNumber))
            {
                var expected = reference.Single(value => value.Cusp == cusp.HouseNumber);
                var error = BenchmarkMath.CircularDistanceDegrees(
                    cusp.CuspPosition.Longitude, expected.LongitudeDeg) * 3600;
                result.Add(new HouseCuspResult(
                    row.Id,
                    row.Utc,
                    row.Latitude,
                    row.EastPositiveLongitude,
                    row.RequestedSystem,
                    row.ReferenceSystem,
                    cusp.HouseNumber,
                    cusp.CuspPosition.Longitude,
                    expected.LongitudeDeg,
                    error,
                    row.ToleranceArcsec,
                    BenchmarkMath.Passed(error, row.ToleranceArcsec)));
            }
        }

        return result;
    }

    private static IReadOnlyList<TimingResult> CalculateTimings(
        BenchmarkCaseManifest cases,
        NormalizedReferences normalized,
        AstronomyEngineEphemeris ephemeris)
    {
        var crossings = new LongitudeCrossingFinder(ephemeris);
        var stations = new StationFinder(ephemeris);
        return cases.Timings.Select(row =>
        {
            var start = ParseInstant(row.SearchStartUtc);
            var reference = normalized.Timings.Single(value => value.CaseId == row.Id);
            var referenceInstant = ParseInstant(reference.ReferenceUtc);
            var calculated = row.Kind == "longitude-crossing"
                ? crossings.FindFirst(
                    Enum.Parse<Planet>(row.Body),
                    row.TargetLongitudeDeg!.Value,
                    row.Motion == "direct" ? LongitudeMotion.Direct : LongitudeMotion.Retrograde,
                    start,
                    Duration.FromDays(row.SearchWindowDays))
                : stations.Find(
                    Enum.Parse<Planet>(row.Body),
                    start,
                    Duration.FromDays(row.SearchWindowDays),
                    row.Extremum == "minimum" ? StationExtremum.Minimum : StationExtremum.Maximum);
            if (calculated is null)
            {
                throw new InvalidDataException($"PathFinder found no timing event for {row.Id}.");
            }

            var error = Math.Abs((calculated.Value - referenceInstant).TotalMinutes);
            return new TimingResult(
                row.Id,
                row.Kind,
                row.Body,
                row.Motion,
                row.TargetLongitudeDeg,
                row.Extremum,
                row.SearchStartUtc,
                row.SearchWindowDays,
                row.Method,
                reference.ReferenceMethod,
                reference.ReferenceUtc,
                InstantPattern.ExtendedIso.Format(calculated.Value),
                error,
                row.ToleranceMinutes,
                BenchmarkMath.Passed(error, row.ToleranceMinutes));
        }).ToArray();
    }

    private static IReadOnlyList<HistoricalTimeResult> CalculateHistorical(
        BenchmarkCaseManifest cases) => cases.HistoricalTimes.Select(row =>
    {
        var requested = LocalDateTimePattern.ExtendedIso.Parse(row.RequestedLocal).Value;
        var resolved = ChartTimeResolver.ResolveLocal("benchmark", requested, row.ZoneId);
        return new HistoricalTimeResult(
            row.Id,
            row.RequestedLocal,
            row.ZoneId,
            row.ResolutionMethod,
            row.CompatibilityCase,
            LocalDateTimePattern.ExtendedIso.Format(resolved.ResolvedLocalDateTime),
            InstantPattern.ExtendedIso.Format(resolved.UtcInstant),
            resolved.AppliedOffset.ToString(),
            resolved.Warnings.Select(warning => warning.Code).ToArray(),
            resolved.TzdbVersion,
            true);
    }).ToArray();

    private static BenchmarkProvenance CreateProvenance(
        VerifiedReferenceManifest references,
        string sourceManifestSha256,
        string referenceManifestSha256) => new(
        "PathFinder.CalculationKernel",
        CalculationKernelMetadata.CalculationRevision,
        sourceManifestSha256,
        referenceManifestSha256,
        "2.1.19",
        references.Manifest.Tzdb.NodaTimePackageVersion,
        references.Manifest.Tzdb.ProviderVersion,
        references.Manifest.Jpl.ApiSource,
        references.Manifest.Jpl.ApiVersion,
        references.Manifest.Swiss.RequiredVersion,
        references.Manifest.Swiss.SourceCommit,
        references.Manifest.Swiss.ExecutableSha256,
        "Horizons TIME_TYPE=UT means UT1 before 1962 and UTC from 1962 onward.",
        "The 1950 proleptic-UTC calendar input is interpreted by Horizons as UT1; no EOP correction is applied.",
        "Swiss swetest -ut receives the recorded UT calendar/Julian date for house sidereal-time calculation; no DUT1 correction is applied.",
        0);

    private static BenchmarkSummary CreateSummary(
        IReadOnlyList<PositionResult> positions,
        IReadOnlyList<HouseCuspResult> houses,
        IReadOnlyList<TimingResult> timings,
        IReadOnlyList<HistoricalTimeResult> historical) => new(
        positions.Count,
        positions.Count(row => row.Passed),
        PublicationPrecision.AngularErrorArcseconds(
            BenchmarkMath.Median(positions.Select(row => row.NominalUtcErrorArcsec))),
        PublicationPrecision.AngularErrorArcseconds(
            BenchmarkMath.Maximum(positions.Select(row => row.NominalUtcErrorArcsec))),
        PublicationPrecision.AngularErrorArcseconds(
            BenchmarkMath.Median(positions.Select(row => row.MatchedTtErrorArcsec))),
        PublicationPrecision.AngularErrorArcseconds(
            BenchmarkMath.Maximum(positions.Select(row => row.MatchedTtErrorArcsec))),
        houses.Count,
        houses.Count(row => row.Passed),
        PublicationPrecision.AngularErrorArcseconds(
            BenchmarkMath.Maximum(houses.Select(row => row.AbsoluteErrorArcsec))),
        timings.Count,
        timings.Count(row => row.Passed),
        PublicationPrecision.TimingErrorMinutes(
            BenchmarkMath.Maximum(timings.Select(row => row.AbsoluteErrorMinutes))),
        historical.Count,
        historical.Count(row => row.Executed));

    private static Instant ParseInstant(string value)
    {
        var parsed = InstantPattern.ExtendedIso.Parse(value);
        return parsed.Success
            ? parsed.Value
            : throw new InvalidDataException($"Invalid benchmark instant: {value}");
    }

    private static bool ContainsDirectory(string path, string directory) =>
        path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Contains(directory, StringComparer.Ordinal);
}
