using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace GameDevManager.Data.Services;

/// <summary>
/// Die Presets der Game Engines: Baupläne dafür, wie ein Eintrag eines Moduls als Objekt in
/// Unity, Unreal oder Godot aussieht.
/// <para>
/// Ein Preset ist kein Spielinhalt, sondern eine Vorschrift für den Export — deshalb keine
/// <c>ContentEntity</c> und kein Eintrag in Suche, Referenzansicht oder Duplizieren. Benutzt
/// wird es vom <see cref="EngineExportWriter"/>, der beim Export in eine Engine aus jedem
/// passenden Eintrag ein fertig gefülltes Objekt schreibt.
/// </para>
/// </summary>
public class EnginePresetService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    PermissionGuard guard,
    IStringLocalizer<DataMessages> messages)
{
    /// <summary>Alle Presets des Projekts samt Zuordnungen, nach Engine und Reihenfolge.</summary>
    public async Task<List<EnginePreset>> GetPresetsAsync(
        Guid projectId, TargetEngine? engine = null, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var query = db.EnginePresets
            .AsNoTracking()
            .Include(preset => preset.Mappings)
            .Include(preset => preset.ContentType)
            .Where(preset => preset.GameProjectId == projectId);

        if (engine is { } wanted)
        {
            query = query.Where(preset => preset.Engine == wanted);
        }

        var presets = await query
            .OrderBy(preset => preset.Engine)
            .ThenBy(preset => preset.SortOrder)
            .ThenBy(preset => preset.Name)
            .ToListAsync(ct);

        presets.ForEach(preset =>
            preset.Mappings = [.. preset.Mappings.OrderBy(m => m.SortOrder).ThenBy(m => m.Target)]);

        return presets;
    }

    public async Task<EnginePreset?> GetPresetAsync(Guid presetId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var preset = await db.EnginePresets
            .AsNoTracking()
            .Include(p => p.Mappings)
            .FirstOrDefaultAsync(p => p.Id == presetId, ct);

        if (preset is not null)
        {
            preset.Mappings = [.. preset.Mappings.OrderBy(m => m.SortOrder).ThenBy(m => m.Target)];
        }

        return preset;
    }

    /// <summary>Legt ein Preset an oder speichert es samt seiner Zuordnungen.</summary>
    public async Task SavePresetAsync(Guid projectId, EnginePreset preset, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(preset.Name))
        {
            throw new ContentValidationException(messages["Preset_NameRequired"].Value);
        }

        if (string.IsNullOrWhiteSpace(preset.TypeName))
        {
            throw new ContentValidationException(messages["Preset_TypeNameRequired"].Value);
        }

        if (string.IsNullOrWhiteSpace(preset.ModuleKey))
        {
            throw new ContentValidationException(messages["Preset_ModuleRequired"].Value);
        }

        // Zwei Zuordnungen auf dieselbe Eigenschaft: Die zweite überschriebe die erste, und
        // welche gewinnt, hinge an der Sortierung — das ist kein Zustand, den man speichern will.
        var duplicate = preset.Mappings
            .GroupBy(mapping => mapping.Target.Trim(), StringComparer.CurrentCultureIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw new ContentValidationException(messages["Preset_TargetDuplicate", duplicate.Key].Value);
        }

        if (preset.Mappings.Any(mapping => string.IsNullOrWhiteSpace(mapping.Target)))
        {
            throw new ContentValidationException(messages["Preset_TargetRequired"].Value);
        }

        if (preset.Mappings.Any(mapping => mapping.Source == PresetSource.Field && mapping.FieldDefinitionId is null))
        {
            throw new ContentValidationException(messages["Preset_FieldRequired"].Value);
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        var stored = await db.EnginePresets
            .Include(p => p.Mappings)
            .FirstOrDefaultAsync(p => p.Id == preset.Id, ct);

        if (stored is null)
        {
            stored = new EnginePreset
            {
                Id = preset.Id,
                GameProjectId = projectId,
                Name = preset.Name.Trim(),
                ModuleKey = preset.ModuleKey,
                TypeName = preset.TypeName.Trim()
            };

            db.EnginePresets.Add(stored);
        }
        else
        {
            stored.Name = preset.Name.Trim();
            stored.ModuleKey = preset.ModuleKey;
            stored.TypeName = preset.TypeName.Trim();
        }

        stored.Engine = preset.Engine;
        stored.Description = string.IsNullOrWhiteSpace(preset.Description) ? null : preset.Description.Trim();
        stored.ContentTypeId = preset.ContentTypeId;
        stored.SortOrder = preset.SortOrder;
        stored.UpdatedAtUtc = DateTime.UtcNow;

        SyncMappings(db, stored, preset.Mappings);

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Gleicht die Zuordnungen ab. Neue Kinder an einem bestehenden Elterndatensatz gehen über
    /// <c>db.Set&lt;T&gt;().Add</c> und nicht über die Navigationsliste — sie bringen ihre GUID
    /// schon mit, und EF erzeugte sonst ein UPDATE auf eine Zeile, die es noch nicht gibt.
    /// </summary>
    private static void SyncMappings(
        GameDevManagerDbContext db, EnginePreset stored, List<EnginePresetMapping> wanted)
    {
        var byId = wanted.ToDictionary(mapping => mapping.Id);

        // Entfernt wird über die Navigationsliste: Der Fremdschlüssel ist pflicht, EF löscht
        // die Waise dadurch von selbst.
        foreach (var existing in stored.Mappings.Where(m => !byId.ContainsKey(m.Id)).ToList())
        {
            stored.Mappings.Remove(existing);
        }

        var order = 0;

        foreach (var mapping in wanted)
        {
            var target = mapping.Target.Trim();
            var existing = stored.Mappings.FirstOrDefault(m => m.Id == mapping.Id);

            if (existing is null)
            {
                db.EnginePresetMappings.Add(new EnginePresetMapping
                {
                    Id = mapping.Id,
                    EnginePresetId = stored.Id,
                    Target = target,
                    Source = mapping.Source,
                    FieldDefinitionId = mapping.FieldDefinitionId,
                    ConstantValue = mapping.ConstantValue,
                    SortOrder = order
                });
            }
            else
            {
                existing.Target = target;
                existing.Source = mapping.Source;
                existing.FieldDefinitionId = mapping.FieldDefinitionId;
                existing.ConstantValue = mapping.ConstantValue;
                existing.SortOrder = order;
            }

            order++;
        }
    }

    public async Task DeletePresetAsync(Guid presetId, CancellationToken ct = default)
    {
        // Reiner ExecuteDelete-Pfad ohne vorheriges Speichern — die Prüfung steht hier.
        await guard.EnsureCanWriteAsync(ct);

        await using var db = await factory.CreateDbContextAsync(ct);

        // Die Zuordnungen fallen per Kaskade mit.
        await db.EnginePresets.Where(preset => preset.Id == presetId).ExecuteDeleteAsync(ct);
    }
}
