using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace GameDevManager.Data.Services;

/// <summary>
/// Die Referenzansicht des Konzepts („Find All References"): zu jeder Entität lässt sich
/// nachschlagen, wer auf ihre GUID verweist.
/// <para>
/// Zwei Quellen fließen zusammen: die Feldwerte vom Typ
/// <see cref="Domain.Entities.ContentFieldType.EntityReference"/>, die es in jedem Modul gibt,
/// und die eigenen Verknüpfungen der Module (Rezept-Zutaten und später Händler-Angebote,
/// Loot-Einträge). Letztere melden die Module über <see cref="IModuleEntitySource"/>.
/// </para>
/// </summary>
public class ReferenceService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    IEnumerable<IModuleEntitySource> sources,
    IStringLocalizer<DataMessages> messages)
{
    /// <summary>Alle Stellen, an denen die übergebene GUID verwendet wird.</summary>
    public async Task<List<EntityReferenceHit>> FindReferencesAsync(Guid entityId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var hits = new List<EntityReferenceHit>();

        // Verweise über benutzerdefinierte Referenzfelder.
        var raw = await db.FieldValues
            .AsNoTracking()
            .Where(v => v.ReferenceValue == entityId)
            .Select(v => new
            {
                v.OwnerEntityId,
                v.OwnerModuleKey,
                FieldName = v.FieldDefinition!.Name
            })
            .ToListAsync(ct);

        foreach (var perModule in raw.GroupBy(r => r.OwnerModuleKey))
        {
            var names = await ResolveNamesAsync(
                db, perModule.Key, [.. perModule.Select(r => r.OwnerEntityId).Distinct()], ct);

            hits.AddRange(perModule.Select(entry => new EntityReferenceHit(
                entry.OwnerEntityId,
                entry.OwnerModuleKey,
                names.GetValueOrDefault(entry.OwnerEntityId) ?? messages["DeletedEntity"].Value,
                entry.FieldName)));
        }

        // Verweise über die eigenen Spalten der Module.
        foreach (var source in sources)
        {
            hits.AddRange(await source.FindReferencesAsync(db, entityId, ct));
        }

        return [.. hits.OrderBy(h => h.SourceModuleKey).ThenBy(h => h.SourceName).ThenBy(h => h.FieldName)];
    }

    /// <summary>
    /// Die auswählbaren Entitäten eines Moduls — Grundlage der Referenz-Auswahlfelder.
    /// Für noch nicht umgesetzte Module ist die Liste leer.
    /// </summary>
    public async Task<List<EntitySummary>> GetEntitiesAsync(
        Guid projectId, string moduleKey, CancellationToken ct = default)
    {
        var source = Find(moduleKey);
        if (source is null)
        {
            return [];
        }

        await using var db = await factory.CreateDbContextAsync(ct);
        return await source.GetEntitiesAsync(db, projectId, ct);
    }

    /// <summary>
    /// Löst GUIDs eines Moduls auf ihre Anzeigenamen auf — für alle Ansichten, die Entitäten
    /// nur über ihre GUID kennen (Referenzansicht, Asset-Bibliothek).
    /// </summary>
    public async Task<Dictionary<Guid, string>> ResolveNamesAsync(
        string moduleKey, List<Guid> ids, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await ResolveNamesAsync(db, moduleKey, ids, ct);
    }

    private async Task<Dictionary<Guid, string>> ResolveNamesAsync(
        GameDevManagerDbContext db, string moduleKey, List<Guid> ids, CancellationToken ct)
    {
        var source = Find(moduleKey);
        return source is null ? [] : await source.ResolveNamesAsync(db, ids, ct);
    }

    private IModuleEntitySource? Find(string moduleKey) =>
        sources.FirstOrDefault(source => source.ModuleKey == moduleKey);
}
