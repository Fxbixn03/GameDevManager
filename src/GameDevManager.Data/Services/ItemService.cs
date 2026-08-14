using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace GameDevManager.Data.Services;

/// <summary>
/// Lesen und Schreiben von Items samt ihrer benutzerdefinierten Feldwerte.
/// Die Feldmechanik selbst steckt in <see cref="ContentFields"/> und ist für alle Module gleich.
/// </summary>
public class ItemService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    ContentTypeService contentTypes,
    AssetService assets,
    IStringLocalizer<DataMessages> messages)
{
    /// <summary>Übersicht aller Items eines Projekts, alphabetisch.</summary>
    public async Task<List<ItemListRow>> GetItemsAsync(Guid projectId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.Items
            .AsNoTracking()
            .Where(i => i.GameProjectId == projectId)
            .OrderBy(i => i.Name)
            .Select(i => new ItemListRow(
                i.Id,
                i.Name,
                i.Description,
                i.ContentTypeId,
                i.ContentType!.Name,
                i.UpdatedAtUtc,
                db.Assets
                    .Where(a => a.OwnerEntityId == i.Id && a.IsPrimary)
                    .Select(a => (Guid?)a.Id)
                    .FirstOrDefault()))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Lädt alles, was die Bearbeitungsmaske braucht. Ohne <paramref name="itemId"/> entsteht
    /// ein neues, noch nicht gespeichertes Item.
    /// </summary>
    public async Task<ContentEditContext<Item>?> LoadForEditAsync(
        Guid projectId, Guid? itemId, CancellationToken ct = default)
    {
        var types = await contentTypes.GetTypesAsync(projectId, ModuleKeys.Items, ct);

        if (itemId is null)
        {
            return new ContentEditContext<Item>
            {
                Entity = new Item { GameProjectId = projectId, Name = string.Empty },
                IsNew = true,
                AvailableTypes = types,
                IndividualFields = [],
                Values = []
            };
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        var item = await db.Items
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == itemId && i.GameProjectId == projectId, ct);

        if (item is null)
        {
            return null;
        }

        return new ContentEditContext<Item>
        {
            Entity = item,
            IsNew = false,
            AvailableTypes = types,
            IndividualFields = await ContentFields.LoadIndividualFieldsAsync(db, item.Id, ct),
            Values = await ContentFields.LoadValuesAsync(db, item.Id, ct)
        };
    }

    /// <summary>Speichert Stammdaten und Feldwerte in einem Zug.</summary>
    public async Task SaveItemAsync(ContentEditContext<Item> context, CancellationToken ct = default)
    {
        var item = context.Entity;

        if (string.IsNullOrWhiteSpace(item.Name))
        {
            throw new ContentValidationException(messages["ItemNameRequired"]);
        }

        ContentFields.ValidateRequired(context, messages);

        await using var db = await factory.CreateDbContextAsync(ct);

        var now = DateTime.UtcNow;
        var stored = await db.Items.FirstOrDefaultAsync(i => i.Id == item.Id, ct);

        if (stored is null)
        {
            stored = new Item
            {
                Id = item.Id,
                GameProjectId = item.GameProjectId,
                ContentTypeId = item.ContentTypeId,
                Name = item.Name.Trim(),
                Description = Normalize(item.Description),
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            db.Items.Add(stored);
        }
        else
        {
            stored.ContentTypeId = item.ContentTypeId;
            stored.Name = item.Name.Trim();
            stored.Description = Normalize(item.Description);
            stored.UpdatedAtUtc = now;
        }

        await ContentFields.StageValuesAsync(db, context, messages, ct);
        await db.SaveChangesAsync(ct);

        // Die Maske zeigt anschließend den gespeicherten Stand.
        item.CreatedAtUtc = stored.CreatedAtUtc;
        item.UpdatedAtUtc = stored.UpdatedAtUtc;
        item.Name = stored.Name;
        item.Description = stored.Description;
    }

    /// <summary>Löscht ein Item mit seinen Werten, individuellen Feldern und Sprites.</summary>
    public async Task DeleteItemAsync(Guid itemId, CancellationToken ct = default)
    {
        // Zuerst die Assets: dabei werden auch Dateien entfernt, und das lässt sich nicht
        // zurückrollen — es soll also passieren, bevor der Rest angefasst wird.
        await assets.DeleteForOwnerAsync(itemId, ct);

        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        await ChangeLog.RecordDeletionAsync(db, db.Items, itemId, ct);
        await EntityCleanup.DeleteForEntityAsync(db, itemId, ct);

        await db.Items
            .Where(i => i.Id == itemId)
            .ExecuteDeleteAsync(ct);

        await transaction.CommitAsync(ct);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
