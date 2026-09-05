using NodaTime;
using PathFinder.AccuracyBenchmark.Cases;

namespace PathFinder.AccuracyBenchmark.References;

public static class ReferenceManifestContractValidator
{
    public static void Validate(ReferenceManifest manifest, BenchmarkCaseManifest cases)
    {
        Require(manifest.DatasetRevision == cases.DatasetRevision,
            "Reference and case dataset revisions differ");
        ValidateJpl(manifest.Jpl, cases);
        ValidateSwiss(manifest.Swiss, cases);
        Require(manifest.Tzdb.ProviderVersion == DateTimeZoneProviders.Tzdb.VersionId,
            "TZDB providerVersion differs from the calculation dependency");
    }

    private static void ValidateJpl(JplReferenceSource source, BenchmarkCaseManifest cases)
    {
        var expected = HorizonsQueryBuilder.Build(cases)
            .Select(query => new ExpectedJplRequest(
                query.Id,
                query.Body,
                PositionPurpose(query.Id),
                query.Parameters,
                query.ExpectedTargetHeader,
                query.ExpectedRowTimes))
            .Concat(HorizonsQueryBuilder.BuildTimings(cases).Select(query => new ExpectedJplRequest(
                $"timing-{query.CaseId}",
                query.Body,
                $"timing:{query.Method}",
                query.Parameters,
                query.ExpectedTargetHeader,
                query.ExpectedRowTimes)))
            .ToArray();
        Require(source.Requests.Count == expected.Length,
            $"JPL request count must be exactly {expected.Length}");
        for (var index = 0; index < expected.Length; index++)
        {
            var actual = source.Requests[index];
            var contract = expected[index];
            Require(actual.Id == contract.Id, $"JPL request {index} id is invalid");
            Require(actual.Body == contract.Body, $"{contract.Id}.body is invalid");
            Require(actual.Purpose == contract.Purpose, $"{contract.Id}.purpose is invalid");
            Require(actual.ExpectedTargetHeader == contract.TargetHeader,
                $"{contract.Id}.expectedTargetHeader is invalid");
            Require(actual.ExpectedRowTimes.SequenceEqual(contract.RowTimes, StringComparer.Ordinal),
                $"{contract.Id}.expectedRowTimes are invalid");
            Require(DictionariesEqual(actual.Parameters, contract.Parameters),
                $"{contract.Id}.parameters do not match the frozen query");
            Require(actual.Response.Path == $"raw/jpl/{contract.Id}.response.json",
                $"{contract.Id}.response path is invalid");
            Require(actual.ResponseHeaders.Path == $"raw/jpl/{contract.Id}.headers.txt",
                $"{contract.Id}.responseHeaders path is invalid");
        }
    }

    private static void ValidateSwiss(SwissReferenceSource source, BenchmarkCaseManifest cases)
    {
        Require(source.VersionOutput.Path == "raw/swiss/version-identification.txt",
            "Swiss versionOutput path is invalid");
        Require(source.Requests.Count == cases.Houses.Count,
            $"Swiss request count must be exactly {cases.Houses.Count}");
        for (var index = 0; index < cases.Houses.Count; index++)
        {
            var row = cases.Houses[index];
            var actual = source.Requests[index];
            var invocation = SwissInvocationBuilder.BuildHouse(row);
            Require(actual.CaseId == row.Id, $"Swiss request {index} caseId is invalid");
            Require(actual.UtJulianDate == invocation.UtJulianDate,
                $"{row.Id}.utJulianDate is invalid");
            Require(actual.EastPositiveLongitude == row.EastPositiveLongitude,
                $"{row.Id}.eastPositiveLongitude is invalid");
            Require(actual.Latitude == row.Latitude, $"{row.Id}.latitude is invalid");
            Require(actual.HouseSystemCode == row.SwissHouseSystemCode,
                $"{row.Id}.houseSystemCode is invalid");
            Require(actual.Arguments.SequenceEqual(invocation.Arguments, StringComparer.Ordinal),
                $"{row.Id}.arguments do not match the frozen invocation");
            Require(actual.StandardOutput.Path == $"raw/swiss/{row.Id}.stdout.txt",
                $"{row.Id}.standardOutput path is invalid");
        }
    }

    private static bool DictionariesEqual(
        IReadOnlyDictionary<string, string> actual,
        IReadOnlyDictionary<string, string> expected) =>
        actual.Count == expected.Count && expected.All(pair =>
            actual.TryGetValue(pair.Key, out var value) && value == pair.Value);

    private static string PositionPurpose(string id) => id switch
    {
        HorizonsQueryBuilder.ChironSeedReferenceId => "chiron-seed-vector",
        _ when id.EndsWith("-positions-ut", StringComparison.Ordinal) => "positions-nominal-ut",
        _ when id.EndsWith("-positions-tt", StringComparison.Ordinal) => "positions-matched-tt",
        _ => throw new InvalidDataException($"Unknown Horizons request id: {id}")
    };

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidDataException(message);
        }
    }

    private sealed record ExpectedJplRequest(
        string Id,
        string Body,
        string Purpose,
        IReadOnlyDictionary<string, string> Parameters,
        string TargetHeader,
        IReadOnlyList<string> RowTimes);
}
