using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Localization;

namespace GameDevManager.Data.Services;

/// <summary>Ein aufbewahrter Exportstand — die Metadaten kommen aus seinem Manifest.</summary>
public sealed record ExportSnapshot(
    string FileName,
    DateTime ExportedAtUtc,
    long SizeBytes,
    int FormatVersion,
    bool IncludesAssetFiles,
    int EntryCount);

/// <summary>Eine Entität, die zwischen zwei Ständen dazukam, wegfiel oder sich änderte.</summary>
public sealed record SnapshotEntityChange(string Id, string Name, IReadOnlyList<string> ChangedProperties);

/// <summary>Die Unterschiede einer Inhaltsdatei zwischen zwei Ständen.</summary>
public sealed record SnapshotFileDiff(
    string File,
    string? ModuleKey,
    IReadOnlyList<SnapshotEntityChange> Added,
    IReadOnlyList<SnapshotEntityChange> Removed,
    IReadOnlyList<SnapshotEntityChange> Changed);

/// <summary>Der Vergleich zweier Stände. Dateien ohne Unterschiede tauchen nicht auf.</summary>
public sealed record SnapshotDiff(IReadOnlyList<SnapshotFileDiff> Files)
{
    public int AddedCount => Files.Sum(f => f.Added.Count);

    public int RemovedCount => Files.Sum(f => f.Removed.Count);

    public int ChangedCount => Files.Sum(f => f.Changed.Count);

    public bool HasChanges => Files.Count > 0;
}

