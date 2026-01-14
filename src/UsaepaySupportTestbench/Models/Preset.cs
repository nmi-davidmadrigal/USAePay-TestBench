using System.ComponentModel.DataAnnotations;

namespace UsaepaySupportTestbench.Models;

public sealed class Preset
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public ApiType ApiType { get; set; } = ApiType.Rest;

    public EnvironmentType Environment { get; set; } = EnvironmentType.Sandbox;

    [MaxLength(20)]
    public string? RestMethod { get; set; }

    [MaxLength(400)]
    public string? RestPathOrEndpoint { get; set; }

    [MaxLength(200)]
    public string? SoapAction { get; set; }

    public string? HeadersJson { get; set; }

    public string? BodyTemplate { get; set; }

    public string? VariablesJson { get; set; }

    public string? Notes { get; set; }

    public string? TagsJson { get; set; }

    public bool IsQuickPreset { get; set; }

    public bool IsSystemPreset { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
