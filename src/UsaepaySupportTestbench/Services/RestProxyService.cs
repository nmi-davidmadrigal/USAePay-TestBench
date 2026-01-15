using System.Diagnostics;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using UsaepaySupportTestbench.Models;

namespace UsaepaySupportTestbench.Services;

public sealed class RestProxyService(
    IHttpClientFactory httpClientFactory,
    IOptions<UsaepayOptions> options,
    IHttpContextAccessor httpContextAccessor,
    ILogger<RestProxyService> logger)
{
    public async Task<ProxyResponse> ExecuteAsync(ProxyRestRequest request, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("UsaepayProxy");
        var baseUrl = ResolveBaseUrl(request.Environment);
        var requestUrl = BuildRequestUrl(baseUrl, request.PathOrUrl);

        using var httpRequest = new HttpRequestMessage(new HttpMethod(request.Method), requestUrl);
        ApplyHeaders(httpRequest, request.Headers);
        ApplyUsaepayApiHashAuthIfConfigured(httpRequest, request.Environment, request.Headers, httpContextAccessor.HttpContext?.Session);

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

    private void ApplyUsaepayApiHashAuthIfConfigured(
        HttpRequestMessage httpRequest,
        EnvironmentType environment,
        Dictionary<string, string>? requestHeaders,
        ISession? session)
    {
        if (HasAuthorizationHeader(requestHeaders, httpRequest))
        {
            return;
        }

        var (sourceKey, pin) = ResolveCredentials(environment, session);
        if (string.IsNullOrWhiteSpace(sourceKey) || string.IsNullOrWhiteSpace(pin))
        {
            return;
        }

        // USAePay REST auth: Basic base64(sourceKey:apihash)
        // apihash = "s2/{seed}/{sha256(sourceKey + seed + pin)}"
        var seed = GenerateSeed(16);
        var prehash = $"{sourceKey}{seed}{pin}";
        var hashHex = Sha256Hex(prehash);
        var apiHash = $"s2/{seed}/{hashHex}";
        var authKey = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{sourceKey}:{apiHash}"));
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", authKey);
    }

    private (string? SourceKey, string? Pin) ResolveCredentials(EnvironmentType environment, ISession? session)
    {
        // Session overrides config (useful for local interactive debugging).
        var prefix = environment == EnvironmentType.Production ? "Usaepay:Production" : "Usaepay:Sandbox";
        var sessionSourceKey = session?.GetString($"{prefix}:SourceKey") ?? session?.GetString($"{prefix}:ApiKey");
        var sessionPin = session?.GetString($"{prefix}:Pin") ?? session?.GetString($"{prefix}:ApiSecret");
        if (!string.IsNullOrWhiteSpace(sessionSourceKey) && !string.IsNullOrWhiteSpace(sessionPin))
        {
            return (sessionSourceKey, sessionPin);
        }

        var envOptions = environment == EnvironmentType.Production
            ? options.Value.Production
            : options.Value.Sandbox;

        return (envOptions.SourceKey ?? envOptions.ApiKey, envOptions.Pin ?? envOptions.ApiSecret);
    }

    private static bool HasAuthorizationHeader(Dictionary<string, string>? headers, HttpRequestMessage httpRequest)
    {
        if (httpRequest.Headers.Authorization is not null)
        {
            return true;
        }

        if (headers is null)
        {
            return false;
        }

        foreach (var header in headers)
        {
            if (string.Equals(header.Key, "Authorization", StringComparison.OrdinalIgnoreCase))
            {
                return !string.IsNullOrWhiteSpace(header.Value);
            }
        }

        return false;
    }

    private static string GenerateSeed(int length)
    {
        // Use URL-safe base64 for compact seed, then trim to requested length.
        // USAePay examples use ~16 chars; exact charset isn't important as it's included verbatim in the hash string.
        var sb = new StringBuilder(length);
        Span<byte> bytes = stackalloc byte[32];

        while (sb.Length < length)
        {
            RandomNumberGenerator.Fill(bytes);
            var chunk = Convert.ToBase64String(bytes)
                .Replace("+", string.Empty, StringComparison.Ordinal)
                .Replace("/", string.Empty, StringComparison.Ordinal)
                .Replace("=", string.Empty, StringComparison.Ordinal);
            sb.Append(chunk);
        }

        return sb.ToString(0, length);
    }

    private static string Sha256Hex(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
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
