using System.Diagnostics;
using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using UsaepaySupportTestbench.Models;

namespace UsaepaySupportTestbench.Services;

public sealed class RestProxyService(
    IHttpClientFactory httpClientFactory,
    IOptions<UsaepayOptions> options,
    ILogger<RestProxyService> logger)
{
    public async Task<ProxyResponse> ExecuteAsync(ProxyRestRequest request, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("UsaepayProxy");
        var baseUrl = ResolveBaseUrl(request.Environment);
        var requestUrl = BuildRequestUrl(baseUrl, request.PathOrUrl);

        using var httpRequest = new HttpRequestMessage(new HttpMethod(request.Method), requestUrl);
        ApplyHeaders(httpRequest, request.Headers);

        if (!string.IsNullOrWhiteSpace(request.Body))
        {
            var contentType = ResolveContentType(request.Headers);
            httpRequest.Content = new StringContent(request.Body);
            httpRequest.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        }

        var stopwatch = Stopwatch.StartNew();
        using var response = await client.SendAsync(httpRequest, cancellationToken);
        stopwatch.Stop();

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var headers = response.Headers.Concat(response.Content.Headers)
            .ToDictionary(h => h.Key, h => string.Join(",", h.Value));

        logger.LogInformation("REST proxy {Method} {Url} -> {StatusCode} in {Latency}ms",
            request.Method, requestUrl, (int)response.StatusCode, stopwatch.ElapsedMilliseconds);

        return new ProxyResponse
        {
            StatusCode = (int)response.StatusCode,
            Body = responseBody,
            Headers = headers,
            LatencyMs = stopwatch.ElapsedMilliseconds
        };
    }

    private string ResolveBaseUrl(EnvironmentType environment)
    {
        var envOptions = environment == EnvironmentType.Production
            ? options.Value.Production
            : options.Value.Sandbox;

        if (string.IsNullOrWhiteSpace(envOptions.RestBaseUrl))
        {
            throw new InvalidOperationException($"REST base URL not configured for {environment}.");
        }

        return envOptions.RestBaseUrl.TrimEnd('/');
    }

    private static string BuildRequestUrl(string baseUrl, string pathOrUrl)
    {
        if (string.IsNullOrWhiteSpace(pathOrUrl))
        {
            return baseUrl;
        }

        if (Uri.TryCreate(pathOrUrl, UriKind.Absolute, out var absolute))
        {
            return absolute.ToString();
        }

        return $"{baseUrl}/{pathOrUrl.TrimStart('/')}";
    }

    private static string ResolveContentType(Dictionary<string, string>? headers)
    {
        if (headers is null)
        {
            return "application/json";
        }

        foreach (var header in headers)
        {
            if (string.Equals(header.Key, "Content-Type", StringComparison.OrdinalIgnoreCase))
            {
                return header.Value;
            }
        }

        return "application/json";
    }

    private static void ApplyHeaders(HttpRequestMessage request, Dictionary<string, string>? headers)
    {
        if (headers is null)
        {
            return;
        }

        foreach (var header in headers)
        {
            if (string.Equals(header.Key, "Content-Type", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
    }
}
