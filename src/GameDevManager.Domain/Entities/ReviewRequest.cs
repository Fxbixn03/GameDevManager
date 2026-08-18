namespace GameDevManager.Domain.Entities;

/// <summary>Wie eine Abnahme ausgegangen ist. Offen ist die Null — der Zustand beim Anlegen.</summary>
public enum ReviewDecision
{
    /// <summary>Wartet auf den Empfänger.</summary>
    Pending = 0,

    /// <summary>Freigegeben — der Inhalt gilt als fertig.</summary>
    Approved = 1,

    /// <summary>Abgelehnt, mit Pflicht-Anmerkung — der Inhalt geht zurück in Arbeit.</summary>
    Rejected = 2
}

/// <summary>
/// Eine Abnahme-Anfrage: „Sieh dir das an und gib es frei.“ Sie macht aus dem
/// Bearbeitungsstand „im Review“ einen Vorgang mit Empfänger und Ergebnis.
/// <para>
/// Angehängt wie die Anmerkungen über <see cref="OwnerEntityId"/> plus
/// <see cref="OwnerModuleKey"/>, ohne Fremdschlüssel — die Entität kann in jedem Modul
/// liegen; <c>EntityCleanup</c> räumt beim Löschen mit ab. Der <b>Empfänger</b> ist dagegen
/// ein echter Fremdschlüssel auf das Konto (SetNull): Ein gelöschtes Konto nimmt seine
/// offenen Abnahmen nicht mit, sie stehen dann ohne Empfänger da — wie bei den Kanban-Karten.
/// </para>
/// <para>
/// Anforderer und Entscheider stehen als <b>Momentaufnahme des Namens</b> im Eintrag, wie
/// beim Änderungsprotokoll. Werkzeug-Daten: nicht im Export, sie überstehen den ersetzenden
/// Import — eine Abnahme ist eine Aussage über die Arbeit, nicht über den Inhalt.
/// </para>
/// </summary>
public class ReviewRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid GameProjectId { get; set; }

    public GameProject? GameProject { get; set; }

    public Guid OwnerEntityId { get; set; }

    /// <summary>Modul der besitzenden Entität — siehe <see cref="ModuleKeys"/>.</summary>
    public required string OwnerModuleKey { get; set; }

    /// <summary>Wer die Abnahme angefordert hat — als Name, nicht als Verweis.</summary>
    public required string RequestedBy { get; set; }

    /// <summary>
    /// Dieselbe Person als GUID, ohne Fremdschlüssel — für die Benachrichtigung über das
    /// Ergebnis. Der Name bleibt die Anzeige; die GUID findet das Postfach.
    /// </summary>
    public Guid? RequestedById { get; set; }

    /// <summary>Der Empfänger. Echter Fremdschlüssel mit SetNull, wie bei den Kanban-Karten.</summary>
    public Guid? AssignedUserId { get; set; }

    public AppUser? AssignedUser { get; set; }

    /// <summary>Was der Empfänger wissen soll — „bitte auf die Werte achten“.</summary>
    public string? Note { get; set; }

    public ReviewDecision Decision { get; set; }

    /// <summary>Die Anmerkung zur Entscheidung — bei einer Ablehnung Pflicht.</summary>
    public string? DecisionNote { get; set; }

    /// <summary>Wer entschieden hat — ebenfalls als Momentaufnahme.</summary>
    public string? DecidedBy { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? DecidedAtUtc { get; set; }

    public bool IsOpen => Decision == ReviewDecision.Pending;
}
