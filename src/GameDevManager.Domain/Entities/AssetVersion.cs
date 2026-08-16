namespace GameDevManager.Domain.Entities;

/// <summary>
/// Eine frühere Fassung einer Datei. Sie entsteht, wenn ein <see cref="Asset"/> ersetzt wird:
/// Die Zeile bleibt stehen — und damit jede GUID-Referenz darauf —, die alte Datei wandert
/// hierher.
/// <para>
/// Der Vorteil gegenüber „neu hochladen und altes löschen“ ist der eigentliche Zweck: Die
/// <b>GUID ändert sich nicht</b>, alle Verweise bleiben, und der Diff zweier Exportstände
/// zeigt dieselbe Grafik in neu statt einer gelöschten und einer neuen.
/// </para>
/// <para>
/// Aufbewahrt wird wie bei den Exportständen nach einer Regel aus der Konfiguration
/// (<c>Assets:MaxVersionsPerAsset</c>) — was von allein wächst, muss von allein wieder
/// abnehmen.
/// </para>
/// </summary>
public class AssetVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AssetId { get; set; }

    public Asset? Asset { get; set; }

    /// <summary>Der Schlüssel der abgelegten Datei — wie beim Asset selbst.</summary>
    public required string StorageKey { get; set; }

    public required string FileName { get; set; }

    public required string MimeType { get; set; }

    public long SizeBytes { get; set; }

    public int? Width { get; set; }

    public int? Height { get; set; }

    /// <summary>Wann diese Fassung abgelöst wurde.</summary>
    public DateTime ReplacedAtUtc { get; set; } = DateTime.UtcNow;
}
