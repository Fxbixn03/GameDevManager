using GameDevManager.Data;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GameDevManager.Web.Services;

/// <summary>
/// Liefert das Spielprojekt, in dem die Oberfläche gerade arbeitet. Alle Modul-Inhalte hängen
/// an einem Projekt, damit sich später mehrere Spiele nebeneinander verwalten lassen.
/// <para>
/// Eine Projektauswahl gibt es noch nicht — bis dahin arbeitet die Anwendung auf dem beim
/// Start angelegten Standardprojekt. Sobald das Projekt-Modul kommt, ändert sich nur dieser
/// Dienst, nicht die aufrufenden Seiten.
/// </para>
/// </summary>
public class ProjectContext(IDbContextFactory<GameDevManagerDbContext> factory)
{
    private GameProject? _current;

    public async Task<GameProject> GetCurrentAsync(CancellationToken ct = default)
    {
        if (_current is not null)
        {
            return _current;
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        _current = await db.GameProjects
            .AsNoTracking()
            .OrderBy(p => p.CreatedAtUtc)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException(
                "Es ist kein Spielprojekt vorhanden. Beim Start hätte eines angelegt werden müssen.");

        return _current;
    }

    public async Task<Guid> GetCurrentIdAsync(CancellationToken ct = default) =>
        (await GetCurrentAsync(ct)).Id;

    /// <summary>
    /// Legt beim Start ein Standardprojekt an, falls die Datenbank noch leer ist. Beim Start
    /// aufgerufen und nicht beim ersten Seitenaufruf, damit nicht zwei gleichzeitige Verbindungen
    /// zwei Projekte erzeugen.
    /// </summary>
    public static async Task EnsureDefaultProjectAsync(
        IDbContextFactory<GameDevManagerDbContext> factory, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        if (await db.GameProjects.AnyAsync(ct))
        {
            return;
        }

        db.GameProjects.Add(new GameProject
        {
            Name = "Standardprojekt",
            Description = "Automatisch angelegt beim ersten Start."
        });

        await db.SaveChangesAsync(ct);
    }
}
