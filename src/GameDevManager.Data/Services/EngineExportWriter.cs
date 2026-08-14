using System.Globalization;
using System.Text;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GameDevManager.Data.Services;

/// <summary>Eine Datei, die für die Engine erzeugt wurde: Pfad im Archiv und ihr Inhalt.</summary>
public sealed record EngineFile(string Path, string Content);

/// <summary>
/// Erzeugt aus den Presets (<see cref="EnginePreset"/>) engine-native Dateien: Statt der
/// neutralen JSON-Ablage bekommt die Engine damit Objekte in <b>ihrer</b> Form, die nur noch
/// benutzt werden müssen.
/// <list type="bullet">
/// <item><b>Unity</b> — je Preset eine <c>ScriptableObject</c>-Klasse und je Eintrag eine
/// JSON-Datei, die sich mit <c>JsonUtility.FromJsonOverwrite</c> hineinlesen lässt.</item>
/// <item><b>Unreal</b> — je Preset eine <c>DataTable</c>-taugliche CSV mit der Spalte
/// <c>Name</c> als Zeilenschlüssel.</item>
/// <item><b>Godot</b> — je Eintrag eine <c>.tres</c>-Ressource im Textformat.</item>
/// </list>
/// <para>
/// Bewusst <b>keine</b> fertigen <c>.asset</c>-Dateien für Unity: Deren YAML trägt Dateiverweise
/// über GUIDs aus <c>.meta</c>-Dateien, die es nur im Zielprojekt gibt — erzeugt man sie blind,
/// entstehen kaputte Verweise statt fertiger Objekte. Die Klasse plus die Werte daneben ist der
/// Weg, der im Zielprojekt auch wirklich trägt.
/// </para>
/// <para>
/// Ohne Preset entsteht für ein Modul nichts — der neutrale Inhalt unter <c>content/</c> steht
/// ohnehin in jedem Export.
/// </para>
/// </summary>
public class EngineExportWriter(IEnumerable<IModuleEntitySource> sources)
{
    /// <summary>Der Ordner im Archiv, unter dem die erzeugten Dateien liegen.</summary>
    public const string Folder = "engine/";

    public async Task<List<EngineFile>> BuildAsync(
        GameDevManagerDbContext db, Guid projectId, TargetEngine engine, CancellationToken ct)
    {
        var presets = await db.EnginePresets
            .AsNoTracking()
            .Include(preset => preset.Mappings)
            .Where(preset => preset.GameProjectId == projectId && preset.Engine == engine)
            .OrderBy(preset => preset.SortOrder).ThenBy(preset => preset.Name)
            .ToListAsync(ct);

        if (presets.Count == 0)
        {
            return [];
        }

        var fields = await db.FieldDefinitions.AsNoTracking().ToDictionaryAsync(field => field.Id, ct);
        var typeNames = await db.ContentTypes
            .AsNoTracking()
            .Where(type => type.GameProjectId == projectId)
            .ToDictionaryAsync(type => type.Id, type => type.Name, ct);

        var files = new List<EngineFile>();

        foreach (var preset in presets)
        {
            var source = sources.FirstOrDefault(entry => entry.ModuleKey == preset.ModuleKey);
            if (source is null)
            {
                continue;
            }

            var entities = await source.LoadAllAsync(db, projectId, ct);

            if (preset.ContentTypeId is { } typeId)
            {
                entities = [.. entities.Where(entity => entity.ContentTypeId == typeId)];
            }

            if (entities.Count == 0)
            {
                continue;
            }

            var ids = entities.Select(entity => entity.Id).ToList();

            var values = (await db.FieldValues
                    .AsNoTracking()
                    .Where(value => ids.Contains(value.OwnerEntityId))
                    .ToListAsync(ct))
                .GroupBy(value => value.OwnerEntityId)
                .ToDictionary(group => group.Key, group => group.ToDictionary(value => value.FieldDefinitionId));

            var icons = await db.Assets
                .AsNoTracking()
                .Where(asset => asset.OwnerEntityId != null
                    && ids.Contains(asset.OwnerEntityId!.Value)
                    && asset.IsPrimary)
                .ToDictionaryAsync(asset => asset.OwnerEntityId!.Value, asset => asset.FileName, ct);

            var mappings = preset.Mappings.OrderBy(m => m.SortOrder).ThenBy(m => m.Target).ToList();

            var rows = entities
                .Select(entity => new PresetRow(
                    entity,
                    mappings.ToDictionary(
                        mapping => mapping.Target,
                        mapping => Resolve(mapping, entity, fields, values, typeNames, icons))))
                .ToList();

            files.AddRange(engine switch
            {
                TargetEngine.Unity => Unity(preset, mappings, fields, rows),
                TargetEngine.Unreal => Unreal(preset, mappings, rows),
                _ => Godot(preset, rows)
            });
        }

        return files;
    }

