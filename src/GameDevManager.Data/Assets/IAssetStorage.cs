namespace GameDevManager.Data.Assets;

/// <summary>
/// Ablage der hochgeladenen Dateien. Die Datenbank kennt nur den Schlüssel, den
/// <see cref="SaveAsync"/> zurückgibt.
/// </summary>
public interface IAssetStorage
{
    /// <summary>Legt eine Datei ab und liefert ihren Schlüssel.</summary>
    Task<string> SaveAsync(
        Guid projectId, Guid assetId, string extension, Stream content, CancellationToken ct = default);

    /// <summary>Öffnet eine abgelegte Datei zum Lesen, oder <c>null</c> wenn sie fehlt.</summary>
    Stream? OpenRead(string storageKey);

    /// <summary>Entfernt eine Datei. Eine bereits fehlende Datei ist kein Fehler.</summary>
    void Delete(string storageKey);

    /// <summary>
    /// Alle Schlüssel, die im Speicher liegen — die Gegenrichtung zu den Zeilen in der
    /// Datenbank. Nur dafür da, verwaiste Dateien zu finden; nichts im laufenden Betrieb
    /// zählt den Speicher durch.
    /// </summary>
    IReadOnlyList<string> ListKeys();
}
