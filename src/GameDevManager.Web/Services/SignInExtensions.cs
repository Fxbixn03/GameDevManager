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

    public static Task SignInWithUserAsync(this HttpContext http, UserRow user, bool persistent)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.DisplayName),
                // Der Anmeldename steht getrennt daneben: Angezeigt wird der Anzeigename,
                // gemeint ist beim Anmelden aber dieser hier.
                new Claim(ClaimTypes.Upn, user.UserName),
                new Claim(AdministratorClaim, user.IsAdministrator ? "true" : "false")
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
}
