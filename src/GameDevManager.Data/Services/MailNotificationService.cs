using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace GameDevManager.Data.Services;

/// <summary>
/// Der Mail-Digest: sammelt, was sich seit dem letzten Lauf für einen Benutzer getan hat —
/// Zuweisungen (Aufgaben und Abnahmen), Anmerkungen an eigenen Inhalten, entschiedene
/// Abnahmen — und schickt es <b>gebündelt</b> als eine Mail je Benutzer. Kein Mail-Sturm je
/// Speichern: Der Takt kommt vom Hintergrunddienst, nicht vom Ereignis.
/// <para>
/// Wer „eigene Inhalte“ hat, beantwortet das Änderungsprotokoll: Der Urheber des
/// Anlege-Eintrags einer Entität bekommt die Anmerkungen dazu. Verschickt wird nur an
/// Konten mit eingetragener Adresse — sie einzutragen ist das Einverständnis; die
/// Ereignisarten schaltet jeder selbst (<c>NotifyOn…</c>), geprüft beim Einsammeln.
/// </para>
/// </summary>
public class MailNotificationService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    IMailSender sender,
    IStringLocalizer<DataMessages> messages)
{
    /// <summary>
    /// Sammelt und verschickt. Gibt zurück, wie viele Mails hinausgegangen sind — der
    /// Hintergrunddienst protokolliert die Zahl.
    /// </summary>
    public async Task<int> SendDigestAsync(DateTime sinceUtc, CancellationToken ct = default)
    {
        if (!sender.IsConfigured)
        {
            return 0;
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        var recipients = (await db.AppUsers
                .AsNoTracking()
                .Where(user => !user.IsDisabled && user.Email != null)
                .ToListAsync(ct))
            .ToDictionary(user => user.Id);

        if (recipients.Count == 0)
        {
            return 0;
        }

        var lines = new Dictionary<Guid, List<string>>();

        // Gefiltert wird beim Einsammeln: Wer die Ereignisart abgeschaltet hat oder keine
        // Adresse trägt, bekommt gar nicht erst eine Zeile.
        void Add(Guid userId, Func<AppUser, bool> wants, string line)
        {
            if (recipients.TryGetValue(userId, out var user) && wants(user))
            {
                (lines.TryGetValue(userId, out var list) ? list : lines[userId] = []).Add(line);
            }
        }

        // ---------------------------------------------- Zuweisungen: Aufgaben und Abnahmen
        var assignedCards = await db.KanbanCards
            .AsNoTracking()
            .Where(card => card.AssignedUserId != null && card.AssignedAtUtc > sinceUtc)
            .Select(card => new { UserId = card.AssignedUserId!.Value, card.Title })
            .ToListAsync(ct);

        foreach (var card in assignedCards)
        {
            Add(card.UserId, user => user.NotifyOnAssignment,
                messages["Mail_TaskAssigned", card.Title].Value);
        }

        var assignedReviews = await db.ReviewRequests
            .AsNoTracking()
            .Where(request => request.AssignedUserId != null
                && request.Decision == ReviewDecision.Pending
                && request.CreatedAtUtc > sinceUtc)
            .ToListAsync(ct);

        foreach (var request in assignedReviews)
        {
            Add(request.AssignedUserId!.Value, user => user.NotifyOnAssignment,
                messages["Mail_ReviewAssigned", request.RequestedBy].Value);
        }

        // ------------------------------------ Anmerkungen an Inhalten, die man angelegt hat
        var comments = await db.ContentComments
            .AsNoTracking()
            .Where(comment => comment.CreatedAtUtc > sinceUtc)
            .ToListAsync(ct);

        if (comments.Count > 0)
        {
            var entityIds = comments.Select(comment => comment.OwnerEntityId).Distinct().ToList();

            // Der Anlege-Eintrag im Protokoll kennt den Urheber — die Entität selbst nicht.
            var creators = (await db.ChangeLogEntries
                    .AsNoTracking()
                    .Where(entry => entityIds.Contains(entry.EntityId)
                        && entry.Action == ChangeAction.Created
                        && entry.UserId != null)
                    .Select(entry => new { entry.EntityId, entry.UserId, entry.EntityName })
                    .ToListAsync(ct))
                .GroupBy(entry => entry.EntityId)
                .ToDictionary(group => group.Key, group => group.First());

            foreach (var comment in comments)
            {
                if (creators.TryGetValue(comment.OwnerEntityId, out var creator))
                {
                    // Die eigene Anmerkung an der eigenen Entität ist keine Neuigkeit.
                    Add(creator.UserId!.Value,
                        user => user.NotifyOnComment && user.DisplayName != comment.AuthorName,
                        messages["Mail_CommentAdded", comment.AuthorName, creator.EntityName].Value);
                }
            }
        }

        // --------------------------------------------------------- Entschiedene Abnahmen
        var decided = await db.ReviewRequests
            .AsNoTracking()
            .Where(request => request.RequestedById != null && request.DecidedAtUtc > sinceUtc)
            .ToListAsync(ct);

        foreach (var request in decided)
        {
            Add(request.RequestedById!.Value, user => user.NotifyOnReview,
                request.Decision == ReviewDecision.Approved
                    ? messages["Mail_ReviewApproved", request.DecidedBy ?? string.Empty].Value
                    : messages["Mail_ReviewRejected",
                        request.DecidedBy ?? string.Empty, request.DecisionNote ?? string.Empty].Value);
        }

        // ---------------------------------------------------------------- Bündeln je Konto
        var sent = 0;

        foreach (var (userId, news) in lines)
        {
            await sender.SendAsync(
                recipients[userId].Email!,
                messages["Mail_DigestSubject", news.Count].Value,
                string.Join(Environment.NewLine, news),
                ct);

            sent++;
        }

        return sent;
    }
}
