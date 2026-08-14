using System.Security.Claims;
using GameDevManager.Data.Services;
using Microsoft.AspNetCore.Components.Authorization;

namespace GameDevManager.Web.Services;

/// <summary>
/// Beantwortet dem Änderungsprotokoll, wer gerade handelt.
/// <para>
/// Die Antwort steht in der Web-Schicht — beim angemeldeten Benutzer der Verbindung —, gebraucht
/// wird sie aber beim Speichern in der Datenschicht. Deshalb dort eine Schnittstelle
/// (<see cref="IChangeAuthorProvider"/>) und hier ihre einzige echte Umsetzung.
/// </para>
/// <para>
/// Gefragt werden zwei Quellen, weil gespeichert wird an Stellen mit sehr verschiedenem
/// Umfeld: Ein laufender Blazor-Kreis hat keinen <c>HttpContext</c> mehr und kennt seinen
/// Benutzer nur über den <see cref="AuthenticationStateProvider"/>; eine statisch gerenderte
/// Seite und ein Endpunkt haben umgekehrt einen <c>HttpContext</c>, aber keinen Kreis. Wo
/// beides fehlt — beim Anwendungsstart etwa —, bleibt es beim Systemnamen.
/// </para>
/// </summary>
public sealed class BlazorChangeAuthorProvider(
    IHttpContextAccessor httpContext,
    AuthenticationStateProvider authentication,
    ISystemUserName fallback) : IChangeAuthorProvider
{
    private ChangeAuthor? _author;

    public async ValueTask<ChangeAuthor> GetCurrentAsync(CancellationToken ct = default) =>
        // Innerhalb einer Verbindung wechselt der Benutzer nicht — eine Abmeldung beendet den
        // Kreis und damit diesen Dienst. Ein Speichervorgang fragt ihn je Entität.
        _author ??= Describe(await FindUserAsync());

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
            // Außerhalb eines Razor-Kreises wirft der Anbieter statt „niemand angemeldet“ zu
            // melden — so läuft die Startmigration, die ebenfalls speichert.
            return null;
        }
    }

    private ChangeAuthor Describe(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            // Ohne Anmeldung geschieht in der Oberfläche nichts — das ist der Weg des
            // Anwendungsstarts und der Ersteinrichtung.
            return new ChangeAuthor(null, fallback.Name);
        }

        var name = user.FindFirst(ClaimTypes.Name)?.Value;

        return new ChangeAuthor(
            user.UserId(),
            string.IsNullOrWhiteSpace(name) ? fallback.Name : name);
    }
}

/// <summary>
/// Der Name, der ohne Anmeldung im Protokoll steht. Ein eigener Dienst, damit der Text wie
/// jeder andere sichtbare Text aus einer resx kommt und nicht im Code steht.
/// </summary>
public interface ISystemUserName
{
    string Name { get; }
}
