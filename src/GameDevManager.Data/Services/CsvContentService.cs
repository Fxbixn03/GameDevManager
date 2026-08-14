using System.Globalization;
using System.Text;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace GameDevManager.Data.Services;

/// <summary>Was ein CSV-Import bewirkt hat. Die Warnungen zeigt die Oberfläche einzeln an.</summary>
public sealed record CsvImportResult(
    int Created, int Updated, int Unchanged, IReadOnlyList<string> Warnings);

/// <summary>
/// CSV je Modul: der Weg Tabelle ↔ Tool, den das Balancing braucht. Exportiert werden die
/// Stammdaten und alle Felder der Arten eines Moduls, importiert wird über dieselbe
/// Spaltenform zurück.
/// <para>
/// Bewusst <b>kein</b> zweites Exportformat neben dem ZIP: Das CSV kann nur, was in eine
/// Tabelle passt (eine Zeile je Entität, ein Wert je Feld) — Kind-Sammlungen, Bedingungen und
/// Assets bleiben draußen. Es ersetzt den Export nicht, es ergänzt ihn um den Weg, auf dem
/// Zahlen gepflegt werden.
/// </para>
/// <para>
/// Der Import <b>aktualisiert</b>, er ersetzt nicht: Eine Zeile findet ihre Entität über die
/// GUID-Spalte, sonst über den Namen; was in keiner Zeile steht, bleibt unangetastet. Ein CSV
/// ist ein Ausschnitt, und ein Ausschnitt darf nichts löschen.
/// </para>
/// </summary>
public class CsvContentService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    IEnumerable<IModuleEntitySource> sources,
    ContentTypeService contentTypes,
    PermissionGuard guard,
    IStringLocalizer<DataMessages> messages)
{
    /// <summary>Die festen Spalten vor den Feldern — kleingeschrieben, damit die Zuordnung beim Import egal ist.</summary>
    public const string IdColumn = "id";
    public const string NameColumn = "name";
    public const string DescriptionColumn = "beschreibung";
    public const string TypeColumn = "art";

    /// <summary>Der ganze Modulbestand als CSV — eine Zeile je Entität, eine Spalte je Feld.</summary>
    public async Task<string> ExportAsync(Guid projectId, string moduleKey, CancellationToken ct = default)
    {
        await guard.EnsureCanExportAsync(ct);

        var source = Source(moduleKey);

        await using var db = await factory.CreateDbContextAsync(ct);

        var types = await contentTypes.GetTypesAsync(projectId, moduleKey, ct);
        var typeNames = types.ToDictionary(type => type.Id, type => type.Name);

        var entities = await source.LoadAllAsync(db, projectId, ct);
        var ids = entities.Select(entity => entity.Id).ToList();

        var fields = await FieldsAsync(db, types, ids, ct);

        // Eine Spalte je Feld<b>name</b>: Zwei Geschwister-Arten mit einem Feld „Schaden“
        // teilen sich die Spalte — in einer Tabelle ist das die erwartete Form.
        var columns = fields.Values
            .SelectMany(list => list)
            .Select(field => field.Name)
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var values = await db.FieldValues
            .AsNoTracking()
            .Where(value => ids.Contains(value.OwnerEntityId))
            .ToListAsync(ct);

        var options = await OptionsAsync(db, fields, ct);
        var byOwner = values
            .GroupBy(value => value.OwnerEntityId)
            .ToDictionary(group => group.Key, group => group.ToDictionary(value => value.FieldDefinitionId));

        var text = new StringBuilder();
        text.AppendLine(Csv.FormatRow([IdColumn, NameColumn, DescriptionColumn, TypeColumn, .. columns]));

        foreach (var entity in entities)
        {
            var applicable = Applicable(entity, fields);
            var stored = byOwner.GetValueOrDefault(entity.Id) ?? [];

            var cells = new List<string?>
            {
                entity.Id.ToString(),
                entity.Name,
                entity.Description,
                entity.ContentTypeId is { } typeId ? typeNames.GetValueOrDefault(typeId) : null
            };

            foreach (var column in columns)
            {
                var field = applicable.FirstOrDefault(f =>
                    string.Equals(f.Name, column, StringComparison.CurrentCultureIgnoreCase));

                cells.Add(field is not null && stored.TryGetValue(field.Id, out var value)
                    ? Format(field, value, options)
                    : null);
            }

            text.AppendLine(Csv.FormatRow(cells));
        }

        return text.ToString();
    }

    /// <summary>
    /// Liest ein CSV zurück. Zeilen mit bekannter GUID oder bekanntem Namen aktualisieren die
    /// vorhandene Entität, alle anderen legen eine neue an — sofern
    /// <paramref name="createMissing"/> es erlaubt.
    /// </summary>
    public async Task<CsvImportResult> ImportAsync(
        Guid projectId, string moduleKey, string content, bool createMissing,
        CancellationToken ct = default)
    {
        // Ein CSV-Import schreibt Inhalte wie der ZIP-Import — er braucht dieselben Rechte.
        await guard.EnsureCanImportAsync(ct);

        var source = Source(moduleKey);

        var rows = Csv.Parse(content, Csv.DetectSeparator(content));
        if (rows.Count < 2)
        {
            throw new ContentValidationException(messages["Csv_Empty"].Value);
        }

        var header = rows[0]
            .Select(cell => cell.Trim())
            .ToList();

        var nameColumn = IndexOf(header, NameColumn);
        if (IndexOf(header, IdColumn) < 0 && nameColumn < 0)
        {
            // Ohne eine der beiden Spalten wüsste keine Zeile, wen sie meint.
            throw new ContentValidationException(messages["Csv_KeyColumnMissing", IdColumn, NameColumn].Value);
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        var types = await contentTypes.GetTypesAsync(projectId, moduleKey, ct);
        var entities = await source.LoadAllAsync(db, projectId, ct);

        var byId = entities.ToDictionary(entity => entity.Id);
        var byName = entities
            .GroupBy(entity => entity.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.CurrentCultureIgnoreCase);

        var ids = entities.Select(entity => entity.Id).ToList();
        var fields = await FieldsAsync(db, types, ids, ct);
        var options = await OptionsAsync(db, fields, ct);

        var values = await db.FieldValues
            .Where(value => ids.Contains(value.OwnerEntityId))
            .ToListAsync(ct);

        var byOwner = values
            .GroupBy(value => value.OwnerEntityId)
            .ToDictionary(group => group.Key, group => group.ToDictionary(value => value.FieldDefinitionId));

        var warnings = new List<string>();
        var created = 0;
        var updated = 0;
        var unchanged = 0;

        for (var index = 1; index < rows.Count; index++)
        {
            var row = rows[index];
            var line = index + 1;

            var id = ParseGuid(Cell(row, IndexOf(header, IdColumn)));
            var name = Cell(row, nameColumn)?.Trim();

            var entity = id is { } known && byId.TryGetValue(known, out var found)
                ? found
                : name is not null && byName.TryGetValue(name, out var namedMatch) ? namedMatch : null;

            if (entity is null)
            {
                if (!createMissing)
                {
                    warnings.Add(messages["Csv_RowSkipped", line, name ?? string.Empty].Value);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(name))
                {
                    warnings.Add(messages["Csv_RowNameMissing", line].Value);
                    continue;
                }

                entity = source.CreateNew(projectId, name);

                // Eine mitgelieferte GUID wird übernommen: So lässt sich ein Bestand aus einer
                // Tabelle einspielen, ohne dass Verweise darauf ins Leere zeigen.
                if (id is { } wanted && wanted != Guid.Empty)
                {
                    entity.Id = wanted;
                }

                db.Add(entity);
                byId[entity.Id] = entity;
                byName[entity.Name] = entity;
                created++;
            }
            else
            {
                var before = Snapshot(entity, byOwner.GetValueOrDefault(entity.Id));

                if (!string.IsNullOrWhiteSpace(name))
                {
                    entity.Name = name;
                }

                ApplyRow(db, entity, row, header, types, fields, options, byOwner, warnings, line);

                if (Snapshot(entity, byOwner.GetValueOrDefault(entity.Id)) == before)
                {
                    unchanged++;
                    continue;
                }

                entity.UpdatedAtUtc = DateTime.UtcNow;
                updated++;
                continue;
            }

            ApplyRow(db, entity, row, header, types, fields, options, byOwner, warnings, line);
        }

        if (created > 0 || updated > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        return new CsvImportResult(created, updated, unchanged, warnings);
    }

    /// <summary>Überträgt Beschreibung, Art und alle Feldspalten einer Zeile auf die Entität.</summary>
    private void ApplyRow(
        GameDevManagerDbContext db, ContentEntity entity, List<string> row, List<string> header,
        List<ContentType> types, Dictionary<Guid, List<FieldDefinition>> fields,
        Dictionary<Guid, List<FieldOption>> options,
        Dictionary<Guid, Dictionary<Guid, FieldValue>> byOwner,
        List<string> warnings, int line)
    {
        var description = Cell(row, IndexOf(header, DescriptionColumn));
        if (description is not null)
        {
            entity.Description = string.IsNullOrWhiteSpace(description) ? null : description;
        }

        var typeName = Cell(row, IndexOf(header, TypeColumn))?.Trim();
        if (!string.IsNullOrEmpty(typeName))
        {
            var type = types.FirstOrDefault(t =>
                string.Equals(t.Name, typeName, StringComparison.CurrentCultureIgnoreCase));

            if (type is null)
            {
                warnings.Add(messages["Csv_TypeUnknown", line, typeName].Value);
            }
            else
            {
                entity.ContentTypeId = type.Id;
            }
        }

        var applicable = Applicable(entity, fields);
        var stored = byOwner.TryGetValue(entity.Id, out var existing) ? existing : byOwner[entity.Id] = [];

        for (var column = 0; column < header.Count; column++)
        {
            var title = header[column];

            if (IsFixedColumn(title))
            {
                continue;
            }

            var field = applicable.FirstOrDefault(f =>
                string.Equals(f.Name, title, StringComparison.CurrentCultureIgnoreCase));

            if (field is null)
            {
                // Kein Fund heißt nicht „unbekannte Spalte“: Das Feld kann an einer anderen
                // Art dieses Moduls hängen, und dann geht die Spalte diese Zeile nichts an.
                continue;
            }

            var raw = Cell(row, column);
            if (raw is null)
            {
                continue;
            }

            var target = stored.GetValueOrDefault(field.Id);

            if (string.IsNullOrWhiteSpace(raw))
            {
                if (target is not null)
                {
                    db.FieldValues.Remove(target);
                    stored.Remove(field.Id);
                }

                continue;
            }

            var parsed = new FieldValue { OwnerModuleKey = entity.ModuleKey };

            if (!TryParse(field, raw, options, parsed, out var problem))
            {
                warnings.Add(messages["Csv_CellInvalid", line, title, messages[problem].Value].Value);
                continue;
            }

            if (target is null)
            {
                target = new FieldValue
                {
                    FieldDefinitionId = field.Id,
                    OwnerEntityId = entity.Id,
                    OwnerModuleKey = entity.ModuleKey
                };

                ContentFields.CopyValues(parsed, target);
                db.FieldValues.Add(target);
                stored[field.Id] = target;
            }
            else
            {
                ContentFields.CopyValues(parsed, target);
            }
        }
    }

    // ------------------------------------------------------------------ Werte lesen/schreiben

    /// <summary>
    /// Ein Wert als Text. Zahlen und Datumswerte in fester Kultur — dieselbe Regel wie bei den
    /// Kurvenausdrücken und im Export: Dieselbe Tabelle muss auf jedem Rechner dasselbe ergeben.
    /// </summary>
    private static string? Format(
        FieldDefinition field, FieldValue value, Dictionary<Guid, List<FieldOption>> options) =>
        field.Type switch
        {
            ContentFieldType.Integer or ContentFieldType.Decimal =>
                value.NumberValue?.ToString("0.##########", CultureInfo.InvariantCulture),
            ContentFieldType.Boolean => value.BooleanValue?.ToString(CultureInfo.InvariantCulture).ToLowerInvariant(),
            ContentFieldType.Date => value.DateValue?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            // Eine Referenzliste steht schon als semikolongetrennter Text da — sie geht als
            // solcher heraus und wieder herein.
            ContentFieldType.EntityReference => field.IsMultiValue
                ? value.TextValue
                : value.ReferenceValue?.ToString(),
            ContentFieldType.Select => options.GetValueOrDefault(field.Id)
                ?.FirstOrDefault(option => option.Id == value.OptionId)?.Label,
            _ => value.TextValue
        };

    /// <summary>
    /// Liest eine Zelle in die passende Wertspalte. Gibt <c>false</c> samt Begründung zurück,
    /// wenn der Text nicht zum Feldtyp passt — die Zeile bleibt dann stehen, nur diese Zelle
    /// nicht: Ein Tippfehler in einer Spalte darf nicht den ganzen Import verwerfen.
    /// </summary>
    private static bool TryParse(
        FieldDefinition field, string raw, Dictionary<Guid, List<FieldOption>> options,
        FieldValue target, out string problem)
    {
        problem = string.Empty;
        var text = raw.Trim();

        switch (field.Type)
        {
            case ContentFieldType.Integer:
            case ContentFieldType.Decimal:
                // Erst feste Kultur, dann die des Benutzers: „1.5“ und „1,5“ meinen beide
                // eineinhalb, je nachdem, wer die Tabelle geschrieben hat.
                if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                    || double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out number))
                {
                    target.NumberValue = field.Type == ContentFieldType.Integer ? Math.Round(number) : number;
                    return true;
                }

                problem = "Csv_NotANumber";
                return false;

            case ContentFieldType.Boolean:
                if (TryParseBoolean(text, out var flag))
                {
                    target.BooleanValue = flag;
                    return true;
                }

                problem = "Csv_NotABoolean";
                return false;

            case ContentFieldType.Date:
                if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                    || DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.None, out date))
                {
                    target.DateValue = date;
                    return true;
                }

                problem = "Csv_NotADate";
                return false;

            case ContentFieldType.EntityReference when field.IsMultiValue:
                var list = GuidList.Normalize(text);
                if (list is null)
                {
                    problem = "Csv_NotAGuid";
                    return false;
                }

                target.TextValue = list;
                return true;

            case ContentFieldType.EntityReference:
                if (Guid.TryParse(text, out var reference))
                {
                    target.ReferenceValue = reference;
                    return true;
                }

                problem = "Csv_NotAGuid";
                return false;

            case ContentFieldType.Select:
                var option = options.GetValueOrDefault(field.Id)?.FirstOrDefault(o =>
                    string.Equals(o.Label, text, StringComparison.CurrentCultureIgnoreCase));

                if (option is null)
                {
                    problem = "Csv_NotAnOption";
                    return false;
                }

                target.OptionId = option.Id;
                return true;

            default:
                // Stichwortlisten werden kanonisiert wie überall sonst — die Zelle darf
                // „Feuer, Eis,,Feuer“ enthalten und ergibt trotzdem zwei Stichwörter.
                target.TextValue = field.IsKeywordField ? KeywordList.Normalize(text) : text;
                return true;
        }
    }

    private static bool TryParseBoolean(string text, out bool value)
    {
        switch (text.ToLowerInvariant())
        {
            case "true" or "ja" or "wahr" or "1" or "x":
                value = true;
                return true;
            case "false" or "nein" or "falsch" or "0" or "":
                value = false;
                return true;
            default:
                value = false;
                return false;
        }
    }

    // ---------------------------------------------------------------------------- Hilfsmittel

    private IModuleEntitySource Source(string moduleKey) =>
        sources.FirstOrDefault(source => source.ModuleKey == moduleKey)
        ?? throw new ContentValidationException(messages["Bulk_ModuleUnknown", moduleKey].Value);

    /// <summary>Die Felder je Art (samt geerbten) plus die individuellen je Entität.</summary>
    private static async Task<Dictionary<Guid, List<FieldDefinition>>> FieldsAsync(
        GameDevManagerDbContext db, List<ContentType> types, List<Guid> entityIds, CancellationToken ct)
    {
        var fields = types.ToDictionary(
            type => type.Id,
            type => type.Fields.Concat(type.InheritedFields).ToList());

        var individual = await db.FieldDefinitions
            .AsNoTracking()
            .Where(field => field.OwnerEntityId != null && entityIds.Contains(field.OwnerEntityId!.Value))
            .ToListAsync(ct);

        foreach (var group in individual.GroupBy(field => field.OwnerEntityId!.Value))
        {
            fields[group.Key] = [.. group];
        }

        return fields;
    }

    /// <summary>Die Auswahlmöglichkeiten der Select-Felder, adressiert über ihr Feld.</summary>
    private static async Task<Dictionary<Guid, List<FieldOption>>> OptionsAsync(
        GameDevManagerDbContext db, Dictionary<Guid, List<FieldDefinition>> fields, CancellationToken ct)
    {
        var selectFields = fields.Values
            .SelectMany(list => list)
            .Where(field => field.Type == ContentFieldType.Select)
            .Select(field => field.Id)
            .Distinct()
            .ToList();

        if (selectFields.Count == 0)
        {
            return [];
        }

        var options = await db.FieldOptions
            .AsNoTracking()
            .Where(option => selectFields.Contains(option.FieldDefinitionId))
            .ToListAsync(ct);

        return options
            .GroupBy(option => option.FieldDefinitionId)
            .ToDictionary(group => group.Key, group => group.ToList());
    }

    /// <summary>Welche Felder für genau diese Entität gelten: die ihrer Art und ihre eigenen.</summary>
    private static List<FieldDefinition> Applicable(
        ContentEntity entity, Dictionary<Guid, List<FieldDefinition>> fields)
    {
        var result = new List<FieldDefinition>();

        if (entity.ContentTypeId is { } typeId && fields.TryGetValue(typeId, out var ofType))
        {
            result.AddRange(ofType);
        }

        if (fields.TryGetValue(entity.Id, out var own))
        {
            result.AddRange(own);
        }

        return result;
    }

    private static bool IsFixedColumn(string title) =>
        string.Equals(title, IdColumn, StringComparison.CurrentCultureIgnoreCase)
        || string.Equals(title, NameColumn, StringComparison.CurrentCultureIgnoreCase)
        || string.Equals(title, DescriptionColumn, StringComparison.CurrentCultureIgnoreCase)
        || string.Equals(title, TypeColumn, StringComparison.CurrentCultureIgnoreCase);

    private static int IndexOf(List<string> header, string column) =>
        header.FindIndex(title => string.Equals(title, column, StringComparison.CurrentCultureIgnoreCase));

    private static string? Cell(List<string> row, int column) =>
        column >= 0 && column < row.Count ? row[column] : null;

    private static Guid? ParseGuid(string? text) =>
        Guid.TryParse(text, out var id) ? id : null;

    /// <summary>
    /// Ein grober Fingerabdruck der Entität samt ihrer Werte. Er beantwortet nur die Frage
    /// „hat sich etwas geändert?“ — ohne ihn zählte jede eingelesene Zeile als Änderung, und
    /// das Änderungsprotokoll bekäme bei jedem Import den ganzen Bestand.
    /// </summary>
    private static string Snapshot(ContentEntity entity, Dictionary<Guid, FieldValue>? values)
    {
        var text = new StringBuilder();

        text.Append(entity.Name).Append('\u001F')
            .Append(entity.Description).Append('\u001F')
            .Append(entity.ContentTypeId).Append('\u001F');

        foreach (var value in (values ?? []).OrderBy(pair => pair.Key).Select(pair => pair.Value))
        {
            text.Append(value.FieldDefinitionId).Append('=')
                .Append(value.TextValue).Append('|')
                .Append(value.NumberValue?.ToString(CultureInfo.InvariantCulture)).Append('|')
                .Append(value.BooleanValue).Append('|')
                .Append(value.DateValue?.ToString("O", CultureInfo.InvariantCulture)).Append('|')
                .Append(value.ReferenceValue).Append('|')
                .Append(value.OptionId).Append('\u001F');
        }

        return text.ToString();
    }
}
