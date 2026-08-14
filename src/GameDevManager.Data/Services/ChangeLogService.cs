using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GameDevManager.Data.Services;

/// <summary>Wonach das Änderungsprotokoll eingegrenzt wird. Alles <c>null</c> heißt „alles“.</summary>
public sealed record ChangeLogFilter(
    string? ModuleKey = null,
    Guid? UserId = null,
    ChangeAction? Action = null,
    Guid? EntityId = null,
    DateTime? SinceUtc = null);

/// <summary>Eine Zeile des Änderungsprotokolls, fertig für die Anzeige.</summary>
public sealed record ChangeLogRow(
    Guid Id,
    DateTime AtUtc,
    Guid? UserId,
    string UserName,
    string ModuleKey,
    Guid EntityId,
    string EntityName,
    ChangeAction Action,
    string? Details);

/// <summary>Ein Seitenausschnitt des Protokolls samt Gesamtzahl — die Ansicht blättert damit.</summary>
public sealed record ChangeLogPage(IReadOnlyList<ChangeLogRow> Rows, int Total);

/// <summary>
/// Die Leseseite des Änderungsprotokolls. Geschrieben wird es an anderer Stelle — vom
/// <see cref="ChangeLogInterceptor"/> beim Speichern und von <see cref="ChangeLog"/> beim
/// Löschen.
/// </summary>
public class ChangeLogService(IDbContextFactory<GameDevManagerDbContext> factory)
{
    /// <summary>Ein Ausschnitt des Protokolls, jüngste Änderung zuerst.</summary>
    public async Task<ChangeLogPage> GetEntriesAsync(
        Guid projectId, ChangeLogFilter? filter = null, int skip = 0, int take = 50,
        CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var query = Filtered(db, projectId, filter ?? new ChangeLogFilter());
        var total = await query.CountAsync(ct);

        var rows = await query
            // Nach GUID als zweitem Kriterium: Beim Speichern entstehen mehrere Einträge mit
            // demselben Zeitstempel, und ohne festen zweiten Schlüssel wäre die Reihenfolge
            // beim Blättern von Seite zu Seite eine andere.
            .OrderByDescending(entry => entry.AtUtc)
            .ThenBy(entry => entry.Id)
            .Skip(skip)
            .Take(take)
            .Select(entry => new ChangeLogRow(
                entry.Id,
                entry.AtUtc,
                entry.UserId,
                entry.UserName,
                entry.ModuleKey,
                entry.EntityId,
                entry.EntityName,
                entry.Action,
                entry.Details))
            .ToListAsync(ct);

        return new ChangeLogPage(rows, total);
    }

    /// <summary>Die Geschichte einer einzelnen Entität — für die Referenzansicht in der Maske.</summary>
    public Task<ChangeLogPage> GetForEntityAsync(
        Guid projectId, Guid entityId, int take = 20, CancellationToken ct = default) =>
        GetEntriesAsync(projectId, new ChangeLogFilter(EntityId: entityId), 0, take, ct);

    /// <summary>Die Benutzer, die in diesem Projekt überhaupt schon etwas getan haben — für den Filter.</summary>
    public async Task<List<ChangeLogAuthor>> GetAuthorsAsync(Guid projectId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        // In eine anonyme Form projizieren und erst danach in den Record umsetzen: Einen
        // Konstruktoraufruf innerhalb einer Gruppierung kann EF nicht übersetzen.
        var grouped = await db.ChangeLogEntries
            .AsNoTracking()
            .Where(entry => entry.GameProjectId == projectId)
            .GroupBy(entry => new { entry.UserId, entry.UserName })
            .Select(group => new { group.Key.UserId, group.Key.UserName, Count = group.Count() })
            .ToListAsync(ct);

        return
        [
            .. grouped
                .Select(entry => new ChangeLogAuthor(entry.UserId, entry.UserName, entry.Count))
                .OrderByDescending(author => author.Count)
                .ThenBy(author => author.UserName, StringComparer.CurrentCultureIgnoreCase)
        ];
    }

    /// <summary>Wie viele Änderungen es seit einem Zeitpunkt gab — die Zahl auf dem Dashboard.</summary>
    public async Task<int> CountSinceAsync(Guid projectId, DateTime sinceUtc, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.ChangeLogEntries
            .CountAsync(entry => entry.GameProjectId == projectId && entry.AtUtc >= sinceUtc, ct);
    }

    private static IQueryable<ChangeLogEntry> Filtered(
        GameDevManagerDbContext db, Guid projectId, ChangeLogFilter filter)
    {
        var query = db.ChangeLogEntries
            .AsNoTracking()
            .Where(entry => entry.GameProjectId == projectId);

        if (!string.IsNullOrWhiteSpace(filter.ModuleKey))
        {
            query = query.Where(entry => entry.ModuleKey == filter.ModuleKey);
        }

        if (filter.UserId is { } userId)
        {
            query = query.Where(entry => entry.UserId == userId);
        }

        if (filter.Action is { } action)
        {
            query = query.Where(entry => entry.Action == action);
        }

        if (filter.EntityId is { } entityId)
        {
            query = query.Where(entry => entry.EntityId == entityId);
        }

        if (filter.SinceUtc is { } since)
        {
            query = query.Where(entry => entry.AtUtc >= since);
        }

        return query;
    }
}

/// <summary>Ein Benutzer, der in einem Projekt Spuren hinterlassen hat.</summary>
public sealed record ChangeLogAuthor(Guid? UserId, string UserName, int Count);
