using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GameDevManager.Data.Services;

/// <summary>
/// Was die Modul-Dienste selbst ins Änderungsprotokoll eintragen müssen: Löschungen.
/// <para>
/// Neues und Geändertes sieht der <see cref="ChangeLogInterceptor"/> von allein. Gelöscht wird
/// dagegen über <c>ExecuteDeleteAsync</c> — das läuft am Änderungsverfolger vorbei direkt in
/// die Datenbank, und danach ist nichts mehr da, dessen Namen man notieren könnte. Deshalb
/// eine Zeile im Dienst, unmittelbar vor dem Löschen und innerhalb derselben Transaktion.
/// </para>
/// </summary>
public static class ChangeLog
{
    /// <summary>
    /// Hält fest, dass diese Entität gelöscht wird. Der Eintrag entsteht sofort, damit er auch
    /// dann steht, wenn der Dienst danach nichts mehr speichert; wer gehandelt hat, trägt der
    /// <see cref="ChangeLogInterceptor"/> nach — den Benutzer kennt nur er.
    /// <para>
    /// Gibt es die Entität nicht mehr, geschieht nichts: Zweimal löschen ist kein Vorgang.
    /// </para>
    /// </summary>
    public static Task RecordDeletionAsync<TEntity>(
        GameDevManagerDbContext db, DbSet<TEntity> set, Guid entityId, CancellationToken ct)
        where TEntity : class, IChangeLogged =>
        RecordDeletionAsync(db, set, [entityId], ct);

    /// <summary>Dasselbe für mehrere Entitäten desselben Moduls auf einmal.</summary>
    public static async Task RecordDeletionAsync<TEntity>(
        GameDevManagerDbContext db, DbSet<TEntity> set, IReadOnlyCollection<Guid> entityIds,
        CancellationToken ct)
        where TEntity : class, IChangeLogged
    {
        if (entityIds.Count == 0 || db.SuppressChangeLog)
        {
            return;
        }

        var doomed = await set
            .AsNoTracking()
            .Where(entity => entityIds.Contains(entity.Id))
            .Select(entity => new { entity.Id, entity.GameProjectId, entity.Name, entity.ModuleKey })
            .ToListAsync(ct);

        foreach (var entity in doomed)
        {
            db.ChangeLogEntries.Add(new ChangeLogEntry
            {
                GameProjectId = entity.GameProjectId,
                // Bleibt leer — der Interceptor trägt den angemeldeten Benutzer nach.
                UserName = string.Empty,
                ModuleKey = entity.ModuleKey,
                EntityId = entity.Id,
                EntityName = entity.Name,
                Action = ChangeAction.Deleted
            });
        }

        if (doomed.Count > 0)
        {
            await db.SaveChangesAsync(ct);
        }
    }

    /// <summary>
    /// Hält einen Vorgang fest, der ein ganzes Projekt betrifft — Import, Duplizieren,
    /// Löschen des Projekts. Statt einer Zeile je Entität eine einzige über das Ganze:
    /// Ein Protokoll, das ein Import mit tausend Zeilen flutet, ist danach unlesbar.
    /// </summary>
    public static async Task RecordProjectActionAsync(
        GameDevManagerDbContext db, Guid projectId, string projectName, ChangeAction action,
        string? details, CancellationToken ct)
    {
        db.ChangeLogEntries.Add(new ChangeLogEntry
        {
            GameProjectId = projectId,
            UserName = string.Empty,
            ModuleKey = Domain.ModuleKeys.Changelog,
            EntityId = projectId,
            EntityName = projectName,
            Action = action,
            Details = details
        });

        await db.SaveChangesAsync(ct);
    }
}
