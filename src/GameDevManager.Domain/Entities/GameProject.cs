namespace GameDevManager.Domain.Entities;

/// <summary>
/// Wurzel-Entität: ein Spielprojekt, dem alle Modul-Inhalte (Items, NPCs, Karten, …) zugeordnet werden.
/// </summary>
public class GameProject
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public required string Name { get; set; }

    public string? Description { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
