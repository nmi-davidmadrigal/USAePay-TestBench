using System.ComponentModel.DataAnnotations;

namespace UsaepaySupportTestbench.Models;

public sealed class ScenarioRun
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? PresetId { get; set; }

    public Preset? Preset { get; set; }

    public ApiType ApiType { get; set; }

    public EnvironmentType Environment { get; set; }

    public string RequestRedacted { get; set; } = string.Empty;

    public string ResponseRedacted { get; set; } = string.Empty;

    public int? HttpStatus { get; set; }

    public bool? SoapFault { get; set; }

    public long LatencyMs { get; set; }

    public string CorrelationId { get; set; } = Guid.NewGuid().ToString("N");

    public string? TicketNumber { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
