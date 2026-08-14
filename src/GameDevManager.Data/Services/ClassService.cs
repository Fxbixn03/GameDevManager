using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace GameDevManager.Data.Services;

/// <summary>
/// Lesen und Schreiben der Klassen samt benutzerdefinierten Feldwerten — und die Frage,
/// wer eine Klasse trägt (NPCs und Spielerfiguren).
/// </summary>
public class ClassService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    ContentTypeService contentTypes,
    AssetService assets,
    IStringLocalizer<DataMessages> messages)
{
    public async Task<List<ClassListRow>> GetClassesAsync(Guid projectId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.CharacterClasses
            .AsNoTracking()
            .Where(c => c.GameProjectId == projectId)
            .OrderBy(c => c.Name)
            .Select(c => new ClassListRow(
                c.Id,
                c.Name,
                c.Description,
                c.ContentTypeId,
                c.ContentType!.Name,
                db.Npcs.Count(n => n.CharacterClassId == c.Id),
                db.PlayerCharacters.Count(p => p.CharacterClassId == c.Id),
                c.UpdatedAtUtc,
                db.Assets
                    .Where(a => a.OwnerEntityId == c.Id && a.IsPrimary)
                    .Select(a => (Guid?)a.Id)
                    .FirstOrDefault()))
            .ToListAsync(ct);
    }

    /// <summary>Wer die Klasse trägt — NPCs und Spielerfiguren, für die Klassen-Maske.</summary>
    public async Task<ClassUsage> GetUsageAsync(Guid classId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var npcs = await db.Npcs
            .AsNoTracking()
            .Where(n => n.CharacterClassId == classId)
            .OrderBy(n => n.Name)
            .Select(n => new EntitySummary(n.Id, ModuleKeys.Npcs, n.Name, null))
            .ToListAsync(ct);

        var characters = await db.PlayerCharacters
            .AsNoTracking()
            .Where(p => p.CharacterClassId == classId)
            .OrderBy(p => p.Name)
            .Select(p => new EntitySummary(p.Id, ModuleKeys.Player, p.Name, null))
            .ToListAsync(ct);

        return new ClassUsage(npcs, characters);
    }

    public async Task<ContentEditContext<CharacterClass>?> LoadForEditAsync(
        Guid projectId, Guid? classId, CancellationToken ct = default)
    {
        var types = await contentTypes.GetTypesAsync(projectId, ModuleKeys.Classes, ct);

        if (classId is null)
        {
            return new ContentEditContext<CharacterClass>
            {
                Entity = new CharacterClass { GameProjectId = projectId, Name = string.Empty },
                IsNew = true,
                AvailableTypes = types,
                IndividualFields = [],
                Values = []
            };
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        var characterClass = await db.CharacterClasses
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == classId && c.GameProjectId == projectId, ct);

        if (characterClass is null)
        {
            return null;
        }

        return new ContentEditContext<CharacterClass>
        {
            Entity = characterClass,
            IsNew = false,
            AvailableTypes = types,
            IndividualFields = await ContentFields.LoadIndividualFieldsAsync(db, characterClass.Id, ct),
            Values = await ContentFields.LoadValuesAsync(db, characterClass.Id, ct)
        };
    }

    public async Task SaveClassAsync(
        ContentEditContext<CharacterClass> context, CancellationToken ct = default)
    {
        var characterClass = context.Entity;

        if (string.IsNullOrWhiteSpace(characterClass.Name))
        {
            throw new ContentValidationException(messages["ClassNameRequired"]);
        }

        ContentFields.ValidateRequired(context, messages);

        await using var db = await factory.CreateDbContextAsync(ct);

        var now = DateTime.UtcNow;
        var stored = await db.CharacterClasses.FirstOrDefaultAsync(c => c.Id == characterClass.Id, ct);

        if (stored is null)
        {
            stored = new CharacterClass
            {
                Id = characterClass.Id,
                GameProjectId = characterClass.GameProjectId,
                Name = characterClass.Name.Trim(),
                CreatedAtUtc = now
            };

            db.CharacterClasses.Add(stored);
        }

        stored.ContentTypeId = characterClass.ContentTypeId;
        stored.Name = characterClass.Name.Trim();
        stored.Description = string.IsNullOrWhiteSpace(characterClass.Description)
            ? null
            : characterClass.Description.Trim();
        stored.UpdatedAtUtc = now;

        // Der Bearbeitungsstand hängt an der Basis aller Inhalte und wird deshalb hier
        // gesetzt und nicht in jedem Zweig der Fallunterscheidung darüber.
        stored.Status = context.Entity.Status;

        await ContentFields.StageValuesAsync(db, context, messages, ct);
        await db.SaveChangesAsync(ct);

        characterClass.CreatedAtUtc = stored.CreatedAtUtc;
        characterClass.UpdatedAtUtc = stored.UpdatedAtUtc;
        characterClass.Name = stored.Name;
        characterClass.Description = stored.Description;
    }

    /// <summary>Löscht eine Klasse. Wer sie trägt, wird klassenlos statt auf eine Leiche zu zeigen.</summary>
    public async Task DeleteClassAsync(Guid classId, CancellationToken ct = default)
    {
        await assets.DeleteForOwnerAsync(classId, ct);

        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        await ChangeLog.RecordDeletionAsync(db, db.CharacterClasses, classId, ct);
        await EntityCleanup.DeleteForEntityAsync(db, classId, ct);

        // Keine Fremdschlüssel über die Modulgrenze — die Verweise müssen von Hand gelöst werden.
        await db.Npcs
            .Where(n => n.CharacterClassId == classId)
            .ExecuteUpdateAsync(update => update.SetProperty(n => n.CharacterClassId, (Guid?)null), ct);

        await db.PlayerCharacters
            .Where(p => p.CharacterClassId == classId)
            .ExecuteUpdateAsync(update => update.SetProperty(p => p.CharacterClassId, (Guid?)null), ct);

        await db.CharacterClasses
            .Where(c => c.Id == classId)
            .ExecuteDeleteAsync(ct);

        await transaction.CommitAsync(ct);
    }
}
