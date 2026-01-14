using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UsaepaySupportTestbench.Data;
using UsaepaySupportTestbench.Models;

namespace UsaepaySupportTestbench.Services;

public sealed class ScenarioRunService(
    ApplicationDbContext dbContext,
    RestProxyService restProxyService,
    SoapProxyService soapProxyService,
    RedactionService redactionService)
{
    public async Task<List<ScenarioRun>> GetRecentRunsAsync(int take = 15)
    {
        return await dbContext.ScenarioRuns
            .Include(r => r.Preset)
            .OrderByDescending(r => r.CreatedAt)
            .Take(take)
            .ToListAsync();
    }

    public async Task<List<ScenarioRun>> GetRecentErrorsAsync(int take = 5)
    {
        return await dbContext.ScenarioRuns
            .Include(r => r.Preset)
            .Where(r => r.HttpStatus >= 400 || r.SoapFault == true)
            .OrderByDescending(r => r.CreatedAt)
            .Take(take)
            .ToListAsync();
    }

    public async Task<ScenarioRun> RecordRestAsync(ProxyRestRequest request, ProxyResponse response)
    {
        var run = new ScenarioRun
        {
            PresetId = request.PresetId,
            ApiType = ApiType.Rest,
            Environment = request.Environment,
            RequestRedacted = redactionService.Redact(SerializeRequest(request)),
            ResponseRedacted = redactionService.Redact(response.Body),
            HttpStatus = response.StatusCode,
            LatencyMs = response.LatencyMs,
            CorrelationId = Guid.NewGuid().ToString("N"),
            TicketNumber = request.TicketNumber
        };

        dbContext.ScenarioRuns.Add(run);
        await dbContext.SaveChangesAsync();
        return run;
    }

    public async Task<ScenarioRun> RecordSoapAsync(ProxySoapRequest request, ProxyResponse response)
    {
        var run = new ScenarioRun
        {
            PresetId = request.PresetId,
            ApiType = ApiType.Soap,
            Environment = request.Environment,
            RequestRedacted = redactionService.Redact(SerializeRequest(request)),
            ResponseRedacted = redactionService.Redact(response.Body),
            HttpStatus = response.StatusCode,
            SoapFault = response.SoapFault,
            LatencyMs = response.LatencyMs,
            CorrelationId = Guid.NewGuid().ToString("N"),
            TicketNumber = request.TicketNumber
        };

        dbContext.ScenarioRuns.Add(run);
        await dbContext.SaveChangesAsync();
        return run;
    }

    public async Task<ScenarioRun> ExecutePresetAsync(Preset preset, string? ticketNumber, bool confirmProduction, CancellationToken cancellationToken)
    {
        if (preset.Environment == EnvironmentType.Production && !confirmProduction)
        {
            throw new InvalidOperationException("Production requests require explicit confirmation.");
        }

        if (preset.ApiType == ApiType.PayJsFlow)
        {
            throw new InvalidOperationException("Pay.js presets are client-side only.");
        }

        if (preset.ApiType == ApiType.Rest)
        {
            var request = new ProxyRestRequest
            {
                Environment = preset.Environment,
                Method = preset.RestMethod ?? "POST",
                PathOrUrl = preset.RestPathOrEndpoint ?? string.Empty,
                Headers = DeserializeHeaders(preset.HeadersJson),
                Body = preset.BodyTemplate,
                PresetId = preset.Id,
                TicketNumber = ticketNumber,
                ConfirmProduction = confirmProduction
            };

            var response = await restProxyService.ExecuteAsync(request, cancellationToken);
            return await RecordRestAsync(request, response);
        }

        var soapRequest = new ProxySoapRequest
        {
            Environment = preset.Environment,
            SoapAction = preset.SoapAction ?? string.Empty,
            EndpointUrl = preset.RestPathOrEndpoint,
            Headers = DeserializeHeaders(preset.HeadersJson),
            Body = preset.BodyTemplate ?? string.Empty,
            PresetId = preset.Id,
            TicketNumber = ticketNumber,
            ConfirmProduction = confirmProduction
        };

        var soapResponse = await soapProxyService.ExecuteAsync(soapRequest, cancellationToken);
        return await RecordSoapAsync(soapRequest, soapResponse);
    }

    private static string SerializeRequest(ProxyRestRequest request)
    {
        return JsonSerializer.Serialize(new
        {
            request.Method,
            request.PathOrUrl,
            request.Headers,
            request.Body
        }, JsonSerializerOptions());
    }

    private static string SerializeRequest(ProxySoapRequest request)
    {
        return JsonSerializer.Serialize(new
        {
            request.SoapAction,
            request.EndpointUrl,
            request.Headers,
            request.Body
        }, JsonSerializerOptions());
    }

    private static Dictionary<string, string>? DeserializeHeaders(string? headersJson)
    {
        if (string.IsNullOrWhiteSpace(headersJson))
        {
            return null;
        }

        return JsonSerializer.Deserialize<Dictionary<string, string>>(headersJson, JsonSerializerOptions());
    }

    private static JsonSerializerOptions JsonSerializerOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
    }
}