    private sealed record PresetRow(ContentEntity Entity, Dictionary<string, string?> Values);

    // ------------------------------------------------------------------------------ Unity

    /// <summary>
    /// Eine ScriptableObject-Klasse plus je Eintrag eine JSON-Datei. Der Klassenname ist der
    /// <see cref="EnginePreset.TypeName"/> — im Zielprojekt liegt die Datei einfach unter
    /// <c>Assets/</c>, und Unity kompiliert sie mit.
    /// </summary>
    private static List<EngineFile> Unity(
        EnginePreset preset, List<EnginePresetMapping> mappings,
        Dictionary<Guid, FieldDefinition> fields, List<PresetRow> rows)
    {
        var code = new StringBuilder();

        code.AppendLine("// Erzeugt vom GameDevManager. Änderungen gehen beim nächsten Export verloren.");
        code.AppendLine("using UnityEngine;");
        code.AppendLine();
        code.AppendLine($"[CreateAssetMenu(menuName = \"GameDevManager/{preset.TypeName}\")]");
        code.AppendLine($"public class {preset.TypeName} : ScriptableObject");
        code.AppendLine("{");

        foreach (var mapping in mappings)
        {
            code.AppendLine($"    public {CSharpType(mapping, fields)} {Identifier(mapping.Target)};");
        }

        code.AppendLine("}");

        var files = new List<EngineFile>
        {
            new($"{Folder}unity/{preset.TypeName}.cs", code.ToString())
        };

        foreach (var row in rows)
        {
            var json = new StringBuilder();
            json.AppendLine("{");

            var written = 0;
            foreach (var mapping in mappings)
            {
                var value = row.Values.GetValueOrDefault(mapping.Target);
                var separator = ++written < mappings.Count ? "," : string.Empty;

                json.AppendLine(
                    $"  \"{Identifier(mapping.Target)}\": {JsonLiteral(mapping, fields, value)}{separator}");
            }

            json.AppendLine("}");

            files.Add(new EngineFile(
                $"{Folder}unity/{preset.TypeName}/{FileName(row.Entity)}.json", json.ToString()));
        }

        return files;
    }

    // ----------------------------------------------------------------------------- Unreal

    /// <summary>
    /// Eine CSV, wie Unreal sie als DataTable importiert: erste Spalte <c>Name</c> als
    /// Zeilenschlüssel. Genommen wird dafür die GUID der Entität und nicht ihr Name — der
    /// Schlüssel muss über Umbenennungen hinweg derselbe bleiben.
    /// </summary>
    private static List<EngineFile> Unreal(
        EnginePreset preset, List<EnginePresetMapping> mappings, List<PresetRow> rows)
    {
        var csv = new StringBuilder();

        // Komma und nicht Semikolon: Unreal liest seine DataTables nach US-Konvention.
        csv.AppendLine(Csv.FormatRow(["Name", .. mappings.Select(mapping => Identifier(mapping.Target))], ','));

        foreach (var row in rows)
        {
            csv.AppendLine(Csv.FormatRow(
                [row.Entity.Id.ToString("N"), .. mappings.Select(m => row.Values.GetValueOrDefault(m.Target))],
                ','));
        }

        return [new EngineFile($"{Folder}unreal/{preset.TypeName}.csv", csv.ToString())];
    }

    // ------------------------------------------------------------------------------ Godot

    /// <summary>
    /// Je Eintrag eine <c>.tres</c>-Ressource im Textformat — das ist Godots eigenes Format
    /// und stabil genug, um es zu schreiben.
    /// </summary>
    private static List<EngineFile> Godot(EnginePreset preset, List<PresetRow> rows)
    {
        var files = new List<EngineFile>();

        foreach (var row in rows)
        {
            var text = new StringBuilder();

            text.AppendLine("[gd_resource type=\"Resource\" format=3]");
            text.AppendLine();
            text.AppendLine("[resource]");
            text.AppendLine($"resource_name = {Quote(row.Entity.Name)}");

            foreach (var (target, value) in row.Values)
            {
                text.AppendLine($"{Identifier(target)} = {Quote(value)}");
            }

            files.Add(new EngineFile(
                $"{Folder}godot/{preset.TypeName}/{FileName(row.Entity)}.tres", text.ToString()));
        }

        return files;
    }

