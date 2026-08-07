using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GameDevManager.Data.Services;

/// <summary>
/// Lesen und Schreiben der Quests samt ihrer Verknüpfungen zu Story, NPCs und Dialogen
/// und der benutzerdefinierten Feldwerte.
/// </summary>
public class QuestService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    ContentTypeService contentTypes,
    AssetService assets)
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
    /// </summary>
    public async Task<List<EntitySummary>> FindQuestsWithoutCompletionAsync(
        Guid projectId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.Quests
            .AsNoTracking()
            .Where(q => q.GameProjectId == projectId
                && !db.ConditionSets.Any(set =>
                    set.OwnerId == q.Id && set.Slot == ConditionSlots.Completion))
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
            .FirstOrDefaultAsync(q => q.Id == questId && q.GameProjectId == projectId, ct);

        if (quest is null)
        {
            return null;
        }

        return new ContentEditContext<Quest>
        {
            Entity = quest,
            IsNew = false,
            AvailableTypes = types,
            IndividualFields = await ContentFields.LoadIndividualFieldsAsync(db, quest.Id, ct),
            Values = await ContentFields.LoadValuesAsync(db, quest.Id, ct)
        };
    }

    public async Task SaveQuestAsync(ContentEditContext<Quest> context, CancellationToken ct = default)
    {
        var quest = context.Entity;

        if (string.IsNullOrWhiteSpace(quest.Name))
        {
            throw new ContentValidationException("Die Quest braucht einen Namen.");
        }

        ContentFields.ValidateRequired(context);

        await using var db = await factory.CreateDbContextAsync(ct);

        var now = DateTime.UtcNow;
        var stored = await db.Quests.FirstOrDefaultAsync(q => q.Id == quest.Id, ct);

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

        await ContentFields.StageValuesAsync(db, context, ct);
        await db.SaveChangesAsync(ct);

        quest.CreatedAtUtc = stored.CreatedAtUtc;
        quest.UpdatedAtUtc = stored.UpdatedAtUtc;
        quest.Name = stored.Name;
        quest.Description = stored.Description;
    }

    /// <summary>Löscht eine Quest mit Feldwerten, individuellen Feldern, Bedingungen und Sprites.</summary>
    public async Task DeleteQuestAsync(Guid questId, CancellationToken ct = default)
    {
        await assets.DeleteForOwnerAsync(questId, ct);

        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        await EntityCleanup.DeleteForEntityAsync(db, questId, ct);

        await db.Quests
            .Where(q => q.Id == questId)
            .ExecuteDeleteAsync(ct);

        await transaction.CommitAsync(ct);
    }

    /// <summary>Anzeigename einer Quest-Form — an einer Stelle, damit alle Ansichten gleich sprechen.</summary>
    public static string KindLabel(QuestKind kind) => kind switch
    {
        QuestKind.MainMission => "Hauptmission",
        QuestKind.SideMission => "Nebenmission",
        QuestKind.Event => "Event",
        _ => kind.ToString()
    };
}
