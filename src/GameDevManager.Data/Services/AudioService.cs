using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace GameDevManager.Data.Services;

/// <summary>
/// Lesen und Schreiben der Sounds samt benutzerdefinierten Feldwerten. Die Audiodateien
/// selbst liegen als Assets an der Entität.
/// </summary>
public class AudioService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    ContentTypeService contentTypes,
    AssetService assets,
    IStringLocalizer<DataMessages> messages)
{
    public async Task<List<SoundEffectListRow>> GetSoundsAsync(Guid projectId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.SoundEffects
            .AsNoTracking()
            .Where(s => s.GameProjectId == projectId)
            .OrderBy(s => s.Name)
            .Select(s => new SoundEffectListRow(
                s.Id,
                s.Name,
                s.Description,
                db.Assets.Count(a => a.OwnerEntityId == s.Id && a.MimeType.StartsWith("audio/")),
                s.ContentTypeId,
                s.ContentType!.Name,
                s.UpdatedAtUtc,
                db.Assets
                    .Where(a => a.OwnerEntityId == s.Id && a.IsPrimary)
                    .Select(a => (Guid?)a.Id)
                    .FirstOrDefault()))
            .ToListAsync(ct);
    }

    /// <summary>Die Audiodateien eines Sounds — für den Player in der Maske.</summary>
    public async Task<List<Asset>> GetAudioAssetsAsync(Guid ownerEntityId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.Assets
            .AsNoTracking()
            .Where(a => a.OwnerEntityId == ownerEntityId && a.MimeType.StartsWith("audio/"))
            .OrderBy(a => a.SortOrder)
            .ToListAsync(ct);
    }

    public async Task<ContentEditContext<SoundEffect>?> LoadForEditAsync(
        Guid projectId, Guid? soundId, CancellationToken ct = default)
    {
        var types = await contentTypes.GetTypesAsync(projectId, ModuleKeys.Audio, ct);

        if (soundId is null)
        {
            return new ContentEditContext<SoundEffect>
            {
                Entity = new SoundEffect { GameProjectId = projectId, Name = string.Empty },
                IsNew = true,
                AvailableTypes = types,
                IndividualFields = [],
                Values = []
            };
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        var sound = await db.SoundEffects
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == soundId && s.GameProjectId == projectId, ct);

        if (sound is null)
        {
            return null;
        }

        return new ContentEditContext<SoundEffect>
        {
            Entity = sound,
            IsNew = false,
            AvailableTypes = types,
            IndividualFields = await ContentFields.LoadIndividualFieldsAsync(db, sound.Id, ct),
            Values = await ContentFields.LoadValuesAsync(db, sound.Id, ct)
        };
    }

    public async Task SaveSoundAsync(ContentEditContext<SoundEffect> context, CancellationToken ct = default)
    {
        var sound = context.Entity;

        if (string.IsNullOrWhiteSpace(sound.Name))
        {
            throw new ContentValidationException(messages["SoundNameRequired"]);
        }

        ContentFields.ValidateRequired(context, messages);

        await using var db = await factory.CreateDbContextAsync(ct);

        var now = DateTime.UtcNow;
        var stored = await db.SoundEffects.FirstOrDefaultAsync(s => s.Id == sound.Id, ct);

        if (stored is null)
        {
            stored = new SoundEffect
            {
                Id = sound.Id,
                GameProjectId = sound.GameProjectId,
                Name = sound.Name.Trim(),
                CreatedAtUtc = now
            };

            db.SoundEffects.Add(stored);
        }

        stored.ContentTypeId = sound.ContentTypeId;
        stored.Name = sound.Name.Trim();
        stored.Description = string.IsNullOrWhiteSpace(sound.Description) ? null : sound.Description.Trim();
        stored.UpdatedAtUtc = now;

        await ContentFields.StageValuesAsync(db, context, messages, ct);
        await db.SaveChangesAsync(ct);

        sound.CreatedAtUtc = stored.CreatedAtUtc;
        sound.UpdatedAtUtc = stored.UpdatedAtUtc;
        sound.Name = stored.Name;
        sound.Description = stored.Description;
    }

    /// <summary>Löscht einen Sound mit Feldwerten, individuellen Feldern und Dateien.</summary>
    public async Task DeleteSoundAsync(Guid soundId, CancellationToken ct = default)
    {
        await assets.DeleteForOwnerAsync(soundId, ct);

        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        await ChangeLog.RecordDeletionAsync(db, db.SoundEffects, soundId, ct);
        await EntityCleanup.DeleteForEntityAsync(db, soundId, ct);

        await db.SoundEffects
            .Where(s => s.Id == soundId)
            .ExecuteDeleteAsync(ct);

        await transaction.CommitAsync(ct);
    }
}