/// <summary>
/// Die versionierten, diffbaren Exporte des Konzepts: Exportstände werden als ZIP im
/// Dateisystem aufbewahrt (<see cref="ExportStorageOptions"/>) und lassen sich paarweise —
/// oder gegen den aktuellen Stand — vergleichen.
/// <para>
/// Ein Stand ist ein ganz normales Export-ZIP im Json-Layout; es lässt sich also auch
/// herunterladen und über den Import wieder einspielen. Metadaten wie der Zeitpunkt stehen
/// im Manifest des Archivs, nicht in der Datenbank — es gibt bewusst keine eigene Tabelle.
/// </para>
/// <para>
/// Der Diff vergleicht die Inhaltsdateien Entität für Entität über deren GUID. Weil der Export
/// stabil sortiert ist, ist derselbe Stand Byte für Byte derselbe Export — was sich unterscheidet,
/// hat sich wirklich geändert. Gemeldet werden je Datei: dazugekommen, weggefallen und geändert
/// (mit den Namen der geänderten Eigenschaften).
/// </para>
/// <para>
/// Wie lange Stände liegen bleiben, sagt die Aufbewahrung in den <see cref="ExportStorageOptions"/>
/// — siehe <see cref="PruneAsync"/>.
/// </para>
/// </summary>
public partial class ExportSnapshotService(
    ExportService export,
    ExportStorageOptions options,
    PermissionGuard guard,
    IStringLocalizer<DataMessages> messages)
{
    /// <summary>Bewahrt den aktuellen Stand des Projekts als neuen Exportstand auf.</summary>
    public async Task<ExportSnapshot> CreateAsync(
        Guid projectId, bool includeAssets, CancellationToken ct = default)
    {
        // Nur der von Hand angestoßene Stand verlangt das Exportrecht — das Sicherheitsnetz
        // (CreateSafetyNetAsync) läuft über CreateCoreAsync daran vorbei: Es gehört zum
        // Import bzw. Projektlöschen und darf nicht am fehlenden Exportrecht reißen.
        await guard.EnsureCanExportAsync(ct);

        return await CreateCoreAsync(projectId, includeAssets, ct);
    }

    private async Task<ExportSnapshot> CreateCoreAsync(
        Guid projectId, bool includeAssets, CancellationToken ct)
    {
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");

        // Zwei Stände in derselben Sekunde bekommen einen Zähler statt sich zu überschreiben.
        var counter = 0;
        string fileName;
        string path;
        do
        {
            fileName = counter == 0
                ? $"{stamp}-{projectId:N}.zip"
                : $"{stamp}-{counter}-{projectId:N}.zip";
            path = Path.Combine(options.RootPath, fileName);
            counter++;
        }
        while (File.Exists(path));

        await using (var file = File.Create(path))
        {
            await export.WriteExportAsync(projectId, ExportTarget.Json, includeAssets, file, ct);
        }

        var snapshot = ReadSnapshotInfo(path)
            ?? throw new ContentValidationException(messages["Export_SnapshotMissing"].Value);

        // Aufgeräumt wird hier und nicht in der Oberfläche: Das Sicherheitsnetz legt bei jedem
        // ersetzenden Import und jedem Projektlöschen einen Stand an, und was von allein
        // wächst, muss auch von allein wieder abnehmen.
        PruneCore(projectId);

        return snapshot;
    }

    /// <summary>
    /// Räumt die Stände des Projekts nach der eingestellten Aufbewahrung ab
    /// (<see cref="ExportStorageOptions.MaxPerProject"/> und
    /// <see cref="ExportStorageOptions.MaxAgeDays"/>) und liefert die entfernten zurück.
    /// <para>
    /// Läuft nach jedem neu angelegten Stand von selbst; von Hand gerufen wird sie, wenn eine
    /// geänderte Einstellung sofort greifen soll, statt erst beim nächsten Stand.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<ExportSnapshot>> PruneAsync(
        Guid projectId, CancellationToken ct = default)
    {
        // Wie beim Löschen eines einzelnen Standes: Die Stände sind Teil des Exports.
        await guard.EnsureCanExportAsync(ct);

        return PruneCore(projectId);
    }

    /// <summary>
    /// Das eigentliche Aufräumen — ungeprüft, damit es auch hinter dem Sicherheitsnetz läuft.
    /// <para>
    /// Gearbeitet wird auf <see cref="List"/> und nicht auf dem Verzeichnis: Entfernt wird
    /// genau das, was die Historie auch zeigt. Eine fremde oder kaputte Datei im Ordner taucht
    /// dort nicht auf und ist nicht unsere, sie zu löschen.
    /// </para>
    /// <para>
    /// <b>Der jüngste Stand bleibt in jedem Fall stehen</b>, auch wenn er über dem Höchstalter
    /// liegt: Ein Projekt, an dem lange niemand arbeitet, stünde sonst irgendwann ganz ohne
    /// Sicherung da — und genau der letzte Stand ist der, auf den man zurückgeht.
    /// </para>
    /// </summary>
    private IReadOnlyList<ExportSnapshot> PruneCore(Guid projectId)
    {
        if (!options.HasRetentionRule)
        {
            return [];
        }

        var snapshots = List(projectId);
        if (snapshots.Count <= 1)
        {
            return [];
        }

        var oldestAllowed = options.MaxAgeDays > 0
            ? DateTime.UtcNow.AddDays(-options.MaxAgeDays)
            : (DateTime?)null;

        var removed = new List<ExportSnapshot>();

        // Index 0 ist der jüngste Stand — die Schleife beginnt bewusst bei 1.
        for (var index = 1; index < snapshots.Count; index++)
        {
            var snapshot = snapshots[index];

            var tooMany = options.MaxPerProject > 0 && index >= options.MaxPerProject;
            var tooOld = oldestAllowed is not null && snapshot.ExportedAtUtc < oldestAllowed;

            if (!tooMany && !tooOld)
            {
                continue;
            }

            try
            {
                File.Delete(Path.Combine(options.RootPath, snapshot.FileName));
                removed.Add(snapshot);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Ein Stand, der gerade heruntergeladen wird, lässt sich nicht löschen. Das
                // darf weder den Export noch das Aufräumen abbrechen — er bleibt stehen und
                // fällt beim nächsten Lauf.
            }
        }

        return removed;
    }

    /// <summary>
    /// Das Sicherheitsnetz vor einer zerstörenden Aktion — dem ersetzenden Import und dem
    /// Löschen eines Projekts. Es ist derselbe Stand wie <see cref="CreateAsync"/>, immer
    /// <b>mit</b> Asset-Dateien: Was gleich gelöscht wird, muss vollständig
    /// wiederherstellbar sein, und der Wipe nimmt die Dateien mit.
    /// <para>
    /// Scheitert das Anlegen, scheitert auch die Aktion — ein Netz, das reißen darf, ist
    /// keines. Der IO-Fehler wird dabei in eine <see cref="ContentValidationException"/>
    /// umgesetzt, weil die Oberfläche nur diese als Meldung durchreicht.
    /// </para>
    /// </summary>
    public async Task<ExportSnapshot> CreateSafetyNetAsync(Guid projectId, CancellationToken ct = default)
    {
        try
        {
            return await CreateCoreAsync(projectId, includeAssets: true, ct);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ContentValidationException(messages["Export_SafetyNetFailed", ex.Message].Value);
        }
    }

    /// <summary>Alle aufbewahrten Stände des Projekts, neueste zuerst.</summary>
    public List<ExportSnapshot> List(Guid projectId)
    {
        if (!Directory.Exists(options.RootPath))
        {
            return [];
        }

        return
        [
            .. Directory.EnumerateFiles(options.RootPath, $"*{projectId:N}.zip")
                .Where(path => IsValidFileName(Path.GetFileName(path)))
                .Select(ReadSnapshotInfo)
                .Where(snapshot => snapshot is not null)
                .Select(snapshot => snapshot!)
                .OrderByDescending(snapshot => snapshot.ExportedAtUtc)
                .ThenByDescending(snapshot => snapshot.FileName)
        ];
    }

    /// <summary>
    /// Der Zeitpunkt des jüngsten aufbewahrten Standes, oder <c>null</c> wenn es keinen gibt.
    /// <para>
    /// Anders als <see cref="List"/> wird dafür kein Archiv geöffnet: der Zeitstempel steht
    /// bereits im Dateinamen, und er ist derselbe, den das Manifest trägt — beide entstehen in
    /// <see cref="CreateAsync"/> aus <see cref="DateTime.UtcNow"/>. Für die Projektleiste des
    /// Dashboards, die bei jedem Aufruf lädt, wäre das Öffnen jedes ZIPs unangemessen teuer.
    /// </para>
    /// </summary>
    public DateTime? FindLatestExportedAtUtc(Guid projectId)
    {
        if (!Directory.Exists(options.RootPath))
        {
            return null;
        }

        DateTime? latest = null;

        foreach (var path in Directory.EnumerateFiles(options.RootPath, $"*{projectId:N}.zip"))
        {
            var fileName = Path.GetFileName(path);
            if (!IsValidFileName(fileName))
            {
                continue;
            }

            // Der Name beginnt immer mit „yyyyMMdd-HHmmss" — das stellt die Prüfung oben sicher.
            if (!DateTime.TryParseExact(
                    fileName[..15], "yyyyMMdd-HHmmss",
                    CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var exportedAt))
            {
                continue;
            }

            if (latest is null || exportedAt > latest)
            {
                latest = exportedAt;
            }
        }

        return latest;
    }

    /// <summary>
    /// Öffnet einen Stand zum Herunterladen, oder <c>null</c> wenn es ihn nicht (mehr) gibt.
    /// Der Name kommt aus dem Browser und wird deshalb streng geprüft.
    /// </summary>
    public Stream? OpenRead(string fileName)
    {
        if (!IsValidFileName(fileName))
        {
            return null;
        }

        var path = Path.Combine(options.RootPath, fileName);
        return File.Exists(path) ? File.OpenRead(path) : null;
    }

    /// <summary>Entfernt einen Stand. Ein bereits fehlender ist kein Fehler.</summary>
    public async Task DeleteAsync(string fileName, CancellationToken ct = default)
    {
        // Die Stände sind Teil des Exports — wer ihn nicht nutzen darf, räumt ihn auch
        // nicht ab. Ein Dateisystem-Vorgang, den kein Interceptor sieht.
        await guard.EnsureCanExportAsync(ct);

        if (!IsValidFileName(fileName))
        {
            throw new ContentValidationException(messages["Export_SnapshotInvalidName"].Value);
        }

        var path = Path.Combine(options.RootPath, fileName);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Vergleicht zwei Stände. <c>null</c> als Dateiname steht für den aktuellen Stand des
    /// Projekts — der wird dafür flüchtig exportiert (ohne Asset-Dateien, verglichen wird
    /// nur der Inhalt).
    /// </summary>
    public async Task<SnapshotDiff> DiffAsync(
        Guid projectId, string? leftFileName, string? rightFileName, CancellationToken ct = default)
    {
        await using var leftStream = await OpenSnapshotOrCurrentAsync(projectId, leftFileName, ct);
        await using var rightStream = await OpenSnapshotOrCurrentAsync(projectId, rightFileName, ct);

        using var leftArchive = new ZipArchive(leftStream, ZipArchiveMode.Read, leaveOpen: true);
        using var rightArchive = new ZipArchive(rightStream, ZipArchiveMode.Read, leaveOpen: true);

        var leftFiles = ReadContentFiles(leftArchive);
        var rightFiles = ReadContentFiles(rightArchive);

        // Bekannte Dateien in der Reihenfolge des Exports, Unbekanntes alphabetisch dahinter.
        var knownOrder = ExportFormat.ContentFileModules.Keys.ToList();
        var fileNames = leftFiles.Keys.Union(rightFiles.Keys, StringComparer.Ordinal)
            .OrderBy(name => knownOrder.IndexOf(name) is var index && index >= 0 ? index : int.MaxValue)
            .ThenBy(name => name, StringComparer.Ordinal);

        var files = new List<SnapshotFileDiff>();

        foreach (var fileName in fileNames)
        {
            var left = leftFiles.GetValueOrDefault(fileName);
            var right = rightFiles.GetValueOrDefault(fileName);

            var leftEntities = CollectEntities(left);
            var rightEntities = CollectEntities(right);

            var added = new List<SnapshotEntityChange>();
            var removed = new List<SnapshotEntityChange>();
            var changed = new List<SnapshotEntityChange>();

            foreach (var (id, (name, node)) in rightEntities)
            {
                if (!leftEntities.TryGetValue(id, out var previous))
                {
                    added.Add(new SnapshotEntityChange(id, name, []));
                }
                else if (!JsonNode.DeepEquals(previous.Node, node))
                {
                    changed.Add(new SnapshotEntityChange(id, name, ChangedProperties(previous.Node, node)));
                }
            }

            foreach (var (id, (name, _)) in leftEntities)
            {
                if (!rightEntities.ContainsKey(id))
                {
                    removed.Add(new SnapshotEntityChange(id, name, []));
                }
            }

            if (added.Count == 0 && removed.Count == 0 && changed.Count == 0)
            {
                continue;
            }

            static List<SnapshotEntityChange> Sorted(List<SnapshotEntityChange> list) =>
                [.. list.OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(e => e.Id)];

            files.Add(new SnapshotFileDiff(
                fileName,
                ExportFormat.ContentFileModules.GetValueOrDefault(fileName),
                Sorted(added),
                Sorted(removed),
                Sorted(changed)));
        }

        return new SnapshotDiff(files);
    }

    private async Task<Stream> OpenSnapshotOrCurrentAsync(
        Guid projectId, string? fileName, CancellationToken ct)
    {
        if (fileName is not null)
        {
            return OpenRead(fileName)
                ?? throw new ContentValidationException(messages["Export_SnapshotMissing"].Value);
        }

        // Der aktuelle Stand: flüchtig in eine Temp-Datei exportieren, die sich beim
        // Schließen selbst aufräumt.
        var temp = new FileStream(
            Path.Combine(Path.GetTempPath(), $"gdm-diff-{Guid.NewGuid():N}.zip"),
            FileMode.Create, FileAccess.ReadWrite, FileShare.None, 81920,
            FileOptions.Asynchronous | FileOptions.DeleteOnClose);

        try
        {
            await export.WriteExportAsync(projectId, ExportTarget.Json, includeAssets: false, temp, ct);
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
    /// Liest die Inhaltsdateien eines Archivs: Dateiname (ohne Präfix und Ordner) → JSON.
    /// </summary>
    private Dictionary<string, JsonNode> ReadContentFiles(ZipArchive archive)
    {
        var manifest = ExportFormat.FindManifest(archive)
            ?? throw new ContentValidationException(messages["Import_ManifestMissing"].Value);
        var contentPrefix = manifest.FullName[..^ExportFormat.ManifestFileName.Length]
            + ExportFormat.ContentFolder;

        var files = new Dictionary<string, JsonNode>(StringComparer.Ordinal);

        foreach (var entry in archive.Entries)
        {
            if (!entry.FullName.StartsWith(contentPrefix, StringComparison.Ordinal)
                || !entry.FullName.EndsWith(".json", StringComparison.Ordinal))
            {
                continue;
            }

            var name = entry.FullName[contentPrefix.Length..];
            if (name.Contains('/'))
            {
                continue;
            }

            using var stream = entry.Open();
            if (JsonNode.Parse(stream) is { } node)
            {
                files[name] = node;
            }
        }

        return files;
    }

    /// <summary>
    /// Sammelt alle Entitäten einer Inhaltsdatei ein: jede Datei ist ein Objekt aus einer
    /// oder mehreren Listen (player.json etwa trägt drei), jedes Element hat eine <c>id</c>.
    /// </summary>
    private static Dictionary<string, (string Name, JsonNode Node)> CollectEntities(JsonNode? root)
    {
        var entities = new Dictionary<string, (string, JsonNode)>(StringComparer.OrdinalIgnoreCase);

        if (root is not JsonObject rootObject)
        {
            return entities;
        }

        foreach (var (_, value) in rootObject)
        {
            if (value is not JsonArray array)
            {
                continue;
            }

            foreach (var element in array)
            {
                if (element is not JsonObject entity
                    || entity["id"]?.GetValue<string>() is not { } id)
                {
                    continue;
                }

                var name = entity["name"] is { } nameNode && nameNode.GetValueKind() == JsonValueKind.String
                    ? nameNode.GetValue<string>()
                    : string.Empty;

                entities[id] = (name, entity);
            }
        }

        return entities;
    }

    /// <summary>Die obersten Eigenschaften, in denen sich zwei Fassungen unterscheiden.</summary>
    private static List<string> ChangedProperties(JsonNode? left, JsonNode? right)
    {
        var leftObject = left as JsonObject;
        var rightObject = right as JsonObject;

        var keys = (leftObject?.Select(p => p.Key) ?? [])
            .Union(rightObject?.Select(p => p.Key) ?? [], StringComparer.Ordinal);

        return
        [
            .. keys
                .Where(key => !JsonNode.DeepEquals(leftObject?[key], rightObject?[key]))
                .OrderBy(key => key, StringComparer.Ordinal)
        ];
    }

    private ExportSnapshot? ReadSnapshotInfo(string path)
    {
        try
        {
            var size = new FileInfo(path).Length;

            using var file = File.OpenRead(path);
            using var archive = new ZipArchive(file, ZipArchiveMode.Read);

            var manifestEntry = ExportFormat.FindManifest(archive);
            if (manifestEntry is null)
            {
                return null;
            }

            using var manifestStream = manifestEntry.Open();
            using var manifest = JsonDocument.Parse(manifestStream);
            var root = manifest.RootElement;

            var entryCount = 0;
            if (root.TryGetProperty("counts", out var counts) && counts.ValueKind == JsonValueKind.Object)
            {
                entryCount = counts.EnumerateObject().Sum(property => property.Value.GetInt32());
            }

            return new ExportSnapshot(
                Path.GetFileName(path),
                root.TryGetProperty("exportedAtUtc", out var exportedAt) ? exportedAt.GetDateTime() : default,
                size,
                root.TryGetProperty("formatVersion", out var version) ? version.GetInt32() : 0,
                root.TryGetProperty("includesAssetFiles", out var flag) && flag.GetBoolean(),
                entryCount);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException)
        {
            // Eine fremde oder kaputte Datei im Verzeichnis macht die Historie nicht kaputt —
            // sie taucht schlicht nicht auf.
            return null;
        }
    }

    /// <summary>
    /// Zeitstempel, optionaler Zähler, Projekt-GUID — mehr darf ein Dateiname nicht enthalten.
    /// Damit ist auch jeder Pfadausbruch ausgeschlossen, die Namen kommen aus dem Browser.
    /// </summary>
    private static bool IsValidFileName(string fileName) => SnapshotFileName().IsMatch(fileName);

    [GeneratedRegex(@"^\d{8}-\d{6}(-\d+)?-[0-9a-f]{32}\.zip$")]
    private static partial Regex SnapshotFileName();
}
