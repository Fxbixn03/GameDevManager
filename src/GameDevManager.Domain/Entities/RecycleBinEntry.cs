namespace GameDevManager.Domain.Entities;

/// <summary>
/// Eine gelöschte Entität, aufbewahrt für den Fall, dass der Klick ein Versehen war.
/// <para>
/// Aufbewahrt wird sie als <b>JSON-Baum</b> und nicht als Soft-Delete-Schalter an
/// <see cref="ContentEntity"/>: Der zöge eine Filterbedingung durch jede Abfrage des gesamten
/// Bestands — Listen, Suche, Referenzansicht, Export, Health Checks — und wäre die Sorte
/// Änderung, die man an einer Stelle vergisst. Dieselbe Strecke wie beim Duplizieren, nur
/// rückwärts: serialisieren, aufbewahren, mit den <b>originalen</b> GUIDs zurücklesen.
/// </para>
/// <para>
/// Werkzeug-Daten wie das Änderungsprotokoll: nicht im Export, überstehen den ersetzenden
/// Import. Aufbewahrt nach einer Regel aus der Konfiguration — was von allein wächst, muss von
/// allein wieder abnehmen.
/// </para>
/// </summary>
public class RecycleBinEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid GameProjectId { get; set; }

    public GameProject? GameProject { get; set; }

    /// <summary>Modul der gelöschten Entität — siehe <see cref="ModuleKeys"/>.</summary>
    public required string ModuleKey { get; set; }

    /// <summary>Die ursprüngliche GUID. Sie kehrt beim Wiederherstellen unverändert zurück.</summary>
    public Guid EntityId { get; set; }

    /// <summary>
    /// Der Name zum Zeitpunkt des Löschens — eine Momentaufnahme wie im
    /// <see cref="ChangeLogEntry"/>: Es gibt nichts mehr aufzulösen.
    /// </summary>
    public required string EntityName { get; set; }

    /// <summary>
    /// Der vollständige Baum als JSON: die Entität samt Kind-Sammlungen, dazu Feldwerte,
    /// individuelle Felder und Bedingungssätze aller beteiligten GUIDs. Ein Text und keine
    /// Tabellenkopie — so kommt ein neu hinzugekommenes Kind ohne Zutun mit, genau wie beim
    /// Duplizieren.
    /// </summary>
    public required string Payload { get; set; }

    public DateTime DeletedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Wer gelöscht hat. Wird wie beim <see cref="ChangeLogEntry"/> leer angelegt und vom
    /// <c>ChangeLogInterceptor</c> nachgetragen — den angemeldeten Benutzer kennt nur er.
    /// </summary>
    public string DeletedBy { get; set; } = string.Empty;
}
