using GameDevManager.Domain;
using Microsoft.EntityFrameworkCore;

namespace GameDevManager.Data.Services;

/// <summary>
/// Die Referenzansicht des Konzepts („Find All References"): zu jeder Entität lässt sich
/// nachschlagen, wer auf ihre GUID verweist.
/// <para>
/// Grundlage sind die Feldwerte vom Typ <see cref="Domain.Entities.FieldType.EntityReference"/>.
/// Module mit eigenen Verknüpfungstabellen (Rezept-Zutaten, Händler-Angebote, Loot-Einträge)
/// ergänzen ihre Abfrage später in <see cref="FindReferencesAsync"/>.
/// </para>
/// </summary>
public class ReferenceService(IDbContextFactory<GameDevManagerDbContext> factory)
{
    /// <summary>Alle Stellen, an denen die übergebene GUID verwendet wird.</summary>
    public async Task<List<EntityReferenceHit>> FindReferencesAsync(Guid entityId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

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

        var hits = new List<EntityReferenceHit>();

        // Rezepte verweisen über eigene Spalten auf Items statt über Feldwerte — Module mit
        // eigenen Verknüpfungstabellen ergänzen ihre Abfrage hier.
        hits.AddRange(await db.Recipes
            .AsNoTracking()
            .Where(r => r.OutputItemId == entityId)
            .Select(r => new EntityReferenceHit(r.Id, ModuleKeys.Crafting, r.Name, "Ergebnis"))
            .ToListAsync(ct));

        hits.AddRange(await db.RecipeIngredients
            .AsNoTracking()
            .Where(i => i.ItemId == entityId)
            .Select(i => new EntityReferenceHit(i.RecipeId, ModuleKeys.Crafting, i.Recipe!.Name, "Zutat"))
            .ToListAsync(ct));

        foreach (var perModule in raw.GroupBy(r => r.OwnerModuleKey))
        {
            var names = await ResolveNamesAsync(
                db, perModule.Key, [.. perModule.Select(r => r.OwnerEntityId).Distinct()], ct);

            hits.AddRange(perModule.Select(entry => new EntityReferenceHit(
                entry.OwnerEntityId,
                entry.OwnerModuleKey,
                names.GetValueOrDefault(entry.OwnerEntityId) ?? "(gelöschte Entität)",
                entry.FieldName)));
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
        await using var db = await factory.CreateDbContextAsync(ct);

        return moduleKey switch
        {
            ModuleKeys.Items => await db.Items
                .AsNoTracking()
                .Where(i => i.GameProjectId == projectId)
                .OrderBy(i => i.Name)
                .Select(i => new EntitySummary(i.Id, ModuleKeys.Items, i.Name, i.ContentType!.Name))
                .ToListAsync(ct),
            ModuleKeys.Crafting => await db.Recipes
                .AsNoTracking()
                .Where(r => r.GameProjectId == projectId)
                .OrderBy(r => r.Name)
                .Select(r => new EntitySummary(r.Id, ModuleKeys.Crafting, r.Name, r.ContentType!.Name))
                .ToListAsync(ct),
            _ => []
        };
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

    private static async Task<Dictionary<Guid, string>> ResolveNamesAsync(
        GameDevManagerDbContext db, string moduleKey, List<Guid> ids, CancellationToken ct) =>
        moduleKey switch
        {
            ModuleKeys.Items => await db.Items
                .AsNoTracking()
                .Where(i => ids.Contains(i.Id))
                .ToDictionaryAsync(i => i.Id, i => i.Name, ct),
            ModuleKeys.Crafting => await db.Recipes
                .AsNoTracking()
                .Where(r => ids.Contains(r.Id))
                .ToDictionaryAsync(r => r.Id, r => r.Name, ct),
            _ => []
        };
}
