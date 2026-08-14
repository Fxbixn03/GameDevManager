using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace GameDevManager.Data.Services;

/// <summary>
/// Lesen und Schreiben der Story-Abschnitte samt Beteiligten und benutzerdefinierten
/// Feldwerten — inklusive der Reihenfolge des Zeitstreifens.
/// </summary>
public class StoryService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    ContentTypeService contentTypes,
    AssetService assets,
    IStringLocalizer<DataMessages> messages)
{
    /// <summary>Der Zeitstreifen: alle Abschnitte eines Projekts in ihrer Reihenfolge.</summary>
    public async Task<List<StoryListRow>> GetEntriesAsync(Guid projectId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.StoryEntries
            .AsNoTracking()
            .Where(s => s.GameProjectId == projectId)
            .OrderBy(s => s.SortOrder).ThenBy(s => s.Name)
            .Select(s => new StoryListRow(
                s.Id,
                s.Name,
                s.Description,
                s.SortOrder,
                s.Body != null && s.Body.Length > 0,
                s.ContentTypeId,
                s.ContentType!.Name,
                s.Participants.Count,
                s.UpdatedAtUtc,
                db.Assets
                    .Where(a => a.OwnerEntityId == s.Id && a.IsPrimary)
                    .Select(a => (Guid?)a.Id)
                    .FirstOrDefault()))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Übernimmt eine per Drag &amp; Drop neu sortierte Reihenfolge des Zeitstreifens.
    /// Abschnitte, die nicht in der Liste stehen (parallel angelegt), bleiben dahinter.
    /// </summary>
    public async Task ReorderAsync(Guid projectId, IReadOnlyList<Guid> orderedIds, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var entries = await db.StoryEntries
            .Where(s => s.GameProjectId == projectId)
            .OrderBy(s => s.SortOrder).ThenBy(s => s.Name)
            .ToListAsync(ct);

        var position = orderedIds
            .Select((id, index) => (id, index))
            .ToDictionary(pair => pair.id, pair => pair.index);

        var next = orderedIds.Count;

        foreach (var entry in entries)
        {
            entry.SortOrder = position.TryGetValue(entry.Id, out var index) ? index : next++;
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Schiebt einen Abschnitt im Zeitstreifen einen Platz nach oben oder unten, indem er
    /// die Position mit seinem Nachbarn tauscht.
    /// </summary>
    public async Task MoveAsync(Guid projectId, Guid entryId, bool up, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        // Reihenfolge einmal komplett neu durchnummerieren, damit Lücken und Dubletten aus
        // gelöschten Abschnitten das Tauschen nicht ins Leere laufen lassen.
        var entries = await db.StoryEntries
            .Where(s => s.GameProjectId == projectId)
            .OrderBy(s => s.SortOrder).ThenBy(s => s.Name)
            .ToListAsync(ct);

        for (var index = 0; index < entries.Count; index++)
        {
            entries[index].SortOrder = index;
        }

        var current = entries.FirstOrDefault(s => s.Id == entryId);
        if (current is null)
        {
            return;
        }

        var neighborOrder = up ? current.SortOrder - 1 : current.SortOrder + 1;
        var neighbor = entries.FirstOrDefault(s => s.SortOrder == neighborOrder);

        if (neighbor is not null)
        {
            (current.SortOrder, neighbor.SortOrder) = (neighbor.SortOrder, current.SortOrder);
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<ContentEditContext<StoryEntry>?> LoadForEditAsync(
        Guid projectId, Guid? entryId, CancellationToken ct = default)
    {
        var types = await contentTypes.GetTypesAsync(projectId, ModuleKeys.Story, ct);

        if (entryId is null)
        {
            await using var countDb = await factory.CreateDbContextAsync(ct);

            // Neue Abschnitte kommen ans Ende des Zeitstreifens.
            var maxOrder = await countDb.StoryEntries
                .Where(s => s.GameProjectId == projectId)
                .Select(s => (int?)s.SortOrder)
                .MaxAsync(ct) ?? -1;

            return new ContentEditContext<StoryEntry>
            {
                Entity = new StoryEntry
                {
                    GameProjectId = projectId,
                    Name = string.Empty,
                    SortOrder = maxOrder + 1
                },
                IsNew = true,
                AvailableTypes = types,
                IndividualFields = [],
                Values = []
            };
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        var entry = await db.StoryEntries
            .AsNoTracking()
            .Include(s => s.Participants)
            .Include(s => s.Links)
            .FirstOrDefaultAsync(s => s.Id == entryId && s.GameProjectId == projectId, ct);

        if (entry is null)
        {
            return null;
        }

        entry.Participants = [.. entry.Participants.OrderBy(p => p.SortOrder)];
        entry.Links = [.. entry.Links.OrderBy(l => l.SortOrder)];

        return new ContentEditContext<StoryEntry>
        {
            Entity = entry,
            IsNew = false,
            AvailableTypes = types,
            IndividualFields = await ContentFields.LoadIndividualFieldsAsync(db, entry.Id, ct),
            Values = await ContentFields.LoadValuesAsync(db, entry.Id, ct)
        };
    }

    public async Task SaveEntryAsync(ContentEditContext<StoryEntry> context, CancellationToken ct = default)
    {
        var entry = context.Entity;

        if (string.IsNullOrWhiteSpace(entry.Name))
        {
            throw new ContentValidationException(messages["StoryEntryNameRequired"]);
        }

        Validate(entry);
        ContentFields.ValidateRequired(context, messages);

        await using var db = await factory.CreateDbContextAsync(ct);

        var now = DateTime.UtcNow;
        var stored = await db.StoryEntries
            .Include(s => s.Participants)
            .Include(s => s.Links)
            .FirstOrDefaultAsync(s => s.Id == entry.Id, ct);

        if (stored is null)
        {
            stored = new StoryEntry
            {
                Id = entry.Id,
                GameProjectId = entry.GameProjectId,
                Name = entry.Name.Trim(),
                CreatedAtUtc = now
            };

            db.StoryEntries.Add(stored);
        }

        stored.ContentTypeId = entry.ContentTypeId;
        stored.Name = entry.Name.Trim();
        stored.Description = string.IsNullOrWhiteSpace(entry.Description) ? null : entry.Description.Trim();
        stored.Body = string.IsNullOrWhiteSpace(entry.Body) ? null : entry.Body;
        stored.Mood = Normalize(entry.Mood);
        stored.GameDate = Normalize(entry.GameDate);
        stored.Duration = Normalize(entry.Duration);
        stored.Location = Normalize(entry.Location);
        stored.TargetMapId = entry.TargetMapId;
        // Eine Markierung ohne Karte wäre nicht zu deuten.
        stored.TargetMapMarkerId = entry.TargetMapId is null ? null : entry.TargetMapMarkerId;
        stored.SortOrder = entry.SortOrder;
        stored.UpdatedAtUtc = now;

        var removedParticipantIds = new List<Guid>();
        SyncParticipants(db, stored, entry, removedParticipantIds);
        SyncLinks(db, stored, entry);

        await EntityCleanup.DeleteForEntitiesAsync(db, removedParticipantIds, ct);

        await ContentFields.StageValuesAsync(db, context, messages, ct);
        await db.SaveChangesAsync(ct);

        entry.CreatedAtUtc = stored.CreatedAtUtc;
        entry.UpdatedAtUtc = stored.UpdatedAtUtc;
        entry.Name = stored.Name;
        entry.Description = stored.Description;
    }

    private void Validate(StoryEntry entry)
    {
        // Ein Beteiligter ohne Entität ist eine unfertige Eingabezeile; die Maske räumt sie vorher weg.
        if (entry.Participants.Any(p => p.TargetEntityId == Guid.Empty))
        {
            throw new ContentValidationException(messages["StoryParticipantEntityRequired"]);
        }

        var duplicate = entry.Participants
            .GroupBy(p => p.TargetEntityId)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw new ContentValidationException(messages["StoryParticipantDuplicate"]);
        }

        // Dieselben Regeln für die Szenen-Verknüpfungen.
        if (entry.Links.Any(l => l.TargetEntryId == Guid.Empty))
        {
            throw new ContentValidationException(messages["StoryLinkEntryRequired"]);
        }

        if (entry.Links.Any(l => l.TargetEntryId == entry.Id))
        {
            throw new ContentValidationException(messages["StoryLinkSelf"]);
        }

        if (entry.Links.GroupBy(l => l.TargetEntryId).Any(group => group.Count() > 1))
        {
            throw new ContentValidationException(messages["StoryLinkDuplicate"]);
        }
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void SyncLinks(GameDevManagerDbContext db, StoryEntry stored, StoryEntry incoming)
    {
        var wanted = incoming.Links;
        var wantedIds = wanted.Select(l => l.Id).ToHashSet();

        // Nur aus der Navigationsliste entfernen — der Fremdschlüssel ist pflicht, EF löscht
        // die Waise dadurch von selbst.
        foreach (var obsolete in stored.Links.Where(l => !wantedIds.Contains(l.Id)).ToList())
        {
            stored.Links.Remove(obsolete);
        }

        for (var index = 0; index < wanted.Count; index++)
        {
            var link = wanted[index];
            var target = stored.Links.FirstOrDefault(l => l.Id == link.Id);

            if (target is null)
            {
                // Ausdrücklich über das DbSet — siehe EF-Fallstrick bei Kind-Sammlungen.
                db.StoryLinks.Add(new StoryLink
                {
                    Id = link.Id,
                    StoryEntryId = stored.Id,
                    TargetEntryId = link.TargetEntryId,
                    Label = Normalize(link.Label),
                    SortOrder = index
                });
            }
            else
            {
                target.TargetEntryId = link.TargetEntryId;
                target.Label = Normalize(link.Label);
                target.SortOrder = index;
            }
        }
    }

    private static void SyncParticipants(
        GameDevManagerDbContext db, StoryEntry stored, StoryEntry incoming, List<Guid> removedIds)
    {
        var wanted = incoming.Participants;
        var wantedIds = wanted.Select(p => p.Id).ToHashSet();

        // Nur aus der Navigationsliste entfernen — der Fremdschlüssel ist pflicht, EF löscht
        // die Waise dadurch von selbst.
        foreach (var obsolete in stored.Participants.Where(p => !wantedIds.Contains(p.Id)).ToList())
        {
            stored.Participants.Remove(obsolete);
            removedIds.Add(obsolete.Id);
        }

        for (var index = 0; index < wanted.Count; index++)
        {
            var participant = wanted[index];
            var target = stored.Participants.FirstOrDefault(p => p.Id == participant.Id);

            if (target is null)
            {
                // Ausdrücklich über das DbSet — siehe EF-Fallstrick bei Kind-Sammlungen.
                db.StoryParticipants.Add(new StoryParticipant
                {
                    Id = participant.Id,
                    StoryEntryId = stored.Id,
                    TargetModuleKey = participant.TargetModuleKey,
                    TargetEntityId = participant.TargetEntityId,
                    SortOrder = index
                });
            }
            else
            {
                target.TargetModuleKey = participant.TargetModuleKey;
                target.TargetEntityId = participant.TargetEntityId;
                target.SortOrder = index;
            }
        }
    }

    /// <summary>Löscht einen Abschnitt mit Beteiligten, Feldwerten, individuellen Feldern und Sprites.</summary>
    public async Task DeleteEntryAsync(Guid entryId, CancellationToken ct = default)
    {
        await assets.DeleteForOwnerAsync(entryId, ct);

        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var participantIds = await db.StoryParticipants
            .Where(p => p.StoryEntryId == entryId)
            .Select(p => p.Id)
            .ToListAsync(ct);

        await ChangeLog.RecordDeletionAsync(db, db.StoryEntries, entryId, ct);
        await EntityCleanup.DeleteForEntitiesAsync(db, [entryId, .. participantIds], ct);

        // Verknüpfungen anderer Abschnitte hierher hängen ohne Fremdschlüssel daran und
        // blieben sonst als Waisen zurück.
        await db.StoryLinks
            .Where(l => l.TargetEntryId == entryId)
            .ExecuteDeleteAsync(ct);

        // Die Beteiligten und eigenen Verknüpfungen fallen über den Fremdschlüssel mit.
        await db.StoryEntries
            .Where(s => s.Id == entryId)
            .ExecuteDeleteAsync(ct);

        await transaction.CommitAsync(ct);
    }
}
