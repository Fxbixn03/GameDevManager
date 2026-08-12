using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GameDevManager.Data.Services;

/// <summary>
/// Per-project module visibility. Only deviations from the default are stored — no row means
/// the module is enabled — so re-enabling a module simply removes its row.
/// </summary>
public class ModuleSettingsService(IDbContextFactory<GameDevManagerDbContext> factory)
{
    public async Task<HashSet<string>> GetDisabledModuleKeysAsync(Guid projectId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var keys = await db.ModuleSettings
            .AsNoTracking()
            .Where(s => s.GameProjectId == projectId && !s.IsEnabled)
            .Select(s => s.ModuleKey)
            .ToListAsync(ct);

        return keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public async Task SetModuleEnabledAsync(Guid projectId, string moduleKey, bool enabled, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var setting = await db.ModuleSettings
            .FirstOrDefaultAsync(s => s.GameProjectId == projectId && s.ModuleKey == moduleKey, ct);

        if (enabled)
        {
            // Enabled is the default — the row only exists to record a deviation.
            if (setting is not null)
            {
                db.ModuleSettings.Remove(setting);
                await db.SaveChangesAsync(ct);
            }

            return;
        }

        if (setting is null)
        {
            db.ModuleSettings.Add(new ModuleSetting
            {
                GameProjectId = projectId,
                ModuleKey = moduleKey,
                IsEnabled = false
            });
        }
        else
        {
            setting.IsEnabled = false;
        }

        await db.SaveChangesAsync(ct);
    }
}
