using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace GameDevManager.Data.Services;

/// <summary>Eine Rolle, wie die Benutzerverwaltung sie zeigt und der Dialog sie anbietet.</summary>
public sealed record UserRoleRow(
    Guid Id,
    string Name,
    bool CanWrite,
    bool CanExport,
    bool CanImport,
    string? AllowedModuleKeys,
    int MemberCount = 0);

/// <summary>Eine Zeile der Benutzerverwaltung — ohne den Hash, der nirgends hin muss.</summary>
public sealed record UserRow(
    Guid Id,
    string UserName,
    string DisplayName,
    bool IsAdministrator,
    bool IsDisabled,
    bool CanWrite,
    bool CanExport,
    bool CanImport,
    string? AllowedModuleKeys,
    DateTime CreatedAtUtc,
    DateTime? LastLoginAtUtc,
    UserRoleRow? Role = null,
    bool OverridesRole = false)
{
    /// <summary>
    /// Die aufgelösten Rechte dieser Zeile — Verwalter bekommen immer alles, sonst ist die
    /// Rolle die Vorgabe und die Konto-Spalten gelten nur ohne Rolle oder mit Abweichung.
    /// </summary>
    public UserPermissions Permissions =>
        UserPermissions.For(
            IsAdministrator, CanWrite, CanExport, CanImport, AllowedModuleKeys, Role, OverridesRole);
}

