using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GameDevManager.Data.Services;

/// <summary>Welcher Art ein Eintrag des Feeds ist — davon hängt ab, wie er gelesen wird.</summary>
public enum ActivityKind
{
    /// <summary>Jemand hat an einer Entität gearbeitet.</summary>
    Change = 0,

    /// <summary>Jemand hat den angemeldeten Benutzer in einer Anmerkung erwähnt.</summary>
    Mention = 1,

    /// <summary>Jemandem wurde eine Aufgabe zugewiesen.</summary>
    Assignment = 2
}

/// <summary>
/// Eine Zeile des Feeds. Bei Änderungen ist sie <b>je Entität</b> zusammengefasst — sonst
/// ertränke der Feed in Speichervorgängen.
/// </summary>
/// <param name="Count">Wie viele Einzeländerungen dahinterstecken.</param>
/// <param name="Actors">Wer beteiligt war, in der Reihenfolge des letzten Beitrags.</param>
public sealed record ActivityEntry(
    ActivityKind Kind,
    Guid EntityId,
    string ModuleKey,
    string EntityName,
    string? Text,
    IReadOnlyList<string> Actors,
    DateTime AtUtc,
    int Count);

/// <summary>
/// Der Aktivitäts-Feed: was sich seit dem letzten Besuch getan hat.
/// <para>
/// <b>Ohne eigenen Datenbestand.</b> Das Änderungsprotokoll hat die Daten längst; hinzu kommt
/// nur ein „gelesen bis“ am Konto. Erwähnungen kommen aus den Anmerkungen, Zuweisungen aus den
/// Kanban-Karten — dieselbe Linie wie beim Freischaltungs-Graphen und beim Loot-Simulator:
/// auswerten statt ein zweites Mal speichern.
/// </para>
/// <para>
/// Zusammengefasst wird <b>nach Entität</b> und nicht nach Einzeländerung: Wer eine Maske
/// dreimal speichert, erzeugt drei Protokollzeilen, aber nur eine Nachricht.
/// </para>
/// <para>
/// Kein Mailversand — das Tool wird self-hosted von kleinen Teams betrieben, ein Glockensymbol
/// in der Appbar reicht.
/// </para>
/// </summary>
public class ActivityFeedService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    IChangeAuthorProvider authors)
{
    /// <summary>So viele Zeilen zeigt der Feed höchstens — darüber hinaus ist es keine Nachricht mehr.</summary>
    public const int MaxEntries = 50;

