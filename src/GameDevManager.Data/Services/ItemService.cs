using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GameDevManager.Data.Services;

/// <summary>
/// Lesen und Schreiben von Items samt ihrer benutzerdefinierten Feldwerte.
/// </summary>
public class ItemService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    ContentTypeService contentTypes)
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
                i.UpdatedAtUtc))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Lädt alles, was die Bearbeitungsmaske braucht. Ohne <paramref name="itemId"/> entsteht
    /// ein neues, noch nicht gespeichertes Item.
    /// </summary>
    public async Task<ItemEditContext?> LoadForEditAsync(
        Guid projectId, Guid? itemId, CancellationToken ct = default)
    {
        var types = await contentTypes.GetTypesAsync(projectId, ModuleKeys.Items, ct);

        if (itemId is null)
        {
            return new ItemEditContext
            {
                Item = new Item { GameProjectId = projectId, Name = string.Empty },
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

        var individualFields = await db.FieldDefinitions
            .AsNoTracking()
            .Where(f => f.OwnerEntityId == item.Id)
            .Include(f => f.Options)
            .ToListAsync(ct);

        foreach (var field in individualFields)
        {
            field.Options = [.. field.Options.OrderBy(o => o.SortOrder).ThenBy(o => o.Label)];
        }

        var values = await db.FieldValues
            .AsNoTracking()
            .Where(v => v.OwnerEntityId == item.Id)
            .ToListAsync(ct);

        return new ItemEditContext
        {
            Item = item,
            IsNew = false,
            AvailableTypes = types,
            IndividualFields = [.. individualFields.OrderBy(f => f.SortOrder).ThenBy(f => f.Name)],
            Values = values.ToDictionary(v => v.FieldDefinitionId)
        };
    }

    /// <summary>
    /// Speichert Stammdaten und Feldwerte in einem Zug. Werte von Feldern, die nach einem
    /// Artwechsel nicht mehr gelten, werden entfernt — sonst bliebe unsichtbarer Inhalt in der
    /// Datenbank stehen und würde in Exporten und der Referenzansicht wieder auftauchen.
    /// </summary>
    public async Task SaveItemAsync(ItemEditContext context, CancellationToken ct = default)
    {
        var item = context.Item;

        if (string.IsNullOrWhiteSpace(item.Name))
        {
            throw new ContentValidationException("Das Item braucht einen Namen.");
        }

        var applicable = context.TypeFields
            .Concat(context.IndividualFields)
            .ToDictionary(f => f.Id);

        foreach (var field in applicable.Values.Where(f => f.IsRequired))
        {
            if (context.ValueFor(field).IsEmpty)
            {
                throw new ContentValidationException($"Das Pflichtfeld „{field.Name}“ ist nicht gefüllt.");
            }
        }

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
                Description = string.IsNullOrWhiteSpace(item.Description) ? null : item.Description.Trim(),
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            db.Items.Add(stored);
        }
        else
        {
            stored.ContentTypeId = item.ContentTypeId;
            stored.Name = item.Name.Trim();
            stored.Description = string.IsNullOrWhiteSpace(item.Description) ? null : item.Description.Trim();
            stored.UpdatedAtUtc = now;
        }

        var existingValues = await db.FieldValues
            .Where(v => v.OwnerEntityId == item.Id)
            .ToListAsync(ct);

        foreach (var existing in existingValues)
        {
            if (!applicable.TryGetValue(existing.FieldDefinitionId, out var field))
            {
                db.FieldValues.Remove(existing);
                continue;
            }

            var edited = context.ValueFor(field);
            if (edited.IsEmpty)
            {
                db.FieldValues.Remove(existing);
                continue;
            }

            CopyValues(edited, existing);
        }

        var alreadyStored = existingValues.Select(v => v.FieldDefinitionId).ToHashSet();

        foreach (var (fieldId, field) in applicable)
        {
            if (alreadyStored.Contains(fieldId))
            {
                continue;
            }

            var edited = context.ValueFor(field);
            if (edited.IsEmpty)
            {
                continue;
            }

            var created = new FieldValue
            {
                Id = edited.Id,
                FieldDefinitionId = fieldId,
                OwnerEntityId = item.Id,
                OwnerModuleKey = ModuleKeys.Items
            };

            CopyValues(edited, created);
            db.FieldValues.Add(created);
        }

        await db.SaveChangesAsync(ct);

        // Die Maske zeigt anschließend den gespeicherten Stand.
        item.CreatedAtUtc = stored.CreatedAtUtc;
        item.UpdatedAtUtc = stored.UpdatedAtUtc;
        item.Name = stored.Name;
        item.Description = stored.Description;
    }

    /// <summary>Löscht ein Item mit seinen Werten und seinen individuellen Feldern.</summary>
    public async Task DeleteItemAsync(Guid itemId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        // Zuerst die individuellen Felder — deren Werte fallen über den Fremdschlüssel mit.
        await db.FieldDefinitions
            .Where(f => f.OwnerEntityId == itemId)
            .ExecuteDeleteAsync(ct);

        // Danach die Werte der Art-Felder, die ohne Fremdschlüssel am Item hängen.
        await db.FieldValues
            .Where(v => v.OwnerEntityId == itemId)
            .ExecuteDeleteAsync(ct);

        await db.Items
            .Where(i => i.Id == itemId)
            .ExecuteDeleteAsync(ct);

        await transaction.CommitAsync(ct);
    }

    /// <summary>Überträgt die Wertspalten, ohne Id und Zuordnung anzufassen.</summary>
    private static void CopyValues(FieldValue source, FieldValue target)
    {
        target.TextValue = string.IsNullOrWhiteSpace(source.TextValue) ? null : source.TextValue.Trim();
        target.NumberValue = source.NumberValue;
        target.BooleanValue = source.BooleanValue;
        target.DateValue = source.DateValue;
        target.ReferenceValue = source.ReferenceValue;
        target.OptionId = source.OptionId;
    }
}
