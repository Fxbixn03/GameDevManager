namespace GameDevManager.Domain.Entities;

/// <summary>
/// Sichtbarkeit und Reihenfolge einer Card auf dem Dashboard, je Projekt. Eine Zeile entsteht
/// erst, wenn der Nutzer das Dashboard anpasst — Cards ohne Zeile zeigt das Dashboard mit dem
/// Standard (sichtbar, Reihenfolge der Registry). Die Import/Export-Card ist laut Konzept
/// immer fest sichtbar und deshalb bewusst nicht konfigurierbar.
/// </summary>
public class DashboardCard
{
    /// <summary>Schlüssel der Datenbank-Status-Card — die einzige konfigurierbare Card ohne Modul.</summary>
    public const string DatabaseCardKey = "database";

    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid GameProjectId { get; set; }

    public GameProject? GameProject { get; set; }

    /// <summary>Modul-Schlüssel oder <see cref="DatabaseCardKey"/>.</summary>
    public required string CardKey { get; set; }

    public bool IsHidden { get; set; }

    public int SortOrder { get; set; }
}
