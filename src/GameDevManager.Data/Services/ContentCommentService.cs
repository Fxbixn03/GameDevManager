using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace GameDevManager.Data.Services;

/// <summary>Eine offene Anmerkung samt Entität — die Zeile, die das Dashboard zeigt.</summary>
public sealed record OpenComment(
    Guid Id,
    Guid OwnerEntityId,
    string OwnerModuleKey,
    string OwnerName,
    string Text,
    string AuthorName,
    DateTime CreatedAtUtc);

/// <summary>
/// Anmerkungen an Entitäten: Rückmeldungen stehen dort, wo sie hingehören, und nicht im Chat.
/// <para>
/// Werkzeug-Daten wie das Änderungsprotokoll — nicht im Export, sie überstehen den ersetzenden
/// Import. Der Urheber kommt wie dort aus dem <see cref="IChangeAuthorProvider"/> und wird als
/// Name gespeichert, nicht als Verweis.
/// </para>
/// </summary>
public class ContentCommentService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    IEnumerable<IModuleEntitySource> sources,
    IChangeAuthorProvider authors,
    PermissionGuard guard,
    IStringLocalizer<DataMessages> messages)
{
    /// <summary>Die Anmerkungen einer Entität — offene zuerst, darin die jüngste oben.</summary>
    public async Task<List<ContentComment>> GetForEntityAsync(Guid entityId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.ContentComments
            .AsNoTracking()
            .Where(comment => comment.OwnerEntityId == entityId)
            .OrderBy(comment => comment.ResolvedAtUtc == null ? 0 : 1)
            .ThenByDescending(comment => comment.CreatedAtUtc)
            .ToListAsync(ct);
    }

    /// <summary>Wie viele Anmerkungen offen sind — für den Zähler an der Maske.</summary>
    public async Task<int> CountOpenAsync(Guid entityId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.ContentComments
            .CountAsync(comment => comment.OwnerEntityId == entityId && comment.ResolvedAtUtc == null, ct);
    }

    public async Task AddAsync(
        Guid projectId, Guid entityId, string moduleKey, string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ContentValidationException(messages["Comment_TextRequired"].Value);
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        db.ContentComments.Add(new ContentComment
        {
            GameProjectId = projectId,
            OwnerEntityId = entityId,
            OwnerModuleKey = moduleKey,
            Text = text.Trim(),
            AuthorName = (await authors.GetCurrentAsync(ct)).UserName
        });

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Hakt eine Anmerkung ab oder nimmt das zurück. Erledigtes bleibt stehen statt gelöscht zu
    /// werden — es ist der Beleg, dass etwas besprochen war.
    /// </summary>
    public async Task SetResolvedAsync(Guid commentId, bool resolved, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var comment = await db.ContentComments.FirstOrDefaultAsync(c => c.Id == commentId, ct);
        if (comment is null)
        {
            return;
        }

        comment.ResolvedAtUtc = resolved ? DateTime.UtcNow : null;
        comment.ResolvedBy = resolved ? (await authors.GetCurrentAsync(ct)).UserName : null;

        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid commentId, CancellationToken ct = default)
    {
        // Reiner ExecuteDelete-Pfad ohne vorheriges Speichern — die Prüfung steht hier.
        await guard.EnsureCanWriteAsync(ct);

        await using var db = await factory.CreateDbContextAsync(ct);
        await db.ContentComments.Where(comment => comment.Id == commentId).ExecuteDeleteAsync(ct);
    }

    /// <summary>
    /// Die offenen Anmerkungen des Projekts, jüngste zuerst — das Dashboard-Band.
    /// <para>
    /// Namen werden über die <see cref="IModuleEntitySource"/> aufgelöst; eine Anmerkung an
    /// einer Entität, die es nicht mehr gibt, kann es nach dem <c>EntityCleanup</c> gar nicht
    /// geben — sie fiele hier trotzdem heraus statt namenlos dazustehen.
    /// </para>
    /// </summary>
    public async Task<List<OpenComment>> GetOpenAsync(
        Guid projectId, int limit = 20, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var comments = await db.ContentComments
            .AsNoTracking()
            .Where(comment => comment.GameProjectId == projectId && comment.ResolvedAtUtc == null)
            .OrderByDescending(comment => comment.CreatedAtUtc)
            .ThenBy(comment => comment.Id)
            .Take(limit)
            .ToListAsync(ct);

        var open = new List<OpenComment>();

        foreach (var perModule in comments.GroupBy(comment => comment.OwnerModuleKey))
        {
            var source = sources.FirstOrDefault(entry => entry.ModuleKey == perModule.Key);
            var names = source is null
                ? []
                : await source.ResolveNamesAsync(db, [.. perModule.Select(c => c.OwnerEntityId)], ct);

            open.AddRange(perModule
                .Where(comment => names.ContainsKey(comment.OwnerEntityId))
                .Select(comment => new OpenComment(
                    comment.Id,
                    comment.OwnerEntityId,
                    comment.OwnerModuleKey,
                    names[comment.OwnerEntityId],
                    comment.Text,
                    comment.AuthorName,
                    comment.CreatedAtUtc)));
        }

        return [.. open.OrderByDescending(comment => comment.CreatedAtUtc)];
    }
}
