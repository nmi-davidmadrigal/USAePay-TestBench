using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UsaepaySupportTestbench.Models;
using UsaepaySupportTestbench.Services;

namespace UsaepaySupportTestbench.Pages.Presets;

public class EditModel(PresetService presetService) : PageModel
{
    [BindProperty]
    public Preset Preset { get; set; } = new();

    public List<ApiType> ApiTypes { get; } = Enum.GetValues<ApiType>().ToList();

    public async Task<IActionResult> OnGetAsync(Guid? id)
    {
        if (id.HasValue)
        {
            var preset = await presetService.GetAsync(id.Value);
            if (preset is null)
            {
                return NotFound();
            }

            Preset = preset;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        Preset = await presetService.UpsertAsync(Preset);
        return RedirectToPage("/Presets/Edit", new { id = Preset.Id });
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        await presetService.DeleteAsync(id);
        return RedirectToPage("/Presets/Index");
    }
}
