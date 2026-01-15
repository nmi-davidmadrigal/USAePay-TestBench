using System.Text.RegularExpressions;
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
    private static readonly Regex TemplateVariableRegex = new(@"\{\{(?<name>[a-zA-Z0-9_:-]+)\}\}",
        RegexOptions.Compiled);

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

        var variables = DeserializeVariables(preset.VariablesJson);

        if (preset.ApiType == ApiType.Rest)
        {
            var request = new ProxyRestRequest
            {
                Environment = preset.Environment,
                Method = RenderTemplate(preset.RestMethod ?? "POST", variables),
                PathOrUrl = RenderTemplate(preset.RestPathOrEndpoint ?? string.Empty, variables),
                Headers = RenderHeaders(DeserializeHeaders(preset.HeadersJson), variables),
                Body = RenderTemplate(preset.BodyTemplate, variables),
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
            SoapAction = RenderTemplate(preset.SoapAction ?? string.Empty, variables),
            EndpointUrl = RenderTemplate(preset.RestPathOrEndpoint, variables),
            Headers = RenderHeaders(DeserializeHeaders(preset.HeadersJson), variables),
            Body = RenderTemplate(preset.BodyTemplate ?? string.Empty, variables),
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

    private static Dictionary<string, string> DeserializeVariables(string? variablesJson)
    {
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(variablesJson))
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(variablesJson, JsonSerializerOptions());
            if (parsed is not null)
            {
                foreach (var kvp in parsed)
                {
                    if (!string.IsNullOrWhiteSpace(kvp.Key))
                    {
                        variables[kvp.Key] = kvp.Value ?? string.Empty;
                    }
                }
            }
        }

        // Built-ins
        variables.TryAdd("timestamp", DateTime.UtcNow.ToString("yyyyMMddHHmmss"));
        variables.TryAdd("timestampIso", DateTime.UtcNow.ToString("O"));
        variables.TryAdd("guid", Guid.NewGuid().ToString("D"));

        return variables;
    }

    private static string? RenderTemplate(string? template, Dictionary<string, string> variables)
    {
        if (template is null)
        {
            return null;
        }

        var missing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in TemplateVariableRegex.Matches(template))
        {
            var name = match.Groups["name"].Value;
            if (!variables.ContainsKey(name))
            {
                missing.Add(name);
            }
        }

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"Preset template is missing variable values for: {string.Join(", ", missing.OrderBy(x => x))}");
        }

        return TemplateVariableRegex.Replace(template, m => variables[m.Groups["name"].Value]);
    }

    private static Dictionary<string, string>? RenderHeaders(Dictionary<string, string>? headers, Dictionary<string, string> variables)
    {
        if (headers is null)
        {
            return null;
        }

        var rendered = new Dictionary<string, string>(headers.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in headers)
        {
            rendered[kvp.Key] = RenderTemplate(kvp.Value, variables) ?? string.Empty;
        }

        return rendered;
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
