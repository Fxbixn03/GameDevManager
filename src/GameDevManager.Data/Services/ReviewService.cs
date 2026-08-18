using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace GameDevManager.Data.Services;

/// <summary>Eine offene Abnahme für das Dashboard-Band — mit aufgelöstem Entitätsnamen.</summary>
public sealed record OpenReview(
    Guid RequestId,
    Guid OwnerEntityId,
    string OwnerModuleKey,
    string EntityName,
    string RequestedBy,
    string? Note,
    DateTime CreatedAtUtc);

/// <summary>
/// Der Review-Workflow: Aus dem Bearbeitungsstand „im Review“ wird ein Vorgang — Inhalt zur
/// Abnahme geben (mit Empfänger), der Empfänger gibt frei oder lehnt mit Anmerkung ab.
/// <para>
/// Die Stand-Wechsel laufen über die verfolgte Entität und ein gewöhnliches
/// <c>SaveChanges</c>: Damit hält das Änderungsprotokoll von selbst fest, <b>wer</b>
/// freigegeben hat — der Entscheider ist der angemeldete Benutzer des Speicherns. Freigabe
/// setzt auf „Fertig“, Ablehnung zurück auf „In Arbeit“.
/// </para>
/// </summary>
public class ReviewService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    IEnumerable<IModuleEntitySource> sources,
    IChangeAuthorProvider author,
    IStringLocalizer<DataMessages> messages)
{
    /// <summary>
    /// Gibt eine Entität zur Abnahme. Je Entität höchstens eine offene Anfrage — zwei
    /// Empfänger für dieselbe Frage widersprächen sich spätestens bei der Entscheidung.
    /// Setzt den Bearbeitungsstand auf „im Review“ und liefert den neuen Zeitstempel
    /// zurück, damit die offene Maske ihn übernimmt — sonst meldete ihr nächstes Speichern
    /// einen Schreibkonflikt mit diesem Aufruf.
    /// </summary>
    public async Task<DateTime> RequestAsync(
        Guid projectId,
        string moduleKey,
        Guid entityId,
        Guid? assignedUserId,
        string? note,
        CancellationToken ct = default)
    {
        var source = sources.FirstOrDefault(s => s.ModuleKey == moduleKey)
            ?? throw new ContentValidationException(messages["ReviewModuleUnknown"].Value);

        await using var db = await factory.CreateDbContextAsync(ct);

        if (await db.ReviewRequests.AnyAsync(
                request => request.OwnerEntityId == entityId
                    && request.Decision == ReviewDecision.Pending, ct))
        {
            throw new ContentValidationException(messages["ReviewAlreadyOpen"].Value);
        }

        // Verfolgt geladen, damit der Stand-Wechsel durch Schreibschutz und Protokoll läuft.
        var entity = (await source.LoadForBulkAsync(db, projectId, [entityId], ct)).FirstOrDefault()
            ?? throw new ContentValidationException(messages["ReviewEntityMissing"].Value);

        if (assignedUserId is { } userId
            && !await db.AppUsers.AnyAsync(user => user.Id == userId && !user.IsDisabled, ct))
        {
            throw new ContentValidationException(messages["ReviewAssigneeUnknown"].Value);
        }

        var requestedBy = (await author.GetCurrentAsync(ct)).UserName;

        db.ReviewRequests.Add(new ReviewRequest
        {
            GameProjectId = projectId,
            OwnerEntityId = entityId,
            OwnerModuleKey = moduleKey,
            RequestedBy = requestedBy,
            AssignedUserId = assignedUserId,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim()
        });

        entity.Status = ContentStatus.InReview;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return entity.UpdatedAtUtc;
    }

    /// <summary>
    /// Entscheidet eine offene Abnahme. Freigabe setzt den Stand auf „Fertig“, Ablehnung
    /// zurück auf „In Arbeit“ — und verlangt eine Anmerkung: Ein „nein“ ohne Begründung
    /// ließe den Autor raten, was zu tun ist.
    /// </summary>
    public async Task DecideAsync(
        Guid requestId, bool approve, string? note, CancellationToken ct = default)
    {
        if (!approve && string.IsNullOrWhiteSpace(note))
        {
            throw new ContentValidationException(messages["ReviewRejectionNeedsNote"].Value);
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        var request = await db.ReviewRequests
            .FirstOrDefaultAsync(r => r.Id == requestId && r.Decision == ReviewDecision.Pending, ct)
            ?? throw new ContentValidationException(messages["ReviewAlreadyDecided"].Value);

        var source = sources.FirstOrDefault(s => s.ModuleKey == request.OwnerModuleKey)
            ?? throw new ContentValidationException(messages["ReviewModuleUnknown"].Value);

        var entity = (await source.LoadForBulkAsync(
            db, request.GameProjectId, [request.OwnerEntityId], ct)).FirstOrDefault();

        request.Decision = approve ? ReviewDecision.Approved : ReviewDecision.Rejected;
        request.DecisionNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        request.DecidedBy = (await author.GetCurrentAsync(ct)).UserName;
        request.DecidedAtUtc = DateTime.UtcNow;

        // Die Entität kann inzwischen gelöscht sein — die Entscheidung gilt trotzdem, nur
        // gibt es keinen Stand mehr zu setzen. (EntityCleanup räumt solche Anfragen ohnehin
        // ab; das hier fängt das Rennen dazwischen.)
        if (entity is not null)
        {
            entity.Status = approve ? ContentStatus.Done : ContentStatus.InProgress;
            entity.UpdatedAtUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Die jüngste Anfrage einer Entität — offen oder entschieden, für die Maske.</summary>
    public async Task<ReviewRequest?> GetLatestForEntityAsync(
        Guid entityId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.ReviewRequests
            .AsNoTracking()
            .Include(request => request.AssignedUser)
            .Where(request => request.OwnerEntityId == entityId)
            .OrderByDescending(request => request.CreatedAtUtc)
            .ThenByDescending(request => request.Id)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Die offenen Abnahmen des angemeldeten Benutzers — das Dashboard-Band. Ohne Anmeldung
    /// leer, wie „Meine Aufgaben“. Entitätsnamen werden live über die Modul-Quellen
    /// aufgelöst; eine Anfrage, deren Entität verschwunden ist, fällt heraus statt mit
    /// leerem Namen zu stehen.
    /// </summary>
    public async Task<List<OpenReview>> GetMyOpenAsync(
        Guid projectId, int limit, CancellationToken ct = default)
    {
        var userId = (await author.GetCurrentAsync(ct)).UserId;

        if (userId is null)
        {
            return [];
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        var requests = await db.ReviewRequests
            .AsNoTracking()
            .Where(request => request.GameProjectId == projectId
                && request.AssignedUserId == userId
                && request.Decision == ReviewDecision.Pending)
            .OrderBy(request => request.CreatedAtUtc)
            .Take(limit)
            .ToListAsync(ct);

        var reviews = new List<OpenReview>(requests.Count);

        foreach (var group in requests.GroupBy(request => request.OwnerModuleKey))
        {
            var source = sources.FirstOrDefault(s => s.ModuleKey == group.Key);

            if (source is null)
            {
                continue;
            }

            var names = await source.ResolveNamesAsync(
                db, [.. group.Select(request => request.OwnerEntityId)], ct);

            reviews.AddRange(group
                .Where(request => names.ContainsKey(request.OwnerEntityId))
                .Select(request => new OpenReview(
                    request.Id,
                    request.OwnerEntityId,
                    request.OwnerModuleKey,
                    names[request.OwnerEntityId],
                    request.RequestedBy,
                    request.Note,
                    request.CreatedAtUtc)));
        }

        return [.. reviews.OrderBy(review => review.CreatedAtUtc)];
    }
}