    // ------------------------------------------------------------------------ Werte lesen

    private static string? Resolve(
        EnginePresetMapping mapping, ContentEntity entity,
        Dictionary<Guid, FieldDefinition> fields,
        Dictionary<Guid, Dictionary<Guid, FieldValue>> values,
        Dictionary<Guid, string> typeNames,
        Dictionary<Guid, string> icons) =>
        mapping.Source switch
        {
            PresetSource.Name => entity.Name,
            PresetSource.Description => entity.Description,
            PresetSource.Constant => mapping.ConstantValue,
            PresetSource.EntityId => entity.Id.ToString(),
            PresetSource.TypeName => entity.ContentTypeId is { } id ? typeNames.GetValueOrDefault(id) : null,
            PresetSource.PrimaryAssetFile => icons.GetValueOrDefault(entity.Id),
            PresetSource.Field => FieldText(mapping, fields, values, entity),
            _ => null
        };

    private static string? FieldText(
        EnginePresetMapping mapping, Dictionary<Guid, FieldDefinition> fields,
        Dictionary<Guid, Dictionary<Guid, FieldValue>> values, ContentEntity entity)
    {
        if (mapping.FieldDefinitionId is not { } fieldId
            || !fields.TryGetValue(fieldId, out var field)
            || !values.TryGetValue(entity.Id, out var stored)
            || !stored.TryGetValue(fieldId, out var value))
        {
            return null;
        }

        // Feste Kultur wie überall: Derselbe Stand muss auf jedem Rechner dieselbe Datei ergeben.
        return field.Type switch
        {
            ContentFieldType.Integer or ContentFieldType.Decimal =>
                value.NumberValue?.ToString("0.##########", CultureInfo.InvariantCulture),
            ContentFieldType.Boolean =>
                value.BooleanValue?.ToString(CultureInfo.InvariantCulture).ToLowerInvariant(),
            ContentFieldType.Date => value.DateValue?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ContentFieldType.EntityReference => value.ReferenceValue?.ToString(),
            _ => value.TextValue
        };
    }

    // -------------------------------------------------------------------------- Schreiben

    /// <summary>Der C#-Typ eines Feldes in der erzeugten ScriptableObject-Klasse.</summary>
    private static string CSharpType(EnginePresetMapping mapping, Dictionary<Guid, FieldDefinition> fields)
    {
        if (mapping.Source != PresetSource.Field
            || mapping.FieldDefinitionId is not { } id
            || !fields.TryGetValue(id, out var field))
        {
            return "string";
        }

        return field.Type switch
        {
            ContentFieldType.Integer => "int",
            ContentFieldType.Decimal => "float",
            ContentFieldType.Boolean => "bool",
            _ => "string"
        };
    }

    private static string JsonLiteral(
        EnginePresetMapping mapping, Dictionary<Guid, FieldDefinition> fields, string? value)
    {
        var type = CSharpType(mapping, fields);

        if (string.IsNullOrEmpty(value))
        {
            return type switch
            {
                "int" or "float" => "0",
                "bool" => "false",
                _ => "\"\""
            };
        }

        return type == "string" ? Quote(value) : value;
    }

    /// <summary>
    /// Macht aus einem beliebigen Zielnamen einen gültigen Bezeichner: Der Nutzer schreibt
    /// „Max. Leben“, die Engine braucht <c>MaxLeben</c>.
    /// </summary>
    private static string Identifier(string target)
    {
        var text = new StringBuilder();

        foreach (var character in target)
        {
            if (char.IsLetterOrDigit(character) || character == '_')
            {
                text.Append(character);
            }
        }

        var result = text.ToString();

        // Ein Bezeichner darf nicht mit einer Ziffer beginnen — dann bekommt er einen Strich davor.
        return result.Length == 0 ? "value" : char.IsDigit(result[0]) ? "_" + result : result;
    }

    /// <summary>Ein Dateiname aus dem Namen der Entität, mit GUID dahinter gegen Dubletten.</summary>
    private static string FileName(ContentEntity entity)
    {
        var safe = Identifier(entity.Name.Replace(' ', '_'));

        return $"{safe}-{entity.Id:N}";
    }

    private static string Quote(string? value) =>
        "\"" + (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") + "\"";
}
