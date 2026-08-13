using GameDevManager.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

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
/// <para>
/// Gesucht wird über mehr als den Namen: Beschreibungen, die Textwerte der
/// benutzerdefinierten Felder und die gesprochenen Zeilen der Dialoge. Gerade Dialogtexte
/// tragen ihren Inhalt nirgends im Namen und wären sonst nicht wiederzufinden. Treffer
/// abseits des Namens sind als solche beschriftet, weil sonst unklar wäre, warum ein Eintrag
/// in der Liste steht.
/// </para>
/// <para>
/// Die Module melden sich über <see cref="IModuleEntitySource"/>; Assets und Arten kommen
/// hinzu, weil sie keine Modul-Entitäten sind.
/// </para>
/// </summary>
public class SearchService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    IEnumerable<IModuleEntitySource> sources,
    IStringLocalizer<DataMessages> messages)
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

        foreach (var source in sources)
        {
            hits.AddRange(await source.SearchAsync(db, projectId, needle, limit, ct));
        }

        // Feldwerte danach und nicht vermischt: Wo dieselbe Entität schon über ihren Namen
        // gefunden wurde, ist der Namenstreffer der bessere — der Feldtreffer entfällt.
        var fieldValueHit = messages["Search_FieldValueHit"].Value;
        var found = hits.Select(hit => hit.Id).ToHashSet();

        foreach (var source in sources)
        {
            hits.AddRange((await source.SearchFieldValuesAsync(db, projectId, needle, limit, ct))
                .Where(hit => found.Add(hit.Id))
                .Select(hit => hit with { Subtitle = fieldValueHit }));
        }

        hits.AddRange(await db.Assets
            .AsNoTracking()
            .Where(a => a.GameProjectId == projectId
                && (a.FileName.ToLower().Contains(needle)
                    || (a.Description != null && a.Description.ToLower().Contains(needle))))
            .OrderBy(a => a.FileName)
            .Take(limit)
            .Select(a => new SearchHit(
                // Bewusst das Asset-Modul und nicht das der besitzenden Entität: der Treffer
                // führt in die Bibliothek, also soll dort auch „Assets“ stehen.
                a.Id, ModuleKeys.Assets, SearchHitKind.Asset,
                a.FileName, a.Description, a.Id))
            .ToListAsync(ct));

        var typeLabel = messages["Search_ContentType"].Value;

        hits.AddRange(await db.ContentTypes
            .AsNoTracking()
            .Where(t => t.GameProjectId == projectId && t.Name.ToLower().Contains(needle))
            .OrderBy(t => t.Name)
            .Take(limit)
            .Select(t => new SearchHit(
                t.Id, t.ModuleKey, SearchHitKind.ContentType, t.Name, typeLabel, null))
            .ToListAsync(ct));

        return [.. Rank(hits, needle).Take(limit)];
    }

    /// <summary>
    /// Löst eine eingefügte GUID auf. Wo sie liegt, ist vorher nicht bekannt — deshalb wird
    /// jede Quelle gefragt, die Referenz-GUIDs vergibt.
    /// </summary>
    private async Task<List<SearchHit>> FindByIdAsync(
        GameDevManagerDbContext db, Guid projectId, Guid id, CancellationToken ct)
    {
        var guidHit = messages["Search_GuidHit"].Value;

        foreach (var source in sources)
        {
            if (await source.FindByIdAsync(db, projectId, id, ct) is { } hit)
            {
                return [hit with { Subtitle = guidHit }];
            }
        }

        var asset = await db.Assets
            .AsNoTracking()
            .Where(a => a.Id == id && a.GameProjectId == projectId)
            .Select(a => new SearchHit(
                // Bewusst das Asset-Modul und nicht das der besitzenden Entität: der Treffer
                // führt in die Bibliothek, also soll dort auch „Assets“ stehen.
                a.Id, ModuleKeys.Assets, SearchHitKind.Asset,
                a.FileName, guidHit, a.Id))
            .FirstOrDefaultAsync(ct);

        if (asset is not null)
        {
            return [asset];
        }

        var guidTypeHit = messages["Search_GuidContentTypeHit"].Value;

        var type = await db.ContentTypes
            .AsNoTracking()
            .Where(t => t.Id == id && t.GameProjectId == projectId)
            .Select(t => new SearchHit(
                t.Id, t.ModuleKey, SearchHitKind.ContentType, t.Name, guidTypeHit, null))
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
