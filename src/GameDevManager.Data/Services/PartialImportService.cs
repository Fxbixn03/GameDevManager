using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace GameDevManager.Data.Services;

/// <summary>Was mit einer einzelnen Entität aus dem fremden Archiv geschehen soll.</summary>
public enum PartialImportChoice
{
    /// <summary>Nichts tun — die Vorgabe für alles, was der Nutzer nicht angehakt hat.</summary>
    Skip = 0,

    /// <summary>Übernehmen: anlegen oder den vorhandenen Stand überschreiben.</summary>
    Take = 1,

    /// <summary>
    /// Als zweiten Datensatz anlegen — mit neuer GUID, damit der vorhandene bleibt. Nur
    /// sinnvoll bei einem Konflikt; bei etwas Neuem ist es dasselbe wie Übernehmen.
    /// </summary>
    Copy = 2
}

/// <summary>Eine Entität im fremden Archiv und ihr Verhältnis zum eigenen Bestand.</summary>
public sealed record PartialImportCandidate(
    Guid Id,
    string Name,
    string ModuleKey,
    string File,
    bool ExistsHere,
    bool IsIdentical,
    IReadOnlyList<string> ChangedProperties);

/// <summary>Die Vorschau eines Teil-Imports, nach Modul gruppiert.</summary>
public sealed record PartialImportPreview(IReadOnlyList<PartialImportCandidate> Candidates)
{
    public int NewCount => Candidates.Count(candidate => !candidate.ExistsHere);

    public int ChangedCount => Candidates.Count(candidate => candidate.ExistsHere && !candidate.IsIdentical);

    public int IdenticalCount => Candidates.Count(candidate => candidate.IsIdentical);
}

/// <summary>Was ein Teil-Import bewirkt hat.</summary>
public sealed record PartialImportResult(int Taken, int Copied, int Skipped, IReadOnlyList<string> Warnings);

