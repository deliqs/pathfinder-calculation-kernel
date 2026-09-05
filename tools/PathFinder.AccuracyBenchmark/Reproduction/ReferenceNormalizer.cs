using System.Text;
using NodaTime.Text;
using PathFinder.AccuracyBenchmark.Cases;
using PathFinder.AccuracyBenchmark.References;

namespace PathFinder.AccuracyBenchmark.Reproduction;

public static class ReferenceNormalizer
{
    public static NormalizedReferences Normalize(
        BenchmarkCaseManifest cases,
        VerifiedReferenceManifest verified)
    {
        var positions = new Dictionary<string, ParsedPositionPair>(StringComparer.Ordinal);
        var timings = new List<NormalizedTimingReference>();
        NormalizedChironSeed? chironSeed = null;
        foreach (var request in verified.Manifest.Jpl.Requests)
        {
            var payload = Encoding.UTF8.GetString(verified.Read(request.Response));
            if (request.Purpose == "chiron-seed-vector")
            {
                chironSeed = ParseChironSeed(request, payload, verified.Manifest.Jpl);
                continue;
            }

            var rows = ParseLongitudes(request, payload, cases, verified.Manifest.Jpl);
            if (request.Purpose.StartsWith("timing:", StringComparison.Ordinal))
            {
                var caseId = request.Id["timing-".Length..];
                var timing = JplTimingDeriver.Derive(
                    cases.Timings.Single(row => row.Id == caseId), rows);
                timings.Add(new NormalizedTimingReference(
                    timing.CaseId,
                    timing.ReferenceMethod,
                    InstantPattern.ExtendedIso.Format(timing.ReferenceInstant)));
            }
            else
            {
                AddPositions(positions, request, rows, cases);
            }
        }

        if (chironSeed is null)
        {
            throw new InvalidDataException("The verified references do not contain a Chiron seed vector.");
        }

        return new NormalizedReferences(
            2,
            cases.DatasetRevision,
            cases.Positions.Select(row => CreatePosition(row, positions)).ToArray(),
            ParseHouses(cases, verified),
            cases.Timings.Select(row => timings.Single(value => value.CaseId == row.Id)).ToArray(),
            chironSeed);
    }

    private static IReadOnlyList<HorizonsLongitudeRow> ParseLongitudes(
        JplReferenceRequest request,
        string payload,
        BenchmarkCaseManifest cases,
        JplReferenceSource source)
    {
        var body = cases.Positions.First(row => row.Body == request.Body);
        return HorizonsResponseParser.Parse(payload, new HorizonsResponseExpectation(
            request.Body,
            body.HorizonsTargetId,
            request.ExpectedTargetHeader,
            "500@399",
            "OBSERVER",
            "31",
            request.Parameters["TIME_TYPE"].Trim('\''),
            "GREGORIAN",
            source.ApiSource,
            source.ApiVersion,
            request.ExpectedRowTimes));
    }

    private static void AddPositions(
        IDictionary<string, ParsedPositionPair> result,
        JplReferenceRequest request,
        IReadOnlyList<HorizonsLongitudeRow> rows,
        BenchmarkCaseManifest cases)
    {
        var caseRows = cases.Positions.Where(row => row.Body == request.Body).ToArray();
        if (caseRows.Length != rows.Count)
        {
            throw new InvalidDataException($"Position reference count differs for {request.Body}.");
        }

        var timeType = request.Parameters["TIME_TYPE"].Trim('\'');
        for (var index = 0; index < rows.Count; index++)
        {
            result.TryGetValue(caseRows[index].Id, out var pair);
            pair ??= new ParsedPositionPair();
            if (timeType == "UT") pair.Nominal = rows[index];
            else if (timeType == "TT") pair.Matched = rows[index];
            else throw new InvalidDataException($"Unsupported position time type: {timeType}");
            result[caseRows[index].Id] = pair;
        }
    }

    private static NormalizedPositionReference CreatePosition(
        PositionCase row,
        IReadOnlyDictionary<string, ParsedPositionPair> references)
    {
        if (!references.TryGetValue(row.Id, out var pair) || pair.Nominal is null || pair.Matched is null)
        {
            throw new InvalidDataException($"Position references are incomplete for {row.Id}.");
        }

        return new NormalizedPositionReference(
            row.Id,
            row.Body,
            "UT",
            pair.Nominal.Time,
            pair.Nominal.LongitudeDegrees,
            "TT",
            pair.Matched.Time,
            pair.Matched.LongitudeDegrees,
            pair.Nominal.TargetHeader);
    }

    private static IReadOnlyList<NormalizedHouseReference> ParseHouses(
        BenchmarkCaseManifest cases,
        VerifiedReferenceManifest verified)
    {
        SwissOutputParser.ParseVersion(
            Encoding.UTF8.GetString(verified.Read(verified.Manifest.Swiss.VersionOutput)),
            verified.Manifest.Swiss.RequiredVersion);
        var result = new List<NormalizedHouseReference>();
        foreach (var request in verified.Manifest.Swiss.Requests)
        {
            var row = cases.Houses.Single(value => value.Id == request.CaseId);
            var parsed = SwissOutputParser.Parse(
                Encoding.UTF8.GetString(verified.Read(request.StandardOutput)),
                new SwissOutputExpectation(
                    request.CaseId,
                    request.HouseSystemCode,
                    request.UtJulianDate,
                    request.EastPositiveLongitude,
                    request.Latitude));
            result.AddRange(parsed.Select(cusp => new NormalizedHouseReference(
                request.CaseId,
                row.ReferenceSystem,
                cusp.Cusp,
                cusp.LongitudeDegrees)));
        }

        return result;
    }

    private static NormalizedChironSeed ParseChironSeed(
        JplReferenceRequest request,
        string payload,
        JplReferenceSource source)
    {
        var row = HorizonsVectorParser.Parse(payload, new HorizonsVectorExpectation(
            request.ExpectedTargetHeader,
            "Sun (10)",
            "ICRF",
            "AU-D",
            "TDB",
            2451545.0,
            source.ApiSource,
            source.ApiVersion));
        ChironSeedBinding.Verify(row);
        return new NormalizedChironSeed(
            request.Id,
            request.ExpectedTargetHeader,
            "Sun (10)",
            "ICRF/J2000",
            "AU-D",
            "TDB",
            row.JulianDateTdb,
            row.X,
            row.Y,
            row.Z,
            row.Vx,
            row.Vy,
            row.Vz);
    }

    private sealed class ParsedPositionPair
    {
        internal HorizonsLongitudeRow? Nominal { get; set; }
        internal HorizonsLongitudeRow? Matched { get; set; }
    }
}
