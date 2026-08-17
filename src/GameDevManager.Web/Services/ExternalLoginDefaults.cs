namespace GameDevManager.Web.Services;

/// <summary>
/// Die Namen der beiden Schemata, über die die externe Anmeldung läuft.
/// <para>
/// Zwei, nicht eines: Der Anbieter beweist nur, <b>wer</b> da ist. Ob dieser Jemand hier ein
/// Konto hat, entscheidet erst die Rückkehr-Seite — und bis dahin darf das eigentliche
/// Anmelde-Cookie nicht stehen, sonst wäre jeder angemeldet, der beim Anbieter ein Konto hat.
/// </para>
/// </summary>
public static class ExternalLoginDefaults
{
    /// <summary>Das OIDC-Schema, das die Anmeldung beim Anbieter anstößt.</summary>
    public const string Scheme = "oidc";

    /// <summary>
    /// Das kurzlebige Cookie, in dem das Ergebnis des Anbieters landet, bis die Rückkehr-Seite
    /// es gegen ein Konto eingetauscht hat.
    /// </summary>
    public const string TemporaryScheme = "gdm.external";
}
