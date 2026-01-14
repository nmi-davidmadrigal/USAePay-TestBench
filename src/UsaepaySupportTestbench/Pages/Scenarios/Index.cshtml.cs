using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UsaepaySupportTestbench.Models;
using UsaepaySupportTestbench.Services;

namespace UsaepaySupportTestbench.Pages.Scenarios;

public class IndexModel(PresetService presetService, ScenarioRunService scenarioRunService) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? SearchTerm { get; set; }

    [BindProperty(SupportsGet = true)]
    public ApiType? FilterApiType { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? TicketNumber { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool ConfirmProduction { get; set; }

    public List<Preset> Presets { get; private set; } = [];

    public ScenarioRun? LastRun { get; private set; }

    public async Task OnGetAsync()
    {
        Presets = await presetService.SearchAsync(SearchTerm, FilterApiType);
    }

    public async Task<IActionResult> OnPostRunAsync(Guid id, CancellationToken cancellationToken)
    {
        var preset = await presetService.GetAsync(id);
        if (preset is null)
        {
            return NotFound();
        }

        if (preset.Environment == EnvironmentType.Production && !ConfirmProduction)
        {
            ModelState.AddModelError(string.Empty, "Production requests require explicit confirmation.");
        }
        else
        {
            try
            {
                LastRun = await scenarioRunService.ExecutePresetAsync(preset, TicketNumber, ConfirmProduction, cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }
            catch (JsonException)
            {
                ModelState.AddModelError(string.Empty, "Headers JSON is invalid.");
            }
        }

        await OnGetAsync();
        return Page();
    }
}
