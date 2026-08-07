namespace GameDevManager.Data.Assets;

/// <summary>
/// Wird aus dem Konfigurationsabschnitt "Assets" gebunden.
/// </summary>
public class AssetStorageOptions
{
    public const string SectionName = "Assets";

    /// <summary>
    /// Wurzelverzeichnis der hochgeladenen Dateien. Ein relativer Pfad wird beim Registrieren
    /// gegen das Anwendungsverzeichnis aufgelöst und landet in <see cref="RootPath"/>.
    /// </summary>
    public string StoragePath { get; set; } = "assets";

    public long MaxFileSizeBytes { get; set; } = 20 * 1024 * 1024;

    /// <summary>
    /// Erlaubte MIME-Typen. Absichtlich eine Positivliste — hochgeladene Dateien werden später
    /// wieder ausgeliefert, und alles außerhalb dieser Liste hat dort nichts zu suchen.
    /// </summary>
    public List<string> AllowedMimeTypes { get; set; } =
    [
        "audio/mpeg",
        "audio/ogg",
        "audio/wav",
        "image/png",
        "image/jpeg",
        "image/gif",
        "image/webp",
        "image/bmp",
        "image/svg+xml"
    ];

    /// <summary>Der aufgelöste absolute Pfad. Wird beim Registrieren gesetzt.</summary>
    public string RootPath { get; set; } = string.Empty;
}
