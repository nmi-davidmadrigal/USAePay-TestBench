namespace UsaepaySupportTestbench.Models;

public enum SoapOperationType
{
    RunSale = 0,
    RunAuthOnly = 1,
    RunCheckSale = 2,
    RunQuickSale = 3,
    VoidTransaction = 4,
    RefundTransaction = 5,
    RunCredit = 6
}

public sealed class SoapTransactionInput
{
    public EnvironmentType Environment { get; set; } = EnvironmentType.Sandbox;
    public SoapOperationType Operation { get; set; } = SoapOperationType.RunSale;

    public string? EndpointUrl { get; set; }
    public string? SourceKey { get; set; }
    public string? Pin { get; set; }
    public string? ClientIp { get; set; }

    public decimal? Amount { get; set; }
    public string? RefNum { get; set; }
    public bool AuthOnly { get; set; }
    public string? Invoice { get; set; }
    public string? Description { get; set; }

    public string? Cardholder { get; set; }
    public string? CardNumber { get; set; }
    public string? CardExpiration { get; set; }
    public string? CardCode { get; set; }
    public string? AvsStreet { get; set; }
    public string? AvsZip { get; set; }

    public string? CheckRouting { get; set; }
    public string? CheckAccount { get; set; }
    public string? CheckNumber { get; set; }
    public string? CheckAccountType { get; set; }
}

public sealed class SoapOperationResult
{
    public SoapOperationType Operation { get; set; }
    public bool Success { get; set; }
    public object? Payload { get; set; }
    public string? ErrorMessage { get; set; }
    public long LatencyMs { get; set; }
    public string? EndpointUrl { get; set; }
}
