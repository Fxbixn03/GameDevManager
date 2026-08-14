using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace GameDevManager.Data.Services;

/// <summary>
/// Lesen und Schreiben der Achievements samt benutzerdefinierten Feldwerten.
/// </summary>
public class AchievementService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    ContentTypeService contentTypes,
    AssetService assets,
    IStringLocalizer<DataMessages> messages)
{
    public async Task<List<AchievementListRow>> GetAchievementsAsync(
        Guid projectId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.Achievements
            .AsNoTracking()
            .Where(a => a.GameProjectId == projectId)
            .OrderBy(a => a.Name)
            .Select(a => new AchievementListRow(
                a.Id,
                a.Name,
                a.Description,
                a.IsSecret,
                db.ConditionSets.Any(set => set.OwnerId == a.Id && set.Slot == ConditionSlots.Unlock),
                a.ContentTypeId,
                a.ContentType!.Name,
                a.UpdatedAtUtc,
                db.Assets
                    .Where(asset => asset.OwnerEntityId == a.Id && asset.IsPrimary)
                    .Select(asset => (Guid?)asset.Id)
                    .FirstOrDefault()))
            .ToListAsync(ct);
    }

    public async Task<ContentEditContext<Achievement>?> LoadForEditAsync(
        Guid projectId, Guid? achievementId, CancellationToken ct = default)
    {
        var types = await contentTypes.GetTypesAsync(projectId, ModuleKeys.Achievements, ct);

        if (achievementId is null)
        {
            return new ContentEditContext<Achievement>
            {
                Entity = new Achievement { GameProjectId = projectId, Name = string.Empty },
                IsNew = true,
                AvailableTypes = types,
                IndividualFields = [],
                Values = []
            };
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        var achievement = await db.Achievements
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == achievementId && a.GameProjectId == projectId, ct);

        if (achievement is null)
        {
            return null;
        }

        return new ContentEditContext<Achievement>
        {
            Entity = achievement,
            IsNew = false,
            AvailableTypes = types,
            IndividualFields = await ContentFields.LoadIndividualFieldsAsync(db, achievement.Id, ct),
            Values = await ContentFields.LoadValuesAsync(db, achievement.Id, ct)
        };
    }

    public async Task SaveAchievementAsync(
        ContentEditContext<Achievement> context, CancellationToken ct = default)
    {
        var achievement = context.Entity;

        if (string.IsNullOrWhiteSpace(achievement.Name))
        {
            throw new ContentValidationException(messages["AchievementNameRequired"]);
        }

        ContentFields.ValidateRequired(context, messages);

        await using var db = await factory.CreateDbContextAsync(ct);

        var now = DateTime.UtcNow;
        var stored = await db.Achievements.FirstOrDefaultAsync(a => a.Id == achievement.Id, ct);

        if (stored is null)
        {
            stored = new Achievement
            {
                Id = achievement.Id,
                GameProjectId = achievement.GameProjectId,
                Name = achievement.Name.Trim(),
                CreatedAtUtc = now
            };

            db.Achievements.Add(stored);
        }

        stored.ContentTypeId = achievement.ContentTypeId;
        stored.Name = achievement.Name.Trim();
        stored.Description = string.IsNullOrWhiteSpace(achievement.Description)
            ? null
            : achievement.Description.Trim();
        stored.IsSecret = achievement.IsSecret;
        stored.UpdatedAtUtc = now;

        // Der Bearbeitungsstand hängt an der Basis aller Inhalte und wird deshalb hier
        // gesetzt und nicht in jedem Zweig der Fallunterscheidung darüber.
        stored.Status = context.Entity.Status;

        await ContentFields.StageValuesAsync(db, context, messages, ct);
        await db.SaveChangesAsync(ct);

        achievement.CreatedAtUtc = stored.CreatedAtUtc;
        achievement.UpdatedAtUtc = stored.UpdatedAtUtc;
        achievement.Name = stored.Name;
        achievement.Description = stored.Description;
    }

    /// <summary>Löscht ein Achievement mit Feldwerten, individuellen Feldern, Bedingungen und Sprites.</summary>
    public async Task DeleteAchievementAsync(Guid achievementId, CancellationToken ct = default)
    {
        await assets.DeleteForOwnerAsync(achievementId, ct);

        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        await ChangeLog.RecordDeletionAsync(db, db.Achievements, achievementId, ct);
        await EntityCleanup.DeleteForEntityAsync(db, achievementId, ct);

        await db.Achievements
            .Where(a => a.Id == achievementId)
            .ExecuteDeleteAsync(ct);

        await transaction.CommitAsync(ct);
    }
}
