using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace GameDevManager.Data.Services;

/// <summary>
/// Verwaltet die benutzerdefinierten Arten und deren Felder. Der Dienst ist modulübergreifend:
/// er bekommt den Modul-Schlüssel übergeben und funktioniert für Items genauso wie später für
/// NPCs, Fraktionen oder Quests.
/// </summary>
public class ContentTypeService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    IEnumerable<IModuleEntitySource> sources,
    IStringLocalizer<DataMessages> messages)
{
    /// <summary>
    /// Alle Arten eines Moduls samt Feldern und Auswahlmöglichkeiten, fertig sortiert.
    /// Unterarten stehen direkt unter ihrer Eltern-Art und tragen deren Felder in
    /// <see cref="ContentType.InheritedFields"/>.
    /// </summary>
    public async Task<List<ContentType>> GetTypesAsync(Guid projectId, string moduleKey, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var types = await db.ContentTypes
            .AsNoTracking()
            .Where(t => t.GameProjectId == projectId && t.ModuleKey == moduleKey)
            .Include(t => t.Fields).ThenInclude(f => f.Options)
            .ToListAsync(ct);

        foreach (var type in types)
        {
            // Individuelle Felder hängen an einer Entität, nicht an der Art — sie dürfen hier
            // nicht auftauchen, auch wenn EF sie über die Beziehung nicht mitlädt.
            type.Fields = [.. type.Fields
                .Where(f => !f.IsIndividual)
                .OrderBy(f => f.SortOrder).ThenBy(f => f.Name)];

            foreach (var field in type.Fields)
            {
                field.Options = [.. field.Options.OrderBy(o => o.SortOrder).ThenBy(o => o.Label)];
            }
        }

        var byId = types.ToDictionary(t => t.Id);

        foreach (var type in types)
        {
            type.InheritedFields = [.. Ancestors(type, byId).Reverse().SelectMany(a => a.Fields)];
        }

        return [.. Hierarchy(types, parentId: null, byId)];
    }

    /// <summary>
    /// Die Arten in der Reihenfolge der Hierarchie: jede Art, direkt gefolgt von ihren
    /// Unterarten. Flach statt verschachtelt, weil alle Aufrufer eine Liste erwarten — die
    /// Auswahlfelder ebenso wie die Arten-Verwaltung, die nur einrückt.
    /// <para>
    /// Eine Art, deren Eltern-Art nicht in der Liste steht (anderes Modul, aus einer kaputten
    /// Einspielung), gilt als oberste Ebene und fällt dadurch nicht unter den Tisch.
    /// </para>
    /// </summary>
    private static IEnumerable<ContentType> Hierarchy(
        List<ContentType> types, Guid? parentId, Dictionary<Guid, ContentType> byId)
    {
        var level = types
            .Where(t => (t.ParentId is { } id && byId.ContainsKey(id) ? id : null) == parentId)
            .OrderBy(t => t.SortOrder)
            .ThenBy(t => t.Name);

        foreach (var type in level)
        {
            yield return type;

            foreach (var child in Hierarchy(types, type.Id, byId))
            {
                yield return child;
            }
        }
    }

    /// <summary>
    /// Die Eltern-Arten von unten nach oben. Bricht ab, sobald eine Art zum zweiten Mal
    /// vorkommt: Ein Ring entsteht beim Speichern zwar nicht, aber ein eingespielter Stand aus
    /// einer fremden Quelle darf das Laden nicht zum Hängen bringen.
    /// </summary>
    private static IEnumerable<ContentType> Ancestors(ContentType type, Dictionary<Guid, ContentType> byId)
    {
        var seen = new HashSet<Guid> { type.Id };
        var current = type;

        while (current.ParentId is { } parentId
               && byId.TryGetValue(parentId, out var parent)
               && seen.Add(parentId))
        {
            yield return parent;
            current = parent;
        }
    }

    /// <summary>Legt eine Art an oder aktualisiert sie.</summary>
    public async Task SaveTypeAsync(ContentType type, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(type.Name))
        {
            throw new ContentValidationException(messages["TypeNameRequired"]);
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        await ValidateParentAsync(db, type, ct);

        var stored = await db.ContentTypes.FirstOrDefaultAsync(t => t.Id == type.Id, ct);

        if (stored is null)
        {
            db.ContentTypes.Add(new ContentType
            {
                Id = type.Id,
                GameProjectId = type.GameProjectId,
                ModuleKey = type.ModuleKey,
                ParentId = type.ParentId,
                Name = type.Name.Trim(),
                Description = Normalize(type.Description),
                Icon = Normalize(type.Icon),
                SortOrder = type.SortOrder
            });
        }
        else
        {
            stored.ParentId = type.ParentId;
            stored.Name = type.Name.Trim();
            stored.Description = Normalize(type.Description);
            stored.Icon = Normalize(type.Icon);
            stored.SortOrder = type.SortOrder;
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Prüft die Eltern-Art: Sie muss im selben Projekt und Modul liegen — sonst erbte eine
    /// Item-Art die Felder einer NPC-Art — und darf keinen Ring bilden. Ein Ring wäre keine
    /// Hierarchie mehr, und das Zusammentragen der geerbten Felder liefe endlos.
    /// </summary>
    private async Task ValidateParentAsync(
        GameDevManagerDbContext db, ContentType type, CancellationToken ct)
    {
        if (type.ParentId is not { } parentId)
        {
            return;
        }

        if (parentId == type.Id)
        {
            throw new ContentValidationException(messages["TypeParentSelf"]);
        }

        var candidates = await db.ContentTypes
            .AsNoTracking()
            .Where(t => t.GameProjectId == type.GameProjectId && t.ModuleKey == type.ModuleKey)
            .Select(t => new { t.Id, t.Name, t.ParentId })
            .ToListAsync(ct);

        var byId = candidates.ToDictionary(t => t.Id);

        if (!byId.ContainsKey(parentId))
        {
            throw new ContentValidationException(messages["TypeParentForeign"]);
        }

        // Aufwärts laufen: Trifft man auf dem Weg die Art selbst, schlösse sich ein Ring.
        var seen = new HashSet<Guid> { type.Id };
        var current = parentId;

        while (byId.TryGetValue(current, out var ancestor))
        {
            if (!seen.Add(current))
            {
                throw new ContentValidationException(messages["TypeParentCycle"]);
            }

            if (ancestor.ParentId is not { } next)
            {
                return;
            }

            current = next;
        }
    }

    /// <summary>
    /// Löscht eine Art samt ihrer Felder und deren Werten. Verweigert den Dienst, solange noch
    /// Entitäten dieser Art existieren — sonst verlöre man unbemerkt Inhalte.
    /// </summary>
    public async Task DeleteTypeAsync(Guid typeId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var stored = await db.ContentTypes.FirstOrDefaultAsync(t => t.Id == typeId, ct)
            ?? throw new ContentValidationException(messages["TypeGone"]);

        var usages = await CountUsagesAsync(db, stored.ModuleKey, typeId, ct);
        if (usages > 0)
        {
            throw new ContentValidationException(messages["TypeInUse", stored.Name, usages]);
        }

        // Unterarten erben die Felder dieser Art — mit ihr fielen sie weg. Zuerst umhängen
        // oder löschen, dann die Eltern-Art.
        var children = await db.ContentTypes.CountAsync(t => t.ParentId == typeId, ct);
        if (children > 0)
        {
            throw new ContentValidationException(messages["TypeHasChildren", stored.Name, children]);
        }

        // Die Werte hängen an den Felddefinitionen, nicht an der Art — sie fallen über den
        // Fremdschlüssel der Felder mit, sobald die Art gelöscht wird.
        db.ContentTypes.Remove(stored);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Zählt, wie viele Entitäten eine Art verwenden.</summary>
    public async Task<int> CountUsagesAsync(string moduleKey, Guid typeId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await CountUsagesAsync(db, moduleKey, typeId, ct);
    }

    /// <summary>
    /// Legt ein Feld an oder aktualisiert es — sowohl Art-Felder als auch individuelle Felder.
    /// Ändert sich der Datentyp, werden die bisherigen Werte geleert, weil sie in einer anderen
    /// Wertspalte stehen und nicht sinnvoll umgedeutet werden können.
    /// </summary>
    public async Task SaveFieldAsync(FieldDefinition field, CancellationToken ct = default)
    {
        Validate(field);

        await using var db = await factory.CreateDbContextAsync(ct);

        await EnsureNameFreeInHierarchyAsync(db, field, ct);

        var stored = await db.FieldDefinitions
            .Include(f => f.Options)
            .FirstOrDefaultAsync(f => f.Id == field.Id, ct);

        if (stored is null)
        {
            db.FieldDefinitions.Add(new FieldDefinition
            {
                Id = field.Id,
                ModuleKey = field.ModuleKey,
                ContentTypeId = field.ContentTypeId,
                OwnerEntityId = field.OwnerEntityId,
                Name = field.Name.Trim(),
                Description = Normalize(field.Description),
                Type = field.Type,
                IsRequired = field.IsRequired,
                IsTagList = ResolveTagList(field),
                Unit = Normalize(field.Unit),
                ReferenceModuleKey = ResolveReferenceModule(field),
                SortOrder = field.SortOrder,
                GroupName = Normalize(field.GroupName),
                Options = field.Type == ContentFieldType.Select
                    ? [.. field.Options.Select((o, index) => new FieldOption
                    {
                        Id = o.Id,
                        FieldDefinitionId = field.Id,
                        Label = o.Label.Trim(),
                        SortOrder = index
                    })]
                    : []
            });

            await db.SaveChangesAsync(ct);
            return;
        }

        // Werte werden direkt per SQL angefasst, die Definition über den Change-Tracker —
        // die Transaktion hält beides zusammen.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        if (stored.Type != field.Type)
        {
            await db.FieldValues
                .Where(v => v.FieldDefinitionId == stored.Id)
                .ExecuteDeleteAsync(ct);
        }

        stored.Name = field.Name.Trim();
        stored.Description = Normalize(field.Description);
        stored.Type = field.Type;
        stored.IsRequired = field.IsRequired;
        stored.IsTagList = ResolveTagList(field);
        stored.Unit = Normalize(field.Unit);
        stored.ReferenceModuleKey = ResolveReferenceModule(field);
        stored.SortOrder = field.SortOrder;
        stored.GroupName = Normalize(field.GroupName);

        await SyncOptionsAsync(db, stored, field, ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    /// <summary>Löscht ein Feld samt aller erfassten Werte.</summary>
    public async Task DeleteFieldAsync(Guid fieldId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var stored = await db.FieldDefinitions.FirstOrDefaultAsync(f => f.Id == fieldId, ct);
        if (stored is null)
        {
            return;
        }

        // Werte fallen über den Fremdschlüssel mit.
        db.FieldDefinitions.Remove(stored);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Verhindert, dass ein Feldname zweimal in derselben Vererbungslinie vorkommt: Die Maske
    /// zeigte dann zwei gleich benannte Felder untereinander, ohne dass zu erkennen wäre,
    /// welches gemeint ist. Geprüft wird in beide Richtungen — ein Feld an der Eltern-Art
    /// erreicht auch alle Unterarten.
    /// <para>
    /// Individuelle Felder einer Entität bleiben außen vor; sie gehören keiner Art an.
    /// </para>
    /// </summary>
    private async Task EnsureNameFreeInHierarchyAsync(
        GameDevManagerDbContext db, FieldDefinition field, CancellationToken ct)
    {
        if (field.ContentTypeId is not { } typeId)
        {
            return;
        }

        var owner = await db.ContentTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == typeId, ct);

        if (owner is null)
        {
            return;
        }

        var relatives = await db.ContentTypes
            .AsNoTracking()
            .Where(t => t.GameProjectId == owner.GameProjectId && t.ModuleKey == owner.ModuleKey)
            .Select(t => new { t.Id, t.ParentId })
            .ToListAsync(ct);

        var parentOf = relatives.ToDictionary(t => t.Id, t => t.ParentId);

        // Alles, was die Linie berührt: die Art selbst, ihre Vorfahren und ihre Nachfahren.
        var line = new HashSet<Guid> { typeId };

        for (var current = parentOf[typeId]; current is { } id && line.Add(id); current = parentOf.GetValueOrDefault(id))
        {
        }

        bool Descends(Guid candidate)
        {
            var guard = new HashSet<Guid>();

            for (var current = parentOf.GetValueOrDefault(candidate);
                 current is { } id && guard.Add(id);
                 current = parentOf.GetValueOrDefault(id))
            {
                if (id == typeId)
                {
                    return true;
                }
            }

            return false;
        }

        line.UnionWith(relatives.Select(t => t.Id).Where(Descends));

        var name = field.Name.Trim().ToLower();

        var clash = await db.FieldDefinitions
            .AsNoTracking()
            .AnyAsync(f => f.Id != field.Id
                && f.ContentTypeId != null
                && line.Contains(f.ContentTypeId.Value)
                && f.Name.ToLower() == name, ct);

        if (clash)
        {
            throw new ContentValidationException(messages["FieldNameInHierarchy", field.Name.Trim()]);
        }
    }

    /// <summary>Nächste freie Sortiernummer innerhalb einer Art bzw. einer Entität.</summary>
    public async Task<int> GetNextFieldSortOrderAsync(Guid? contentTypeId, Guid? ownerEntityId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var query = db.FieldDefinitions.AsNoTracking();
        query = contentTypeId is { } typeId
            ? query.Where(f => f.ContentTypeId == typeId)
            : query.Where(f => f.OwnerEntityId == ownerEntityId);

        var maximum = await query.MaxAsync(f => (int?)f.SortOrder, ct);
        return (maximum ?? -1) + 1;
    }

    /// <summary>
    /// Gleicht die Auswahlmöglichkeiten eines Feldes ab. Entfernte Optionen werden aus den
    /// Werten gelöst, damit keine Verweise ins Leere zeigen.
    /// </summary>
    private static async Task SyncOptionsAsync(
        GameDevManagerDbContext db, FieldDefinition stored, FieldDefinition incoming, CancellationToken ct)
    {
        var wanted = incoming.Type == ContentFieldType.Select ? incoming.Options : [];
        var wantedIds = wanted.Select(o => o.Id).ToHashSet();

        foreach (var obsolete in stored.Options.Where(o => !wantedIds.Contains(o.Id)).ToList())
        {
            await db.FieldValues
                .Where(v => v.OptionId == obsolete.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(v => v.OptionId, (Guid?)null), ct);

            db.FieldOptions.Remove(obsolete);
            stored.Options.Remove(obsolete);
        }

        for (var index = 0; index < wanted.Count; index++)
        {
            var option = wanted[index];
            var target = stored.Options.FirstOrDefault(o => o.Id == option.Id);

            if (target is null)
            {
                stored.Options.Add(new FieldOption
                {
                    Id = option.Id,
                    FieldDefinitionId = stored.Id,
                    Label = option.Label.Trim(),
                    SortOrder = index
                });
            }
            else
            {
                target.Label = option.Label.Trim();
                target.SortOrder = index;
            }
        }
    }

    /// <summary>
    /// Zählt Entitäten je Modul. Module ohne eigene Quelle sind noch nicht umgesetzt — für
    /// sie gilt eine Art als unbenutzt.
    /// </summary>
    private async Task<int> CountUsagesAsync(
        GameDevManagerDbContext db, string moduleKey, Guid typeId, CancellationToken ct)
    {
        var source = sources.FirstOrDefault(candidate => candidate.ModuleKey == moduleKey);
        return source is null ? 0 : await source.CountByTypeAsync(db, typeId, ct);
    }

    private void Validate(FieldDefinition field)
    {
        if (string.IsNullOrWhiteSpace(field.Name))
        {
            throw new ContentValidationException(messages["FieldNameRequired"]);
        }

        if (field.ContentTypeId is null == field.OwnerEntityId is null)
        {
            throw new ContentValidationException(messages["FieldOwnerAmbiguous"]);
        }

        if (field.Type == ContentFieldType.EntityReference && string.IsNullOrWhiteSpace(field.ReferenceModuleKey))
        {
            throw new ContentValidationException(messages["FieldReferenceModuleRequired"]);
        }

        if (field.Type == ContentFieldType.Select && field.Options.Any(o => string.IsNullOrWhiteSpace(o.Label)))
        {
            throw new ContentValidationException(messages["FieldOptionsNotEmpty"]);
        }
    }

    /// <summary>
    /// Der Feldtyp „Seltenheit“ ist eine Referenz mit fest verdrahtetem Zielmodul — der
    /// Schlüssel wird beim Speichern gesetzt, damit Auswahlfelder und Referenzansicht
    /// keinen Sonderfall brauchen.
    /// </summary>
    private static string? ResolveReferenceModule(FieldDefinition field) => field.Type switch
    {
        ContentFieldType.EntityReference => field.ReferenceModuleKey,
        ContentFieldType.Rarity => ModuleKeys.Rarities,
        _ => null
    };

    /// <summary>
    /// Stichwörter gibt es nur im Textfeld. Der Schalter wird beim Typwechsel hier gelöscht und
    /// nicht bloß in der Maske ausgeblendet — sonst stünde er noch an einem Zahlenfeld, und die
    /// Rückkehr zum Text brächte ihn unbemerkt wieder mit.
    /// </summary>
    private static bool ResolveTagList(FieldDefinition field) =>
        field.Type == ContentFieldType.Text && field.IsTagList;

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
