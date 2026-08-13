namespace GameDevManager.Web.Services;

/// <summary>
/// Hält installationsweit fest, welches Spielprojekt gerade bearbeitet wird. Singleton, weil
/// das Tool self-hosted von einer Person betrieben wird — alle Verbindungen arbeiten auf
/// demselben Projekt, so wie es auch nur einen Dateispeicher und eine Datenbank gibt.
/// <para>
/// Der Startwert kommt aus der Konfiguration (<c>Project:CurrentId</c>, geschrieben nach
/// <c>appsettings.Local.json</c>); Wechsel zur Laufzeit schreibt der
/// <see cref="ProjectContext"/> über die <see cref="LocalSettingsFile"/> dorthin zurück,
/// damit die Auswahl einen Neustart überlebt.
/// </para>
/// </summary>
public class ProjectSelection(IConfiguration configuration)
{
    public Guid? CurrentId { get; set; } =
        Guid.TryParse(configuration["Project:CurrentId"], out var id) ? id : null;
}
