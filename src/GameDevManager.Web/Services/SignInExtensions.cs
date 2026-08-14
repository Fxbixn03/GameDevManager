using System.Security.Claims;
using GameDevManager.Data.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace GameDevManager.Web.Services;

/// <summary>
/// Das Ausstellen des Anmelde-Cookies. An einer Stelle, weil es zwei Aufrufer gibt — die
/// Anmeldung und die Ersteinrichtung, die den ersten Benutzer gleich anmeldet.
/// </summary>
public static class SignInExtensions
{
    /// <summary>Die Ansprüche, die im Cookie stehen. Mehr braucht die Oberfläche nicht.</summary>
    public const string AdministratorClaim = "gdm:admin";

    /// <summary>Berechtigungen — siehe <see cref="UserPermissions"/>. „Alle Module“ steht als „*“.</summary>
    public const string WriteClaim = "gdm:write";

    public const string ExportClaim = "gdm:export";

    public const string ImportClaim = "gdm:import";

    public const string ModulesClaim = "gdm:modules";

    private const string AllModules = "*";

    public static Task SignInWithUserAsync(this HttpContext http, UserRow user, bool persistent)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.DisplayName),
                // Der Anmeldename steht getrennt daneben: Angezeigt wird der Anzeigename,
                // gemeint ist beim Anmelden aber dieser hier.
                new Claim(ClaimTypes.Upn, user.UserName),
                new Claim(AdministratorClaim, user.IsAdministrator ? "true" : "false"),
                // Die Berechtigungen wandern mit ins Cookie und gelten damit — wie das
                // Verwalterrecht — ab der nächsten Anmeldung, nicht rückwirkend in offene
                // Sitzungen. Für Verwalter stehen sie aufgelöst da (immer alles erlaubt).
                new Claim(WriteClaim, user.Permissions.CanWrite ? "true" : "false"),
                new Claim(ExportClaim, user.Permissions.CanExport ? "true" : "false"),
                new Claim(ImportClaim, user.Permissions.CanImport ? "true" : "false"),
                new Claim(ModulesClaim, user.Permissions.AllowedModules is null
                    ? AllModules
                    : string.Join(",", user.Permissions.AllowedModules.OrderBy(key => key, StringComparer.Ordinal)))
            ],
            CookieAuthenticationDefaults.AuthenticationScheme);

        return http.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = persistent });
    }

    /// <summary>Ob der angemeldete Benutzer Benutzer verwalten darf.</summary>
    public static bool IsAdministrator(this ClaimsPrincipal user) =>
        user.HasClaim(AdministratorClaim, "true");

    /// <summary>Die GUID des angemeldeten Benutzers, oder <c>null</c> ohne Anmeldung.</summary>
    public static Guid? UserId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;

    /// <summary>
    /// Die Berechtigungen aus den Ansprüchen des Cookies. Ein Cookie aus der Zeit vor den
    /// Berechtigungen trägt die Ansprüche nicht — ein fehlender zählt deshalb als erlaubt,
    /// so wie jedes Konto vor der Erweiterung alles durfte. Verwalter bekommen immer alles.
    /// </summary>
    public static UserPermissions Permissions(this ClaimsPrincipal user) =>
        UserPermissions.For(
            user.IsAdministrator(),
            canWrite: !user.HasClaim(WriteClaim, "false"),
            canExport: !user.HasClaim(ExportClaim, "false"),
            canImport: !user.HasClaim(ImportClaim, "false"),
            allowedModuleKeys: user.FindFirst(ModulesClaim)?.Value is { } modules && modules != AllModules
                ? modules
                : null);
}
