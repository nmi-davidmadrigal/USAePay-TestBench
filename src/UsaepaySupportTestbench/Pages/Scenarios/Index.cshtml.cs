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

    [BindProperty]
    public EnvironmentType CredentialEnvironment { get; set; } = EnvironmentType.Sandbox;

    [BindProperty]
    public string? SourceKey { get; set; }

    [BindProperty]
    public string? Pin { get; set; }

    [TempData]
    public string? CredentialStatus { get; set; }

    public List<Preset> Presets { get; private set; } = [];

    public ScenarioRun? LastRun { get; private set; }

    public async Task OnGetAsync()
    {
        Presets = await presetService.SearchAsync(SearchTerm, FilterApiType);
    }

    public async Task<IActionResult> OnPostRunAsync(string? id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id) || !Guid.TryParse(id, out var presetId) || presetId == Guid.Empty)
        {
            ModelState.AddModelError(string.Empty, "Missing/invalid preset id. Refresh the page and try again.");
            await OnGetAsync();
            return Page();
        }

        var preset = await presetService.GetAsync(presetId);
        if (preset is null)
        {
            return NotFound();
        }

        try
        {
            LastRun = await scenarioRunService.ExecutePresetAsync(preset, TicketNumber, cancellationToken);
            // Show results immediately (request/response are on the Logs page).
            return RedirectToPage("/Logs/Index", new { id = LastRun.Id });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
        }

        await OnGetAsync();
        return Page();
    }

    public IActionResult OnPostSetCredentials(string action)
    {
        const string prefix = "Usaepay:Sandbox";

        if (string.Equals(action, "clear", StringComparison.OrdinalIgnoreCase))
        {
            HttpContext.Session.Remove($"{prefix}:SourceKey");
            HttpContext.Session.Remove($"{prefix}:Pin");
            HttpContext.Session.Remove($"{prefix}:ApiKey");
            HttpContext.Session.Remove($"{prefix}:ApiSecret");
            CredentialStatus = "Cleared sandbox credentials from this session.";
        }
        else
        {
            if (string.IsNullOrWhiteSpace(SourceKey))
            {
                ModelState.AddModelError(string.Empty, "Source Key is required to save credentials.");
                return Page();
            }

            if (string.IsNullOrWhiteSpace(Pin))
            {
                ModelState.AddModelError(string.Empty, "PIN is required to save credentials.");
                return Page();
            }

            HttpContext.Session.SetString($"{prefix}:SourceKey", SourceKey.Trim());
            HttpContext.Session.SetString($"{prefix}:Pin", Pin.Trim());
            CredentialStatus = "Saved sandbox credentials to this session.";
        }

        // Avoid leaking PIN in query string: redirect back to current filter state.
        return RedirectToPage(new
        {
            SearchTerm,
            FilterApiType,
            TicketNumber
        });
    }
}
