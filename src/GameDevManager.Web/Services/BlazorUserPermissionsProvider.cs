using System.Security.Claims;
using GameDevManager.Data.Services;
using Microsoft.AspNetCore.Components.Authorization;

namespace GameDevManager.Web.Services;

/// <summary>
/// Beantwortet der Datenschicht, was der handelnde Benutzer darf — das Gegenstück zum
/// <see cref="BlazorChangeAuthorProvider"/> und nach demselben Muster gebaut: Die Antwort
/// steht in den Ansprüchen des Anmelde-Cookies, gebraucht wird sie beim Speichern
/// (<c>WriteGuardInterceptor</c>) und in den Diensten (<c>PermissionGuard</c>).
/// <para>
/// Gefragt werden dieselben zwei Quellen: der <c>HttpContext</c> (statisch gerenderte Seiten,
/// Endpunkte) und der <see cref="AuthenticationStateProvider"/> (laufender Blazor-Kreis).
/// Wo beides fehlt — beim Anwendungsstart etwa — bleibt es bei „alles erlaubt“, denn dort
/// handelt niemand, den es einzuschränken gäbe.
/// </para>
/// </summary>
public sealed class BlazorUserPermissionsProvider(
    IHttpContextAccessor httpContext,
    AuthenticationStateProvider authentication) : IUserPermissionsProvider
{
    private UserPermissions? _current;

    public async ValueTask<UserPermissions> GetCurrentAsync(CancellationToken ct = default) =>
        // Innerhalb einer Verbindung wechselt der Benutzer nicht — eine Abmeldung beendet
        // den Kreis und damit diesen Dienst. Ein Speichervorgang fragt mehrfach.
        _current ??= Describe(await FindUserAsync());

    private async ValueTask<ClaimsPrincipal?> FindUserAsync()
    {
        if (httpContext.HttpContext?.User is { Identity.IsAuthenticated: true } fromRequest)
        {
            return fromRequest;
        }

        try
        {
            return (await authentication.GetAuthenticationStateAsync()).User;
        }
        catch (InvalidOperationException)
        {
            // Außerhalb eines Razor-Kreises wirft der Anbieter, statt „niemand angemeldet“
            // zu melden — derselbe Fall wie beim Urheber des Änderungsprotokolls.
            return null;
        }
    }

    private static UserPermissions Describe(ClaimsPrincipal? user) =>
        user?.Identity?.IsAuthenticated == true
            ? user.Permissions()
            : UserPermissions.Full;
}
