using System.Globalization;

namespace GameDevManager.Web.Services;

/// <summary>
/// Die Sprache der <b>Oberfläche</b> — nicht die der Spielinhalte, die steht im
/// Lokalisierungs-Modul.
/// <para>
/// Installationsweit festgehalten, aus demselben Grund wie die Hell/Dunkel-Wahl und die
/// Projektauswahl: Das Tool wird self-hosted von einer Person betrieben. Der Startwert kommt
/// aus <c>Ui:Language</c> in <c>appsettings.Local.json</c>; ohne Eintrag gilt Deutsch — das
/// ist die neutrale Sprache aller <c>.resx</c>-Dateien.
/// </para>
/// <para>
/// Ein Wechsel schreibt den Wert zurück und setzt die Kultur des laufenden Prozesses. Weil
/// Blazor Server seine Komponenten serverseitig rendert, ist die Kultur des Threads das, was
/// der <c>IStringLocalizer</c> liest — ein Cookie je Browser wäre bei einem Ein-Personen-Tool
/// der aufwendigere Weg zum selben Ergebnis.
/// </para>
/// </summary>
public class LanguageSelection(IConfiguration configuration, LocalSettingsFile settings)
{
    /// <summary>Die Sprachen, für die es Texte gibt. Deutsch ist die neutrale Fassung.</summary>
    public static readonly IReadOnlyList<(string Code, string Name)> Available =
    [
        ("de", "Deutsch"),
        ("en", "English")
    ];

    public string Code { get; private set; } = Normalize(configuration["Ui:Language"]);

    public async Task SetLanguageAsync(string code, CancellationToken ct = default)
    {
        Code = Normalize(code);

        Apply();

        await settings.WriteLanguageAsync(Code, ct);
    }

    /// <summary>
    /// Setzt die Kultur für alles, was danach gerendert wird. Wird beim Start einmal gerufen
    /// und bei jedem Wechsel.
    /// </summary>
    public void Apply()
    {
        var culture = new CultureInfo(Code);

        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    /// <summary>Unbekanntes fällt auf Deutsch zurück — die Sprache, in der die Texte ohnehin stehen.</summary>
    private static string Normalize(string? code) =>
        Available.Any(entry => string.Equals(entry.Code, code, StringComparison.OrdinalIgnoreCase))
            ? code!.ToLowerInvariant()
            : "de";
}
