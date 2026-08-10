using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace GameDevManager.Data.Services;

/// <summary>
/// Lesen und Schreiben der Sammelobjekte samt benutzerdefinierten Feldwerten.
/// </summary>
public class CollectibleService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    ContentTypeService contentTypes,
    AssetService assets,
    IStringLocalizer<DataMessages> messages)
{
    public async Task<List<CollectibleListRow>> GetCollectiblesAsync(
        Guid projectId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.Collectibles
            .AsNoTracking()
            .Where(c => c.GameProjectId == projectId)
            .OrderBy(c => c.Name)
            .Select(c => new CollectibleListRow(
                c.Id,
                c.Name,
                c.Description,
                db.MapMarkers.Count(marker => marker.TargetEntityId == c.Id),
                c.ContentTypeId,
                c.ContentType!.Name,
                c.UpdatedAtUtc,
                db.Assets
                    .Where(a => a.OwnerEntityId == c.Id && a.IsPrimary)
                    .Select(a => (Guid?)a.Id)
                    .FirstOrDefault()))
            .ToListAsync(ct);
    }

    public async Task<ContentEditContext<Collectible>?> LoadForEditAsync(
        Guid projectId, Guid? collectibleId, CancellationToken ct = default)
    {
        var types = await contentTypes.GetTypesAsync(projectId, ModuleKeys.Collectibles, ct);

        if (collectibleId is null)
        {
            return new ContentEditContext<Collectible>
            {
                Entity = new Collectible { GameProjectId = projectId, Name = string.Empty },
                IsNew = true,
                AvailableTypes = types,
                IndividualFields = [],
                Values = []
            };
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        var collectible = await db.Collectibles
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == collectibleId && c.GameProjectId == projectId, ct);

        if (collectible is null)
        {
            return null;
        }

        return new ContentEditContext<Collectible>
        {
            Entity = collectible,
            IsNew = false,
            AvailableTypes = types,
            IndividualFields = await ContentFields.LoadIndividualFieldsAsync(db, collectible.Id, ct),
            Values = await ContentFields.LoadValuesAsync(db, collectible.Id, ct)
        };
    }

    public async Task SaveCollectibleAsync(
        ContentEditContext<Collectible> context, CancellationToken ct = default)
    {
        var collectible = context.Entity;

        if (string.IsNullOrWhiteSpace(collectible.Name))
        {
            throw new ContentValidationException(messages["CollectibleNameRequired"]);
        }

        ContentFields.ValidateRequired(context, messages);

        await using var db = await factory.CreateDbContextAsync(ct);

        var now = DateTime.UtcNow;
        var stored = await db.Collectibles.FirstOrDefaultAsync(c => c.Id == collectible.Id, ct);

        if (stored is null)
        {
            stored = new Collectible
            {
                Id = collectible.Id,
                GameProjectId = collectible.GameProjectId,
                Name = collectible.Name.Trim(),
                CreatedAtUtc = now
            };

            db.Collectibles.Add(stored);
        }

        stored.ContentTypeId = collectible.ContentTypeId;
        stored.Name = collectible.Name.Trim();
        stored.Description = string.IsNullOrWhiteSpace(collectible.Description)
            ? null
            : collectible.Description.Trim();
        stored.UpdatedAtUtc = now;

        await ContentFields.StageValuesAsync(db, context, ct);
        await db.SaveChangesAsync(ct);

        collectible.CreatedAtUtc = stored.CreatedAtUtc;
        collectible.UpdatedAtUtc = stored.UpdatedAtUtc;
        collectible.Name = stored.Name;
        collectible.Description = stored.Description;
    }

    /// <summary>Löscht ein Sammelobjekt mit Feldwerten, individuellen Feldern und Sprites.</summary>
    public async Task DeleteCollectibleAsync(Guid collectibleId, CancellationToken ct = default)
    {
        await assets.DeleteForOwnerAsync(collectibleId, ct);

        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        await EntityCleanup.DeleteForEntityAsync(db, collectibleId, ct);

        await db.Collectibles
            .Where(c => c.Id == collectibleId)
            .ExecuteDeleteAsync(ct);

        await transaction.CommitAsync(ct);
    }
}
