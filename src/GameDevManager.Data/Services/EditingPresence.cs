using System.Collections.Concurrent;

namespace GameDevManager.Data.Services;

/// <summary>Wer eine Entität gerade offen hat — seit wann, und wann das letzte Lebenszeichen kam.</summary>
public sealed record EditingSession(string UserName, DateTime StartedAtUtc, DateTime LastSeenUtc);

/// <summary>Eine Zeile der Präsenz-Übersicht: wer sitzt seit wann an welcher Entität.</summary>
public sealed record PresenceSnapshot(Guid EntityId, string UserName, DateTime StartedAtUtc);

/// <summary>
/// „Wird gerade bearbeitet von …“ — wer welche Entität offen hat.
/// <para>
/// Die Schreibkonflikt-Erkennung meldet den Zusammenstoß erst beim Speichern; sie verhindert
/// ihn nicht. Diese Auskunft kommt vorher: Wer eine Maske öffnet, sieht, dass jemand anders
/// schon daran sitzt.
/// </para>
/// <para>
/// <b>Reiner Arbeitsspeicher, keine Tabelle.</b> Das Tool läuft in einem Prozess — eine Zeile in
/// der Datenbank wäre für eine Angabe, die Sekunden gilt, viermal Migration und ein Aufräumer
/// dazu. Ein Eintrag ohne Lebenszeichen verfällt nach <see cref="Timeout"/>: Ein abgestürzter
/// Browser meldet sich nicht ab, und eine Sperre, die niemand mehr lösen kann, wäre schlimmer
/// als gar keine Auskunft. Dasselbe Muster wie beim <c>WhiteboardNotifier</c> — Singleton in der
/// Datenschicht, damit alle Verbindungen dieselbe Sicht haben.
/// </para>
/// </summary>
public sealed class EditingPresence(TimeProvider? time = null)
{
    /// <summary>
    /// So lange gilt ein Eintrag ohne neues Lebenszeichen. Großzügiger als der Herzschlag der
    /// Maske, damit ein kurzer Aussetzer der Verbindung niemanden aus der Liste wirft.
    /// </summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromMinutes(3);

    private readonly TimeProvider _time = time ?? TimeProvider.System;

    /// <summary>Je Entität die offenen Sitzungen, je Sitzungskennung eine.</summary>
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, EditingSession>> _open = new();

    /// <summary>
    /// Meldet eine Maske als offen oder frischt sie auf. Die Sitzungskennung vergibt die
    /// Oberfläche je Bearbeitungsmaske — derselbe Benutzer darf dieselbe Entität in zwei
    /// Fenstern offen haben, und beide sollen sich einzeln wieder abmelden können.
    /// </summary>
    public void Announce(Guid entityId, Guid sessionId, string userName)
    {
        var sessions = _open.GetOrAdd(entityId, _ => new ConcurrentDictionary<Guid, EditingSession>());
        var now = _time.GetUtcNow().UtcDateTime;

        // Der Beginn bleibt stehen — ein Lebenszeichen frischt nur „zuletzt gesehen“ auf,
        // sonst hieße „seit wann“ in der Übersicht immer „seit eben“.
        sessions.AddOrUpdate(
            sessionId,
            _ => new EditingSession(userName, now, now),
            (_, existing) => existing with { UserName = userName, LastSeenUtc = now });
    }

    /// <summary>Meldet eine Maske ab. Die letzte Sitzung nimmt den Eintrag der Entität mit.</summary>
    public void Release(Guid entityId, Guid sessionId)
    {
        if (!_open.TryGetValue(entityId, out var sessions))
        {
            return;
        }

        sessions.TryRemove(sessionId, out _);

        if (sessions.IsEmpty)
        {
            _open.TryRemove(entityId, out _);
        }
    }

    /// <summary>
    /// Die <b>anderen</b>, die diese Entität offen haben — die eigene Sitzung bleibt draußen:
    /// „Sie bearbeiten das gerade“ ist keine Auskunft. Je Benutzername nur einmal, auch wenn
    /// jemand zwei Fenster offen hat.
    /// </summary>
    public IReadOnlyList<EditingSession> Others(Guid entityId, Guid sessionId)
    {
        if (!_open.TryGetValue(entityId, out var sessions))
        {
            return [];
        }

        var cutoff = _time.GetUtcNow().UtcDateTime - Timeout;
        var alive = new List<EditingSession>();

        foreach (var (id, session) in sessions)
        {
            // Verfallenes wird beim Nachsehen aufgeräumt: Ein eigener Hintergrunddienst wäre
            // für eine Handvoll Einträge im Arbeitsspeicher unangemessen.
            if (session.LastSeenUtc < cutoff)
            {
                sessions.TryRemove(id, out _);
                continue;
            }

            if (id != sessionId)
            {
                alive.Add(session);
            }
        }

        if (sessions.IsEmpty)
        {
            _open.TryRemove(entityId, out _);
        }

        return
        [
            .. alive
                .GroupBy(session => session.UserName, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.MaxBy(session => session.LastSeenUtc)!)
                .OrderBy(session => session.UserName, StringComparer.CurrentCultureIgnoreCase)
        ];
    }

    /// <summary>
    /// Alle lebendigen Einträge auf einmal — die Grundlage der Präsenz-Übersicht („wer
    /// arbeitet gerade woran?“). Je Benutzer und Entität eine Zeile, auch bei zwei Fenstern;
    /// „seit wann“ ist der früheste Beginn. Verfallenes wird dabei aufgeräumt, wie in
    /// <see cref="Others"/> — kein Eintrag überlebt den Verfall.
    /// </summary>
    public IReadOnlyList<PresenceSnapshot> Snapshot()
    {
        var cutoff = _time.GetUtcNow().UtcDateTime - Timeout;
        var rows = new List<PresenceSnapshot>();

        foreach (var (entityId, sessions) in _open)
        {
            foreach (var (id, session) in sessions)
            {
                if (session.LastSeenUtc < cutoff)
                {
                    sessions.TryRemove(id, out _);
                    continue;
                }

                rows.Add(new PresenceSnapshot(entityId, session.UserName, session.StartedAtUtc));
            }

            if (sessions.IsEmpty)
            {
                _open.TryRemove(entityId, out _);
            }
        }

        return
        [
            .. rows
                .GroupBy(row => (row.EntityId, Name: row.UserName.ToLowerInvariant()))
                .Select(group => group.MinBy(row => row.StartedAtUtc)!)
                .OrderBy(row => row.StartedAtUtc)
        ];
    }
}
