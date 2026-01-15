using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UsaepaySupportTestbench.Models;
using UsaepaySupportTestbench.Services;

namespace UsaepaySupportTestbench.Pages.Manual;

public class IndexModel(
    RestProxyService restProxyService,
    SoapProxyService soapProxyService,
    ScenarioRunService scenarioRunService,
    PresetService presetService,
    RedactionService redactionService) : PageModel
{
    [BindProperty]
    public ApiType ApiType { get; set; } = ApiType.Rest;

    [BindProperty]
    public EnvironmentType Environment { get; set; } = EnvironmentType.Sandbox;

    [BindProperty]
    public string Method { get; set; } = "POST";

    [BindProperty]
    public string PathOrUrl { get; set; } = string.Empty;

    [BindProperty]
    public string SoapAction { get; set; } = string.Empty;

    [BindProperty]
    public string EndpointUrl { get; set; } = string.Empty;

    [BindProperty]
    public string HeadersJson { get; set; } = "{\n  \"Content-Type\": \"application/json\"\n}";

    [BindProperty]
    public string Body { get; set; } = string.Empty;

    [BindProperty]
    public bool SaveAsPreset { get; set; }

    [BindProperty]
    public string? PresetName { get; set; }

    [BindProperty]
    public string? TicketNumber { get; set; }

    [BindProperty]
    public bool ConfirmProduction { get; set; }

    public ProxyResponse? Response { get; private set; }

    public string? RedactedRequest { get; private set; }

    public List<string> Diagnostics { get; private set; } = [];

    public string? LastToken { get; private set; }

    public string? LastPaymentKey { get; private set; }

    public void OnGet()
    {
        LastToken = HttpContext.Session.GetString("PayJs:Token");
        LastPaymentKey = HttpContext.Session.GetString("PayJs:PaymentKey");
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        LastToken = HttpContext.Session.GetString("PayJs:Token");
        LastPaymentKey = HttpContext.Session.GetString("PayJs:PaymentKey");

        if (Environment == EnvironmentType.Production && !ConfirmProduction)
        {
            ModelState.AddModelError(string.Empty, "Production requests require explicit confirmation.");
        }

        Dictionary<string, string>? headers = null;
        if (!string.IsNullOrWhiteSpace(HeadersJson))
        {
            try
            {
                headers = JsonSerializer.Deserialize<Dictionary<string, string>>(HeadersJson);
            }
            catch (JsonException)
            {
                ModelState.AddModelError(string.Empty, "Headers JSON is invalid.");
            }
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        switch (ApiType)
        {
            case ApiType.Rest:
            {
                var request = new ProxyRestRequest
                {
                    Environment = Environment,
                    Method = Method,
                    PathOrUrl = PathOrUrl,
                    Headers = headers,
                    Body = Body,
                    TicketNumber = TicketNumber,
                    ConfirmProduction = ConfirmProduction
                };

                Response = await restProxyService.ExecuteAsync(request, cancellationToken);
                await scenarioRunService.RecordRestAsync(request, Response);
                RedactedRequest = redactionService.Redact(JsonSerializer.Serialize(new
                {
                    request.Method,
                    request.PathOrUrl,
                    request.Headers,
                    request.Body
                }, new JsonSerializerOptions { WriteIndented = true }));
                Diagnostics = BuildDiagnostics(Response);

                if (SaveAsPreset)
                {
                    await presetService.UpsertAsync(new Preset
                    {
                        Name = PresetName ?? "Manual REST Preset",
                        ApiType = ApiType.Rest,
                        Environment = Environment,
                        RestMethod = Method,
                        RestPathOrEndpoint = PathOrUrl,
                        HeadersJson = HeadersJson,
                        BodyTemplate = Body,
                        Notes = $"Saved from Manual Requests on {DateTime.UtcNow:O}"
                    });
                }

                break;
            }

            case ApiType.Soap:
            {
                var request = new ProxySoapRequest
                {
                    Environment = Environment,
                    SoapAction = SoapAction,
                    EndpointUrl = EndpointUrl,
                    Headers = headers,
                    Body = Body,
                    TicketNumber = TicketNumber,
                    ConfirmProduction = ConfirmProduction
                };

                Response = await soapProxyService.ExecuteAsync(request, cancellationToken);
                await scenarioRunService.RecordSoapAsync(request, Response);
                RedactedRequest = redactionService.Redact(JsonSerializer.Serialize(new
                {
                    request.SoapAction,
                    request.EndpointUrl,
                    request.Headers,
                    request.Body
                }, new JsonSerializerOptions { WriteIndented = true }));
                Diagnostics = BuildDiagnostics(Response);

                if (SaveAsPreset)
                {
                    await presetService.UpsertAsync(new Preset
                    {
                        Name = PresetName ?? "Manual SOAP Preset",
                        ApiType = ApiType.Soap,
                        Environment = Environment,
                        SoapAction = SoapAction,
                        RestPathOrEndpoint = EndpointUrl,
                        HeadersJson = HeadersJson,
                        BodyTemplate = Body,
                        Notes = $"Saved from Manual Requests on {DateTime.UtcNow:O}"
                    });
                }

                break;
            }

            case ApiType.PayJsFlow:
                // Pay.js tokenization is a client-side flow and should never be proxied
                // as REST/SOAP from this page.
                ModelState.AddModelError(string.Empty,
                    "PayJsFlow is a client-side tokenization flow and cannot be executed from Manual Requests. Use the Pay.js page.");
                return Page();

            default:
                ModelState.AddModelError(string.Empty, $"Unsupported API type: {ApiType}");
                return Page();
        }

        return Page();
    }

    private static List<string> BuildDiagnostics(ProxyResponse response)
    {
        var diagnostics = new List<string>();
        if (response.StatusCode == 401 || response.StatusCode == 403)
        {
            diagnostics.Add("Auth error: verify API key/secret and headers.");
        }

        if (response.StatusCode == 429)
        {
            diagnostics.Add("Rate limit detected: check retry/backoff.");
        }

        if (response.SoapFault == true)
        {
            diagnostics.Add("SOAP fault detected in response.");
        }

        if (response.StatusCode >= 400 && response.StatusCode != 401 && response.StatusCode != 403 && response.StatusCode != 429)
        {
            diagnostics.Add("Request failed: inspect response for schema or validation errors.");
        }

        return diagnostics;
    }
}
