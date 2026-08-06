namespace GameDevManager.Domain.Entities;

/// <summary>
/// Ein frei definierbares Stichwort für Assets — „Prio“, „Animation“, „Alternatives Design“.
/// Das Konzept gibt bewusst nichts vor; die Liste legt der Nutzer selbst an.
/// <para>
/// Bewusst auf Assets beschränkt. Das geplante Tag-Modul vergibt Tags modulübergreifend und
/// regelt, wo sie verfügbar sind — das ist eine andere Fachlichkeit und wird diese hier
/// voraussichtlich ablösen.
/// </para>
/// </summary>
public class AssetTag
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid GameProjectId { get; set; }

    public GameProject? GameProject { get; set; }

    public required string Name { get; set; }

    /// <summary>Optionale Farbe als Hex-Wert, damit sich Tags in der Bibliothek unterscheiden.</summary>
    public string? Color { get; set; }

    public int SortOrder { get; set; }

    public List<AssetTagAssignment> Assignments { get; set; } = [];
}
