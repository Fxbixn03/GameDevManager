namespace GameDevManager.Domain.Entities;

/// <summary>
/// Eine Ebene auf einer Karte — „NPCs“, „Gebiete“, „Notizen“. Markierungen können einer
/// Ebene zugeordnet werden (<see cref="MapMarker.LayerId"/>); im Karten-Editor lassen sich
/// Ebenen ein- und ausblenden, und die aktive Ebene nimmt neu gesetzte Markierungen auf.
/// <para>
/// Die Sichtbarkeit ist persistiert, damit eine aufgeräumte Ansicht („Notizen aus“) beim
/// nächsten Öffnen so wiederkommt. Welche Ebene gerade aktiv ist, bleibt dagegen
/// Bedien-Zustand des Editors — das gehört nicht zum Projektstand.
/// </para>
/// </summary>
public class MapLayer
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid MapId { get; set; }

    public GameMap? Map { get; set; }

    public required string Name { get; set; }

    /// <summary>Ausgeblendete Ebenen werden im Editor nicht gezeichnet.</summary>
    public bool IsVisible { get; set; } = true;

    public int SortOrder { get; set; }
}
