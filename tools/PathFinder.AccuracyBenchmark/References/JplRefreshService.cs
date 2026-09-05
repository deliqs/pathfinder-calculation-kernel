using System.Net;
using System.Text;
using PathFinder.AccuracyBenchmark.Cases;
using PathFinder.AccuracyBenchmark.Commands;
using PathFinder.AccuracyBenchmark.Serialization;

namespace PathFinder.AccuracyBenchmark.References;

public static class JplRefreshService
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static async Task<JplReferenceSource> RefreshAsync(
        string outputDirectory,
        string frozenReferencesDirectory,
        BenchmarkCaseManifest cases,
        IJplClient client,
        CancellationToken cancellationToken)
    {
        var output = CandidateOutputGuard.Validate(outputDirectory, frozenReferencesDirectory);
        Directory.CreateDirectory(Path.Combine(output, "jpl"));
        var requests = new List<JplReferenceRequest>();
        foreach (var query in HorizonsQueryBuilder.Build(cases))
        {
            var response = await ReadCheckedAsync(client, query.RequestUri, cancellationToken);
            var payload = Decode(response.Content);
            if (query.Id == HorizonsQueryBuilder.ChironSeedReferenceId)
            {
                var vector = HorizonsVectorParser.Parse(payload, new HorizonsVectorExpectation(
                    query.ExpectedTargetHeader,
                    "Sun (10)",
                    "ICRF",
                    "AU-D",
                    "TDB",
                    2451545.0,
                    HorizonsQueryBuilder.ApiSource,
                    HorizonsQueryBuilder.ApiVersion));
                ChironSeedBinding.Verify(vector);
            }
            else
            {
                var position = cases.Positions.First(row => row.Body == query.Body);
                _ = HorizonsResponseParser.Parse(payload, new HorizonsResponseExpectation(
                    query.Body,
                    position.HorizonsTargetId,
                    query.ExpectedTargetHeader,
                    "500@399",
                    "OBSERVER",
                    "31",
                    query.Parameters["TIME_TYPE"].Trim('\''),
                    "GREGORIAN",
                    HorizonsQueryBuilder.ApiSource,
                    HorizonsQueryBuilder.ApiVersion,
                    query.ExpectedRowTimes));
            }

            requests.Add(await ArchiveAsync(
                output,
                query.Id,
                query.Body,
                Purpose(query.Id),
                query.Parameters,
                query.ExpectedTargetHeader,
                query.ExpectedRowTimes,
                response,
                cancellationToken));
        }

        foreach (var query in HorizonsQueryBuilder.BuildTimings(cases))
        {
            var response = await ReadCheckedAsync(client, query.RequestUri, cancellationToken);
            _ = HorizonsResponseParser.Parse(Decode(response.Content), new HorizonsResponseExpectation(
                query.Body,
                cases.Positions.First(row => row.Body == query.Body).HorizonsTargetId,
                query.ExpectedTargetHeader,
                "500@399",
                "OBSERVER",
                "31",
                "UT",
                "GREGORIAN",
                HorizonsQueryBuilder.ApiSource,
                HorizonsQueryBuilder.ApiVersion,
                query.ExpectedRowTimes));
            requests.Add(await ArchiveAsync(
                output,
                $"timing-{query.CaseId}",
                query.Body,
                $"timing:{query.Method}",
                query.Parameters,
                query.ExpectedTargetHeader,
                query.ExpectedRowTimes,
                response,
                cancellationToken));
        }

        var source = new JplReferenceSource(
            HorizonsQueryBuilder.ApiSource,
            HorizonsQueryBuilder.ApiVersion,
            HorizonsQueryBuilder.Endpoint,
            requests);
        await File.WriteAllBytesAsync(
            Path.Combine(output, "jpl-reference-candidate.json"),
            CanonicalJson.Serialize(source),
            cancellationToken);
        await RefreshEvidenceWriter.WriteJplAsync(
            output, frozenReferencesDirectory, cases, source, cancellationToken);
        return source;
    }

    private static async Task<JplHttpResponse> ReadCheckedAsync(
        IJplClient client,
        Uri requestUri,
        CancellationToken cancellationToken)
    {
        var response = await client.GetAsync(requestUri, cancellationToken);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new InvalidDataException(
                $"Horizons returned HTTP {(int)response.StatusCode} ({response.StatusCode}).");
        }

        if (!response.Headers.Any(pair =>
                pair.Key.Equals("content-type", StringComparison.OrdinalIgnoreCase) &&
                pair.Value.StartsWith("application/json", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidDataException("Horizons response content type is not application/json.");
        }

        return response;
    }

    private static async Task<JplReferenceRequest> ArchiveAsync(
        string output,
        string id,
        string body,
        string purpose,
        IReadOnlyDictionary<string, string> parameters,
        string expectedTargetHeader,
        IReadOnlyList<string> expectedRowTimes,
        JplHttpResponse response,
        CancellationToken cancellationToken)
    {
        var responseArtifact = await WriteArtifactAsync(
            output, $"jpl/{id}.response.json", response.Content, cancellationToken);
        var headerBytes = SerializeHeaders(response);
        var headersArtifact = await WriteArtifactAsync(
            output, $"jpl/{id}.headers.txt", headerBytes, cancellationToken);
        return new JplReferenceRequest(
            id,
            body,
            purpose,
            Sort(parameters),
            expectedTargetHeader,
            expectedRowTimes,
            responseArtifact,
            headersArtifact);
    }

    private static byte[] SerializeHeaders(JplHttpResponse response)
    {
        var lines = new[] { $"HTTP {(int)response.StatusCode} {response.StatusCode}" }
            .Concat(response.Headers
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .ThenBy(pair => pair.Value, StringComparer.Ordinal)
                .Select(pair => $"{pair.Key}: {pair.Value}"));
        return Encoding.UTF8.GetBytes(string.Join('\n', lines) + "\n");
    }

    private static IReadOnlyDictionary<string, string> Sort(
        IReadOnlyDictionary<string, string> parameters)
    {
        var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var parameter in parameters)
        {
            result.Add(parameter.Key, parameter.Value);
        }

        return result;
    }

    private static async Task<RawArtifact> WriteArtifactAsync(
        string output,
        string relativePath,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        await File.WriteAllBytesAsync(Path.Combine(output, relativePath), bytes, cancellationToken);
        return new RawArtifact(relativePath, Sha256Verifier.Hash(bytes));
    }

    private static string Decode(byte[] bytes)
    {
        try
        {
            return StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException("Horizons response is not valid UTF-8.", exception);
        }
    }

    private static string Purpose(string id) => id switch
    {
        HorizonsQueryBuilder.ChironSeedReferenceId => "chiron-seed-vector",
        _ when id.EndsWith("-positions-ut", StringComparison.Ordinal) => "positions-nominal-ut",
        _ when id.EndsWith("-positions-tt", StringComparison.Ordinal) => "positions-matched-tt",
        _ => throw new InvalidDataException($"Unknown Horizons query purpose: {id}")
    };
}
