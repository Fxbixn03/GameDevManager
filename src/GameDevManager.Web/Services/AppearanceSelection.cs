namespace GameDevManager.Web.Services;

/// <summary>
/// Hält die Hell/Dunkel-Wahl fest — installationsweit, aus demselben Grund wie die
/// <see cref="ProjectSelection"/>: Das Tool wird self-hosted von einer Person betrieben.
/// <para>
/// Der Startwert kommt aus der Konfiguration (<c>Appearance:DarkMode</c>, geschrieben nach
/// <c>appsettings.Local.json</c>); ohne Eintrag gilt Dunkel, das ist das Grunddesign.
/// Ein Wechsel schreibt den Wert zurück, damit er einen Neustart überlebt.
/// </para>
/// </summary>
public class AppearanceSelection(IConfiguration configuration, LocalSettingsFile settings)
{
    public bool IsDarkMode { get; private set; } =
        !bool.TryParse(configuration["Appearance:DarkMode"], out var dark) || dark;

    public async Task SetDarkModeAsync(bool isDarkMode, CancellationToken ct = default)
    {
        IsDarkMode = isDarkMode;
        await settings.WriteDarkModeAsync(isDarkMode, ct);
    }
}