/// <summary>
/// Die Benutzer der Installation: anlegen, ändern, sperren, entfernen — und anmelden.
/// <para>
/// Benutzer hängen an keinem Projekt, wie die Projekte selbst auch. Unterschieden wird, wer
/// <b>weitere Benutzer</b> verwalten darf — und je Benutzer, was er darf: lesen oder auch
/// schreiben, welche Module er sieht, ob Export und Import offenstehen. Die Rechte landen
/// als Ansprüche im Anmelde-Cookie und gelten deshalb ab der nächsten Anmeldung.
/// </para>
/// </summary>
public class UserService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    IPasswordPolicyProvider passwordPolicy,
    PermissionGuard guard,
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
                user.CanWrite,
                user.CanExport,
                user.CanImport,
                user.AllowedModuleKeys,
                user.CreatedAtUtc,
                user.LastLoginAtUtc,
                user.Role == null
                    ? null
                    : new UserRoleRow(
                        user.Role.Id, user.Role.Name, user.Role.CanWrite, user.Role.CanExport,
                        user.Role.CanImport, user.Role.AllowedModuleKeys, 0),
                user.OverridesRole))
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
                user.CanWrite,
                user.CanExport,
                user.CanImport,
                user.AllowedModuleKeys,
                user.CreatedAtUtc,
                user.LastLoginAtUtc,
                user.Role == null
                    ? null
                    : new UserRoleRow(
                        user.Role.Id, user.Role.Name, user.Role.CanWrite, user.Role.CanExport,
                        user.Role.CanImport, user.Role.AllowedModuleKeys, 0),
                user.OverridesRole))
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Legt einen Benutzer an. Der erste ist immer Verwalter, egal was übergeben wurde —
    /// sonst käme nach der Ersteinrichtung niemand mehr an die Benutzerverwaltung.
    /// Die Berechtigungen stehen als Vorgabe auf „alles erlaubt“; für Verwalter zählen sie nicht.
    /// </summary>
    public async Task<Guid> CreateUserAsync(
        string userName, string displayName, string password, bool isAdministrator,
        bool canWrite = true, bool canExport = true, bool canImport = true,
        string? allowedModuleKeys = null,
        Guid? roleId = null, bool overridesRole = false,
        CancellationToken ct = default)
    {
        await guard.EnsureAdministratorAsync(ct);

        var name = Normalize(userName)
            ?? throw new ContentValidationException(messages["UserNameRequired"]);

        ValidatePassword(password);

        await using var db = await factory.CreateDbContextAsync(ct);

        var lowered = name.ToLowerInvariant();
        if (await db.AppUsers.AnyAsync(user => user.UserName.ToLower() == lowered, ct))
        {
            throw new ContentValidationException(messages["UserNameExists", name]);
        }

        await EnsureRoleExistsAsync(db, roleId, ct);

        var isFirst = !await db.AppUsers.AnyAsync(ct);

        var created = new AppUser
        {
            UserName = name,
            DisplayName = Normalize(displayName) ?? name,
            // Ohne Passwort (nur bei deaktivierten Passwörtern erreichbar) bleibt der Hash
            // leer — ein leerer Hash lässt bei wieder eingeschalteten Passwörtern niemanden
            // herein, bis ein Verwalter eines setzt.
            PasswordHash = string.IsNullOrWhiteSpace(password) ? string.Empty : PasswordHasher.Hash(password),
            IsAdministrator = isAdministrator || isFirst,
            CanWrite = canWrite,
            CanExport = canExport,
            CanImport = canImport,
            AllowedModuleKeys = UserPermissions.FormatModuleKeys(
                UserPermissions.ParseModuleKeys(allowedModuleKeys)),
            RoleId = roleId,
            // Eine Abweichung ohne Rolle wäre keine — sie hieße nur, was ohnehin gilt.
            OverridesRole = roleId is not null && overridesRole
        };

        db.AppUsers.Add(created);
        await db.SaveChangesAsync(ct);

        return created.Id;
    }

    /// <summary>
    /// Ändert Anzeigename, Verwalterrecht, Sperre und Berechtigungen. Das Passwort läuft über
    /// einen eigenen Weg. Geänderte Rechte greifen ab der nächsten Anmeldung — sie stehen als
    /// Ansprüche im Cookie, wie das Verwalterrecht auch.
    /// </summary>
    public async Task UpdateUserAsync(
        Guid userId, string displayName, bool isAdministrator, bool isDisabled,
        bool canWrite = true, bool canExport = true, bool canImport = true,
        string? allowedModuleKeys = null,
        Guid? roleId = null, bool overridesRole = false,
        CancellationToken ct = default)
    {
        await guard.EnsureAdministratorAsync(ct);

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

        await EnsureRoleExistsAsync(db, roleId, ct);

        user.DisplayName = Normalize(displayName) ?? user.UserName;
        user.IsAdministrator = isAdministrator;
        user.IsDisabled = isDisabled;
        user.CanWrite = canWrite;
        user.CanExport = canExport;
        user.CanImport = canImport;
        user.AllowedModuleKeys = UserPermissions.FormatModuleKeys(
            UserPermissions.ParseModuleKeys(allowedModuleKeys));
        user.RoleId = roleId;
        user.OverridesRole = roleId is not null && overridesRole;

        await db.SaveChangesAsync(ct);
    }

    // ----------------------------------------------------------------------- Rollen

    /// <summary>Alle Rollen samt Mitgliederzahl — für Verwaltung und Auswahl im Dialog.</summary>
    public async Task<List<UserRoleRow>> GetRolesAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.UserRoles
            .AsNoTracking()
            .OrderBy(role => role.Name)
            .Select(role => new UserRoleRow(
                role.Id,
                role.Name,
                role.CanWrite,
                role.CanExport,
                role.CanImport,
                role.AllowedModuleKeys,
                db.AppUsers.Count(user => user.RoleId == role.Id)))
            .ToListAsync(ct);
    }

    /// <summary>Legt eine Rolle an oder ändert sie — geänderte Rechte gelten ab der nächsten Anmeldung.</summary>
    public async Task<Guid> SaveRoleAsync(UserRole role, CancellationToken ct = default)
    {
        await guard.EnsureAdministratorAsync(ct);

        var name = Normalize(role.Name)
            ?? throw new ContentValidationException(messages["UserRoleNameRequired"]);

        await using var db = await factory.CreateDbContextAsync(ct);

        var lowered = name.ToLowerInvariant();
        if (await db.UserRoles.AnyAsync(other => other.Id != role.Id && other.Name.ToLower() == lowered, ct))
        {
            throw new ContentValidationException(messages["UserRoleNameExists", name]);
        }

        var stored = await db.UserRoles.FirstOrDefaultAsync(other => other.Id == role.Id, ct);

        if (stored is null)
        {
            stored = new UserRole { Id = role.Id, Name = name };
            db.UserRoles.Add(stored);
        }

        stored.Name = name;
        stored.CanWrite = role.CanWrite;
        stored.CanExport = role.CanExport;
        stored.CanImport = role.CanImport;
        stored.AllowedModuleKeys = UserPermissions.FormatModuleKeys(
            UserPermissions.ParseModuleKeys(role.AllowedModuleKeys));

        await db.SaveChangesAsync(ct);
        return stored.Id;
    }

    /// <summary>
    /// Löscht eine Rolle. Ihre Rechte werden vorher auf alle Konten gestempelt, die nicht
    /// abweichen — sonst fielen die auf ihre alten Konto-Spalten zurück und bekämen still
    /// mehr, als die Rolle erlaubte.
    /// </summary>
    public async Task DeleteRoleAsync(Guid roleId, CancellationToken ct = default)
    {
        await guard.EnsureAdministratorAsync(ct);

        await using var db = await factory.CreateDbContextAsync(ct);

        var role = await db.UserRoles.FirstOrDefaultAsync(other => other.Id == roleId, ct);
        if (role is null)
        {
            return;
        }

        foreach (var member in await db.AppUsers.Where(user => user.RoleId == roleId).ToListAsync(ct))
        {
            if (!member.OverridesRole)
            {
                member.CanWrite = role.CanWrite;
                member.CanExport = role.CanExport;
                member.CanImport = role.CanImport;
                member.AllowedModuleKeys = role.AllowedModuleKeys;
            }

            member.RoleId = null;
            member.OverridesRole = false;
        }

        db.UserRoles.Remove(role);
        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureRoleExistsAsync(
        GameDevManagerDbContext db, Guid? roleId, CancellationToken ct)
    {
        if (roleId is { } id && !await db.UserRoles.AnyAsync(role => role.Id == id, ct))
        {
            throw new ContentValidationException(messages["UserRoleNotFound"]);
        }
    }

    public async Task SetPasswordAsync(Guid userId, string password, CancellationToken ct = default)
    {
        await guard.EnsureAdministratorAsync(ct);

        EnsurePasswordsEnabled();
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
        EnsurePasswordsEnabled();
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
        await guard.EnsureAdministratorAsync(ct);

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
    /// Sind Passwörter per Richtlinie deaktiviert, genügt der Anmeldename; die Sperre gilt weiter.
    /// </summary>
    public async Task<UserRow?> AuthenticateAsync(
        string userName, string password, CancellationToken ct = default)
    {
        var policy = passwordPolicy.Current;

        var name = Normalize(userName);
        if (name is null || (!policy.PasswordsDisabled && string.IsNullOrEmpty(password)))
        {
            return null;
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        var lowered = name.ToLowerInvariant();

        // Mit Rolle: Ihre Rechte wandern über diese Zeile als Ansprüche ins Cookie.
        var user = await db.AppUsers
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserName.ToLower() == lowered, ct);

        if (user is null || user.IsDisabled)
        {
            return null;
        }

        if (!policy.PasswordsDisabled && !PasswordHasher.Verify(password, user.PasswordHash))
        {
            return null;
        }

        user.LastLoginAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return new UserRow(
            user.Id, user.UserName, user.DisplayName, user.IsAdministrator, user.IsDisabled,
            user.CanWrite, user.CanExport, user.CanImport, user.AllowedModuleKeys,
            user.CreatedAtUtc, user.LastLoginAtUtc,
            user.Role is null
                ? null
                : new UserRoleRow(
                    user.Role.Id, user.Role.Name, user.Role.CanWrite, user.Role.CanExport,
                    user.Role.CanImport, user.Role.AllowedModuleKeys),
            user.OverridesRole);
    }

    /// <summary>Ob außer diesem Benutzer kein einsatzfähiger Verwalter mehr übrig bliebe.</summary>
    private static async Task<bool> IsLastAdministratorAsync(
        GameDevManagerDbContext db, Guid userId, CancellationToken ct) =>
        !await db.AppUsers
            .AnyAsync(other => other.Id != userId && other.IsAdministrator && !other.IsDisabled, ct);

    /// <summary>Setzen und Ändern von Passwörtern gibt es nur, solange die Richtlinie sie kennt.</summary>
    private void EnsurePasswordsEnabled()
    {
        if (passwordPolicy.Current.PasswordsDisabled)
        {
            throw new ContentValidationException(messages["UserPasswordsDisabled"]);
        }
    }

    private void ValidatePassword(string password)
    {
        var policy = passwordPolicy.Current;

        // Ohne Passwörter gibt es nichts zu prüfen — beim Anlegen bleibt das Feld leer.
        if (policy.PasswordsDisabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(password) || password.Length < policy.MinimumLength)
        {
            throw new ContentValidationException(messages["UserPasswordTooShort", policy.MinimumLength]);
        }

        if (policy.RequireDigit && !password.Any(char.IsDigit))
        {
            throw new ContentValidationException(messages["UserPasswordNeedsDigit"]);
        }

        if (policy.RequireSpecialCharacter && password.All(char.IsLetterOrDigit))
        {
            throw new ContentValidationException(messages["UserPasswordNeedsSpecial"]);
        }
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
