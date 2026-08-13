namespace GameDevManager.Domain.Entities;

/// <summary>
/// Sichtbarkeit und Reihenfolge eines Bandes auf dem Dashboard, je Projekt. Eine Zeile entsteht
/// erst, wenn der Nutzer das Dashboard anpasst — Bänder ohne Zeile zeigt das Dashboard mit dem
/// Standard aus <see cref="DashboardBands"/>.
/// <para>
/// Der Name der Klasse stammt aus der Zeit, als das Dashboard eine Karte je Modul zeigte. Die
/// Tabelle heißt weiter so, weil eine Umbenennung eine Migration in allen vier Providern
/// verlangte, ohne etwas zu gewinnen.
/// </para>
/// </summary>
public class DashboardCard
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid GameProjectId { get; set; }

    public GameProject? GameProject { get; set; }

    /// <summary>Schlüssel des Bandes — siehe <see cref="DashboardBands"/>.</summary>
    public required string CardKey { get; set; }

    public bool IsHidden { get; set; }

    public int SortOrder { get; set; }
}
