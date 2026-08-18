namespace GameDevManager.Domain.Entities;

/// <summary>
/// Ein bewusst stummgeschalteter Health-Check-Fund: „das ist so gewollt“. Stummgeschaltet
/// wird je Entität und Prüfart — der Fund zählt dann nicht mehr im Zustandsband, bleibt auf
/// der Statistik-Seite aber einsehbar.
/// <para>
/// Werkzeug-Daten wie das Änderungsprotokoll: nicht im Export, überstehen den ersetzenden
/// Import. Verschwindet der Fund (die Entität ist gelöscht oder das Problem behoben), fällt
/// die Stummschaltung beim nächsten Prüf-Lauf weg — kehrt der Fund später zurück, meldet er
/// sich wieder, statt still zu bleiben.
/// </para>
/// </summary>
public class HealthCheckMute
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid GameProjectId { get; set; }

    public GameProject? GameProject { get; set; }

    /// <summary>Der Schlüssel der Prüfung — dieselben Werte wie im Zustandsband.</summary>
    public required string CheckKey { get; set; }

    /// <summary>Die Entität, um die es beim Fund geht. GUID-Referenz ohne Fremdschlüssel.</summary>
    public Guid EntityId { get; set; }

    /// <summary>
    /// Name der Entität als Momentaufnahme — für die Liste der Stummschaltungen, wie beim
    /// Änderungsprotokoll: Nach dem Löschen gäbe es nichts mehr aufzulösen.
    /// </summary>
    public string? EntityName { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
