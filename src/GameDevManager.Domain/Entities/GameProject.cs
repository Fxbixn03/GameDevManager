namespace GameDevManager.Domain.Entities;

/// <summary>
/// Wurzel-Entität: ein Spielprojekt, dem alle Modul-Inhalte (Items, NPCs, Karten, …) zugeordnet werden.
/// </summary>
public class GameProject
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public required string Name { get; set; }

    public string? Description { get; set; }

    /// <summary>
    /// Ein archiviertes Projekt ist aus dem Weg, aber nicht weg: Es fällt aus Projektauswahl
    /// und Hintergrundläufen (Zeitplan-Stände, Wartung), behält seinen Bestand aber
    /// vollständig. Entarchivieren stellt alles wieder her — anders als das Löschen, das
    /// getrennt bleibt und sein Sicherheitsnetz behält.
    /// </summary>
    public bool IsArchived { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
