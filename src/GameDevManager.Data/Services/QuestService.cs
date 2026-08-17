using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace GameDevManager.Data.Services;

/// <summary>
/// Lesen und Schreiben der Quests samt ihrer Verknüpfungen zu Story, NPCs und Dialogen
/// und der benutzerdefinierten Feldwerte.
/// </summary>
public class QuestService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    ContentTypeService contentTypes,
    AssetService assets,
    IStringLocalizer<DataMessages> messages)
{
    public async Task<List<QuestListRow>> GetQuestsAsync(Guid projectId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.Quests
            .AsNoTracking()
            .Where(q => q.GameProjectId == projectId)
            .OrderBy(q => q.Name)
            .Select(q => new QuestListRow(
                q.Id,
                q.Name,
                q.Description,
                q.Kind,
                q.GiverNpcId,
                db.Npcs.Where(n => n.Id == q.GiverNpcId).Select(n => n.Name).FirstOrDefault(),
                q.StoryEntryId,
                db.StoryEntries.Where(s => s.Id == q.StoryEntryId).Select(s => s.Name).FirstOrDefault(),
                q.Objectives.Count,
                q.ContentTypeId,
                q.ContentType!.Name,
                q.UpdatedAtUtc,
                db.Assets
                    .Where(a => a.OwnerEntityId == q.Id && a.IsPrimary)
                    .Select(a => (Guid?)a.Id)
                    .FirstOrDefault()))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Der Health Check „Quests ohne Abschlussbedingung“ aus dem Konzept: Quests, an deren
    /// Abschluss-Slot kein Bedingungssatz hängt. Ohne Abschlussbedingung wüsste das Spiel
    /// nie, wann die Quest erledigt ist.
    /// <para>
    /// Zerfällt eine Quest in Ziele, zählt sie als versorgt, sobald <b>jedes</b> Ziel eine
    /// Abschlussbedingung hat — dann sagt der Verlauf, was die Quest als Ganzes nicht mehr
    /// sagen muss. Ein Ziel ohne Bedingung ist derselbe Fund wie eine Quest ohne: Der Schritt
    /// ließe sich nie abhaken. Optionale Ziele sind dabei nicht ausgenommen — auch ein
    /// Nebenziel muss erfüllbar sein, sonst ist es keines.
    /// </para>
    /// </summary>
    public async Task<List<EntitySummary>> FindQuestsWithoutCompletionAsync(
        Guid projectId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.Quests
            .AsNoTracking()
            .Where(q => q.GameProjectId == projectId
                && (q.Objectives.Count == 0
                    ? !db.ConditionSets.Any(set =>
                        set.OwnerId == q.Id && set.Slot == ConditionSlots.Completion)
                    : q.Objectives.Any(objective => !db.ConditionSets.Any(set =>
                        set.OwnerId == objective.Id && set.Slot == ConditionSlots.Completion))))
            .OrderBy(q => q.Name)
            .Select(q => new EntitySummary(q.Id, ModuleKeys.Quests, q.Name, q.ContentType!.Name))
            .ToListAsync(ct);
    }

    public async Task<ContentEditContext<Quest>?> LoadForEditAsync(
        Guid projectId, Guid? questId, CancellationToken ct = default)
    {
        var types = await contentTypes.GetTypesAsync(projectId, ModuleKeys.Quests, ct);

        if (questId is null)
        {
            return new ContentEditContext<Quest>
            {
                Entity = new Quest { GameProjectId = projectId, Name = string.Empty },
                IsNew = true,
                AvailableTypes = types,
                IndividualFields = [],
                Values = []
            };
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        var quest = await db.Quests
            .AsNoTracking()
            .Include(q => q.Objectives)
            .FirstOrDefaultAsync(q => q.Id == questId && q.GameProjectId == projectId, ct);

        if (quest is null)
        {
            return null;
        }

        quest.Objectives = [.. quest.Objectives.OrderBy(objective => objective.SortOrder)];

        return new ContentEditContext<Quest>
        {
            Entity = quest,
            IsNew = false,
            AvailableTypes = types,
            IndividualFields = await ContentFields.LoadIndividualFieldsAsync(db, quest.Id, ct),
            Values = await ContentFields.LoadValuesAsync<Quest>(db, quest.Id, ct)
        };
    }

    public async Task SaveQuestAsync(ContentEditContext<Quest> context, CancellationToken ct = default)
    {
        var quest = context.Entity;

        if (string.IsNullOrWhiteSpace(quest.Name))
        {
            throw new ContentValidationException(messages["QuestNameRequired"]);
        }

        // Ein Ziel ohne Text ist eine unfertige Eingabezeile; die Maske räumt sie vorher weg.
        if (quest.Objectives.Any(objective => string.IsNullOrWhiteSpace(objective.Text)))
        {
            throw new ContentValidationException(messages["QuestObjectiveTextRequired"]);
        }

        ContentFields.ValidateRequired(context, messages);

        await using var db = await factory.CreateDbContextAsync(ct);

        var now = DateTime.UtcNow;
        var stored = await db.Quests
            .Include(q => q.Objectives)
            .FirstOrDefaultAsync(q => q.Id == quest.Id, ct);

        if (stored is null)
        {
            stored = new Quest
            {
                Id = quest.Id,
                GameProjectId = quest.GameProjectId,
                Name = quest.Name.Trim(),
                CreatedAtUtc = now
            };

            db.Quests.Add(stored);
        }

        stored.ContentTypeId = quest.ContentTypeId;
        stored.Name = quest.Name.Trim();
        stored.Description = string.IsNullOrWhiteSpace(quest.Description) ? null : quest.Description.Trim();
        stored.Kind = quest.Kind;
        stored.GiverNpcId = quest.GiverNpcId;
        stored.StoryEntryId = quest.StoryEntryId;
        stored.DialogueId = quest.DialogueId;
        stored.UpdatedAtUtc = now;

        var removedObjectiveIds = new List<Guid>();
        SyncObjectives(db, stored, quest, removedObjectiveIds);

        // Ziele tragen ihre Abschlussbedingung unter der eigenen GUID — ein entferntes Ziel
        // ließe sie sonst als Waise stehen.
        await EntityCleanup.DeleteForSubObjectsAsync(db, removedObjectiveIds, ct);

        // Der Bearbeitungsstand hängt an der Basis aller Inhalte und wird deshalb hier
        // gesetzt und nicht in jedem Zweig der Fallunterscheidung darüber.
        stored.Status = context.Entity.Status;

        await ContentFields.StageValuesAsync(db, context, messages, ct);
        await db.SaveChangesAsync(ct);

        quest.CreatedAtUtc = stored.CreatedAtUtc;
        quest.UpdatedAtUtc = stored.UpdatedAtUtc;
        quest.Name = stored.Name;
        quest.Description = stored.Description;
    }

    private static void SyncObjectives(
        GameDevManagerDbContext db, Quest stored, Quest incoming, List<Guid> removedIds)
    {
        var wanted = incoming.Objectives;
        var wantedIds = wanted.Select(objective => objective.Id).ToHashSet();

        // Nur aus der Navigationsliste entfernen — der Fremdschlüssel ist pflicht, EF löscht
        // die Waise dadurch von selbst.
        foreach (var obsolete in stored.Objectives.Where(o => !wantedIds.Contains(o.Id)).ToList())
        {
            stored.Objectives.Remove(obsolete);
            removedIds.Add(obsolete.Id);
        }

        for (var index = 0; index < wanted.Count; index++)
        {
            var objective = wanted[index];
            var target = stored.Objectives.FirstOrDefault(o => o.Id == objective.Id);

            if (target is null)
            {
                // Ausdrücklich über das DbSet — siehe EF-Fallstrick bei Kind-Sammlungen.
                db.QuestObjectives.Add(new QuestObjective
                {
                    Id = objective.Id,
                    QuestId = stored.Id,
                    Text = objective.Text.Trim(),
                    IsOptional = objective.IsOptional,
                    SortOrder = index
                });
            }
            else
            {
                target.Text = objective.Text.Trim();
                target.IsOptional = objective.IsOptional;
                target.SortOrder = index;
            }
        }
    }

    /// <summary>Löscht eine Quest mit Zielen, Feldwerten, individuellen Feldern, Bedingungen und Sprites.</summary>
    public async Task DeleteQuestAsync(Guid questId, CancellationToken ct = default)
    {
        await assets.DeleteForOwnerAsync(questId, ct);

        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        // Ziele haben eigene GUIDs und tragen daran ihre Abschlussbedingung.
        var objectiveIds = await db.QuestObjectives
            .Where(objective => objective.QuestId == questId)
            .Select(objective => objective.Id)
            .ToListAsync(ct);

        await ChangeLog.RecordDeletionAsync(db, db.Quests, questId, ct);
        await EntityCleanup.DeleteForEntityAsync(db, db.Quests, questId, objectiveIds, ct);

        // Die Ziele selbst fallen über den Fremdschlüssel mit.
        await db.Quests
            .Where(q => q.Id == questId)
            .ExecuteDeleteAsync(ct);

        await transaction.CommitAsync(ct);
    }

    /// <summary>Anzeigename einer Quest-Form — an einer Stelle, damit alle Ansichten gleich sprechen.</summary>
    public string KindLabel(QuestKind kind) => kind switch
    {
        QuestKind.MainMission => messages["QuestKind_MainMission"],
        QuestKind.SideMission => messages["QuestKind_SideMission"],
        QuestKind.Event => messages["QuestKind_Event"],
        _ => kind.ToString()
    };
}
