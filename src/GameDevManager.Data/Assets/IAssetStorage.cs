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
}
