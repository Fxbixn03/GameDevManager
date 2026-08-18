namespace GameDevManager.Domain.Entities;

/// <summary>
/// Die Feld-Zuordnung des Kampf-Simulators: welches benutzerdefinierte Feld welche Rolle
/// spielt (Leben, Schaden, Verteidigung, Tempo). Der Simulator verdrahtet bewusst keine
/// Feldnamen — die Felder definiert der Nutzer, also ordnet er sie auch zu.
/// <para>
/// Eine Zeile je Projekt, Werkzeug-Konfiguration nach dem Muster von <c>DashboardCard</c>:
/// nicht im Export, übersteht den ersetzenden Import. Die Feld-GUIDs stehen ohne
/// Fremdschlüssel da — ein gelöschtes Feld macht die Rolle wieder unzugeordnet, statt die
/// Zeile zu reißen.
/// </para>
/// </summary>
public class CombatMapping
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid GameProjectId { get; set; }

    public GameProject? GameProject { get; set; }

    /// <summary>Die Lebenspunkte — ohne sie endet kein Kampf.</summary>
    public Guid? HealthFieldId { get; set; }

    /// <summary>Der Schaden je Treffer — ohne ihn gewinnt niemand.</summary>
    public Guid? DamageFieldId { get; set; }

    /// <summary>Die Verteidigung; unzugeordnet zählt sie als 0.</summary>
    public Guid? DefenseFieldId { get; set; }

    /// <summary>Das Tempo — es entscheidet Trefferchance und Zugreihenfolge; unzugeordnet 0.</summary>
    public Guid? SpeedFieldId { get; set; }
}
