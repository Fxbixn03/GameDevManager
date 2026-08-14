using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace GameDevManager.Data.Services;

/// <summary>Was eine Massenänderung bewirkt hat — die Zahl steht anschließend in der Meldung.</summary>
public sealed record BulkEditResult(int Changed, int Skipped)
{
    public int Total => Changed + Skipped;
}

/// <summary>
/// Die Massenbearbeitung: dieselbe Änderung an vielen Entitäten auf einmal — Art zuweisen,
/// Tags vergeben oder entziehen, einen Feldwert setzen oder leeren.
/// <para>
/// Der Zugang zu den Entitäten läuft über die <see cref="IModuleEntitySource"/> und nicht über
/// einen <c>switch</c> je Modul: Ein neues Modul ist damit von selbst dabei — dieselbe
/// Überlegung wie bei Suche, Referenzansicht und Duplizieren.
/// </para>
/// <para>
/// Geändert wird <b>verfolgt</b> und nicht über <c>ExecuteUpdate</c>: Schreibschutz und
/// Änderungsprotokoll hängen am <c>SaveChanges</c>, und eine Massenänderung ist genau die,
/// die man später im Protokoll sucht. Der Preis ist eine Protokollzeile je Entität — richtig
/// so, denn anders als beim Import ist hier jede einzelne Zeile eine bewusste Auswahl.
/// </para>
/// </summary>
public class BulkEditService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    IEnumerable<IModuleEntitySource> sources,
    ContentTypeService contentTypes,
    IStringLocalizer<DataMessages> messages)
{
    /// <summary>
    /// Weist allen gewählten Entitäten dieselbe Art zu. <c>null</c> nimmt die Art weg.
    /// <para>
    /// Feldwerte, die nach dem Wechsel nicht mehr gelten, werden entfernt — dieselbe Regel wie
    /// beim Speichern einer einzelnen Entität (<see cref="ContentFields.StageValuesAsync"/>).
    /// Sonst bliebe unsichtbarer Inhalt stehen und tauchte in Export und Referenzansicht
    /// wieder auf.
    /// </para>
    /// </summary>
    public async Task<BulkEditResult> AssignTypeAsync(
        Guid projectId, string moduleKey, IReadOnlyCollection<Guid> entityIds, Guid? typeId,
        CancellationToken ct = default)
    {
        if (entityIds.Count == 0)
        {
            return new BulkEditResult(0, 0);
        }

        var source = Source(moduleKey);

        await using var db = await factory.CreateDbContextAsync(ct);

        if (typeId is { } wanted)
        {
            var type = await db.ContentTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == wanted && t.GameProjectId == projectId, ct);

            // Eine Art aus einem fremden Modul oder Projekt hätte Felder, die hier niemand
            // pflegen kann — dieselbe Prüfung wie in den Modul-Diensten.
            if (type is null || !string.Equals(type.ModuleKey, moduleKey, StringComparison.Ordinal))
            {
                throw new ContentValidationException(messages["Bulk_TypeInvalid"].Value);
            }
        }

        var entities = await source.LoadForBulkAsync(db, projectId, entityIds, ct);
        var changed = 0;

        foreach (var entity in entities)
        {
            if (entity.ContentTypeId == typeId)
            {
                continue;
            }

            entity.ContentTypeId = typeId;
            entity.UpdatedAtUtc = DateTime.UtcNow;
            changed++;
        }

        if (changed > 0)
        {
            await RemoveStaleValuesAsync(db, projectId, moduleKey, entities, ct);
            await db.SaveChangesAsync(ct);
        }

        return new BulkEditResult(changed, entityIds.Count - changed);
    }

    /// <summary>
    /// Setzt den Bearbeitungsstand aller gewählten Entitäten. Der Stand ist die eine Angabe,
    /// die man fast nie einzeln pflegt — „alle NPCs des Prologs sind fertig“ ist ein Satz und
    /// keine vierzig Klicks.
    /// </summary>
    public async Task<BulkEditResult> SetStatusAsync(
        Guid projectId, string moduleKey, IReadOnlyCollection<Guid> entityIds, ContentStatus status,
        CancellationToken ct = default)
    {
        if (entityIds.Count == 0)
        {
            return new BulkEditResult(0, 0);
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        var entities = await Source(moduleKey).LoadForBulkAsync(db, projectId, entityIds, ct);
        var changed = 0;

        foreach (var entity in entities.Where(entity => entity.Status != status))
        {
            entity.Status = status;
            entity.UpdatedAtUtc = DateTime.UtcNow;
            changed++;
        }

        if (changed > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        return new BulkEditResult(changed, entityIds.Count - changed);
    }

    /// <summary>
    /// Setzt denselben Feldwert an allen gewählten Entitäten. Ein leerer Wert löscht ihn —
    /// „überall zurücksetzen“ ist derselbe Vorgang wie „überall setzen“.
    /// <para>
    /// Übergeben wird ein fertiger <see cref="FieldValue"/> als Vorlage: Die Oberfläche füllt
    /// ihn mit derselben Eingabemaske (<c>DynamicFieldInput</c>), die auch der Einzelfall
    /// benutzt — so gelten für Zahlen, Datumswerte, Referenzen und Stichwortlisten überall
    /// dieselben Regeln.
    /// </para>
    /// </summary>
    public async Task<BulkEditResult> SetFieldValueAsync(
        Guid projectId, string moduleKey, IReadOnlyCollection<Guid> entityIds,
        Guid fieldDefinitionId, FieldValue template, CancellationToken ct = default)
    {
        if (entityIds.Count == 0)
        {
            return new BulkEditResult(0, 0);
        }

        var source = Source(moduleKey);

        await using var db = await factory.CreateDbContextAsync(ct);

        var field = await db.FieldDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == fieldDefinitionId, ct)
            ?? throw new ContentValidationException(messages["Bulk_FieldInvalid"].Value);

        var entities = await source.LoadForBulkAsync(db, projectId, entityIds, ct);
        if (entities.Count == 0)
        {
            return new BulkEditResult(0, entityIds.Count);
        }

        // Kanonisieren vor der Leer-Prüfung: Eine Stichwortliste aus lauter Kommas trägt Text,
        // aber kein Stichwort — dieselbe Reihenfolge wie beim Speichern einer Entität.
        if (field.IsKeywordField)
        {
            template.TextValue = KeywordList.Normalize(template.TextValue);
        }

        var clearing = template.IsEmpty;

        // Grenzen und Muster gelten hier wie beim Speichern einer einzelnen Entität — sonst
        // wäre die Massenbearbeitung der Weg, sie zu umgehen. Geprüft wird einmal, nicht je
        // Entität: Es ist ein Wert für alle.
        if (!clearing)
        {
            ContentFields.ValidateValue(field, template, messages);
        }

        // Welche Felder an welcher Entität überhaupt gelten, hängt an ihrer Art. Ein Feld an
        // einer Entität zu setzen, die es gar nicht führt, erzeugte unsichtbaren Inhalt.
        var applicable = await ApplicableFieldsAsync(db, projectId, moduleKey, entities, ct);

        var ids = entities.Select(entity => entity.Id).ToList();
        var existing = await db.FieldValues
            .Where(value => ids.Contains(value.OwnerEntityId) && value.FieldDefinitionId == fieldDefinitionId)
            .ToDictionaryAsync(value => value.OwnerEntityId, ct);

        var changed = 0;

        foreach (var entity in entities)
        {
            if (!applicable.TryGetValue(entity.Id, out var fields) || !fields.Contains(fieldDefinitionId))
            {
                continue;
            }

            if (existing.TryGetValue(entity.Id, out var stored))
            {
                if (clearing)
                {
                    db.FieldValues.Remove(stored);
                }
                else
                {
                    ContentFields.CopyValues(template, stored);
                }
            }
            else
            {
                if (clearing)
                {
                    continue;
                }

                var created = new FieldValue
                {
                    FieldDefinitionId = fieldDefinitionId,
                    OwnerEntityId = entity.Id,
                    OwnerModuleKey = moduleKey
                };

                ContentFields.CopyValues(template, created);
                db.FieldValues.Add(created);
            }

            // Der Zeitstempel gehört mitgezogen: Das „Weiterarbeiten“ des Dashboards und die
            // Schreibkonflikt-Erkennung lesen ihn, und geändert hat sich die Entität ja.
            entity.UpdatedAtUtc = DateTime.UtcNow;
            changed++;
        }

        if (changed > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        return new BulkEditResult(changed, entityIds.Count - changed);
    }

    /// <summary>
    /// Vergibt ein Tag an alle gewählten Entitäten oder entzieht es ihnen. Bereits vergebene
    /// Tags werden übersprungen statt doppelt eingetragen.
    /// </summary>
    public async Task<BulkEditResult> SetTagAsync(
        Guid projectId, string moduleKey, IReadOnlyCollection<Guid> entityIds, Guid tagId, bool assign,
        CancellationToken ct = default)
    {
        if (entityIds.Count == 0)
        {
            return new BulkEditResult(0, 0);
        }

        var source = Source(moduleKey);

        await using var db = await factory.CreateDbContextAsync(ct);

        var tagExists = await db.ContentTags.AnyAsync(tag => tag.Id == tagId && tag.GameProjectId == projectId, ct);
        if (!tagExists)
        {
            throw new ContentValidationException(messages["Bulk_TagInvalid"].Value);
        }

        var entities = await source.LoadForBulkAsync(db, projectId, entityIds, ct);
        var ids = entities.Select(entity => entity.Id).ToList();

        var assigned = await db.ContentTagAssignments
            .Where(a => a.ContentTagId == tagId && ids.Contains(a.TargetEntityId))
            .ToListAsync(ct);

        var changed = 0;

        if (assign)
        {
            var already = assigned.Select(a => a.TargetEntityId).ToHashSet();

            foreach (var entity in entities.Where(entity => !already.Contains(entity.Id)))
            {
                db.ContentTagAssignments.Add(new ContentTagAssignment
                {
                    ContentTagId = tagId,
                    TargetModuleKey = moduleKey,
                    TargetEntityId = entity.Id
                });

                changed++;
            }
        }
        else
        {
            db.ContentTagAssignments.RemoveRange(assigned);
            changed = assigned.Count;
        }

        if (changed > 0)
        {
            // Über SaveChanges und nicht über ExecuteDelete: So greift der Schreibschutz von
            // selbst, und der Weg ist derselbe wie beim Vergeben.
            await db.SaveChangesAsync(ct);
        }

        return new BulkEditResult(changed, entityIds.Count - changed);
    }

    private IModuleEntitySource Source(string moduleKey) =>
        sources.FirstOrDefault(source => source.ModuleKey == moduleKey)
        ?? throw new ContentValidationException(messages["Bulk_ModuleUnknown", moduleKey].Value);

    /// <summary>
    /// Welche Feldern je Entität gelten: die ihrer Art samt geerbten und ihre individuellen.
    /// </summary>
    private async Task<Dictionary<Guid, HashSet<Guid>>> ApplicableFieldsAsync(
        GameDevManagerDbContext db, Guid projectId, string moduleKey,
        List<ContentEntity> entities, CancellationToken ct)
    {
        var types = await contentTypes.GetTypesAsync(projectId, moduleKey, ct);

        var byType = types.ToDictionary(
            type => type.Id,
            type => type.Fields.Concat(type.InheritedFields).Select(field => field.Id).ToHashSet());

        var ids = entities.Select(entity => entity.Id).ToList();
        var individual = await db.FieldDefinitions
            .AsNoTracking()
            .Where(field => field.OwnerEntityId != null && ids.Contains(field.OwnerEntityId!.Value))
            .Select(field => new { field.Id, OwnerId = field.OwnerEntityId!.Value })
            .ToListAsync(ct);

        var byEntity = individual
            .GroupBy(field => field.OwnerId)
            .ToDictionary(group => group.Key, group => group.Select(field => field.Id).ToHashSet());

        var result = new Dictionary<Guid, HashSet<Guid>>();

        foreach (var entity in entities)
        {
            var fields = entity.ContentTypeId is { } typeId && byType.TryGetValue(typeId, out var ofType)
                ? new HashSet<Guid>(ofType)
                : [];

            if (byEntity.TryGetValue(entity.Id, out var own))
            {
                fields.UnionWith(own);
            }

            result[entity.Id] = fields;
        }

        return result;
    }

    /// <summary>
    /// Entfernt nach einem Artwechsel die Werte, die zur neuen Art nicht mehr gehören.
    /// Gearbeitet wird auf dem <b>neuen</b> Stand der Entitäten — sie tragen ihre neue Art
    /// bereits, gespeichert wird erst danach.
    /// </summary>
    private async Task RemoveStaleValuesAsync(
        GameDevManagerDbContext db, Guid projectId, string moduleKey,
        List<ContentEntity> entities, CancellationToken ct)
    {
        var applicable = await ApplicableFieldsAsync(db, projectId, moduleKey, entities, ct);

        var ids = entities.Select(entity => entity.Id).ToList();
        var values = await db.FieldValues
            .Where(value => ids.Contains(value.OwnerEntityId))
            .ToListAsync(ct);

        foreach (var value in values)
        {
            if (!applicable.TryGetValue(value.OwnerEntityId, out var fields)
                || !fields.Contains(value.FieldDefinitionId))
            {
                db.FieldValues.Remove(value);
            }
        }
    }
}
