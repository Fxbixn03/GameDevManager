namespace GameDevManager.Domain.Entities;

/// <summary>
/// Eine hochgeladene Datei — in aller Regel ein Sprite.
/// <para>
/// Ein Asset gehört entweder zu einer Entität eines Moduls (<see cref="OwnerEntityId"/> und
/// <see cref="OwnerModuleKey"/> gesetzt) oder zu keiner. Assets ohne Entität sind die
/// Werkzeug-Assets des Konzepts: Marker für Karten, Platzhalter und alles, was nur im
/// Management-Tool selbst gebraucht wird — und zugleich der Zwischenstand von Dateien, die
/// hochgeladen, aber noch nicht zugeordnet wurden.
/// </para>
/// <para>
/// Die Datei selbst liegt nicht in der Datenbank, sondern im Dateispeicher; hier steht nur
/// der <see cref="StorageKey"/>. Das hält das Verhalten über alle vier Datenbank-Provider
/// gleich und die Sicherungen der Datenbank klein.
/// </para>
/// </summary>
public class Asset
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid GameProjectId { get; set; }

    public GameProject? GameProject { get; set; }

    /// <summary>Modul der besitzenden Entität — <c>null</c> bei Werkzeug-Assets.</summary>
    public string? OwnerModuleKey { get; set; }

    /// <summary>
    /// GUID der besitzenden Entität. Wie bei den Feldwerten bewusst ohne Fremdschlüssel,
    /// weil die Entität in jedem beliebigen Modul liegen kann.
    /// </summary>
    public Guid? OwnerEntityId { get; set; }

    /// <summary>Ursprünglicher Dateiname, so wie der Nutzer ihn hochgeladen hat.</summary>
    public required string FileName { get; set; }

    /// <summary>MIME-Typ, für die Auslieferung an den Browser.</summary>
    public required string MimeType { get; set; }

    /// <summary>Pfad im Dateispeicher, relativ zu dessen Wurzel.</summary>
    public required string StorageKey { get; set; }

    public long SizeBytes { get; set; }

    /// <summary>Bildbreite in Pixeln, sofern sie sich aus der Datei lesen ließ.</summary>
    public int? Width { get; set; }

    public int? Height { get; set; }

    public string? Description { get; set; }

    /// <summary>
    /// Das Sprite, das die Module als Icon der Entität zeigen. Je Entität ist höchstens eines
    /// primär; der <c>AssetService</c> hält das beim Setzen nach.
    /// </summary>
    public bool IsPrimary { get; set; }

    /// <summary>Reihenfolge innerhalb einer Entität, z. B. für Animationsschritte.</summary>
    public int SortOrder { get; set; }

    public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;

    public List<AssetTagAssignment> Tags { get; set; } = [];

    /// <summary>Ein Asset ohne Entität — Marker, Platzhalter oder noch nicht zugeordnet.</summary>
    public bool IsToolAsset => OwnerEntityId is null;
    /// <summary>
    /// Frühere Fassungen dieser Datei. Sie stehen bewusst am Asset und nicht als eigene
    /// Zeilen daneben: Ohne ihr Asset bedeuten sie nichts, und sie fallen über den
    /// Fremdschlüssel mit — dieselbe Überlegung wie bei den Rezept-Zutaten.
    /// </summary>
    public List<AssetVersion> Versions { get; set; } = [];

}
