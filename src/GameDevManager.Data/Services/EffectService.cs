using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace GameDevManager.Data.Services;

/// <summary>
/// Lesen und Schreiben der Effekte samt Item-Zuweisungen und benutzerdefinierten Feldwerten.
/// </summary>
public class EffectService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    ContentTypeService contentTypes,
    AssetService assets,
    IStringLocalizer<DataMessages> messages)
{
    public async Task<List<EffectListRow>> GetEffectsAsync(Guid projectId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.GameEffects
            .AsNoTracking()
            .Where(e => e.GameProjectId == projectId)
            .OrderBy(e => e.Name)
            .Select(e => new EffectListRow(
                e.Id,
                e.Name,
                e.Description,
                e.Assignments.Count,
                e.ContentTypeId,
                e.ContentType!.Name,
                e.UpdatedAtUtc,
                db.Assets
                    .Where(a => a.OwnerEntityId == e.Id && a.IsPrimary)
                    .Select(a => (Guid?)a.Id)
                    .FirstOrDefault()))
            .ToListAsync(ct);
    }

    /// <summary>Die Effekte eines Items — für die Item-Maske.</summary>
    public async Task<List<EntitySummary>> GetEffectsForItemAsync(
        Guid projectId, Guid itemId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.EffectAssignments
            .AsNoTracking()
            .Where(a => a.ItemId == itemId && a.GameEffect!.GameProjectId == projectId)
            .OrderBy(a => a.GameEffect!.Name)
            .Select(a => new EntitySummary(
                a.GameEffectId, ModuleKeys.Effects, a.GameEffect!.Name, a.GameEffect.ContentType!.Name))
            .ToListAsync(ct);
    }

    public async Task<ContentEditContext<GameEffect>?> LoadForEditAsync(
        Guid projectId, Guid? effectId, CancellationToken ct = default)
    {
        var types = await contentTypes.GetTypesAsync(projectId, ModuleKeys.Effects, ct);

        if (effectId is null)
        {
            return new ContentEditContext<GameEffect>
            {
                Entity = new GameEffect { GameProjectId = projectId, Name = string.Empty },
                IsNew = true,
                AvailableTypes = types,
                IndividualFields = [],
                Values = []
            };
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        var effect = await db.GameEffects
            .AsNoTracking()
            .Include(e => e.Assignments)
            .FirstOrDefaultAsync(e => e.Id == effectId && e.GameProjectId == projectId, ct);

        if (effect is null)
        {
            return null;
        }

        effect.Assignments = [.. effect.Assignments.OrderBy(a => a.SortOrder)];

        return new ContentEditContext<GameEffect>
        {
            Entity = effect,
            IsNew = false,
            AvailableTypes = types,
            IndividualFields = await ContentFields.LoadIndividualFieldsAsync(db, effect.Id, ct),
            Values = await ContentFields.LoadValuesAsync(db, effect.Id, ct)
        };
    }

    public async Task SaveEffectAsync(ContentEditContext<GameEffect> context, CancellationToken ct = default)
    {
        var effect = context.Entity;

        if (string.IsNullOrWhiteSpace(effect.Name))
        {
            throw new ContentValidationException(messages["EffectNameRequired"]);
        }

        Validate(effect);
        ContentFields.ValidateRequired(context, messages);

        await using var db = await factory.CreateDbContextAsync(ct);

        var now = DateTime.UtcNow;
        var stored = await db.GameEffects
            .Include(e => e.Assignments)
            .FirstOrDefaultAsync(e => e.Id == effect.Id, ct);

        if (stored is null)
        {
            stored = new GameEffect
            {
                Id = effect.Id,
                GameProjectId = effect.GameProjectId,
                Name = effect.Name.Trim(),
                CreatedAtUtc = now
            };

            db.GameEffects.Add(stored);
        }

        stored.ContentTypeId = effect.ContentTypeId;
        stored.Name = effect.Name.Trim();
        stored.Description = string.IsNullOrWhiteSpace(effect.Description) ? null : effect.Description.Trim();
        stored.UpdatedAtUtc = now;

        var removedAssignmentIds = new List<Guid>();
        SyncAssignments(db, stored, effect, removedAssignmentIds);

        await EntityCleanup.DeleteForEntitiesAsync(db, removedAssignmentIds, ct);

        await ContentFields.StageValuesAsync(db, context, ct);
        await db.SaveChangesAsync(ct);

        effect.CreatedAtUtc = stored.CreatedAtUtc;
        effect.UpdatedAtUtc = stored.UpdatedAtUtc;
        effect.Name = stored.Name;
        effect.Description = stored.Description;
    }

    private void Validate(GameEffect effect)
    {
        // Eine Zuweisung ohne Item ist eine unfertige Eingabezeile; die Maske räumt sie vorher weg.
        if (effect.Assignments.Any(a => a.ItemId == Guid.Empty))
        {
            throw new ContentValidationException(messages["EffectAssignmentItemRequired"]);
        }

        var duplicate = effect.Assignments
            .GroupBy(a => a.ItemId)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw new ContentValidationException(messages["EffectAssignmentDuplicate"]);
        }
    }

    private static void SyncAssignments(
        GameDevManagerDbContext db, GameEffect stored, GameEffect incoming, List<Guid> removedIds)
    {
        var wanted = incoming.Assignments;
        var wantedIds = wanted.Select(a => a.Id).ToHashSet();

        // Nur aus der Navigationsliste entfernen — der Fremdschlüssel ist pflicht, EF löscht
        // die Waise dadurch von selbst.
        foreach (var obsolete in stored.Assignments.Where(a => !wantedIds.Contains(a.Id)).ToList())
        {
            stored.Assignments.Remove(obsolete);
            removedIds.Add(obsolete.Id);
        }

        for (var index = 0; index < wanted.Count; index++)
        {
            var assignment = wanted[index];
            var target = stored.Assignments.FirstOrDefault(a => a.Id == assignment.Id);

            if (target is null)
            {
                // Ausdrücklich über das DbSet — siehe EF-Fallstrick bei Kind-Sammlungen.
                db.EffectAssignments.Add(new EffectAssignment
                {
                    Id = assignment.Id,
                    GameEffectId = stored.Id,
                    ItemId = assignment.ItemId,
                    SortOrder = index
                });
            }
            else
            {
                target.ItemId = assignment.ItemId;
                target.SortOrder = index;
            }
        }
    }

    /// <summary>Löscht einen Effekt mit Zuweisungen, Feldwerten, individuellen Feldern und Sprites.</summary>
    public async Task DeleteEffectAsync(Guid effectId, CancellationToken ct = default)
    {
        await assets.DeleteForOwnerAsync(effectId, ct);

        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var assignmentIds = await db.EffectAssignments
            .Where(a => a.GameEffectId == effectId)
            .Select(a => a.Id)
            .ToListAsync(ct);

        await EntityCleanup.DeleteForEntitiesAsync(db, [effectId, .. assignmentIds], ct);

        // Die Zuweisungen fallen über den Fremdschlüssel mit.
        await db.GameEffects
            .Where(e => e.Id == effectId)
            .ExecuteDeleteAsync(ct);

        await transaction.CommitAsync(ct);
    }
}
