namespace GameDevManager.Domain.Entities;

/// <summary>Was mit einer Entität geschehen ist. Die Zahlenwerte stehen in der Datenbank.</summary>
public enum ChangeAction
{
    Created = 0,

    Updated = 1,

    Deleted = 2,

    /// <summary>
    /// Ein Import hat den Bestand ersetzt. Bewusst ein einziger Eintrag statt einer Zeile je
    /// Entität: Ein Import ist eine Handlung, kein Tausend-Einzeländerungen-Vorgang, und ein
    /// Protokoll, das von einem Import geflutet wird, ist danach unlesbar.
    /// </summary>
    Imported = 3
}

/// <summary>
/// Ein Eintrag des Änderungsprotokolls: wer hat wann was getan.
/// <para>
/// Name des Benutzers und Name der Entität stehen als <b>Momentaufnahme</b> darin und nicht
/// als Verweis. Nach dem Löschen einer Entität gäbe es nichts mehr aufzulösen, und genau
/// dieser Eintrag ist der wichtigste — dieselbe Überlegung wie beim Export, der Referenzen
/// als GUID mitnimmt und Namen ausschreibt.
/// </para>
/// <para>
/// Das Protokoll gehört zum Werkzeug und nicht zum Spielinhalt: Es steht wie die
/// Moduleinstellungen und die Dashboard-Bänder <b>nicht im Export</b> und übersteht den
/// ersetzenden Import — der als eigener Eintrag darin auftaucht.
/// </para>
/// </summary>
public class ChangeLogEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid GameProjectId { get; set; }

    public GameProject? GameProject { get; set; }

    public DateTime AtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Der handelnde Benutzer. <c>null</c> bei allem, was ohne Anmeldung geschieht —
    /// Ersteinrichtung, Wartungsaufgaben, Tests.
    /// </summary>
    public Guid? UserId { get; set; }

    public required string UserName { get; set; }

    /// <summary>Modul der geänderten Entität — siehe <see cref="ModuleKeys"/>.</summary>
    public required string ModuleKey { get; set; }

    public Guid EntityId { get; set; }

    public required string EntityName { get; set; }

    public ChangeAction Action { get; set; }

    /// <summary>
    /// Die geänderten Eigenschaften, mit Komma getrennt — bei <see cref="ChangeAction.Updated"/>
    /// die eigentliche Auskunft. Bewusst nur die Namen und nicht alt/neu: Der Wert eines Feldes
    /// kann ein ganzer Beschreibungstext sein, und das Protokoll soll die Datenbank nicht
    /// verdoppeln.
    /// </summary>
    public string? Details { get; set; }
}
