using Microsoft.EntityFrameworkCore;

namespace GameDevManager.Data.Services;

/// <summary>Eine Zeile der Präsenz-Ansicht: Benutzer → Entität → seit wann, mit Sprungziel.</summary>
public sealed record PresenceEntry(
    string UserName,
    Guid EntityId,
    string ModuleKey,
    string EntityName,
    DateTime StartedAtUtc);

/// <summary>
/// „Wer arbeitet gerade woran?“ — die projektweite Sicht auf die <see cref="EditingPresence"/>.
/// Die Präsenz kennt nur GUIDs; Name und Modul der Entität kommen über die Modul-Quellen
/// dazu — je Modul eine Abfrage über alle offenen GUIDs, nicht je Entität eine.
/// <para>
/// Die <b>eigenen</b> Sitzungen erscheinen nicht, wie im Banner: „Du arbeitest gerade an …“
/// ist keine Auskunft. Und was sich zu keinem Modul auflöst (gerade gelöscht), fällt heraus,
/// statt als GUID dazustehen.
/// </para>
/// </summary>
public class PresenceOverviewService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    EditingPresence presence,
    IEnumerable<IModuleEntitySource> sources,
    IChangeAuthorProvider author)
{
    public async Task<List<PresenceEntry>> GetAsync(CancellationToken ct = default)
    {
        var own = (await author.GetCurrentAsync(ct)).UserName;

        List<PresenceSnapshot> open =
        [
            .. presence.Snapshot()
                .Where(row => !row.UserName.Equals(own, StringComparison.OrdinalIgnoreCase))
        ];

        if (open.Count == 0)
        {
            return [];
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        List<Guid> ids = [.. open.Select(row => row.EntityId).Distinct()];
        var resolved = new Dictionary<Guid, (string ModuleKey, string Name)>();

        foreach (var source in sources)
        {
            foreach (var (id, name) in await source.ResolveNamesAsync(db, ids, ct))
            {
                resolved.TryAdd(id, (source.ModuleKey, name));
            }
        }

        return
        [
            .. open
                .Where(row => resolved.ContainsKey(row.EntityId))
                .Select(row => new PresenceEntry(
                    row.UserName,
                    row.EntityId,
                    resolved[row.EntityId].ModuleKey,
                    resolved[row.EntityId].Name,
                    row.StartedAtUtc))
                .OrderBy(entry => entry.StartedAtUtc)
        ];
    }
}
