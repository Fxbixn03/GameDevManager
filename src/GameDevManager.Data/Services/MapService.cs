using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace GameDevManager.Data.Services;

/// <summary>
/// Lesen und Schreiben der Karten samt ihrer Markierungen.
/// </summary>
public class MapService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    ContentTypeService contentTypes,
    AssetService assets,
    IStringLocalizer<DataMessages> messages)
{
    public async Task<List<MapListRow>> GetMapsAsync(Guid projectId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.Maps
            .AsNoTracking()
            .Where(m => m.GameProjectId == projectId)
            .OrderBy(m => m.Name)
            .Select(m => new MapListRow(
                m.Id,
                m.Name,
                m.Description,
                m.ContentTypeId,
                m.ContentType!.Name,
                m.Markers.Count,
                m.Markers.Count(marker => marker.TargetModuleKey == ModuleKeys.Maps),
                m.UpdatedAtUtc,
                db.Assets
                    .Where(a => a.OwnerEntityId == m.Id && a.IsPrimary)
                    .Select(a => (Guid?)a.Id)
                    .FirstOrDefault()))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Wo eine Entität auf den Karten vorkommt — für NPCs sind das ihre Spawn-Orte, für
    /// Karten die Stellen, von denen aus sie erreichbar sind.
    /// </summary>
    public async Task<List<MapPlacement>> GetPlacementsForEntityAsync(
        Guid projectId, Guid entityId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.MapMarkers
            .AsNoTracking()
            .Where(marker => marker.TargetEntityId == entityId && marker.Map!.GameProjectId == projectId)
            .OrderBy(marker => marker.Map!.Name)
            .Select(marker => new MapPlacement(
                marker.MapId,
                marker.Map!.Name,
                marker.Id,
                marker.Label,
                marker.X,
                marker.Y,
                marker.Radius))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Die Markierungen einer Karte als Auswahlliste — etwa für den Schauplatz eines
    /// Story-Abschnitts.
    /// </summary>
    public async Task<List<MapMarkerOption>> GetMarkerOptionsAsync(Guid mapId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.MapMarkers
            .AsNoTracking()
            .Where(marker => marker.MapId == mapId)
            .OrderBy(marker => marker.SortOrder)
            .Select(marker => new MapMarkerOption(marker.Id, marker.Label, marker.X, marker.Y))
            .ToListAsync(ct);
    }

    public async Task<ContentEditContext<GameMap>?> LoadForEditAsync(
        Guid projectId, Guid? mapId, CancellationToken ct = default)
    {
        var types = await contentTypes.GetTypesAsync(projectId, ModuleKeys.Maps, ct);

        if (mapId is null)
        {
            return new ContentEditContext<GameMap>
            {
                Entity = new GameMap { GameProjectId = projectId, Name = string.Empty },
                IsNew = true,
                AvailableTypes = types,
                IndividualFields = [],
                Values = []
            };
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        var map = await db.Maps
            .AsNoTracking()
            .Include(m => m.Markers)
            .Include(m => m.Layers)
            .FirstOrDefaultAsync(m => m.Id == mapId && m.GameProjectId == projectId, ct);

        if (map is null)
        {
            return null;
        }

        map.Markers = [.. map.Markers.OrderBy(marker => marker.SortOrder)];
        map.Layers = [.. map.Layers.OrderBy(layer => layer.SortOrder)];

        return new ContentEditContext<GameMap>
        {
            Entity = map,
            IsNew = false,
            AvailableTypes = types,
            IndividualFields = await ContentFields.LoadIndividualFieldsAsync(db, map.Id, ct),
            Values = await ContentFields.LoadValuesAsync(db, map.Id, ct)
        };
    }

    public async Task SaveMapAsync(ContentEditContext<GameMap> context, CancellationToken ct = default)
    {
        var map = context.Entity;

        if (string.IsNullOrWhiteSpace(map.Name))
        {
            throw new ContentValidationException(messages["MapNameRequired"]);
        }

        Validate(map);
        ContentFields.ValidateRequired(context, messages);

        await using var db = await factory.CreateDbContextAsync(ct);

        var now = DateTime.UtcNow;
        var stored = await db.Maps
            .Include(m => m.Markers)
            .Include(m => m.Layers)
            .FirstOrDefaultAsync(m => m.Id == map.Id, ct);

        if (stored is null)
        {
            stored = new GameMap
            {
                Id = map.Id,
                GameProjectId = map.GameProjectId,
                Name = map.Name.Trim(),
                CreatedAtUtc = now
            };

            db.Maps.Add(stored);
        }

        stored.ContentTypeId = map.ContentTypeId;
        stored.Name = map.Name.Trim();
        stored.Description = string.IsNullOrWhiteSpace(map.Description) ? null : map.Description.Trim();
        stored.UpdatedAtUtc = now;

        SyncLayers(db, stored, map);
        SyncMarkers(db, stored, map);

        // Der Bearbeitungsstand hängt an der Basis aller Inhalte und wird deshalb hier
        // gesetzt und nicht in jedem Zweig der Fallunterscheidung darüber.
        stored.Status = context.Entity.Status;

        await ContentFields.StageValuesAsync(db, context, messages, ct);
        await db.SaveChangesAsync(ct);

        map.CreatedAtUtc = stored.CreatedAtUtc;
        map.UpdatedAtUtc = stored.UpdatedAtUtc;
        map.Name = stored.Name;
        map.Description = stored.Description;
    }

    private void Validate(GameMap map)
    {
        // Eine Ebene ohne Namen wäre in der Ebenen-Liste nicht zu erkennen.
        if (map.Layers.Any(layer => string.IsNullOrWhiteSpace(layer.Name)))
        {
            throw new ContentValidationException(messages["MapLayerNameRequired"]);
        }

        foreach (var marker in map.Markers)
        {
            if (marker.X is < 0 or > 1 || marker.Y is < 0 or > 1)
            {
                throw new ContentValidationException(messages["MapMarkerRange"]);
            }

            if (marker.Radius is < 0 or > 1)
            {
                throw new ContentValidationException(messages["MapRadiusRange"]);
            }

            if (marker.IsPolygon)
            {
                // Unlesbare Punktlisten kommen als leere Liste zurück und laufen in dieselbe
                // Meldung — ein halbes Polygon zu speichern wäre schlimmer als keines.
                var corners = marker.GetPolygonPoints();

                if (corners.Count < 3)
                {
                    throw new ContentValidationException(messages["MapPolygonTooFewPoints"]);
                }

                if (corners.Any(corner => corner.X is < 0 or > 1 || corner.Y is < 0 or > 1))
                {
                    throw new ContentValidationException(messages["MapPolygonRange"]);
                }
            }

            // Eine Karte, die auf sich selbst verweist, führt beim Klick nirgendwohin.
            if (marker.TargetModuleKey == ModuleKeys.Maps && marker.TargetEntityId == map.Id)
            {
                throw new ContentValidationException(messages["MapSelfLink"]);
            }
        }
    }

    private static void SyncLayers(GameDevManagerDbContext db, GameMap stored, GameMap incoming)
    {
        var wanted = incoming.Layers;
        var wantedIds = wanted.Select(layer => layer.Id).ToHashSet();

        // Nur aus der Navigationsliste entfernen — der Fremdschlüssel ist pflicht, EF löscht
        // die Waise dadurch von selbst.
        foreach (var obsolete in stored.Layers.Where(l => !wantedIds.Contains(l.Id)).ToList())
        {
            stored.Layers.Remove(obsolete);
        }

        for (var index = 0; index < wanted.Count; index++)
        {
            var layer = wanted[index];
            var target = stored.Layers.FirstOrDefault(l => l.Id == layer.Id);

            if (target is null)
            {
                // Ausdrücklich über das DbSet — siehe SyncMarkers.
                db.MapLayers.Add(new MapLayer
                {
                    Id = layer.Id,
                    MapId = stored.Id,
                    Name = layer.Name.Trim(),
                    IsVisible = layer.IsVisible,
                    SortOrder = index
                });
            }
            else
            {
                target.Name = layer.Name.Trim();
                target.IsVisible = layer.IsVisible;
                target.SortOrder = index;
            }
        }
    }

    private static void SyncMarkers(GameDevManagerDbContext db, GameMap stored, GameMap incoming)
    {
        var wanted = incoming.Markers;
        var wantedIds = wanted.Select(marker => marker.Id).ToHashSet();

        // Eine Zuordnung zu einer Ebene, die es (nicht mehr) gibt, fällt auf die Grundebene
        // zurück — etwa nach dem Löschen einer Ebene in der Maske.
        var layerIds = incoming.Layers.Select(layer => layer.Id).ToHashSet();

        // Nur aus der Navigationsliste entfernen — der Fremdschlüssel ist pflicht, EF löscht
        // die Waise dadurch von selbst.
        foreach (var obsolete in stored.Markers.Where(m => !wantedIds.Contains(m.Id)).ToList())
        {
            stored.Markers.Remove(obsolete);
        }

        for (var index = 0; index < wanted.Count; index++)
        {
            var marker = wanted[index];
            var target = stored.Markers.FirstOrDefault(m => m.Id == marker.Id);

            if (target is null)
            {
                // Ausdrücklich über das DbSet: die Markierung bringt ihre GUID schon mit, und
                // EF hielte sie beim Anhängen an eine bestehende Karte sonst für einen
                // vorhandenen Datensatz — es entstünde ein UPDATE auf eine fehlende Zeile.
                db.MapMarkers.Add(new MapMarker
                {
                    Id = marker.Id,
                    MapId = stored.Id,
                    X = marker.X,
                    Y = marker.Y,
                    Radius = marker.IsPolygon ? null : marker.Radius,
                    Points = NormalizePoints(marker),
                    Label = Normalize(marker.Label),
                    TargetModuleKey = marker.TargetEntityId is null ? null : marker.TargetModuleKey,
                    TargetEntityId = marker.TargetEntityId,
                    IconAssetId = marker.IconAssetId,
                    Color = Normalize(marker.Color),
                    LayerId = marker.LayerId is { } layerId && layerIds.Contains(layerId) ? layerId : null,
                    SortOrder = index
                });
            }
            else
            {
                target.X = marker.X;
                target.Y = marker.Y;
                target.Radius = marker.IsPolygon ? null : marker.Radius;
                target.Points = NormalizePoints(marker);
                target.Label = Normalize(marker.Label);
                target.TargetModuleKey = marker.TargetEntityId is null ? null : marker.TargetModuleKey;
                target.TargetEntityId = marker.TargetEntityId;
                target.IconAssetId = marker.IconAssetId;
                target.Color = Normalize(marker.Color);
                target.LayerId = marker.LayerId is { } movedLayerId && layerIds.Contains(movedLayerId) ? movedLayerId : null;
                target.SortOrder = index;
            }
        }
    }

    /// <summary>
    /// Löscht eine Karte samt Markierungen, Feldwerten, individuellen Feldern und Bildern.
    /// Markierungen anderer Karten, die hierher verwiesen, verlieren ihr Ziel.
    /// </summary>
    public async Task DeleteMapAsync(Guid mapId, CancellationToken ct = default)
    {
        await assets.DeleteForOwnerAsync(mapId, ct);

        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        await ChangeLog.RecordDeletionAsync(db, db.Maps, mapId, ct);
        await EntityCleanup.DeleteForEntityAsync(db, mapId, ct);

        // Sonst zeigten Verknüpfungen anderer Karten auf eine Karte, die es nicht mehr gibt.
        await db.MapMarkers
            .Where(marker => marker.TargetEntityId == mapId && marker.TargetModuleKey == ModuleKeys.Maps)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(marker => marker.TargetEntityId, (Guid?)null)
                .SetProperty(marker => marker.TargetModuleKey, (string?)null), ct);

        // Dasselbe für Story-Abschnitte, deren Schauplatz diese Karte war.
        await db.StoryEntries
            .Where(s => s.TargetMapId == mapId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(s => s.TargetMapId, (Guid?)null)
                .SetProperty(s => s.TargetMapMarkerId, (Guid?)null), ct);

        // Die eigenen Markierungen fallen über den Fremdschlüssel mit.
        await db.Maps
            .Where(m => m.Id == mapId)
            .ExecuteDeleteAsync(ct);

        await transaction.CommitAsync(ct);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Kanonische Schreibweise der Punktliste (feste Kultur, feste Rundung) — derselbe Stand
    /// ergibt so denselben Export. Ein Kreis oder Punkt bleibt <c>null</c>.
    /// </summary>
    private static string? NormalizePoints(MapMarker marker) =>
        MapMarker.FormatPoints(MapMarker.ParsePoints(marker.Points));
}
