using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Localization;

namespace GameDevManager.Data.Services;

/// <summary>
/// Setzt das Schreibrecht durch — an einer Stelle für alle Module, wie beim Änderungsprotokoll:
/// Wer nicht schreiben darf, dessen <c>SaveChanges</c> wird abgewiesen, statt die Prüfung in
/// gut zwanzig Modul-Diensten zu wiederholen (und in einem davon zu vergessen).
/// <para>
/// Das deckt auch die Löschpfade ab, obwohl die über <c>ExecuteDeleteAsync</c> am
/// Änderungsverfolger vorbei arbeiten: Jeder Modul-Dienst schreibt unmittelbar davor — den
/// Protokolleintrag über <see cref="ChangeLog.RecordDeletionAsync"/> oder die Asset-Zeilen —
/// und dieses Speichern läuft hier auf. Die wenigen Pfade ganz ohne vorheriges Speichern
/// (reine <c>ExecuteDelete</c>-Aufrufe, Dateisystem-Vorgänge) prüfen selbst über den
/// <see cref="PermissionGuard"/>.
/// </para>
/// <para>
/// Ausgenommen ist allein <see cref="AppUser"/>: Das eigene Passwort ändern darf auch, wer
/// keine Inhalte schreiben darf, und die Benutzerverwaltung sichert der <c>UserService</c>
/// selbst über das Verwalterrecht ab.
/// </para>
/// </summary>
public sealed class WriteGuardInterceptor(
    IUserPermissionsProvider permissions,
    IStringLocalizer<DataMessages> messages) : SaveChangesInterceptor
{
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is GameDevManagerDbContext db)
        {
            await GuardAsync(db, cancellationToken);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>Der synchrone Weg — kommt in der Anwendung nicht vor, darf aber keine Lücke sein.</summary>
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is GameDevManagerDbContext db)
        {
            GuardAsync(db, CancellationToken.None).AsTask().GetAwaiter().GetResult();
        }

        return base.SavingChanges(eventData, result);
    }

    private async ValueTask GuardAsync(GameDevManagerDbContext db, CancellationToken ct)
    {
        var current = await permissions.GetCurrentAsync(ct);
        if (current.CanWrite)
        {
            return;
        }

        var writesContent = db.ChangeTracker.Entries()
            .Any(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted
                && entry.Entity is not AppUser);

        if (writesContent)
        {
            throw new ContentValidationException(messages["PermissionWriteDenied"]);
        }
    }
}
