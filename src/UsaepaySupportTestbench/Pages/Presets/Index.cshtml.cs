using Microsoft.AspNetCore.Mvc.RazorPages;
using UsaepaySupportTestbench.Models;
using UsaepaySupportTestbench.Services;

namespace UsaepaySupportTestbench.Pages.Presets;

public class IndexModel(PresetService presetService) : PageModel
{
    public List<Preset> Presets { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Presets = await presetService.GetRecentPresetsAsync(50);
    }
}