/// <summary>
/// Teil-Import aus einem fremden Export (F42): einzelne Entitäten übernehmen, statt den ganzen
/// Bestand zu ersetzen — der Weg, auf dem ein Team eine gemeinsame Item-Basis pflegt.
/// <para>
/// Beide Bausteine lagen schon vor: Der Diff der Exportstände vergleicht Entität für Entität
/// über die GUID (<c>JsonNode.DeepEquals</c>), und <see cref="GuidRemap"/> tauscht GUIDs, wenn
/// eine Entität als Kopie statt als Überschreibung kommen soll. Zusammen ergibt das den
/// Auswahl-Import.
/// </para>
/// <para>
/// Bewusst <b>ohne Kind-Sammlungen fremder Module und ohne Löschen</b>: Übernommen wird, was in
/// der Inhaltsdatei einer Entität steht, samt ihrer eingebetteten Kinder. Was im Zielprojekt
/// steht und im Archiv fehlt, bleibt — ein Ausschnitt darf nichts löschen, dieselbe Regel wie
/// beim Modul-CSV.
/// </para>
/// </summary>
public class PartialImportService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    ExportService export,
    IEnumerable<IModuleEntitySource> sources,
    PermissionGuard guard,
    IStringLocalizer<DataMessages> messages)
{
    // ---------------------------------------------------------------------------- Vorschau

    /// <summary>
    /// Liest ein fremdes Archiv und stellt es dem eigenen Bestand gegenüber: was neu wäre, was
    /// einen vorhandenen Stand überschriebe und was identisch ist.
    /// </summary>
    public async Task<PartialImportPreview> PreviewAsync(
        Guid projectId, Stream archiveStream, CancellationToken ct = default)
    {
        using var incoming = new ZipArchive(archiveStream, ZipArchiveMode.Read, leaveOpen: true);

        EnsureFormat(incoming);

        // Der eigene Stand als flüchtiger Export — dieselbe Strecke wie beim Diff zweier
        // Stände, und damit derselbe Vergleich auf denselben Regeln.
        await using var own = await ExportCurrentAsync(projectId, ct);
        using var ownArchive = new ZipArchive(own, ZipArchiveMode.Read, leaveOpen: true);

        var candidates = new List<PartialImportCandidate>();

        foreach (var (file, moduleKey) in ExportFormat.ContentFileModules)
        {
            if (moduleKey is null || sources.All(source => source.ModuleKey != moduleKey))
            {
                // Dateien ohne Modul (Arten/Felder, Feldwerte, Bedingungen) kommen mit ihren
                // Entitäten, nicht für sich — sie stehen deshalb nicht zur Auswahl.
                continue;
            }

            var incomingEntities = ReadEntities(incoming, file);
            var ownEntities = ReadEntities(ownArchive, file);

            foreach (var (id, node) in incomingEntities)
            {
                var exists = ownEntities.TryGetValue(id, out var mine);
                var identical = exists && JsonNode.DeepEquals(mine, node);

                candidates.Add(new PartialImportCandidate(
                    id,
                    node["name"]?.GetValue<string>() ?? id.ToString(),
                    moduleKey,
                    file,
                    exists,
                    identical,
                    exists && !identical ? ChangedProperties(mine!, node) : []));
            }
        }

        return new PartialImportPreview(
        [
            .. candidates
                .OrderBy(candidate => candidate.ModuleKey, StringComparer.Ordinal)
                .ThenBy(candidate => candidate.Name, StringComparer.CurrentCultureIgnoreCase)
        ]);
    }

    // ------------------------------------------------------------------------- Übernehmen

    /// <summary>
    /// Übernimmt die gewählten Entitäten. Zu jeder GUID sagt <paramref name="choices"/>, was
    /// geschehen soll; alles Ungenannte bleibt unangetastet.
    /// </summary>
    public async Task<PartialImportResult> ImportAsync(
        Guid projectId, Stream archiveStream,
        IReadOnlyDictionary<Guid, PartialImportChoice> choices, CancellationToken ct = default)
    {
        await guard.EnsureCanImportAsync(ct);

        using var incoming = new ZipArchive(archiveStream, ZipArchiveMode.Read, leaveOpen: true);

        EnsureFormat(incoming);

        var warnings = new List<string>();
        var taken = 0;
        var copied = 0;

        // Feldwerte, individuelle Felder und Bedingungen des Archivs — sie hängen über die
        // GUID an ihren Entitäten und kommen mit, wenn deren GUID gewählt ist.
        var attachments = ReadAttachments(incoming);

        await using var db = await factory.CreateDbContextAsync(ct);

        // Wie beim vollen Import: Ein Vorgang, kein Bestand an Änderungen — sonst flutete ein
        // Teil-Import über zweihundert Items das Protokoll.
        db.SuppressChangeLog = true;

        foreach (var (file, moduleKey) in ExportFormat.ContentFileModules)
        {
            if (moduleKey is null)
            {
                continue;
            }

            var entities = ReadEntities(incoming, file);

            foreach (var (id, node) in entities)
            {
                var choice = choices.GetValueOrDefault(id, PartialImportChoice.Skip);

                if (choice == PartialImportChoice.Skip)
                {
                    continue;
                }

                var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var payload = BuildPayload(node, attachments, id);

                if (choice == PartialImportChoice.Copy)
                {
                    // Neue GUIDs für alles Mitkopierte — Verweise nach außen bleiben stehen,
                    // dieselbe Regel wie beim Duplizieren einer Entität.
                    GuidRemap.Collect(JsonNode.Parse(payload), map);
                    payload = GuidRemap.Apply(payload, map);
                }

                try
                {
                    await WriteAsync(db, projectId, file, payload, ct);

                    if (choice == PartialImportChoice.Copy)
                    {
                        copied++;
                    }
                    else
                    {
                        taken++;
                    }
                }
                catch (Exception ex)
                {
                    // Eine Entität, die nicht ankommt, verwirft nicht den ganzen Import —
                    // dieselbe Zurückhaltung wie beim Modul-CSV.
                    warnings.Add(messages["PartialImport_EntityFailed",
                        node["name"]?.GetValue<string>() ?? id.ToString(), ex.Message].Value);
                }
            }
        }

        await ChangeLog.RecordProjectActionAsync(
            db, projectId,
            (await db.GameProjects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId, ct))?.Name
                ?? string.Empty,
            ChangeAction.Imported,
            messages["PartialImport_LogDetails", taken, copied].Value,
            ct);

        return new PartialImportResult(taken, copied, choices.Count(c => c.Value == PartialImportChoice.Skip), warnings);
    }

    /// <summary>
    /// Schreibt eine Entität samt ihrer Anhängsel. Vorhandenes wird ersetzt: Übernehmen heißt
    /// „der fremde Stand gilt“, ein Verschmelzen einzelner Felder wäre nicht zu erklären.
    /// </summary>
    private static async Task WriteAsync(
        GameDevManagerDbContext db, Guid projectId, string file, string payload, CancellationToken ct)
    {
        var parcel = JsonSerializer.Deserialize<EntityParcel>(payload, ExportFormat.JsonOptions)
            ?? throw new InvalidOperationException("Der Eintrag ließ sich nicht lesen.");

        var entity = ImportTypes.Read(file, parcel.Entity?.ToJsonString(ExportFormat.JsonOptions) ?? "{}")
            ?? throw new InvalidOperationException($"Zu „{file}“ gibt es keinen bekannten Typ.");

        entity.GameProjectId = projectId;

        // Erst weg, dann neu: Ein Update-Abgleich über alle Kind-Sammlungen hinweg wäre
        // dasselbe noch einmal, nur fehleranfälliger.
        await ImportTypes.DeleteAsync(db, file, entity.Id, ct);
        await EntityCleanup.DeleteForSubObjectsAsync(db, [entity.Id], ct);

        db.Add(entity);

        foreach (var field in parcel.Fields)
        {
            db.FieldDefinitions.Add(field);
        }

        foreach (var value in parcel.Values.Where(value => !value.IsInherited))
        {
            db.FieldValues.Add(value);
        }

        foreach (var set in parcel.Conditions)
        {
            set.GameProjectId = projectId;
            db.ConditionSets.Add(set);
        }

        await db.SaveChangesAsync(ct);
    }

    // ------------------------------------------------------------------------------ Hilfen

    private void EnsureFormat(ZipArchive archive)
    {
        var manifest = ExportFormat.FindManifest(archive)
            ?? throw new ContentValidationException(messages["Import_ManifestMissing"]);

        using var stream = manifest.Open();
        using var document = JsonDocument.Parse(stream);

        var version = document.RootElement.TryGetProperty("formatVersion", out var value)
            ? value.GetInt32()
            : 0;

        if (version != ExportService.FormatVersion)
        {
            throw new ContentValidationException(
                messages["Import_FormatVersionMismatch", version, ExportService.FormatVersion]);
        }
    }

    private async Task<Stream> ExportCurrentAsync(Guid projectId, CancellationToken ct)
    {
        var temp = new FileStream(
            Path.Combine(Path.GetTempPath(), $"gdm-partial-{Guid.NewGuid():N}.zip"),
            FileMode.Create, FileAccess.ReadWrite, FileShare.None, 81920,
            FileOptions.Asynchronous | FileOptions.DeleteOnClose);

        try
        {
            await export.WriteExportAsync(projectId, ExportTarget.Json, includeAssets: false, temp, ct: ct);
            temp.Position = 0;

            return temp;
        }
        catch
        {
            await temp.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Die Entitäten einer Inhaltsdatei, nach GUID. Gelesen wird die <b>erste</b> Liste der
    /// Datei — das ist überall die der Entitäten; was daneben steht (die Beziehungsarten der
    /// NPCs), gehört keiner einzelnen.
    /// </summary>
    private static Dictionary<Guid, JsonNode> ReadEntities(ZipArchive archive, string file)
    {
        var entities = new Dictionary<Guid, JsonNode>();

        foreach (var entry in archive.Entries.Where(candidate =>
            candidate.FullName.EndsWith(ExportFormat.ContentFolder + file, StringComparison.Ordinal)))
        {
            using var stream = entry.Open();

            if (JsonNode.Parse(stream) is not JsonObject root)
            {
                continue;
            }

            foreach (var property in root)
            {
                if (property.Value is not JsonArray array)
                {
                    continue;
                }

                foreach (var node in array.OfType<JsonObject>())
                {
                    if (node["id"]?.GetValue<string>() is { } raw && Guid.TryParse(raw, out var id))
                    {
                        entities[id] = node;
                    }
                }

                // Nur die erste Liste — die zweite trägt Nebendaten des Moduls.
                break;
            }
        }

        return entities;
    }

    private static Attachments ReadAttachments(ZipArchive archive)
    {
        var result = new Attachments();

        foreach (var entry in archive.Entries)
        {
            if (entry.FullName.EndsWith(ExportFormat.ContentFolder + "field-values.json", StringComparison.Ordinal))
            {
                using var stream = entry.Open();
                var file = JsonSerializer.Deserialize<FieldValuesWrapper>(stream, ExportFormat.JsonOptions);
                result.Values.AddRange(file?.Values ?? []);
            }
            else if (entry.FullName.EndsWith(ExportFormat.ContentFolder + "conditions.json", StringComparison.Ordinal))
            {
                using var stream = entry.Open();
                var file = JsonSerializer.Deserialize<ConditionsWrapper>(stream, ExportFormat.JsonOptions);
                result.Conditions.AddRange(file?.ConditionSets ?? []);
            }
            else if (entry.FullName.EndsWith(ExportFormat.ContentFolder + "types-and-fields.json", StringComparison.Ordinal))
            {
                using var stream = entry.Open();
                var file = JsonSerializer.Deserialize<TypesWrapper>(stream, ExportFormat.JsonOptions);
                result.IndividualFields.AddRange(file?.IndividualFields ?? []);
            }
        }

        return result;
    }

    /// <summary>
    /// Packt eine Entität mit allem, was an ihrer GUID hängt, in <b>einen</b> JSON-Text —
    /// damit der GUID-Tausch beim Kopieren jeden Verweis auf einmal trifft.
    /// </summary>
    private static string BuildPayload(JsonNode entity, Attachments attachments, Guid entityId)
    {
        var owners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        GuidRemap.Collect(entity.DeepClone(), owners);

        var ownerIds = owners.Keys.Select(Guid.Parse).ToHashSet();
        ownerIds.Add(entityId);

        // Die Entität als **Knoten** und nicht als eingebetteter Text: Als String wäre sie
        // beim Serialisieren escaped (Anführungszeichen werden zu „\u0022“), und die
        // Wortgrenze im GUID-Muster von GuidRemap fände die GUID dahinter nicht mehr.
        return JsonSerializer.Serialize(new
        {
            entity,
            fields = attachments.IndividualFields
                .Where(field => field.OwnerEntityId is { } owner && ownerIds.Contains(owner)),
            values = attachments.Values.Where(value => ownerIds.Contains(value.OwnerEntityId)),
            conditions = attachments.Conditions.Where(set => ownerIds.Contains(set.OwnerId))
        }, ExportFormat.JsonOptions);
    }

    private static List<string> ChangedProperties(JsonNode mine, JsonNode theirs)
    {
        if (mine is not JsonObject left || theirs is not JsonObject right)
        {
            return [];
        }

        return
        [
            .. left.Select(pair => pair.Key)
                .Union(right.Select(pair => pair.Key), StringComparer.Ordinal)
                .Where(key => !JsonNode.DeepEquals(left[key], right[key]))
                .OrderBy(key => key, StringComparer.Ordinal)
        ];
    }

    private sealed class Attachments
    {
        public List<FieldDefinition> IndividualFields { get; } = [];

        public List<FieldValue> Values { get; } = [];

        public List<ConditionSet> Conditions { get; } = [];
    }

    private sealed class EntityParcel
    {
        public JsonNode? Entity { get; set; }

        public List<FieldDefinition> Fields { get; set; } = [];

        public List<FieldValue> Values { get; set; } = [];

        public List<ConditionSet> Conditions { get; set; } = [];
    }

    private sealed class FieldValuesWrapper { public List<FieldValue> Values { get; set; } = []; }

    private sealed class ConditionsWrapper { public List<ConditionSet> ConditionSets { get; set; } = []; }

    private sealed class TypesWrapper { public List<FieldDefinition> IndividualFields { get; set; } = []; }
}
