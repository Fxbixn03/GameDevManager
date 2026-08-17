using System.IO.Compression;
using System.Text.Json;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace GameDevManager.Data.Services;

/// <summary>Was beim Wiederherstellen einer Installation angekommen ist.</summary>
public sealed record InstallationRestoreResult(
    int Projects, int Users, int Roles, int ApiKeys, IReadOnlyList<string> Warnings);

/// <summary>
/// Sicherung und Wiederherstellung der <b>ganzen Installation</b> (F44) — alle Projekte plus
/// das, was bewusst in keinem Projekt-Export steht: Benutzer, Rollen, API-Schlüssel,
/// Kanban-Boards, Whiteboards, Änderungsprotokoll, Papierkorb, Favoriten, gespeicherte
/// Ansichten, eigene Regeln, Webhooks und die Moduleinstellungen.
/// <para>
/// Das Archiv enthält je Projekt das <b>normale Export-ZIP</b> unter <c>projects/</c> — der
/// Projekt-Export ist erprobt, versioniert und diffbar, und ihn hier nachzubauen hieße, zwei
/// Formate für dieselbe Sache zu pflegen. Daneben liegt unter <c>installation/</c> genau das,
/// was ihm fehlt.
/// </para>
/// <para>
/// <b>Die Verbindungszeichenfolge geht nicht mit.</b> Sie beschreibt den Server, von dem man
/// gerade wegzieht, und ist das einzige Geheimnis in der Konfiguration — ein Archiv, das man
/// weitergibt, soll es nicht enthalten. Passwort-Hashes dagegen schon: Ohne sie käme nach dem
/// Umzug niemand mehr herein, und das ist der Zweck der Übung.
/// </para>
/// </summary>
public class InstallationBackupService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    ExportService export,
    ImportService import,
    PermissionGuard guard,
    IStringLocalizer<DataMessages> messages)
{
    /// <summary>
    /// Version des Installations-Archivs. Eigenes Format und eigene Zählung — es enthält
    /// Projekt-Exporte, deren <c>FormatVersion</c> unabhängig davon weiterläuft.
    /// </summary>
    public const int BackupVersion = 1;

    private const string ManifestFileName = "installation.json";
    private const string ProjectsFolder = "projects/";
    private const string DataFileName = "installation/data.json";

    // ---------------------------------------------------------------------------- Sichern

    /// <summary>Schreibt die gesamte Installation als ZIP.</summary>
    public async Task WriteBackupAsync(Stream output, CancellationToken ct = default)
    {
        await guard.EnsureCanExportAsync(ct);

        // Erst in eine Temp-Datei: ZipArchive schließt seine Einträge synchron ab, und der
        // Response-Stream von ASP.NET Core lässt synchrone Schreibzugriffe nicht zu — dieselbe
        // Strecke wie beim Projekt-Export.
        var tempPath = Path.Combine(Path.GetTempPath(), $"gdm-backup-{Guid.NewGuid():N}.zip");

        await using var temp = new FileStream(
            tempPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 81920,
            FileOptions.Asynchronous | FileOptions.DeleteOnClose);

        await BuildAsync(temp, ct);

        temp.Position = 0;
        await temp.CopyToAsync(output, ct);
    }

    private async Task BuildAsync(Stream zipStream, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var projects = await db.GameProjects.AsNoTracking().OrderBy(p => p.Name).ToListAsync(ct);

        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true);

        async Task WriteJsonAsync(string path, object payload)
        {
            var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
            await using var entryStream = entry.Open();
            await JsonSerializer.SerializeAsync(entryStream, payload, ExportFormat.JsonOptions, ct);
        }

        await WriteJsonAsync(ManifestFileName, new
        {
            backupVersion = BackupVersion,
            formatVersion = ExportService.FormatVersion,
            createdAtUtc = DateTime.UtcNow,
            projects = projects.Select(project => new { project.Id, project.Name })
        });

        foreach (var project in projects)
        {
            // Je Projekt das normale Export-ZIP, eingebettet. Mit Assets — eine Sicherung ohne
            // die Dateien wäre keine.
            var entry = archive.CreateEntry($"{ProjectsFolder}{project.Id:D}.zip", CompressionLevel.NoCompression);
            await using var entryStream = entry.Open();

            await export.WriteExportAsync(
                project.Id, ExportTarget.Json, includeAssets: true, entryStream, ct: ct);
        }

        await WriteJsonAsync(DataFileName, await CollectAsync(db, ct));
    }

    /// <summary>
    /// Alles, was in keinem Projekt-Export steht. Bewusst <b>nach</b> den Projekt-Exporten
    /// eingesammelt: Wer die Reihenfolge dreht, sichert Werkzeug-Daten zu einem Bestand, den
    /// die Exporte noch nicht kennen.
    /// </summary>
    private static async Task<InstallationData> CollectAsync(
        GameDevManagerDbContext db, CancellationToken ct) => new()
    {
        Users = await db.AppUsers.AsNoTracking().OrderBy(u => u.UserName).ToListAsync(ct),
        Roles = await db.UserRoles.AsNoTracking().OrderBy(r => r.Name).ToListAsync(ct),
        ApiKeys = await db.ApiKeys.AsNoTracking().OrderBy(k => k.Name).ToListAsync(ct),
        Boards = await db.KanbanBoards.AsNoTracking()
            .Include(board => board.Columns).ThenInclude(column => column.Cards)
            .OrderBy(board => board.Name).ToListAsync(ct),
        Whiteboards = await db.Whiteboards.AsNoTracking()
            .Include(board => board.Notes)
            .Include(board => board.Strokes)
            .OrderBy(board => board.Name).ToListAsync(ct),
        ChangeLog = await db.ChangeLogEntries.AsNoTracking().OrderBy(e => e.AtUtc).ToListAsync(ct),
        RecycleBin = await db.RecycleBinEntries.AsNoTracking().OrderBy(e => e.DeletedAtUtc).ToListAsync(ct),
        Pins = await db.UserPins.AsNoTracking().ToListAsync(ct),
        Views = await db.SavedViews.AsNoTracking().OrderBy(v => v.Name).ToListAsync(ct),
        Rules = await db.ContentRules.AsNoTracking().OrderBy(r => r.SortOrder).ToListAsync(ct),
        Webhooks = await db.Webhooks.AsNoTracking().OrderBy(h => h.Name).ToListAsync(ct),
        Comments = await db.ContentComments.AsNoTracking().OrderBy(c => c.CreatedAtUtc).ToListAsync(ct),
        ModuleSettings = await db.ModuleSettings.AsNoTracking().ToListAsync(ct),
        DashboardCards = await db.DashboardCards.AsNoTracking().ToListAsync(ct)
    };

    // -------------------------------------------------------------------- Wiederherstellen

    /// <summary>
    /// Spielt ein Installations-Archiv zurück. <b>Alles wird ersetzt</b> — der Zweck ist der
    /// Umzug auf einen neuen Server, nicht das Zusammenführen zweier Bestände.
    /// <para>
    /// Dieselbe Vorsicht wie beim ersetzenden Import: Jedes bestehende Projekt bekommt vorher
    /// seinen Sicherheitsnetz-Stand, und das übernimmt der <see cref="ImportService"/> von
    /// selbst — er wird hier je Projekt aufgerufen.
    /// </para>
    /// </summary>
    public async Task<InstallationRestoreResult> RestoreAsync(
        Stream backup, CancellationToken ct = default)
    {
        await guard.EnsureCanImportAsync(ct);

        using var archive = new ZipArchive(backup, ZipArchiveMode.Read, leaveOpen: true);

        var manifest = archive.GetEntry(ManifestFileName)
            ?? throw new ContentValidationException(messages["Backup_ManifestMissing"]);

        int backupVersion;

        await using (var stream = manifest.Open())
        {
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            backupVersion = document.RootElement.TryGetProperty("backupVersion", out var version)
                ? version.GetInt32()
                : 0;
        }

        if (backupVersion != BackupVersion)
        {
            throw new ContentValidationException(
                messages["Backup_VersionMismatch", backupVersion, BackupVersion]);
        }

        var warnings = new List<string>();
        var projects = 0;

        await using var db = await factory.CreateDbContextAsync(ct);

        foreach (var entry in archive.Entries
            .Where(candidate => candidate.FullName.StartsWith(ProjectsFolder, StringComparison.Ordinal)
                && candidate.FullName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            .OrderBy(candidate => candidate.FullName, StringComparer.Ordinal))
        {
            if (!Guid.TryParse(Path.GetFileNameWithoutExtension(entry.FullName), out var projectId))
            {
                warnings.Add(messages["Backup_UnknownProjectFile", entry.FullName]);
                continue;
            }

            // Ein Projekt, das es hier noch nicht gibt, entsteht leer — Name und Beschreibung
            // holt der Import gleich aus dem Manifest des Projekt-Archivs.
            if (!await db.GameProjects.AnyAsync(project => project.Id == projectId, ct))
            {
                db.GameProjects.Add(new GameProject { Id = projectId, Name = projectId.ToString("D") });
                await db.SaveChangesAsync(ct);
            }

            // Der Strom eines ZIP-Eintrags ist nicht suchbar; der Import braucht das.
            using var buffer = new MemoryStream();

            await using (var source = entry.Open())
            {
                await source.CopyToAsync(buffer, ct);
            }

            buffer.Position = 0;

            var result = await import.ImportAsync(projectId, buffer, replaceExisting: true, ct);

            warnings.AddRange(result.Warnings);
            projects++;
        }

        var data = await ReadDataAsync(archive, ct);
        await RestoreDataAsync(db, data, ct);

        return new InstallationRestoreResult(
            projects, data.Users.Count, data.Roles.Count, data.ApiKeys.Count, warnings);
    }

    private static async Task<InstallationData> ReadDataAsync(ZipArchive archive, CancellationToken ct)
    {
        var entry = archive.GetEntry(DataFileName);

        if (entry is null)
        {
            return new InstallationData();
        }

        await using var stream = entry.Open();

        return await JsonSerializer.DeserializeAsync<InstallationData>(
            stream, ExportFormat.JsonOptions, ct) ?? new InstallationData();
    }

    private static async Task RestoreDataAsync(
        GameDevManagerDbContext db, InstallationData data, CancellationToken ct)
    {
        // Kein Änderungsprotokoll: Das Zurückspielen einer Installation ist ein Vorgang, kein
        // Bestand an Änderungen — und das Protokoll kommt gleich selbst aus dem Archiv.
        db.SuppressChangeLog = true;

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        // Erst leeren, dann füllen — in der Reihenfolge der Abhängigkeiten: Was auf Benutzer
        // zeigt, fällt zuerst, sonst blockiert der Fremdschlüssel.
        await db.SavedViews.ExecuteDeleteAsync(ct);
        await db.UserPins.ExecuteDeleteAsync(ct);
        await db.ApiKeys.ExecuteDeleteAsync(ct);
        await db.KanbanCards.ExecuteDeleteAsync(ct);
        await db.KanbanColumns.ExecuteDeleteAsync(ct);
        await db.KanbanBoards.ExecuteDeleteAsync(ct);
        await db.WhiteboardNotes.ExecuteDeleteAsync(ct);
        await db.WhiteboardStrokes.ExecuteDeleteAsync(ct);
        await db.Whiteboards.ExecuteDeleteAsync(ct);
        await db.ChangeLogEntries.ExecuteDeleteAsync(ct);
        await db.RecycleBinEntries.ExecuteDeleteAsync(ct);
        await db.ContentRules.ExecuteDeleteAsync(ct);
        await db.Webhooks.ExecuteDeleteAsync(ct);
        await db.ContentComments.ExecuteDeleteAsync(ct);
        await db.ModuleSettings.ExecuteDeleteAsync(ct);
        await db.DashboardCards.ExecuteDeleteAsync(ct);

        // Die Konten zuletzt löschen und zuerst wieder anlegen: Rollen und Ansichten hängen
        // über echte Fremdschlüssel daran.
        await db.AppUsers.ExecuteDeleteAsync(ct);
        await db.UserRoles.ExecuteDeleteAsync(ct);

        db.UserRoles.AddRange(data.Roles);
        db.AppUsers.AddRange(data.Users);
        db.ApiKeys.AddRange(data.ApiKeys);
        db.KanbanBoards.AddRange(data.Boards);
        db.Whiteboards.AddRange(data.Whiteboards);
        db.ChangeLogEntries.AddRange(data.ChangeLog);
        db.RecycleBinEntries.AddRange(data.RecycleBin);
        db.UserPins.AddRange(data.Pins);
        db.SavedViews.AddRange(data.Views);
        db.ContentRules.AddRange(data.Rules);
        db.Webhooks.AddRange(data.Webhooks);
        db.ContentComments.AddRange(data.Comments);
        db.ModuleSettings.AddRange(data.ModuleSettings);
        db.DashboardCards.AddRange(data.DashboardCards);

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    /// <summary>
    /// Der Träger für <c>installation/data.json</c> — alles, was in keinem Projekt-Export
    /// steht. Öffentlich, weil der Serialisierer ihn in beide Richtungen braucht.
    /// </summary>
    public sealed class InstallationData
    {
        public List<AppUser> Users { get; set; } = [];

        public List<UserRole> Roles { get; set; } = [];

        public List<ApiKey> ApiKeys { get; set; } = [];

        public List<KanbanBoard> Boards { get; set; } = [];

        public List<Whiteboard> Whiteboards { get; set; } = [];

        public List<ChangeLogEntry> ChangeLog { get; set; } = [];

        public List<RecycleBinEntry> RecycleBin { get; set; } = [];

        public List<UserPin> Pins { get; set; } = [];

        public List<SavedView> Views { get; set; } = [];

        public List<ContentRule> Rules { get; set; } = [];

        public List<Webhook> Webhooks { get; set; } = [];

        public List<ContentComment> Comments { get; set; } = [];

        public List<ModuleSetting> ModuleSettings { get; set; } = [];

        public List<DashboardCard> DashboardCards { get; set; } = [];
    }
}
