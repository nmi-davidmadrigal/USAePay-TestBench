using Microsoft.EntityFrameworkCore;
using UsaepaySupportTestbench.Data;
using UsaepaySupportTestbench.Models;

namespace UsaepaySupportTestbench.Services;

public sealed class PresetService(ApplicationDbContext dbContext)
{
    public async Task<List<Preset>> GetRecentPresetsAsync(int take = 10)
    {
        return await dbContext.Presets
            .OrderByDescending(p => p.UpdatedAt)
            .Take(take)
            .ToListAsync();
    }

    public async Task<List<Preset>> GetQuickPresetsAsync(int take = 6)
    {
        return await dbContext.Presets
            .Where(p => p.IsQuickPreset)
            .OrderBy(p => p.Name)
            .Take(take)
            .ToListAsync();
    }

    public async Task<List<Preset>> GetCustomPresetsAsync(int take = 6)
    {
        return await dbContext.Presets
            .Where(p => !p.IsSystemPreset)
            .OrderByDescending(p => p.UpdatedAt)
            .Take(take)
            .ToListAsync();
    }

    public async Task<List<Preset>> SearchAsync(string? term, ApiType? apiType)
    {
        var query = dbContext.Presets.AsQueryable();

        if (!string.IsNullOrWhiteSpace(term))
        {
            query = query.Where(p => p.Name.Contains(term) || (p.TagsJson ?? string.Empty).Contains(term));
        }

        if (apiType.HasValue)
        {
            query = query.Where(p => p.ApiType == apiType.Value);
        }

        return await query
            .OrderByDescending(p => p.UpdatedAt)
            .ToListAsync();
    }

    public Task<Preset?> GetAsync(Guid id) => dbContext.Presets.FirstOrDefaultAsync(p => p.Id == id);

    public async Task<Preset> UpsertAsync(Preset preset)
    {
        var now = DateTime.UtcNow;
        if (preset.Id == Guid.Empty)
        {
            preset.Id = Guid.NewGuid();
        }

        var existing = await dbContext.Presets.AsNoTracking().FirstOrDefaultAsync(p => p.Id == preset.Id);
        if (existing is null)
        {
            preset.CreatedAt = now;
            preset.UpdatedAt = now;
            dbContext.Presets.Add(preset);
        }
        else
        {
            preset.CreatedAt = existing.CreatedAt;
            preset.UpdatedAt = now;
            dbContext.Presets.Update(preset);
        }

        await dbContext.SaveChangesAsync();
        return preset;
    }

    public async Task DeleteAsync(Guid id)
    {
        var preset = await dbContext.Presets.FirstOrDefaultAsync(p => p.Id == id);
        if (preset is null)
        {
            return;
        }

        dbContext.Presets.Remove(preset);
        await dbContext.SaveChangesAsync();
    }
}
