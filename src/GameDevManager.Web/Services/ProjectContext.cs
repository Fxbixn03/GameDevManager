using GameDevManager.Data;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GameDevManager.Web.Services;

/// <summary>
/// Liefert das Spielprojekt, in dem die Oberfläche gerade arbeitet. Alle Modul-Inhalte hängen
/// an einem Projekt; welches das aktive ist, hält die <see cref="ProjectSelection"/>
/// installationsweit fest. Dieser Dienst ist je Verbindung (Scoped) und cached das geladene
/// Projekt — die Seiten fragen es bei jedem Laden neu ab, ein Projektwechsel lädt die
/// Anwendung deshalb komplett neu (forceLoad) statt 57 Seiten einzeln zu benachrichtigen.
/// </summary>
public class ProjectContext(
    IDbContextFactory<GameDevManagerDbContext> factory,
    ProjectSelection selection,
    LocalSettingsFile settings)
{
    private GameProject? _current;

    /// <summary>Wird ausgelöst, wenn sich das aktive Projekt dieser Verbindung ändert oder neu geladen werden muss.</summary>
    public event Action? Changed;

    public async Task<GameProject> GetCurrentAsync(CancellationToken ct = default)
    {
        if (_current is not null)
        {
            return _current;
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        if (selection.CurrentId is { } id)
        {
            _current = await db.GameProjects
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id, ct);
        }

        // Nichts gemerkt oder das gemerkte Projekt wurde gelöscht → das älteste. Archivierte
        // stehen hinten an: Sie sollen nicht von selbst wieder aktiv werden — nur wenn es
        // sonst gar keines gäbe, ist ein archiviertes besser als keines.
        _current ??= await db.GameProjects
            .AsNoTracking()
            .OrderBy(p => p.IsArchived)
            .ThenBy(p => p.CreatedAtUtc)
            .ThenBy(p => p.Id)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException(
                "Es ist kein Spielprojekt vorhanden. Beim Start hätte eines angelegt werden müssen.");

        return _current;
    }

    public async Task<Guid> GetCurrentIdAsync(CancellationToken ct = default) =>
        (await GetCurrentAsync(ct)).Id;

    /// <summary>
    /// Wechselt das aktive Projekt der ganzen Installation und merkt es sich über Neustarts
    /// hinweg. Andere offene Verbindungen behalten ihr gecachtes Projekt bis zum nächsten
    /// Neuladen — der Aufrufer sollte deshalb mit <c>forceLoad</c> neu laden.
    /// </summary>
    public async Task SetCurrentAsync(Guid projectId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var project = await db.GameProjects
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == projectId, ct)
            ?? throw new InvalidOperationException("Das gewählte Projekt existiert nicht mehr.");

        selection.CurrentId = project.Id;
        await settings.WriteCurrentProjectAsync(project.Id, ct);

        _current = project;
        Changed?.Invoke();
    }

    /// <summary>
    /// Vergisst das zwischengespeicherte Projekt — nach Umbenennen oder Löschen holt der
    /// nächste Zugriff den frischen Stand aus der Datenbank.
    /// </summary>
    public void Invalidate()
    {
        _current = null;
        Changed?.Invoke();
    }

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
