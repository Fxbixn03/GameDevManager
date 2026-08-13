namespace GameDevManager.Data.Services;

/// <summary>
/// Wird aus dem Konfigurationsabschnitt "Exports" gebunden. Aufbewahrte Exportstände liegen
/// wie die Assets im Dateisystem, nicht in der Datenbank — das hält das Verhalten über alle
/// vier Provider gleich und die Datenbanksicherungen klein.
/// </summary>
public class ExportStorageOptions
{
    public const string SectionName = "Exports";

    /// <summary>
    /// Wurzelverzeichnis der aufbewahrten Exportstände. Ein relativer Pfad wird beim
    /// Registrieren gegen das Anwendungsverzeichnis aufgelöst und landet in <see cref="RootPath"/>.
    /// </summary>
    public string StoragePath { get; set; } = "exports";

    /// <summary>Der aufgelöste absolute Pfad. Wird beim Registrieren gesetzt.</summary>
    public string RootPath { get; set; } = string.Empty;
}
