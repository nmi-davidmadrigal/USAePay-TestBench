namespace UsaepaySupportTestbench.Models;

public sealed class UsaepayOptions
{
    public UsaepayEnvironmentOptions Sandbox { get; set; } = new();
    public UsaepayEnvironmentOptions Production { get; set; } = new();
}

public sealed class UsaepayEnvironmentOptions
{
    public string RestBaseUrl { get; set; } = string.Empty;
    public string SoapEndpoint { get; set; } = string.Empty;
    /// <summary>
    /// USAePay "Source Key" (aka API key) for REST Basic auth.
    /// </summary>
    public string? SourceKey { get; set; }

    /// <summary>
    /// USAePay "PIN" used to compute API-hash (s2) for REST Basic auth.
    /// </summary>
    public string? Pin { get; set; }

    // Back-compat: earlier docs in this repo used ApiKey/ApiSecret.
    public string? ApiKey { get; set; }
    public string? ApiSecret { get; set; }
}
