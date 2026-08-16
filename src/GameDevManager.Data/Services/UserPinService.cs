using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GameDevManager.Data.Services;

/// <summary>Ein Favorit samt aufgelöstem Namen — die Zeile, die das Dashboard zeigt.</summary>
public sealed record PinnedEntry(Guid EntityId, string ModuleKey, string Name, DateTime CreatedAtUtc);

/// <summary>
/// Favoriten: die paar Entitäten, an denen jemand gerade arbeitet.
/// <para>
/// Das Dashboard-Band „Weiterarbeiten“ zeigt das zuletzt <b>Geänderte</b> — das ist nicht
/// dasselbe wie das absichtlich Angeheftete, und in einer Liste mit 300 Items geht das eine im
/// anderen unter.
/// </para>
/// <para>
/// Wer anheftet, beantwortet der <see cref="IChangeAuthorProvider"/> — dieselbe Quelle, aus der
/// auch das Änderungsprotokoll seinen Urheber nimmt. Ohne Anmeldung gibt es keine Merkliste:
/// Eine Liste ohne Besitzer gehörte allen und damit niemandem.
/// </para>
/// </summary>
public class UserPinService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    IEnumerable<IModuleEntitySource> sources,
    IChangeAuthorProvider authors,
    PermissionGuard guard)
{
    /// <summary>Ob diese Entität für den angemeldeten Benutzer angeheftet ist.</summary>
    public async Task<bool> IsPinnedAsync(Guid entityId, CancellationToken ct = default)
    {
        if (await CurrentUserIdAsync(ct) is not { } userId)
        {
            return false;
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.UserPins.AnyAsync(pin => pin.AppUserId == userId && pin.EntityId == entityId, ct);
    }

    /// <summary>
    /// Heftet an oder löst wieder — ein Sternsymbol hat genau zwei Zustände. Gibt zurück, ob
    /// die Entität danach angeheftet ist.
    /// </summary>
    public async Task<bool> ToggleAsync(
        Guid projectId, string moduleKey, Guid entityId, CancellationToken ct = default)
    {
        // Ein reiner ExecuteDelete-Pfad ohne vorheriges Speichern — die Prüfung steht hier.
        await guard.EnsureCanWriteAsync(ct);

        if (await CurrentUserIdAsync(ct) is not { } userId)
        {
            return false;
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        var existing = await db.UserPins
            .FirstOrDefaultAsync(pin => pin.AppUserId == userId && pin.EntityId == entityId, ct);

        if (existing is not null)
        {
            db.UserPins.Remove(existing);
            await db.SaveChangesAsync(ct);
            return false;
        }

        db.UserPins.Add(new UserPin
        {
            AppUserId = userId,
            GameProjectId = projectId,
            ModuleKey = moduleKey,
            EntityId = entityId
        });

        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Die Favoriten des angemeldeten Benutzers in diesem Projekt, zuletzt angeheftete zuerst.
    /// <para>
    /// Namen werden über die <see cref="IModuleEntitySource"/> aufgelöst; eine Entität, die es
    /// nicht mehr gibt, fällt heraus statt namenlos dazustehen — und ihr Eintrag gleich mit,
    /// denn eine Merkliste, die auf Gelöschtes zeigt, ist keine.
    /// </para>
    /// </summary>
    public async Task<List<PinnedEntry>> GetPinnedAsync(
        Guid projectId, int limit = 20, CancellationToken ct = default)
    {
        if (await CurrentUserIdAsync(ct) is not { } userId)
        {
            return [];
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        var pins = await db.UserPins
            .AsNoTracking()
            .Where(pin => pin.AppUserId == userId && pin.GameProjectId == projectId)
            .OrderByDescending(pin => pin.CreatedAtUtc)
            .ThenBy(pin => pin.EntityId)
            .Take(limit)
            .ToListAsync(ct);

        if (pins.Count == 0)
        {
            return [];
        }

        var entries = new List<PinnedEntry>();
        var stale = new List<Guid>();

        foreach (var perModule in pins.GroupBy(pin => pin.ModuleKey))
        {
            var source = sources.FirstOrDefault(entry => entry.ModuleKey == perModule.Key);
            var names = source is null
                ? []
                : await source.ResolveNamesAsync(db, [.. perModule.Select(pin => pin.EntityId)], ct);

            foreach (var pin in perModule)
            {
                if (names.TryGetValue(pin.EntityId, out var name))
                {
                    entries.Add(new PinnedEntry(pin.EntityId, pin.ModuleKey, name, pin.CreatedAtUtc));
                }
                else
                {
                    stale.Add(pin.Id);
                }
            }
        }

        if (stale.Count > 0)
        {
            await db.UserPins.Where(pin => stale.Contains(pin.Id)).ExecuteDeleteAsync(ct);
        }

        return [.. entries.OrderByDescending(entry => entry.CreatedAtUtc).ThenBy(entry => entry.Name)];
    }

    private async ValueTask<Guid?> CurrentUserIdAsync(CancellationToken ct) =>
        (await authors.GetCurrentAsync(ct)).UserId;
}
