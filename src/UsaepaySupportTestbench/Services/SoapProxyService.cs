using System.Diagnostics;
using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using UsaepaySupportTestbench.Models;

namespace UsaepaySupportTestbench.Services;

public sealed class SoapProxyService(
    IHttpClientFactory httpClientFactory,
    IOptions<UsaepayOptions> options,
    ILogger<SoapProxyService> logger)
{
    public async Task<ProxyResponse> ExecuteAsync(ProxySoapRequest request, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("UsaepayProxy");

        // Manual SOAP envelope sender is preferred here: it allows arbitrary WSDL versions,
        // custom headers, and malformed requests without re-generating typed clients.
        var endpoint = ResolveEndpoint(request);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
        httpRequest.Content = new StringContent(request.Body);
        var contentType = ResolveContentType(request.Headers);
        httpRequest.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);

        if (!string.IsNullOrWhiteSpace(request.SoapAction))
        {
            httpRequest.Headers.Add("SOAPAction", request.SoapAction);
        }

        ApplyHeaders(httpRequest, request.Headers);

        var stopwatch = Stopwatch.StartNew();
        using var response = await client.SendAsync(httpRequest, cancellationToken);
        stopwatch.Stop();

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var headers = response.Headers.Concat(response.Content.Headers)
            .ToDictionary(h => h.Key, h => string.Join(",", h.Value));

        var faultDetected = responseBody.Contains("Fault", StringComparison.OrdinalIgnoreCase);

        logger.LogInformation("SOAP proxy {Endpoint} -> {StatusCode} in {Latency}ms",
            endpoint, (int)response.StatusCode, stopwatch.ElapsedMilliseconds);

        return new ProxyResponse
        {
            StatusCode = (int)response.StatusCode,
            Body = responseBody,
            Headers = headers,
            LatencyMs = stopwatch.ElapsedMilliseconds,
            SoapFault = faultDetected
        };
    }

    private string ResolveEndpoint(ProxySoapRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.EndpointUrl))
        {
            return request.EndpointUrl;
        }

        var envOptions = options.Value.Sandbox;

        if (string.IsNullOrWhiteSpace(envOptions.SoapEndpoint))
        {
            throw new InvalidOperationException("SOAP endpoint not configured for sandbox.");
        }

        return envOptions.SoapEndpoint;
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

    private static string ResolveContentType(Dictionary<string, string>? headers)
    {
        if (headers is null)
        {
            return "text/xml; charset=utf-8";
        }

        foreach (var header in headers)
        {
            if (string.Equals(header.Key, "Content-Type", StringComparison.OrdinalIgnoreCase))
            {
                return header.Value;
            }
        }

        return "text/xml; charset=utf-8";
    }
}
