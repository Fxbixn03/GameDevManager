using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GameDevManager.Data.Services;

/// <summary>
/// Die Arbeit mit benutzerdefinierten Feldern, die in jedem Modul gleich abläuft: laden,
/// prüfen und speichern der Werte einer Entität.
/// <para>
/// Bewusst statische Hilfen statt einer Basisklasse — die Modul-Services rufen sie innerhalb
/// ihres eigenen DbContexts auf, sodass Stammdaten und Feldwerte in einem einzigen
/// <c>SaveChanges</c> landen.
/// </para>
/// </summary>
public static class ContentFields
{
    /// <summary>Die individuellen Felder einer Entität, fertig sortiert und mit Auswahlmöglichkeiten.</summary>
    public static async Task<List<FieldDefinition>> LoadIndividualFieldsAsync(
        GameDevManagerDbContext db, Guid entityId, CancellationToken ct)
    {
        var fields = await db.FieldDefinitions
            .AsNoTracking()
            .Where(f => f.OwnerEntityId == entityId)
            .Include(f => f.Options)
            .ToListAsync(ct);

        foreach (var field in fields)
        {
            field.Options = [.. field.Options.OrderBy(o => o.SortOrder).ThenBy(o => o.Label)];
        }

        return [.. fields.OrderBy(f => f.SortOrder).ThenBy(f => f.Name)];
    }

    /// <summary>Die erfassten Werte einer Entität, nach Felddefinition abgelegt.</summary>
    public static async Task<Dictionary<Guid, FieldValue>> LoadValuesAsync(
        GameDevManagerDbContext db, Guid entityId, CancellationToken ct)
    {
        var values = await db.FieldValues
            .AsNoTracking()
            .Where(v => v.OwnerEntityId == entityId)
            .ToListAsync(ct);

        return values.ToDictionary(v => v.FieldDefinitionId);
    }

    /// <summary>Wirft, sobald ein Pflichtfeld leer geblieben ist.</summary>
    public static void ValidateRequired<TEntity>(ContentEditContext<TEntity> context)
        where TEntity : ContentEntity
    {
        foreach (var field in context.ApplicableFields.Where(f => f.IsRequired))
        {
            if (context.ValueFor(field).IsEmpty)
            {
                throw new ContentValidationException($"Das Pflichtfeld „{field.Name}“ ist nicht gefüllt.");
            }
        }
    }

    /// <summary>
    /// Trägt die Werte der Maske in den DbContext ein — ohne zu speichern, damit der Aufrufer
    /// sie zusammen mit seinen Stammdaten schreibt.
    /// <para>
    /// Werte von Feldern, die nach einem Artwechsel nicht mehr gelten, werden entfernt. Sonst
    /// bliebe unsichtbarer Inhalt in der Datenbank stehen und tauchte in Exporten und der
    /// Referenzansicht wieder auf.
    /// </para>
    /// </summary>
    public static async Task StageValuesAsync<TEntity>(
        GameDevManagerDbContext db, ContentEditContext<TEntity> context, CancellationToken ct)
        where TEntity : ContentEntity
    {
        var entity = context.Entity;
        var applicable = context.ApplicableFields.ToDictionary(f => f.Id);

        var existingValues = await db.FieldValues
            .Where(v => v.OwnerEntityId == entity.Id)
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
                OwnerEntityId = entity.Id,
                OwnerModuleKey = entity.ModuleKey
            };

            CopyValues(edited, created);
            db.FieldValues.Add(created);
        }
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