    /// <summary>
    /// Was seit dem letzten Lesen geschehen ist. Ohne angemeldeten Benutzer ist der Feed leer:
    /// „seit deinem letzten Besuch“ setzt voraus, dass es ein Du gibt.
    /// </summary>
    public async Task<List<ActivityEntry>> GetFeedAsync(
        Guid projectId, CancellationToken ct = default)
    {
        var author = await authors.GetCurrentAsync(ct);
        if (author.UserId is not { } userId)
        {
            return [];
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        var since = await ReadMarkerAsync(db, userId, ct);
        var entries = new List<ActivityEntry>();

        // ------------------------------------------------------------------ Änderungen
        // Das eigene Tun ist keine Nachricht — man war ja dabei.
        var changes = await db.ChangeLogEntries
            .AsNoTracking()
            .Where(entry => entry.GameProjectId == projectId
                && entry.AtUtc > since
                && entry.UserId != userId)
            .OrderByDescending(entry => entry.AtUtc)
            .Take(MaxEntries * 10)
            .ToListAsync(ct);

        entries.AddRange(changes
            .GroupBy(entry => entry.EntityId)
            .Select(group => new ActivityEntry(
                ActivityKind.Change,
                group.Key,
                group.First().ModuleKey,
                group.First().EntityName,
                null,
                [.. group.OrderByDescending(entry => entry.AtUtc).Select(entry => entry.UserName).Distinct()],
                group.Max(entry => entry.AtUtc),
                group.Count())));

        // ------------------------------------------------------------------ Erwähnungen
        // Gesucht wird über „@Name“ im Text. Über LIKE und damit über alle vier Provider
        // gleich, wie schon die globale Suche.
        var me = author.UserName;
        var needle = "@" + me;

        var mentions = await db.ContentComments
            .AsNoTracking()
            .Where(comment => comment.GameProjectId == projectId
                && comment.CreatedAtUtc > since
                && comment.AuthorName != me
                && comment.Text.Contains(needle))
            .OrderByDescending(comment => comment.CreatedAtUtc)
            .Take(MaxEntries)
            .ToListAsync(ct);

        entries.AddRange(mentions.Select(comment => new ActivityEntry(
            ActivityKind.Mention,
            comment.OwnerEntityId,
            comment.OwnerModuleKey,
            string.Empty,
            comment.Text,
            [comment.AuthorName],
            comment.CreatedAtUtc,
            1)));

        // ------------------------------------------------------------------- Zuweisungen
        var assignments = await db.KanbanCards
            .AsNoTracking()
            .Where(card => card.AssignedUserId == userId
                && card.CreatedAtUtc > since
                && card.Column!.Board!.GameProjectId == projectId)
            .OrderByDescending(card => card.CreatedAtUtc)
            .Select(card => new { card.Id, card.Title, BoardId = card.Column!.BoardId, card.CreatedAtUtc })
            .Take(MaxEntries)
            .ToListAsync(ct);

        entries.AddRange(assignments.Select(card => new ActivityEntry(
            ActivityKind.Assignment,
            card.BoardId,
            Domain.ModuleKeys.Todo,
            card.Title,
            null,
            [],
            card.CreatedAtUtc,
            1)));

        // Namen der erwähnten Entitäten nachtragen — die Anmerkung kennt nur deren GUID.
        await ResolveNamesAsync(db, entries, ct);

        return [.. entries.OrderByDescending(entry => entry.AtUtc).Take(MaxEntries)];
    }

    /// <summary>Wie viele Zeilen der Feed hätte — die Zahl an der Glocke.</summary>
    public async Task<int> CountUnreadAsync(Guid projectId, CancellationToken ct = default) =>
        (await GetFeedAsync(projectId, ct)).Count;

    /// <summary>
    /// Setzt die Marke auf jetzt. Kein Schreibrecht nötig: Der Benutzer ändert nur seinen
    /// eigenen Lesestand, und der ist keine Änderung am Inhalt — dieselbe Ausnahme, die der
    /// <c>WriteGuardInterceptor</c> schon für das eigene Passwort macht.
    /// </summary>
    public async Task MarkReadAsync(CancellationToken ct = default)
    {
        if ((await authors.GetCurrentAsync(ct)).UserId is not { } userId)
        {
            return;
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        var user = await db.AppUsers.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
        {
            return;
        }

        user.FeedReadAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Ab wann gelesen wird. Wer den Feed noch nie geöffnet hat, bekommt alles seit seiner
    /// <b>ersten Anmeldung</b> und nicht den gesamten Bestand seit Projektbeginn: Eine Glocke
    /// mit dreihundert Einträgen liest niemand.
    /// </summary>
    private static async Task<DateTime> ReadMarkerAsync(
        GameDevManagerDbContext db, Guid userId, CancellationToken ct)
    {
        var user = await db.AppUsers
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.FeedReadAtUtc, u.CreatedAtUtc })
            .FirstOrDefaultAsync(ct);

        return user?.FeedReadAtUtc ?? user?.CreatedAtUtc ?? DateTime.UtcNow;
    }

    /// <summary>
    /// Trägt fehlende Entitätsnamen nach. Der Feed listet Werte, keine Objekte — deshalb wird
    /// die Liste ersetzt statt verändert.
    /// </summary>
    private static async Task ResolveNamesAsync(
        GameDevManagerDbContext db, List<ActivityEntry> entries, CancellationToken ct)
    {
        var missing = entries
            .Where(entry => string.IsNullOrEmpty(entry.EntityName))
            .Select(entry => entry.EntityId)
            .Distinct()
            .ToList();

        if (missing.Count == 0)
        {
            return;
        }

        // Über das Änderungsprotokoll statt über die Modul-Quellen: Dort steht der Name schon
        // als Momentaufnahme, und eine Anmerkung hängt fast immer an etwas, das jemand
        // angelegt oder geändert hat.
        var names = await db.ChangeLogEntries
            .AsNoTracking()
            .Where(entry => missing.Contains(entry.EntityId))
            .GroupBy(entry => entry.EntityId)
            .Select(group => new { group.Key, Name = group.OrderByDescending(e => e.AtUtc).First().EntityName })
            .ToDictionaryAsync(row => row.Key, row => row.Name, ct);

        for (var index = 0; index < entries.Count; index++)
        {
            if (string.IsNullOrEmpty(entries[index].EntityName)
                && names.TryGetValue(entries[index].EntityId, out var name))
            {
                entries[index] = entries[index] with { EntityName = name };
            }
        }
    }
}
