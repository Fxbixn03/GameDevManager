namespace GameDevManager.Domain.Entities;

/// <summary>
/// Eine Anmerkung an einer Entität — „Schaden ist zu hoch, siehe Playtest vom 3.“.
/// <para>
/// Angehängt wie Feldwerte, Bedingungen und Übersetzungen über <see cref="OwnerEntityId"/> plus
/// <see cref="OwnerModuleKey"/>, ohne Fremdschlüssel: Die Entität kann in jedem Modul liegen.
/// <c>EntityCleanup</c> räumt beim Löschen mit ab.
/// </para>
/// <para>
/// Der <b>Urheber steht als Momentaufnahme</b> im Eintrag und nicht als Verweis — dieselbe
/// Überlegung wie beim <see cref="ChangeLogEntry"/>: Nach dem Löschen eines Kontos gäbe es
/// nichts mehr aufzulösen, und gerade dann will man wissen, von wem die Anmerkung war.
/// </para>
/// <para>
/// Werkzeug-Daten wie das Änderungsprotokoll: nicht im Export, sie überstehen den ersetzenden
/// Import. Eine Anmerkung ist eine Aussage über die Arbeit am Inhalt, nicht über den Inhalt.
/// </para>
/// </summary>
public class ContentComment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid GameProjectId { get; set; }

    public GameProject? GameProject { get; set; }

    public Guid OwnerEntityId { get; set; }

    /// <summary>Modul der besitzenden Entität — siehe <see cref="ModuleKeys"/>.</summary>
    public required string OwnerModuleKey { get; set; }

    public required string Text { get; set; }

    /// <summary>Wer die Anmerkung geschrieben hat — als Name, nicht als Verweis.</summary>
    public required string AuthorName { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Wann sie als erledigt markiert wurde; <c>null</c> heißt „offen“. Erledigte Anmerkungen
    /// bleiben stehen statt gelöscht zu werden — sie sind der Beleg, dass etwas besprochen war.
    /// </summary>
    public DateTime? ResolvedAtUtc { get; set; }

    /// <summary>Wer sie erledigt hat — ebenfalls als Momentaufnahme.</summary>
    public string? ResolvedBy { get; set; }

    public bool IsResolved => ResolvedAtUtc is not null;
}
