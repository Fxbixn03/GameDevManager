using GameDevManager.Data.Assets;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace GameDevManager.Data.Services;

/// <summary>
/// Verwaltung der Spielprojekte: anlegen, umbenennen, duplizieren, löschen. Das Löschen
/// entfernt den kompletten Inhaltsbestand über denselben Wipe wie der ersetzende Import —
/// Feldwerte, individuelle Felder, Bedingungen und Assets hängen ohne Fremdschlüssel am
/// Projekt und blieben sonst als Waisen zurück; davor legt es einen Exportstand als
/// Sicherheitsnetz an. Namen sind installationsweit eindeutig, weil die Projektauswahl
/// sonst zwei gleichnamige Einträge zeigte.
/// </summary>
public class ProjectService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    IAssetStorage storage,
    ExportService export,
    ImportService import,
    ExportSnapshotService snapshots,
    IStringLocalizer<DataMessages> messages)
{
    public async Task<List<GameProject>> GetProjectsAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.GameProjects
            .AsNoTracking()
            .OrderBy(p => p.CreatedAtUtc)
            .ThenBy(p => p.Id)
            .ToListAsync(ct);
    }

    public async Task SaveProjectAsync(GameProject project, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(project.Name))
        {
            throw new ContentValidationException(messages["ProjectNameRequired"].Value);
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        var name = project.Name.Trim();
        var lowered = name.ToLower();

        if (await db.GameProjects.AnyAsync(p => p.Id != project.Id && p.Name.ToLower() == lowered, ct))
        {
            throw new ContentValidationException(messages["ProjectNameExists", name].Value);
        }

        var description = string.IsNullOrWhiteSpace(project.Description) ? null : project.Description.Trim();
        var existing = await db.GameProjects.FirstOrDefaultAsync(p => p.Id == project.Id, ct);

        if (existing is null)
        {
            project.Name = name;
            project.Description = description;
            db.GameProjects.Add(project);
        }
        else
        {
            existing.Name = name;
            existing.Description = description;
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Legt eine vollständige Kopie eines Projekts an — für Vorlagen und für Experimente, die
    /// den Bestand nicht anfassen sollen.
    /// <para>
    /// Gearbeitet wird über die vorhandene Export→Import-Strecke: Das Original wird flüchtig
    /// exportiert, <see cref="ProjectDuplication"/> tauscht alle GUIDs, und der Import spielt
    /// das Ergebnis in das frisch angelegte, leere Projekt. Ein eigener Kopierpfad durch
    /// zwei Dutzend Tabellen wäre die zweite Stelle, an der jedes neue Modul nachgetragen
    /// werden müsste — vergisst man sie, fehlt das Modul in der Kopie stillschweigend.
    /// </para>
    /// <para>
    /// Nicht mitkopiert wird die Werkzeug-Konfiguration (Module an/aus, Anordnung der
    /// Dashboard-Bänder): Sie steht wie beim Export bewusst nicht im Archiv. Die Kopie
    /// beginnt damit bei den Vorgaben.
    /// </para>
    /// </summary>
    public async Task<GameProject> DuplicateProjectAsync(
        Guid sourceId, string name, string? description, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ContentValidationException(messages["ProjectNameRequired"].Value);
        }

        var copyName = name.Trim();
        var lowered = copyName.ToLower();
        var copyDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

        var copy = new GameProject { Name = copyName, Description = copyDescription };

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            if (!await db.GameProjects.AnyAsync(p => p.Id == sourceId, ct))
            {
                throw new ContentValidationException(messages["Export_ProjectMissing"].Value);
            }

            if (await db.GameProjects.AnyAsync(p => p.Name.ToLower() == lowered, ct))
            {
                throw new ContentValidationException(messages["ProjectNameExists", copyName].Value);
            }

            db.GameProjects.Add(copy);
            await db.SaveChangesAsync(ct);
        }

        try
        {
            await using var exported = CreateTempFile("copy-source");
            await export.WriteExportAsync(sourceId, ExportTarget.Json, includeAssets: true, exported, ct);
            exported.Position = 0;

            await using var rewritten = CreateTempFile("copy-target");
            ProjectDuplication.WriteCopy(exported, rewritten, copyName, copyDescription);
            rewritten.Position = 0;

            await import.ImportAsync(copy.Id, rewritten, replaceExisting: false, ct);

            // Woher die Kopie stammt, weiß nur diese Stelle — der Import sieht nur ein Archiv.
            await using var log = await factory.CreateDbContextAsync(ct);
            var sourceName = await log.GameProjects
                .Where(p => p.Id == sourceId)
                .Select(p => p.Name)
                .FirstOrDefaultAsync(ct) ?? copyName;

            await ChangeLog.RecordProjectActionAsync(
                log, copy.Id, copyName, ChangeAction.Created,
                messages["ChangeLog_ProjectDuplicated", sourceName].Value, ct);
        }
        catch
        {
            // Eine halbe Kopie ist schlimmer als keine: Sie sähe in der Projektliste vollständig
            // aus. Das leere Gerüst wird deshalb wieder abgeräumt, die Ursache weitergereicht.
            await RemoveProjectAsync(copy.Id, ct);
            throw;
        }

        return copy;
    }

    public async Task DeleteProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            if (!await db.GameProjects.AnyAsync(p => p.Id == projectId, ct))
            {
                return;
            }

            // Mindestens ein Projekt muss bestehen bleiben — die ganze Oberfläche arbeitet
            // auf dem aktuellen Projekt und hätte sonst keines mehr.
            if (await db.GameProjects.CountAsync(ct) <= 1)
            {
                throw new ContentValidationException(messages["ProjectLastOne"].Value);
            }

            // Sicherheitsnetz: Der Bestand bleibt als Exportstand erhalten und lässt sich in
            // ein neues Projekt zurückspielen. Ein leeres Projekt braucht keines.
            if (await ImportService.HasContentAsync(db, projectId, ct))
            {
                await snapshots.CreateSafetyNetAsync(projectId, ct);
            }
        }

        await RemoveProjectAsync(projectId, ct);
    }

    /// <summary>
    /// Entfernt Projekt und Bestand ohne jede Prüfung. Getrennt von
    /// <see cref="DeleteProjectAsync"/>, weil das Aufräumen einer gescheiterten Kopie weder
    /// den Schutz des letzten Projekts noch ein Sicherheitsnetz braucht — es gäbe nichts
    /// zu sichern.
    /// </summary>
    private async Task RemoveProjectAsync(Guid projectId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var project = await db.GameProjects.FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project is null)
        {
            return;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var assetKeys = await ImportService.WipeProjectAsync(db, projectId, ct);
        db.GameProjects.Remove(project);

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        // Erst nach dem Commit — bei einem Rollback wäre der Bestand sonst ohne Dateien.
        foreach (var storageKey in assetKeys)
        {
            storage.Delete(storageKey);
        }
    }

    /// <summary>
    /// Eine Temp-Datei, die sich beim Schließen selbst aufräumt — wie beim Export und beim
    /// Import: ZipArchive braucht wahlfreien Zugriff, ein Speicherstrom hielte den ganzen
    /// Projektstand samt Assets im Arbeitsspeicher.
    /// </summary>
    private static FileStream CreateTempFile(string purpose) => new(
        Path.Combine(Path.GetTempPath(), $"gdm-{purpose}-{Guid.NewGuid():N}.zip"),
        FileMode.Create, FileAccess.ReadWrite, FileShare.None, 81920,
        FileOptions.Asynchronous | FileOptions.DeleteOnClose);
}
