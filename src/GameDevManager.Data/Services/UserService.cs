using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace GameDevManager.Data.Services;

/// <summary>Eine Zeile der Benutzerverwaltung — ohne den Hash, der nirgends hin muss.</summary>
public sealed record UserRow(
    Guid Id,
    string UserName,
    string DisplayName,
    bool IsAdministrator,
    bool IsDisabled,
    DateTime CreatedAtUtc,
    DateTime? LastLoginAtUtc);

/// <summary>
/// Die Benutzer der Installation: anlegen, ändern, sperren, entfernen — und anmelden.
/// <para>
/// Benutzer hängen an keinem Projekt, wie die Projekte selbst auch. Wer sich anmeldet, darf
/// jedes Projekt bearbeiten; feinere Rechte wären eine Rollenverwaltung, die das Konzept nicht
/// verlangt. Unterschieden wird allein, wer <b>weitere Benutzer</b> verwalten darf.
/// </para>
/// </summary>
public class UserService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    IStringLocalizer<DataMessages> messages)
{
    /// <summary>
    /// Ob überhaupt schon ein Konto besteht. Ist keines da, führt der erste Aufruf der
    /// Anwendung in die Ersteinrichtung — es gibt bewusst kein ausgeliefertes Standardkonto.
    /// </summary>
    public async Task<bool> HasAnyUserAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.AppUsers.AnyAsync(ct);
    }

    public async Task<List<UserRow>> GetUsersAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.AppUsers
            .AsNoTracking()
            .OrderBy(user => user.UserName)
            .Select(user => new UserRow(
                user.Id,
                user.UserName,
                user.DisplayName,
                user.IsAdministrator,
                user.IsDisabled,
                user.CreatedAtUtc,
                user.LastLoginAtUtc))
            .ToListAsync(ct);
    }

    public async Task<UserRow?> GetUserAsync(Guid userId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.AppUsers
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new UserRow(
                user.Id,
                user.UserName,
                user.DisplayName,
                user.IsAdministrator,
                user.IsDisabled,
                user.CreatedAtUtc,
                user.LastLoginAtUtc))
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Legt einen Benutzer an. Der erste ist immer Verwalter, egal was übergeben wurde —
    /// sonst käme nach der Ersteinrichtung niemand mehr an die Benutzerverwaltung.
    /// </summary>
    public async Task<Guid> CreateUserAsync(
        string userName, string displayName, string password, bool isAdministrator,
        CancellationToken ct = default)
    {
        var name = Normalize(userName)
            ?? throw new ContentValidationException(messages["UserNameRequired"]);

        ValidatePassword(password);

        await using var db = await factory.CreateDbContextAsync(ct);

        var lowered = name.ToLowerInvariant();
        if (await db.AppUsers.AnyAsync(user => user.UserName.ToLower() == lowered, ct))
        {
            throw new ContentValidationException(messages["UserNameExists", name]);
        }

        var isFirst = !await db.AppUsers.AnyAsync(ct);

        var created = new AppUser
        {
            UserName = name,
            DisplayName = Normalize(displayName) ?? name,
            PasswordHash = PasswordHasher.Hash(password),
            IsAdministrator = isAdministrator || isFirst
        };

        db.AppUsers.Add(created);
        await db.SaveChangesAsync(ct);

        return created.Id;
    }

    /// <summary>Ändert Anzeigename, Verwalterrecht und Sperre. Das Passwort läuft über einen eigenen Weg.</summary>
    public async Task UpdateUserAsync(
        Guid userId, string displayName, bool isAdministrator, bool isDisabled,
        CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var user = await db.AppUsers.FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new ContentValidationException(messages["UserNotFound"]);

        // Der letzte Verwalter darf sich weder das Recht nehmen noch sich sperren — danach
        // käme niemand mehr an die Benutzerverwaltung, und das ließe sich nur noch in der
        // Datenbank von Hand geradebiegen.
        if (user.IsAdministrator && (!isAdministrator || isDisabled) && await IsLastAdministratorAsync(db, userId, ct))
        {
            throw new ContentValidationException(messages["UserLastAdministrator"]);
        }

        user.DisplayName = Normalize(displayName) ?? user.UserName;
        user.IsAdministrator = isAdministrator;
        user.IsDisabled = isDisabled;

        await db.SaveChangesAsync(ct);
    }

    public async Task SetPasswordAsync(Guid userId, string password, CancellationToken ct = default)
    {
        ValidatePassword(password);

        await using var db = await factory.CreateDbContextAsync(ct);

        var user = await db.AppUsers.FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new ContentValidationException(messages["UserNotFound"]);

        user.PasswordHash = PasswordHasher.Hash(password);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Ändert das eigene Passwort — nur gegen das alte.</summary>
    public async Task ChangeOwnPasswordAsync(
        Guid userId, string currentPassword, string newPassword, CancellationToken ct = default)
    {
        ValidatePassword(newPassword);

        await using var db = await factory.CreateDbContextAsync(ct);

        var user = await db.AppUsers.FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new ContentValidationException(messages["UserNotFound"]);

        if (!PasswordHasher.Verify(currentPassword, user.PasswordHash))
        {
            throw new ContentValidationException(messages["UserCurrentPasswordWrong"]);
        }

        user.PasswordHash = PasswordHasher.Hash(newPassword);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteUserAsync(Guid userId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var user = await db.AppUsers.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
        {
            return;
        }

        if (await IsLastAdministratorAsync(db, userId, ct))
        {
            throw new ContentValidationException(messages["UserLastAdministrator"]);
        }

        db.AppUsers.Remove(user);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Prüft Anmeldename und Passwort. <c>null</c> heißt abgelehnt — ohne zu verraten, woran
    /// es lag: Ein „diesen Benutzer gibt es nicht“ wäre eine Auskunft über bestehende Konten.
    /// </summary>
    public async Task<UserRow?> AuthenticateAsync(
        string userName, string password, CancellationToken ct = default)
    {
        var name = Normalize(userName);
        if (name is null || string.IsNullOrEmpty(password))
        {
            return null;
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        var lowered = name.ToLowerInvariant();
        var user = await db.AppUsers.FirstOrDefaultAsync(u => u.UserName.ToLower() == lowered, ct);

        if (user is null || user.IsDisabled || !PasswordHasher.Verify(password, user.PasswordHash))
        {
            return null;
        }

        user.LastLoginAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return new UserRow(
            user.Id, user.UserName, user.DisplayName, user.IsAdministrator,
            user.IsDisabled, user.CreatedAtUtc, user.LastLoginAtUtc);
    }

    /// <summary>Ob außer diesem Benutzer kein einsatzfähiger Verwalter mehr übrig bliebe.</summary>
    private static async Task<bool> IsLastAdministratorAsync(
        GameDevManagerDbContext db, Guid userId, CancellationToken ct) =>
        !await db.AppUsers
            .AnyAsync(other => other.Id != userId && other.IsAdministrator && !other.IsDisabled, ct);

    private void ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < PasswordHasher.MinimumLength)
        {
            throw new ContentValidationException(messages["UserPasswordTooShort", PasswordHasher.MinimumLength]);
        }
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
