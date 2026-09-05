using System.Net;

namespace PathFinder.AccuracyBenchmark.References;

public sealed record JplHttpResponse(
    HttpStatusCode StatusCode,
    byte[] Content,
    IReadOnlyList<KeyValuePair<string, string>> Headers);

public interface IJplClient
{
    Task<JplHttpResponse> GetAsync(Uri requestUri, CancellationToken cancellationToken);
}

public sealed class JplClient(HttpClient httpClient) : IJplClient
{
    public async Task<JplHttpResponse> GetAsync(
        Uri requestUri,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(requestUri, cancellationToken);
        var content = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        var headers = response.Headers
            .Concat(response.Content.Headers)
            .SelectMany(pair => pair.Value.Select(value =>
                new KeyValuePair<string, string>(pair.Key, value)))
            .ToArray();
        return new JplHttpResponse(response.StatusCode, content, headers);
    }
}
