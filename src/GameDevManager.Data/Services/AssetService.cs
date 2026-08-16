using GameDevManager.Data.Assets;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace GameDevManager.Data.Services;

/// <summary>
/// Verwaltet die Asset-Bibliothek: hochladen, zuordnen, benennen, verschlagworten, löschen.
/// Datei und Datensatz werden hier gemeinsam gepflegt — die Datei liegt im
/// <see cref="IAssetStorage"/>, die Beschreibung in der Datenbank.
/// </summary>
public class AssetService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    IAssetStorage storage,
    AssetStorageOptions options,
    ReferenceService references,
    PermissionGuard guard,
    IStringLocalizer<DataMessages> messages)
{
    public AssetStorageOptions Options => options;

    // ------------------------------------------------------------------------------ Hochladen

    /// <summary>
    /// Nimmt eine Datei entgegen, legt sie ab und erzeugt den Datensatz. Ohne
    /// <paramref name="ownerEntityId"/> entsteht ein Werkzeug-Asset ohne Entität.
    /// </summary>
    public async Task<Asset> UploadAsync(
        Guid projectId,
        string fileName,
        string mimeType,
        Stream content,
        string? ownerModuleKey = null,
        Guid? ownerEntityId = null,
        CancellationToken ct = default)
    {
        // Vor dem Ablegen der Datei prüfen — das Speichern der Zeile würde zwar ohnehin am
        // WriteGuardInterceptor scheitern, aber die Datei wäre dann schon geschrieben und
        // gleich wieder gelöscht.
        await guard.EnsureCanWriteAsync(ct);

        // Manche Browser melden für Sprites nur "application/octet-stream" oder gar nichts.
        // In dem Fall entscheidet die Dateiendung, bevor abgelehnt wird.
        if (!options.AllowedMimeTypes.Contains(mimeType, StringComparer.OrdinalIgnoreCase))
        {
            mimeType = AssetMimeTypes.FromFileName(fileName) ?? mimeType;
        }

        if (!options.AllowedMimeTypes.Contains(mimeType, StringComparer.OrdinalIgnoreCase))
        {
            throw new ContentValidationException(
                messages["AssetMimeNotAllowed", fileName, mimeType, string.Join(", ", options.AllowedMimeTypes)]);
        }

        var asset = new Asset
        {
            GameProjectId = projectId,
            OwnerModuleKey = ownerEntityId is null ? null : ownerModuleKey,
            OwnerEntityId = ownerEntityId,
            FileName = Path.GetFileName(fileName),
            MimeType = mimeType,
            StorageKey = string.Empty
        };

        asset.StorageKey = await storage.SaveAsync(
            projectId, asset.Id, DetermineExtension(fileName, mimeType), content, ct);

        try
        {
            await MeasureAsync(asset, ct);

            await using var db = await factory.CreateDbContextAsync(ct);

            asset.SortOrder = ownerEntityId is null
                ? 0
                : await NextSortOrderAsync(db, ownerEntityId.Value, ct);

            // Das erste Sprite einer Entität ist automatisch ihr Icon.
            asset.IsPrimary = ownerEntityId is not null
                && !await db.Assets.AnyAsync(a => a.OwnerEntityId == ownerEntityId, ct);

            db.Assets.Add(asset);
            await db.SaveChangesAsync(ct);

            return asset;
        }
        catch
        {
            // Kein Datensatz, keine Datei — sonst bliebe eine Waise im Speicher liegen.
            storage.Delete(asset.StorageKey);
            throw;
        }
    }

    /// <summary>Ermittelt Größe und Bildmaße aus der abgelegten Datei.</summary>
    private async Task MeasureAsync(Asset asset, CancellationToken ct)
    {
        await using var stored = storage.OpenRead(asset.StorageKey);
        if (stored is null)
        {
            return;
        }

        asset.SizeBytes = stored.Length;

        if (await ImageDimensionReader.TryReadAsync(stored, ct) is var (width, height))
        {
            asset.Width = width;
            asset.Height = height;
        }
    }

    // -------------------------------------------------------------------------------- Abfragen

    /// <summary>Alle Assets einer Entität, primäres zuerst.</summary>
    public async Task<List<Asset>> GetForOwnerAsync(Guid ownerEntityId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var assets = await db.Assets
            .AsNoTracking()
            .Where(a => a.OwnerEntityId == ownerEntityId)
            .Include(a => a.Tags)
            .ToListAsync(ct);

        return [.. assets.OrderByDescending(a => a.IsPrimary).ThenBy(a => a.SortOrder).ThenBy(a => a.FileName)];
    }

    /// <summary>Die GUID des primären Sprites einer Entität, oder <c>null</c>.</summary>
    public async Task<Guid?> GetPrimaryAssetIdAsync(Guid ownerEntityId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.Assets
            .AsNoTracking()
            .Where(a => a.OwnerEntityId == ownerEntityId && a.IsPrimary)
            .Select(a => (Guid?)a.Id)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Asset?> GetAsync(Guid assetId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.Assets
            .AsNoTracking()
            .Include(a => a.Tags)
            .FirstOrDefaultAsync(a => a.Id == assetId, ct);
    }

    /// <summary>Öffnet den Dateiinhalt zur Auslieferung. <c>null</c>, wenn das Asset fehlt.</summary>
    public async Task<(Stream Content, string MimeType, string FileName)?> OpenContentAsync(
        Guid assetId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var asset = await db.Assets
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == assetId, ct);

        if (asset is null)
        {
            return null;
        }

        var content = storage.OpenRead(asset.StorageKey);
        return content is null ? null : (content, asset.MimeType, asset.FileName);
    }

    /// <summary>
    /// Die gesamte Bibliothek eines Projekts, angereichert um den Namen der besitzenden
    /// Entität — die Bibliothek gruppiert danach.
    /// </summary>
    public async Task<List<AssetLibraryEntry>> GetLibraryAsync(Guid projectId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var assets = await db.Assets
            .AsNoTracking()
            .Where(a => a.GameProjectId == projectId)
            .Include(a => a.Tags)
            .ToListAsync(ct);

        var names = new Dictionary<Guid, string>();

        foreach (var perModule in assets
            .Where(a => a.OwnerEntityId is not null && a.OwnerModuleKey is not null)
            .GroupBy(a => a.OwnerModuleKey!))
        {
            var ids = perModule.Select(a => a.OwnerEntityId!.Value).Distinct().ToList();

            foreach (var (id, name) in await references.ResolveNamesAsync(perModule.Key, ids, ct))
            {
                names[id] = name;
            }
        }

        return
        [
            .. assets
                .Select(asset => new AssetLibraryEntry(
                    asset,
                    asset.OwnerEntityId is { } ownerId ? names.GetValueOrDefault(ownerId) : null))
                .OrderBy(entry => entry.OwnerName ?? string.Empty)
                .ThenByDescending(entry => entry.Asset.IsPrimary)
                .ThenBy(entry => entry.Asset.SortOrder)
                .ThenBy(entry => entry.Asset.FileName)
        ];
    }

    // ------------------------------------------------------------------------------- Bearbeiten

    /// <summary>Schreibt Beschreibung, Zuordnung und Stichwörter eines Assets fort.</summary>
    public async Task SaveMetadataAsync(
        Guid assetId,
        string? description,
        string? ownerModuleKey,
        Guid? ownerEntityId,
        IReadOnlyCollection<Guid> tagIds,
        CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var asset = await db.Assets
            .Include(a => a.Tags)
            .FirstOrDefaultAsync(a => a.Id == assetId, ct)
            ?? throw new ContentValidationException(messages["AssetGone"]);

        var previousOwnerId = asset.OwnerEntityId;
        var wasPrimary = asset.IsPrimary;
        var ownerChanged = asset.OwnerEntityId != ownerEntityId;

        asset.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        asset.OwnerEntityId = ownerEntityId;
        asset.OwnerModuleKey = ownerEntityId is null ? null : ownerModuleKey;

        if (ownerChanged)
        {
            // Als Werkzeug-Asset gibt es kein Icon, und in einer neuen Entität muss das Icon
            // erst wieder vergeben werden.
            asset.IsPrimary = ownerEntityId is not null
                && !await db.Assets.AnyAsync(a => a.OwnerEntityId == ownerEntityId && a.Id != assetId, ct);
            asset.SortOrder = ownerEntityId is null ? 0 : await NextSortOrderAsync(db, ownerEntityId.Value, ct);
        }

        var wanted = tagIds.ToHashSet();

        foreach (var obsolete in asset.Tags.Where(assignment => !wanted.Contains(assignment.AssetTagId)).ToList())
        {
            asset.Tags.Remove(obsolete);
        }

        foreach (var tagId in wanted.Where(id => asset.Tags.All(assignment => assignment.AssetTagId != id)))
        {
            asset.Tags.Add(new AssetTagAssignment { AssetId = assetId, AssetTagId = tagId });
        }

        await db.SaveChangesAsync(ct);

        // Wurde das Icon einer Entität weggehängt, bekommt sie eines ihrer übrigen Sprites.
        if (ownerChanged && wasPrimary && previousOwnerId is { } formerOwnerId)
        {
            await PromoteSuccessorAsync(db, formerOwnerId, ct);
        }
    }

    /// <summary>Macht ein Asset zum Icon seiner Entität und nimmt den Status dem bisherigen ab.</summary>
    public async Task SetPrimaryAsync(Guid assetId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var asset = await db.Assets.FirstOrDefaultAsync(a => a.Id == assetId, ct)
            ?? throw new ContentValidationException(messages["AssetGone"]);

        if (asset.OwnerEntityId is not { } ownerId)
        {
            throw new ContentValidationException(messages["AssetPrimaryNeedsOwner"]);
        }

        var siblings = await db.Assets.Where(a => a.OwnerEntityId == ownerId).ToListAsync(ct);

        foreach (var sibling in siblings)
        {
            sibling.IsPrimary = sibling.Id == assetId;
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Verschiebt ein Asset innerhalb seiner Entität, z. B. für die Reihenfolge einer Animation.</summary>
    public async Task MoveAsync(Guid assetId, int offset, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var asset = await db.Assets.FirstOrDefaultAsync(a => a.Id == assetId, ct);
        if (asset?.OwnerEntityId is not { } ownerId)
        {
            return;
        }

        var ordered = await db.Assets
            .Where(a => a.OwnerEntityId == ownerId)
            .OrderBy(a => a.SortOrder).ThenBy(a => a.FileName)
            .ToListAsync(ct);

        var index = ordered.FindIndex(a => a.Id == assetId);
        var targetIndex = index + offset;

        if (index < 0 || targetIndex < 0 || targetIndex >= ordered.Count)
        {
            return;
        }

        (ordered[index], ordered[targetIndex]) = (ordered[targetIndex], ordered[index]);

        for (var position = 0; position < ordered.Count; position++)
        {
            ordered[position].SortOrder = position;
        }

        await db.SaveChangesAsync(ct);
    }

    // --------------------------------------------------------------------------------- Löschen

    public async Task DeleteAsync(Guid assetId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var asset = await db.Assets.FirstOrDefaultAsync(a => a.Id == assetId, ct);
        if (asset is null)
        {
            return;
        }

        var ownerId = asset.OwnerEntityId;
        var wasPrimary = asset.IsPrimary;

        db.Assets.Remove(asset);
        await db.SaveChangesAsync(ct);

        storage.Delete(asset.StorageKey);

        if (wasPrimary && ownerId is { } formerOwnerId)
        {
            await PromoteSuccessorAsync(db, formerOwnerId, ct);
        }
    }

    /// <summary>
    /// Stellt sicher, dass eine Entität mit Sprites auch eines als Icon hat. Nötig, sobald das
    /// bisherige Icon gelöscht oder auf eine andere Entität umgehängt wurde — sonst stünde die
    /// Entität in allen Listen ohne Bild da, obwohl sie noch Sprites besitzt.
    /// </summary>
    private static async Task PromoteSuccessorAsync(
        GameDevManagerDbContext db, Guid ownerEntityId, CancellationToken ct)
    {
        if (await db.Assets.AnyAsync(a => a.OwnerEntityId == ownerEntityId && a.IsPrimary, ct))
        {
            return;
        }

        var successor = await db.Assets
            .Where(a => a.OwnerEntityId == ownerEntityId)
            .OrderBy(a => a.SortOrder).ThenBy(a => a.FileName)
            .FirstOrDefaultAsync(ct);

        if (successor is null)
        {
            return;
        }

        successor.IsPrimary = true;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Entfernt alle Assets einer Entität samt Dateien. Wird beim Löschen einer Entität
    /// aufgerufen — Assets hängen über die GUID und nicht über einen Fremdschlüssel daran.
    /// </summary>
    public async Task DeleteForOwnerAsync(Guid ownerEntityId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var assets = await db.Assets.Where(a => a.OwnerEntityId == ownerEntityId).ToListAsync(ct);
        if (assets.Count == 0)
        {
            return;
        }

        db.Assets.RemoveRange(assets);
        await db.SaveChangesAsync(ct);

        foreach (var asset in assets)
        {
            storage.Delete(asset.StorageKey);
        }
    }

    // ------------------------------------------------------------------------------ Stichwörter

    // ------------------------------------------------------------------- Zuordnung nach Namen

    /// <summary>
    /// Schlägt zu jedem noch nicht zugeordneten Asset die Entitäten vor, deren Name zum
    /// Dateinamen passt — `eisenschwert.png` zum Item „Eisenschwert“.
    /// <para>
    /// Gesucht wird über die <see cref="IModuleEntitySource"/> in <b>allen</b> Modulen; ein
    /// neues Modul ist damit von selbst dabei. Zugeordnet wird nie stillschweigend: Bei zwei
    /// gleichnamigen Entitäten in verschiedenen Modulen wäre die Wahl geraten, und die trifft
    /// der Nutzer.
    /// </para>
    /// </summary>
    public async Task<List<AssetOwnerSuggestion>> SuggestOwnersAsync(
        Guid projectId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var unassigned = await db.Assets
            .AsNoTracking()
            .Where(asset => asset.GameProjectId == projectId && asset.OwnerEntityId == null)
            .OrderBy(asset => asset.FileName)
            .ToListAsync(ct);

        if (unassigned.Count == 0)
        {
            return [];
        }

        // Ein Verzeichnis über den normalisierten Namen; gleichnamige Entitäten stehen
        // nebeneinander und werden dem Nutzer beide angeboten.
        var byName = new Dictionary<string, List<EntitySummary>>(StringComparer.Ordinal);

        foreach (var source in references.Sources)
        {
            foreach (var entity in await source.GetEntitiesAsync(db, projectId, ct))
            {
                var key = NormalizeForMatch(entity.Name);
                if (key.Length == 0)
                {
                    continue;
                }

                if (!byName.TryGetValue(key, out var list))
                {
                    byName[key] = list = [];
                }

                list.Add(entity);
            }
        }

        var suggestions = new List<AssetOwnerSuggestion>();

        foreach (var asset in unassigned)
        {
            var key = NormalizeForMatch(Path.GetFileNameWithoutExtension(asset.FileName));
            var candidates = byName.GetValueOrDefault(key) ?? [];

            suggestions.Add(new AssetOwnerSuggestion(asset, candidates));
        }

        return suggestions;
    }

    /// <summary>
    /// Hängt mehrere Assets in einem Rutsch an ihre Entitäten. Je Entität wird das erste
    /// zugeordnete Asset ihr Icon — das übernimmt <see cref="SaveMetadataAsync"/> ohnehin.
    /// </summary>
    public async Task<int> AssignOwnersAsync(
        IReadOnlyDictionary<Guid, (string ModuleKey, Guid EntityId)> assignments,
        CancellationToken ct = default)
    {
        var assigned = 0;

        foreach (var (assetId, target) in assignments)
        {
            await SaveMetadataAsync(assetId, null, target.ModuleKey, target.EntityId, [], ct);
            assigned++;
        }

        return assigned;
    }

    /// <summary>
    /// Vergleichsform eines Namens: kleingeschrieben und ohne alles, was kein Buchstabe und
    /// keine Ziffer ist. Damit trifft „eisen-schwert.png“ auch „Eisenschwert“ — Dateinamen
    /// tragen Trennzeichen, wo ein Anzeigename ein Leerzeichen hat.
    /// </summary>
    private static string NormalizeForMatch(string value) =>
        new([.. value.ToLowerInvariant().Where(char.IsLetterOrDigit)]);

    // ------------------------------------------------------------------------ Verwaiste Dateien

    /// <summary>
    /// Dateien im Speicher, zu denen es keine Zeile in der Datenbank gibt. Sie entstehen bei
    /// jedem abgebrochenen Import und bei jedem Fehler zwischen Dateisystem und Transaktion —
    /// der Health Check „verwaiste Sprites“ prüft die Gegenrichtung.
    /// <para>
    /// Gelistet wird <b>installationsweit</b> und nicht je Projekt: Die Dateien eines
    /// gelöschten Projekts sind genau der Fall, den man sucht, und deren Projekt-GUID steht in
    /// keiner Tabelle mehr.
    /// </para>
    /// </summary>
    public async Task<List<string>> FindOrphanedFilesAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var known = (await db.Assets
                .AsNoTracking()
                .Select(asset => asset.StorageKey)
                .ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);

        return [.. storage.ListKeys().Where(key => !known.Contains(key))];
    }

    /// <summary>
    /// Löscht die übergebenen verwaisten Dateien. Ein zweiter, ausdrücklicher Klick — dieselbe
    /// Zurückhaltung wie bei den Exportständen, die fremde Dateien bewusst stehen lassen.
    /// <para>
    /// Geprüft wird <b>erneut</b>, ob der Schlüssel wirklich verwaist ist: Zwischen Anzeigen
    /// und Klicken kann ein Upload dazwischengekommen sein, und eine Datei zu löschen, an der
    /// eine Zeile hängt, wäre der schlimmere Fehler.
    /// </para>
    /// </summary>
    public async Task<int> DeleteOrphanedFilesAsync(
        IReadOnlyCollection<string> storageKeys, CancellationToken ct = default)
    {
        await guard.EnsureCanWriteAsync(ct);

        if (storageKeys.Count == 0)
        {
            return 0;
        }

        var orphans = (await FindOrphanedFilesAsync(ct)).ToHashSet(StringComparer.Ordinal);
        var deleted = 0;

        foreach (var key in storageKeys.Where(orphans.Contains))
        {
            try
            {
                storage.Delete(key);
                deleted++;
            }
            catch (IOException)
            {
                // Eine Datei, die gerade gelesen wird, bleibt stehen und fällt beim nächsten
                // Lauf — dasselbe Verhalten wie beim Aufräumen der Exportstände.
            }
        }

        return deleted;
    }

    public async Task<List<AssetTag>> GetTagsAsync(Guid projectId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.AssetTags
            .AsNoTracking()
            .Where(tag => tag.GameProjectId == projectId)
            .OrderBy(tag => tag.SortOrder).ThenBy(tag => tag.Name)
            .ToListAsync(ct);
    }

    public async Task SaveTagAsync(AssetTag tag, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(tag.Name))
        {
            throw new ContentValidationException(messages["AssetTagNameRequired"]);
        }

        var name = tag.Name.Trim();

        await using var db = await factory.CreateDbContextAsync(ct);

        var taken = await db.AssetTags.AnyAsync(
            other => other.GameProjectId == tag.GameProjectId && other.Name == name && other.Id != tag.Id, ct);

        if (taken)
        {
            throw new ContentValidationException(messages["AssetTagExists", name]);
        }

        var stored = await db.AssetTags.FirstOrDefaultAsync(other => other.Id == tag.Id, ct);

        if (stored is null)
        {
            db.AssetTags.Add(new AssetTag
            {
                Id = tag.Id,
                GameProjectId = tag.GameProjectId,
                Name = name,
                Color = tag.Color,
                SortOrder = tag.SortOrder
            });
        }
        else
        {
            stored.Name = name;
            stored.Color = tag.Color;
            stored.SortOrder = tag.SortOrder;
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Löscht ein Stichwort; die Zuordnungen fallen über den Fremdschlüssel mit.</summary>
    public async Task DeleteTagAsync(Guid tagId, CancellationToken ct = default)
    {
        // Reines ExecuteDelete ohne vorheriges Speichern — hier greift der
        // WriteGuardInterceptor nicht, die Prüfung steht deshalb ausdrücklich da.
        await guard.EnsureCanWriteAsync(ct);

        await using var db = await factory.CreateDbContextAsync(ct);

        await db.AssetTags.Where(tag => tag.Id == tagId).ExecuteDeleteAsync(ct);
    }

    // ----------------------------------------------------------------------------------- Hilfen

    private static async Task<int> NextSortOrderAsync(
        GameDevManagerDbContext db, Guid ownerEntityId, CancellationToken ct)
    {
        var maximum = await db.Assets
            .Where(a => a.OwnerEntityId == ownerEntityId)
            .MaxAsync(a => (int?)a.SortOrder, ct);

        return (maximum ?? -1) + 1;
    }

    /// <summary>
    /// Endung für die abgelegte Datei. Der Name des Nutzers ist die erste Wahl, weil er die
    /// Variante genauer trifft (etwa .jpeg gegenüber .jpg); der MIME-Typ springt ein, wenn
    /// der Name keine brauchbare Endung hat.
    /// </summary>
    private static string DetermineExtension(string fileName, string mimeType)
    {
        var extension = Path.GetExtension(fileName);

        if (extension.Length is > 1 and <= 10 && extension[1..].All(char.IsLetterOrDigit))
        {
            return extension.ToLowerInvariant();
        }

        return mimeType.ToLowerInvariant() switch
        {
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            "image/gif" => ".gif",
            "image/webp" => ".webp",
            "image/bmp" => ".bmp",
            "image/svg+xml" => ".svg",
            _ => ".bin"
        };
    }
}
