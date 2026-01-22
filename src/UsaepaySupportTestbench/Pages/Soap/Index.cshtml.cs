using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UsaepaySupportTestbench.Models;
using UsaepaySupportTestbench.Services;

namespace UsaepaySupportTestbench.Pages.Soap;

public class IndexModel(
    UsaepaySoapService soapService,
    ScenarioRunService scenarioRunService,
    RedactionService redactionService) : PageModel
{
    [BindProperty]
    public EnvironmentType Environment { get; set; } = EnvironmentType.Sandbox;

    [BindProperty]
    public bool ConfirmProduction { get; set; }

    [BindProperty]
    public SoapOperationType Operation { get; set; } = SoapOperationType.RunSale;

    [BindProperty]
    public string? SourceKey { get; set; }

    [BindProperty]
    public string? Pin { get; set; }

    [BindProperty]
    public string? ClientIp { get; set; }

    [BindProperty]
    public string? EndpointUrl { get; set; }

    [BindProperty]
    public bool RememberCredentials { get; set; }

    [BindProperty]
    public string? TicketNumber { get; set; }

    [BindProperty]
    public decimal? Amount { get; set; }

    [BindProperty]
    public string? RefNum { get; set; }

    [BindProperty]
    public bool AuthOnly { get; set; }

    [BindProperty]
    public string? Invoice { get; set; }

    [BindProperty]
    public string? Description { get; set; }

    [BindProperty]
    public string? Cardholder { get; set; }

    [BindProperty]
    public string? CardNumber { get; set; }

    [BindProperty]
    public string? CardExpiration { get; set; }

    [BindProperty]
    public string? CardCode { get; set; }

    [BindProperty]
    public string? AvsStreet { get; set; }

    [BindProperty]
    public string? AvsZip { get; set; }

    [BindProperty]
    public string? CheckRouting { get; set; }

    [BindProperty]
    public string? CheckAccount { get; set; }

    [BindProperty]
    public string? CheckNumber { get; set; }

    [BindProperty]
    public string? CheckAccountType { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public SoapOperationResult? OperationResult { get; private set; }
    public string? RedactedRequest { get; private set; }
    public string? RedactedResponse { get; private set; }
    public string? CredentialHint { get; private set; }
    public string? EndpointHint { get; private set; }

    public void OnGet()
    {
        LoadSavedHints();
    }

    public async Task<IActionResult> OnPostExecuteAsync(CancellationToken cancellationToken)
    {
        if (Environment == EnvironmentType.Production && !ConfirmProduction)
        {
            ModelState.AddModelError(string.Empty, "Production requests require explicit confirmation.");
        }

        ValidateInputs();

        if (!ModelState.IsValid)
        {
            LoadSavedHints();
            return Page();
        }

        SaveCredentialsIfRequested();

        var input = new SoapTransactionInput
        {
            Environment = Environment,
            Operation = Operation,
            EndpointUrl = EndpointUrl,
            SourceKey = SourceKey,
            Pin = Pin,
            ClientIp = ClientIp,
            Amount = Amount,
            RefNum = RefNum,
            AuthOnly = AuthOnly,
            Invoice = Invoice,
            Description = Description,
            Cardholder = Cardholder,
            CardNumber = CardNumber,
            CardExpiration = CardExpiration,
            CardCode = CardCode,
            AvsStreet = AvsStreet,
            AvsZip = AvsZip,
            CheckRouting = CheckRouting,
            CheckAccount = CheckAccount,
            CheckNumber = CheckNumber,
            CheckAccountType = CheckAccountType
        };

        try
        {
            OperationResult = await soapService.ExecuteAsync(input, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            LoadSavedHints();
            return Page();
        }

        var requestJson = SerializeRequestSnapshot(input, OperationResult?.EndpointUrl ?? EndpointUrl);
        var responseJson = SerializeResponse(OperationResult);
        RedactedRequest = redactionService.Redact(requestJson);
        RedactedResponse = redactionService.Redact(responseJson);

        if (OperationResult is not null)
        {
            var proxyRequest = new ProxySoapRequest
            {
                Environment = Environment,
                SoapAction = Operation.ToString(),
                EndpointUrl = OperationResult.EndpointUrl ?? EndpointUrl,
                Body = requestJson,
                TicketNumber = TicketNumber
            };
            var proxyResponse = new ProxyResponse
            {
                StatusCode = OperationResult.Success ? 200 : 500,
                Body = responseJson,
                LatencyMs = OperationResult.LatencyMs,
                SoapFault = !OperationResult.Success
            };

            await scenarioRunService.RecordSoapAsync(proxyRequest, proxyResponse);
        }

        LoadSavedHints();
        return Page();
    }

    public IActionResult OnPostClearCredentials()
    {
        ClearStoredCredentials(EnvironmentType.Sandbox);
        ClearStoredCredentials(EnvironmentType.Production);
        StatusMessage = "Cleared saved SOAP credentials for this session.";
        return RedirectToPage();
    }

    private void ValidateInputs()
    {
        switch (Operation)
        {
            case SoapOperationType.RunSale:
            case SoapOperationType.RunAuthOnly:
            case SoapOperationType.RunCredit:
                RequireAmount();
                RequireCard();
                break;
            case SoapOperationType.RunCheckSale:
                RequireAmount();
                RequireCheck();
                break;
            case SoapOperationType.RunQuickSale:
                RequireAmount();
                RequireRefNum();
                break;
            case SoapOperationType.VoidTransaction:
                RequireRefNum();
                break;
            case SoapOperationType.RefundTransaction:
                RequireAmount();
                RequireRefNum();
                break;
            default:
                ModelState.AddModelError(string.Empty, $"Unsupported SOAP operation: {Operation}");
                break;
        }
    }

    private void RequireAmount()
    {
        if (!Amount.HasValue)
        {
            ModelState.AddModelError(nameof(Amount), "Amount is required for this operation.");
        }
    }

    private void RequireRefNum()
    {
        if (string.IsNullOrWhiteSpace(RefNum))
        {
            ModelState.AddModelError(nameof(RefNum), "RefNum is required for this operation.");
        }
    }

    private void RequireCard()
    {
        if (string.IsNullOrWhiteSpace(CardNumber))
        {
            ModelState.AddModelError(nameof(CardNumber), "Card number is required.");
        }

        if (string.IsNullOrWhiteSpace(CardExpiration))
        {
            ModelState.AddModelError(nameof(CardExpiration), "Card expiration is required.");
        }
    }

    private void RequireCheck()
    {
        if (string.IsNullOrWhiteSpace(CheckRouting))
        {
            ModelState.AddModelError(nameof(CheckRouting), "Routing number is required.");
        }

        if (string.IsNullOrWhiteSpace(CheckAccount))
        {
            ModelState.AddModelError(nameof(CheckAccount), "Account number is required.");
        }

        if (string.IsNullOrWhiteSpace(CheckNumber))
        {
            ModelState.AddModelError(nameof(CheckNumber), "Check number is required.");
        }
    }

    private void SaveCredentialsIfRequested()
    {
        if (!RememberCredentials)
        {
            return;
        }

        var prefix = Environment == EnvironmentType.Production ? "Usaepay:Production" : "Usaepay:Sandbox";
        if (!string.IsNullOrWhiteSpace(SourceKey) && !string.IsNullOrWhiteSpace(Pin))
        {
            HttpContext.Session.SetString($"{prefix}:SourceKey", SourceKey.Trim());
            HttpContext.Session.SetString($"{prefix}:Pin", Pin.Trim());
            StatusMessage = $"Saved SOAP credentials for {Environment} in this session.";
        }

        if (!string.IsNullOrWhiteSpace(EndpointUrl))
        {
            HttpContext.Session.SetString($"{prefix}:SoapEndpoint", EndpointUrl.Trim());
        }
    }

    private void ClearStoredCredentials(EnvironmentType environment)
    {
        var prefix = environment == EnvironmentType.Production ? "Usaepay:Production" : "Usaepay:Sandbox";
        HttpContext.Session.Remove($"{prefix}:SourceKey");
        HttpContext.Session.Remove($"{prefix}:Pin");
        HttpContext.Session.Remove($"{prefix}:ApiKey");
        HttpContext.Session.Remove($"{prefix}:ApiSecret");
        HttpContext.Session.Remove($"{prefix}:SoapEndpoint");
    }

    private void LoadSavedHints()
    {
        var prefix = Environment == EnvironmentType.Production ? "Usaepay:Production" : "Usaepay:Sandbox";
        var session = HttpContext.Session;

        var sourceKey = session.GetString($"{prefix}:SourceKey") ?? session.GetString($"{prefix}:ApiKey");
        var pin = session.GetString($"{prefix}:Pin") ?? session.GetString($"{prefix}:ApiSecret");
        if (!string.IsNullOrWhiteSpace(sourceKey) && !string.IsNullOrWhiteSpace(pin))
        {
            CredentialHint = $"Stored credentials available for {Environment}. Leave fields blank to use them.";
        }

        var endpoint = session.GetString($"{prefix}:SoapEndpoint");
        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            EndpointHint = $"Stored endpoint override: {endpoint}";
        }

    }

    private static string SerializeRequestSnapshot(SoapTransactionInput input, string? endpointUrl)
    {
        var snapshot = new
        {
            operation = input.Operation.ToString(),
            environment = input.Environment.ToString(),
            endpointUrl,
            sourceKey = input.SourceKey,
            clientIp = input.ClientIp,
            amount = input.Amount,
            refNum = input.RefNum,
            authOnly = input.AuthOnly,
            invoice = input.Invoice,
            description = input.Description,
            card = new
            {
                cardholder = input.Cardholder,
                cardNumber = input.CardNumber,
                cardExpiration = input.CardExpiration,
                cardCode = input.CardCode,
                avsStreet = input.AvsStreet,
                avsZip = input.AvsZip
            },
            check = new
            {
                routing = input.CheckRouting,
                account = input.CheckAccount,
                accountType = input.CheckAccountType,
                checkNumber = input.CheckNumber
            }
        };

        return JsonSerializer.Serialize(snapshot, JsonSerializerOptions());
    }

    private static string SerializeResponse(SoapOperationResult? result)
    {
        if (result is null)
        {
            return string.Empty;
        }

        if (!result.Success)
        {
            return JsonSerializer.Serialize(new { error = result.ErrorMessage }, JsonSerializerOptions());
        }

        return JsonSerializer.Serialize(result.Payload, JsonSerializerOptions());
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
