namespace UsaepaySupportTestbench.Models;

public sealed class ProxyRestRequest
{
    public EnvironmentType Environment { get; set; } = EnvironmentType.Sandbox;

    public string Method { get; set; } = "POST";

    public string PathOrUrl { get; set; } = string.Empty;

    public Dictionary<string, string>? Headers { get; set; }

    public string? Body { get; set; }

    public Guid? PresetId { get; set; }

    public string? TicketNumber { get; set; }

    public bool ConfirmProduction { get; set; }
}

public sealed class ProxySoapRequest
{
    public EnvironmentType Environment { get; set; } = EnvironmentType.Sandbox;

    public string SoapAction { get; set; } = string.Empty;

    public string? EndpointUrl { get; set; }

    public Dictionary<string, string>? Headers { get; set; }

    public string Body { get; set; } = string.Empty;

    public Guid? PresetId { get; set; }

    public string? TicketNumber { get; set; }

    public bool ConfirmProduction { get; set; }
}

public sealed class ProxyResponse
{
    public int StatusCode { get; set; }

    public string Body { get; set; } = string.Empty;

    public Dictionary<string, string> Headers { get; set; } = new();

    public long LatencyMs { get; set; }

    public bool? SoapFault { get; set; }
}

public sealed class PayJsTokenRequest
{
    public string Token { get; set; } = string.Empty;

    public string? PaymentKey { get; set; }

    public string? MetadataJson { get; set; }
}
