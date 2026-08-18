using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GameDevManager.Data.Services;

/// <summary>
/// Schreibt das Änderungsprotokoll mit — an einer Stelle für alle Module.
/// <para>
/// Das Konzept verlangt zu protokollieren, „welcher angemeldete Benutzer welche Änderungen
/// getan hat“. Das ließe sich in jedem der gut zwanzig Modul-Dienste von Hand eintragen; dann
/// fehlte es aber in dem einen, in dem man es vergisst, und ein neues Modul brächte es nicht
/// von selbst mit. Der Änderungsverfolger von EF weiß beim Speichern ohnehin genau, was neu
/// ist und welche Eigenschaften sich geändert haben — dieselbe Überlegung wie bei
/// <see cref="EntityCleanup"/>: einmal gebündelt statt je Modul wiederholt.
/// </para>
/// <para>
/// Nicht gesehen werden Löschungen: Die Modul-Dienste löschen über <c>ExecuteDeleteAsync</c>,
/// das am Änderungsverfolger vorbei direkt in der Datenbank arbeitet. Sie melden ihre
/// Löschung deshalb selbst über <see cref="ChangeLog.RecordDeletionAsync"/> — und lassen den
/// Benutzernamen dabei leer, damit auch dort nur diese Klasse beantwortet, wer gehandelt hat.
/// </para>
/// </summary>
public sealed class ChangeLogInterceptor(
    IChangeAuthorProvider authors, WebhookQueue webhooks, SyncEventBroadcaster sync)
    : SaveChangesInterceptor
{
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is GameDevManagerDbContext db)
        {
            await RecordAsync(db, cancellationToken);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>
    /// Der synchrone Weg. Er kommt in der Anwendung nicht vor — sie speichert durchgehend
    /// asynchron —, darf das Protokoll aber trotzdem nicht stillschweigend übergehen.
    /// </summary>
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is GameDevManagerDbContext db)
        {
            RecordAsync(db, CancellationToken.None).AsTask().GetAwaiter().GetResult();
        }

        return base.SavingChanges(eventData, result);
    }

    private async ValueTask RecordAsync(GameDevManagerDbContext db, CancellationToken ct)
    {
        var author = await authors.GetCurrentAsync(ct);

        // Zuerst die Einträge, die ein Dienst selbst schon angelegt hat (Löschungen und der
        // Sammeleintrag eines Imports): Sie tragen noch keinen Benutzer, weil nur hier steht,
        // wer gerade handelt.
        foreach (var pending in db.ChangeTracker.Entries<ChangeLogEntry>()
                     .Where(entry => entry.State == EntityState.Added
                         && string.IsNullOrEmpty(entry.Entity.UserName)))
        {
            pending.Entity.UserId = author.UserId;
            pending.Entity.UserName = author.UserName;

            // Der Live-Sync bekommt auch diese Einträge: Löschungen und die Sammeleinträge
            // von Import und Serie laufen nur hier vorbei — und gerade sie muss ein
            // verbundener Editor erfahren (Sammeleinträge tragen ModuleKey „changelog“ und
            // heißen für ihn: Voll-Abgleich).
            if (sync.HasSubscribers)
            {
                sync.Publish(new SyncEvent(
                    pending.Entity.GameProjectId, pending.Entity.ModuleKey, pending.Entity.EntityId,
                    pending.Entity.EntityName, pending.Entity.Action.ToString(), pending.Entity.AtUtc));
            }
        }

        // Papierkorb-Einträge nach derselben Regel: Sie entstehen in EntityCleanup, das den
        // angemeldeten Benutzer nicht kennt.
        foreach (var pending in db.ChangeTracker.Entries<RecycleBinEntry>()
                     .Where(entry => entry.State == EntityState.Added
                         && string.IsNullOrEmpty(entry.Entity.DeletedBy)))
        {
            pending.Entity.DeletedBy = author.UserName;
        }

        if (db.SuppressChangeLog)
        {
            return;
        }

        // Erst einsammeln, dann anhängen: Das Anhängen verändert den Änderungsverfolger,
        // über den gerade gelaufen wird.
        var entries = db.ChangeTracker.Entries<IChangeLogged>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(entry => Describe(entry, author))
            .Where(entry => entry is not null)
            .ToList();

        foreach (var entry in entries)
        {
            db.ChangeLogEntries.Add(entry!);

            // Dieselbe Stelle bedient die Webhooks: Der Interceptor sieht jede Änderung ohnehin,
            // und eingereiht wird nur in den Arbeitsspeicher — eine HTTP-Anfrage hier drin
            // hielte die Transaktion auf. Zugestellt wird aus dem Hintergrunddienst.
            webhooks.Enqueue(new WebhookEvent(
                entry!.GameProjectId, entry.ModuleKey, entry.EntityId, entry.EntityName,
                entry.Action, entry.UserName, entry.AtUtc));

            // Und dieselbe Stelle bedient den Live-Sync — reiner Arbeitsspeicher, kein HTTP.
            if (sync.HasSubscribers)
            {
                sync.Publish(new SyncEvent(
                    entry.GameProjectId, entry.ModuleKey, entry.EntityId, entry.EntityName,
                    entry.Action.ToString(), entry.AtUtc));
            }
        }
    }

    private static ChangeLogEntry? Describe(EntityEntry<IChangeLogged> entry, ChangeAuthor author)
    {
        var entity = entry.Entity;

        // Ohne Projekt gibt es nichts einzuordnen — das kommt nur bei einer noch unfertig
        // aufgebauten Entität vor und wäre ein Eintrag ohne Aussage.
        if (entity.GameProjectId == Guid.Empty)
        {
            return null;
        }

        var changed = entry.State == EntityState.Modified
            ? ChangedProperties(entry)
            : null;

        return new ChangeLogEntry
        {
            GameProjectId = entity.GameProjectId,
            UserId = author.UserId,
            UserName = author.UserName,
            ModuleKey = entity.ModuleKey,
            EntityId = entity.Id,
            EntityName = Shorten(entity.Name),
            Action = entry.State switch
            {
                EntityState.Added => ChangeAction.Created,
                EntityState.Deleted => ChangeAction.Deleted,
                _ => ChangeAction.Updated
            },
            Details = changed
        };
    }

    /// <summary>
    /// Die geänderten Eigenschaften als lesbare Aufzählung. Der Zeitstempel bleibt draußen —
    /// er ändert sich bei jedem Speichern und stünde sonst in jedem Eintrag.
    /// </summary>
    private static string? ChangedProperties(EntityEntry<IChangeLogged> entry)
    {
        var names = entry.Properties
            .Where(property => property.IsModified
                && property.Metadata.Name != nameof(ContentEntity.UpdatedAtUtc))
            .Select(property => property.Metadata.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        return names.Count == 0 ? null : Shorten(string.Join(", ", names), 2000);
    }

    private static string Shorten(string value, int limit = 200) =>
        value.Length <= limit ? value : string.Concat(value.AsSpan(0, limit - 1), "…");
}
