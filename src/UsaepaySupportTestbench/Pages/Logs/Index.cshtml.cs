using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using UsaepaySupportTestbench.Data;
using UsaepaySupportTestbench.Models;

namespace UsaepaySupportTestbench.Pages.Logs;

public class IndexModel(ApplicationDbContext dbContext) : PageModel
{
    public List<ScenarioRun> Runs { get; private set; } = [];

    public ScenarioRun? SelectedRun { get; private set; }

    public async Task OnGetAsync(Guid? id)
    {
        Runs = await dbContext.ScenarioRuns
            .Include(r => r.Preset)
            .OrderByDescending(r => r.CreatedAt)
            .Take(50)
            .ToListAsync();

        if (id.HasValue)
        {
            SelectedRun = Runs.FirstOrDefault(r => r.Id == id);
        }
    }

    public async Task<IActionResult> OnPostExportAsync(Guid id)
    {
        var run = await dbContext.ScenarioRuns
            .Include(r => r.Preset)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (run is null)
        {
            return NotFound();
        }

        var exportPayload = new
        {
            run.Id,
            PresetName = run.Preset?.Name,
            run.ApiType,
            run.Environment,
            run.HttpStatus,
            run.SoapFault,
            run.LatencyMs,
            run.CorrelationId,
            run.TicketNumber,
            run.CreatedAt,
            run.RequestRedacted,
            run.ResponseRedacted
        };

        var json = JsonSerializer.Serialize(exportPayload, new JsonSerializerOptions { WriteIndented = true });
        var fileName = $"scenario-run-{run.Id:N}-redacted.json";

        return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", fileName);
    }
}
