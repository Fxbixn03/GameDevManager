using GameDevManager.Domain;
using Microsoft.EntityFrameworkCore;

namespace GameDevManager.Data.Services;

/// <summary>Was für ein Datensatz gefunden wurde — bestimmt, wohin die Oberfläche springt.</summary>
public enum SearchHitKind
{
    /// <summary>Eine Modul-Entität, etwa ein Item oder ein Rezept.</summary>
    Entity,

    /// <summary>Eine benutzerdefinierte Art.</summary>
    ContentType,

    /// <summary>Eine Datei aus der Asset-Bibliothek.</summary>
    Asset
}

/// <summary>Ein Treffer der globalen Suche.</summary>
public sealed record SearchHit(
    Guid Id,
    string ModuleKey,
    SearchHitKind Kind,
    string Name,
    string? Subtitle,
    Guid? PrimaryAssetId);

/// <summary>
/// Die globale Suche über alle Entitäten aus dem Konzept. Sie durchsucht Namen und
/// Beschreibungen aller umgesetzten Module und löst zusätzlich GUIDs direkt auf — Referenzen
/// laufen in diesem Tool ausschließlich über GUIDs, und die aus einer Fundstelle oder einem
/// Export zu kopieren und hier einzufügen ist der schnellste Weg zur Entität.
/// </summary>
public class SearchService(IDbContextFactory<GameDevManagerDbContext> factory)
{
    /// <summary>Ab dieser Länge wird gesucht — kürzer träfe fast alles.</summary>
    public const int MinimumQueryLength = 2;

    public async Task<List<SearchHit>> SearchAsync(
        Guid projectId, string? query, int limit = 20, CancellationToken ct = default)
    {
        var trimmed = query?.Trim() ?? string.Empty;

        if (trimmed.Length < MinimumQueryLength)
        {
            return [];
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        if (Guid.TryParse(trimmed, out var id))
        {
            return await FindByIdAsync(db, projectId, id, ct);
        }

        // Kleinschreibung auf beiden Seiten statt LIKE: das übersetzt sich über alle vier
        // Provider gleich und verhält sich unabhängig von der Sortierfolge der Datenbank.
        var needle = trimmed.ToLowerInvariant();

        var hits = new List<SearchHit>();

        hits.AddRange(await db.Items
            .AsNoTracking()
            .Where(i => i.GameProjectId == projectId
                && (i.Name.ToLower().Contains(needle)
                    || (i.Description != null && i.Description.ToLower().Contains(needle))))
            .Take(limit)
            .Select(i => new SearchHit(
                i.Id,
                ModuleKeys.Items,
                SearchHitKind.Entity,
                i.Name,
                i.ContentType!.Name,
                db.Assets.Where(a => a.OwnerEntityId == i.Id && a.IsPrimary)
                    .Select(a => (Guid?)a.Id).FirstOrDefault()))
            .ToListAsync(ct));

        hits.AddRange(await db.Recipes
            .AsNoTracking()
            .Where(r => r.GameProjectId == projectId
                && (r.Name.ToLower().Contains(needle)
                    || (r.Description != null && r.Description.ToLower().Contains(needle))))
            .Take(limit)
            .Select(r => new SearchHit(
                r.Id,
                ModuleKeys.Crafting,
                SearchHitKind.Entity,
                r.Name,
                db.Items.Where(i => i.Id == r.OutputItemId).Select(i => "ergibt " + i.Name).FirstOrDefault(),
                db.Assets.Where(a => a.OwnerEntityId == r.OutputItemId && a.IsPrimary)
                    .Select(a => (Guid?)a.Id).FirstOrDefault()))
            .ToListAsync(ct));

        hits.AddRange(await db.Assets
            .AsNoTracking()
            .Where(a => a.GameProjectId == projectId
                && (a.FileName.ToLower().Contains(needle)
                    || (a.Description != null && a.Description.ToLower().Contains(needle))))
            .Take(limit)
            .Select(a => new SearchHit(
                a.Id,
                ModuleKeys.Assets,
                SearchHitKind.Asset,
                a.FileName,
                a.Description,
                a.Id))
            .ToListAsync(ct));

        hits.AddRange(await db.ContentTypes
            .AsNoTracking()
            .Where(t => t.GameProjectId == projectId && t.Name.ToLower().Contains(needle))
            .Take(limit)
            .Select(t => new SearchHit(
                t.Id,
                t.ModuleKey,
                SearchHitKind.ContentType,
                t.Name,
                "Art",
                null))
            .ToListAsync(ct));

        return [.. Rank(hits, needle).Take(limit)];
    }

    /// <summary>
    /// Löst eine eingefügte GUID auf. Wo sie liegt, ist vorher nicht bekannt — deshalb wird
    /// jede Tabelle gefragt, die eine Referenz-GUID vergibt.
    /// </summary>
    private static async Task<List<SearchHit>> FindByIdAsync(
        GameDevManagerDbContext db, Guid projectId, Guid id, CancellationToken ct)
    {
        var item = await db.Items
            .AsNoTracking()
            .Where(i => i.Id == id && i.GameProjectId == projectId)
            .Select(i => new SearchHit(
                i.Id, ModuleKeys.Items, SearchHitKind.Entity, i.Name, "GUID-Treffer",
                db.Assets.Where(a => a.OwnerEntityId == i.Id && a.IsPrimary)
                    .Select(a => (Guid?)a.Id).FirstOrDefault()))
            .FirstOrDefaultAsync(ct);

        if (item is not null)
        {
            return [item];
        }

        var recipe = await db.Recipes
            .AsNoTracking()
            .Where(r => r.Id == id && r.GameProjectId == projectId)
            .Select(r => new SearchHit(
                r.Id, ModuleKeys.Crafting, SearchHitKind.Entity, r.Name, "GUID-Treffer", null))
            .FirstOrDefaultAsync(ct);

        if (recipe is not null)
        {
            return [recipe];
        }

        var asset = await db.Assets
            .AsNoTracking()
            .Where(a => a.Id == id && a.GameProjectId == projectId)
            .Select(a => new SearchHit(
                a.Id, ModuleKeys.Assets, SearchHitKind.Asset, a.FileName, "GUID-Treffer", a.Id))
            .FirstOrDefaultAsync(ct);

        if (asset is not null)
        {
            return [asset];
        }

        var type = await db.ContentTypes
            .AsNoTracking()
            .Where(t => t.Id == id && t.GameProjectId == projectId)
            .Select(t => new SearchHit(
                t.Id, t.ModuleKey, SearchHitKind.ContentType, t.Name, "GUID-Treffer, Art", null))
            .FirstOrDefaultAsync(ct);

        return type is null ? [] : [type];
    }

    /// <summary>
    /// Sortiert die Treffer nach Nützlichkeit: exakte Namen zuerst, dann Namensanfänge, dann
    /// der Rest. Ohne das stünde ein zufälliger Beschreibungstreffer vor dem gesuchten Namen.
    /// </summary>
    private static IEnumerable<SearchHit> Rank(List<SearchHit> hits, string needle) =>
        hits
            .OrderBy(hit => hit.Name.Equals(needle, StringComparison.OrdinalIgnoreCase) ? 0
                : hit.Name.StartsWith(needle, StringComparison.OrdinalIgnoreCase) ? 1
                : hit.Name.Contains(needle, StringComparison.OrdinalIgnoreCase) ? 2
                : 3)
            .ThenBy(hit => hit.Name.Length)
            .ThenBy(hit => hit.Name, StringComparer.OrdinalIgnoreCase);
}
