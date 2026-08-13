using GameDevManager.Data.Assets;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace GameDevManager.Data.Services;

/// <summary>
/// Verwaltung der Spielprojekte: anlegen, umbenennen, löschen. Das Löschen entfernt den
/// kompletten Inhaltsbestand über denselben Wipe wie der ersetzende Import — Feldwerte,
/// individuelle Felder, Bedingungen und Assets hängen ohne Fremdschlüssel am Projekt und
/// blieben sonst als Waisen zurück. Namen sind installationsweit eindeutig, weil die
/// Projektauswahl sonst zwei gleichnamige Einträge zeigte.
/// </summary>
public class ProjectService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    IAssetStorage storage,
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

    public async Task DeleteProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var project = await db.GameProjects.FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project is null)
        {
            return;
        }

        // Mindestens ein Projekt muss bestehen bleiben — die ganze Oberfläche arbeitet
        // auf dem aktuellen Projekt und hätte sonst keines mehr.
        if (await db.GameProjects.CountAsync(ct) <= 1)
        {
            throw new ContentValidationException(messages["ProjectLastOne"].Value);
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
}
