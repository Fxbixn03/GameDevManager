using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace GameDevManager.Data.Services;

/// <summary>
/// Lesen und Schreiben der Zufalls-Events samt Mob-Spawns, Belohnung und
/// benutzerdefinierten Feldwerten.
/// </summary>
public class EventService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    ContentTypeService contentTypes,
    AssetService assets,
    IStringLocalizer<DataMessages> messages)
{
    public async Task<List<EventListRow>> GetEventsAsync(Guid projectId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.GameEvents
            .AsNoTracking()
            .Where(e => e.GameProjectId == projectId)
            .OrderBy(e => e.Name)
            .Select(e => new EventListRow(
                e.Id,
                e.Name,
                e.Description,
                e.Chance,
                e.Spawns.Count,
                e.RewardLootTableId,
                db.LootTables.Where(t => t.Id == e.RewardLootTableId).Select(t => t.Name).FirstOrDefault(),
                e.ContentTypeId,
                e.ContentType!.Name,
                e.UpdatedAtUtc,
                db.Assets
                    .Where(a => a.OwnerEntityId == e.Id && a.IsPrimary)
                    .Select(a => (Guid?)a.Id)
                    .FirstOrDefault()))
            .ToListAsync(ct);
    }

    public async Task<ContentEditContext<GameEvent>?> LoadForEditAsync(
        Guid projectId, Guid? eventId, CancellationToken ct = default)
    {
        var types = await contentTypes.GetTypesAsync(projectId, ModuleKeys.Events, ct);

        if (eventId is null)
        {
            return new ContentEditContext<GameEvent>
            {
                Entity = new GameEvent { GameProjectId = projectId, Name = string.Empty },
                IsNew = true,
                AvailableTypes = types,
                IndividualFields = [],
                Values = []
            };
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        var gameEvent = await db.GameEvents
            .AsNoTracking()
            .Include(e => e.Spawns)
            .FirstOrDefaultAsync(e => e.Id == eventId && e.GameProjectId == projectId, ct);

        if (gameEvent is null)
        {
            return null;
        }

        gameEvent.Spawns = [.. gameEvent.Spawns.OrderBy(s => s.SortOrder)];

        return new ContentEditContext<GameEvent>
        {
            Entity = gameEvent,
            IsNew = false,
            AvailableTypes = types,
            IndividualFields = await ContentFields.LoadIndividualFieldsAsync(db, gameEvent.Id, ct),
            Values = await ContentFields.LoadValuesAsync(db, gameEvent.Id, ct)
        };
    }

    public async Task SaveEventAsync(ContentEditContext<GameEvent> context, CancellationToken ct = default)
    {
        var gameEvent = context.Entity;

        if (string.IsNullOrWhiteSpace(gameEvent.Name))
        {
            throw new ContentValidationException(messages["EventNameRequired"]);
        }

        Validate(gameEvent);
        ContentFields.ValidateRequired(context, messages);

        await using var db = await factory.CreateDbContextAsync(ct);

        var now = DateTime.UtcNow;
        var stored = await db.GameEvents
            .Include(e => e.Spawns)
            .FirstOrDefaultAsync(e => e.Id == gameEvent.Id, ct);

        if (stored is null)
        {
            stored = new GameEvent
            {
                Id = gameEvent.Id,
                GameProjectId = gameEvent.GameProjectId,
                Name = gameEvent.Name.Trim(),
                CreatedAtUtc = now
            };

            db.GameEvents.Add(stored);
        }

        stored.ContentTypeId = gameEvent.ContentTypeId;
        stored.Name = gameEvent.Name.Trim();
        stored.Description = string.IsNullOrWhiteSpace(gameEvent.Description) ? null : gameEvent.Description.Trim();
        stored.Chance = gameEvent.Chance;
        stored.RewardLootTableId = gameEvent.RewardLootTableId;
        stored.UpdatedAtUtc = now;

        var removedSpawnIds = new List<Guid>();
        SyncSpawns(db, stored, gameEvent, removedSpawnIds);

        await EntityCleanup.DeleteForEntitiesAsync(db, removedSpawnIds, ct);

        await ContentFields.StageValuesAsync(db, context, ct);
        await db.SaveChangesAsync(ct);

        gameEvent.CreatedAtUtc = stored.CreatedAtUtc;
        gameEvent.UpdatedAtUtc = stored.UpdatedAtUtc;
        gameEvent.Name = stored.Name;
        gameEvent.Description = stored.Description;
    }

    private void Validate(GameEvent gameEvent)
    {
        if (gameEvent.Chance is < 0 or > 100)
        {
            throw new ContentValidationException(messages["EventChanceRange"]);
        }

        // Ein Spawn ohne Mob ist eine unfertige Eingabezeile; die Maske räumt sie vorher weg.
        if (gameEvent.Spawns.Any(spawn => spawn.NpcId == Guid.Empty))
        {
            throw new ContentValidationException(messages["EventSpawnNpcRequired"]);
        }

        var duplicate = gameEvent.Spawns
            .GroupBy(spawn => spawn.NpcId)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw new ContentValidationException(messages["EventSpawnDuplicate"]);
        }

        if (gameEvent.Spawns.Any(spawn => spawn.Count < 1))
        {
            throw new ContentValidationException(messages["EventSpawnCount"]);
        }
    }

    private static void SyncSpawns(
        GameDevManagerDbContext db, GameEvent stored, GameEvent incoming, List<Guid> removedIds)
    {
        var wanted = incoming.Spawns;
        var wantedIds = wanted.Select(spawn => spawn.Id).ToHashSet();

        // Nur aus der Navigationsliste entfernen — der Fremdschlüssel ist pflicht, EF löscht
        // die Waise dadurch von selbst.
        foreach (var obsolete in stored.Spawns.Where(s => !wantedIds.Contains(s.Id)).ToList())
        {
            stored.Spawns.Remove(obsolete);
            removedIds.Add(obsolete.Id);
        }

        for (var index = 0; index < wanted.Count; index++)
        {
            var spawn = wanted[index];
            var target = stored.Spawns.FirstOrDefault(s => s.Id == spawn.Id);

            if (target is null)
            {
                // Ausdrücklich über das DbSet — siehe EF-Fallstrick bei Kind-Sammlungen.
                db.EventSpawns.Add(new EventSpawn
                {
                    Id = spawn.Id,
                    GameEventId = stored.Id,
                    NpcId = spawn.NpcId,
                    Count = spawn.Count,
                    SortOrder = index
                });
            }
            else
            {
                target.NpcId = spawn.NpcId;
                target.Count = spawn.Count;
                target.SortOrder = index;
            }
        }
    }

    /// <summary>Löscht ein Event mit Spawns, Feldwerten, individuellen Feldern, Bedingungen und Sprites.</summary>
    public async Task DeleteEventAsync(Guid eventId, CancellationToken ct = default)
    {
        await assets.DeleteForOwnerAsync(eventId, ct);

        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var spawnIds = await db.EventSpawns
            .Where(spawn => spawn.GameEventId == eventId)
            .Select(spawn => spawn.Id)
            .ToListAsync(ct);

        await EntityCleanup.DeleteForEntitiesAsync(db, [eventId, .. spawnIds], ct);

        // Die Spawns fallen über den Fremdschlüssel mit.
        await db.GameEvents
            .Where(e => e.Id == eventId)
            .ExecuteDeleteAsync(ct);

        await transaction.CommitAsync(ct);
    }
}
