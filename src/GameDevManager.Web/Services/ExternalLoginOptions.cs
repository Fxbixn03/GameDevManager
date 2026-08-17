namespace GameDevManager.Web.Services;

/// <summary>
/// Die Anmeldung über einen externen Anbieter (F46) — GitHub, Google oder ein eigener
/// OIDC-Server. Wird aus dem Konfigurationsabschnitt „ExternalLogin“ gebunden.
/// <para>
/// Konfiguration und keine Tabelle: Der Anbieter gehört zur Installation, nicht zum Projekt —
/// dieselbe Linie wie bei der Passwortrichtlinie und den Aufbewahrungsregeln. Ohne
/// <see cref="Authority"/> und <see cref="ClientId"/> ist die externe Anmeldung schlicht
/// abgeschaltet, und die Anmeldeseite zeigt den Knopf gar nicht erst.
/// </para>
/// </summary>
public class ExternalLoginOptions
{
    public const string SectionName = "ExternalLogin";

    /// <summary>Der Name, der auf dem Knopf steht — „Mit GitHub anmelden“ heißt hier „GitHub“.</summary>
    public string DisplayName { get; set; } = "OpenID Connect";

    /// <summary>Die Adresse des Anbieters, unter der seine Konfiguration liegt.</summary>
    public string? Authority { get; set; }

    public string? ClientId { get; set; }

    public string? ClientSecret { get; set; }

    /// <summary>
    /// Zusätzlich angeforderte Bereiche. <c>openid</c> und <c>profile</c> kommen ohnehin —
    /// hier steht, was ein bestimmter Anbieter darüber hinaus verlangt.
    /// </summary>
    public List<string> Scopes { get; set; } = [];

    /// <summary>Ob die Anmeldung über den Anbieter überhaupt eingerichtet ist.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Authority) && !string.IsNullOrWhiteSpace(ClientId);
}
