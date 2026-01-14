using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using UsaepaySupportTestbench.Models;
using UsaepaySupportTestbench.Services;

namespace UsaepaySupportTestbench.Pages;

public class IndexModel(
    ScenarioRunService scenarioRunService,
    PresetService presetService,
    IOptions<UsaepayOptions> options) : PageModel
{
    public List<ScenarioRun> RecentRuns { get; private set; } = [];
    public List<Preset> RecentPresets { get; private set; } = [];
    public List<ScenarioRun> RecentErrors { get; private set; } = [];
    public EnvironmentStatusViewModel EnvironmentStatus { get; private set; } = new();

    public async Task OnGetAsync()
    {
        RecentRuns = await scenarioRunService.GetRecentRunsAsync();
        RecentPresets = await presetService.GetRecentPresetsAsync();
        RecentErrors = await scenarioRunService.GetRecentErrorsAsync();

        EnvironmentStatus = new EnvironmentStatusViewModel
        {
            SandboxConfigured = !string.IsNullOrWhiteSpace(options.Value.Sandbox.RestBaseUrl)
                                && !string.IsNullOrWhiteSpace(options.Value.Sandbox.SoapEndpoint),
            ProductionConfigured = !string.IsNullOrWhiteSpace(options.Value.Production.RestBaseUrl)
                                   && !string.IsNullOrWhiteSpace(options.Value.Production.SoapEndpoint)
        };
    }

    public sealed class EnvironmentStatusViewModel
    {
        public bool SandboxConfigured { get; set; }
        public bool ProductionConfigured { get; set; }
    }
}
