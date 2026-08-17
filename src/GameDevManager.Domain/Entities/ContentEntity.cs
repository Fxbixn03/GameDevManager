namespace GameDevManager.Domain.Entities;

/// <summary>
/// Wie weit eine Entität ist. Eine echte Spalte an <see cref="ContentEntity"/> und keine Art
/// und kein benutzerdefiniertes Feld: Das Tool filtert danach, der Export kennt sie, und sie
/// bedeutet in jedem der Inhaltsmodule dasselbe — genau das kann eine nutzerdefinierte Art
/// nicht leisten.
/// <para>
/// Die Zahlen stehen in der Datenbank und dürfen sich nicht mehr ändern. <b>Entwurf</b> ist die
/// Null und damit der Stand alles Bestehenden — was vor dieser Erweiterung angelegt wurde, ist
/// ausdrücklich noch nicht als fertig erklärt worden.
/// </para>
/// </summary>
public enum ContentStatus
{
    /// <summary>Angelegt, aber noch nicht ausgearbeitet.</summary>
    Draft = 0,

    /// <summary>Wird gerade bearbeitet.</summary>
    InProgress = 1,

    /// <summary>Fertig aus Sicht des Autors, wartet auf Durchsicht.</summary>
    InReview = 2,

    /// <summary>Abgenommen — worauf sich alle verlassen können.</summary>
    Done = 3
}

/// <summary>
/// Gemeinsame Basis aller fachlichen Inhalte (Items, NPCs, Quests, …).
/// <para>
/// Jedes Modul bekommt seine eigene Tabelle, weil die Module später sehr unterschiedliche
/// Beziehungen brauchen (Rezept-Zutaten, Händler-Angebote, Karten-Marker). Gemeinsam sind
/// nur GUID, Projektzugehörigkeit, Art und die Stammdaten — die restlichen Felder definiert
/// der Nutzer über <see cref="FieldDefinition"/> und <see cref="FieldValue"/>.
/// </para>
/// </summary>
public abstract class ContentEntity : IChangeLogged
{
    /// <summary>Zugleich die Referenz-GUID, über die andere Module diese Entität ansprechen.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid GameProjectId { get; set; }

    public GameProject? GameProject { get; set; }

    /// <summary>Die Art der Entität. <c>null</c> heißt „ohne Art" — erlaubt, damit man schnell erfassen kann.</summary>
    public Guid? ContentTypeId { get; set; }

    public ContentType? ContentType { get; set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    /// <summary>
    /// Das Vorbild dieser Entität — „Eisenschwert +1“ ist eine Variante von „Eisenschwert“ und
    /// übernimmt jeden Feldwert, den es nicht selbst setzt. <c>null</c> heißt „eigenständig“.
    /// <para>
    /// Eine GUID <b>ohne Fremdschlüssel</b> wie alles Modulübergreifende — obwohl das Vorbild
    /// hier immer im selben Modul und damit in derselben Tabelle liegt: Ein Fremdschlüssel auf
    /// dieselbe Tabelle liefe im Kreis, und seine Löschregeln wären über die vier Provider
    /// nicht einheitlich zu bekommen. Dieselbe Überlegung wie bei
    /// <see cref="DialogueChoice.NextLineId"/>.
    /// </para>
    /// <para>
    /// Geerbt werden <b>Feldwerte</b> und sonst nichts. Name, Beschreibung und Bearbeitungsstand
    /// bleiben eigen — eine Variante, die den Namen ihres Vorbilds trüge, wäre in keiner Liste
    /// wiederzufinden; und ein Sprite erbt sie nicht, weil eine Variante fast immer anders
    /// aussieht.
    /// </para>
    /// </summary>
    public Guid? BasedOnId { get; set; }

    /// <summary>
    /// Bearbeitungsstand — Entwurf, in Arbeit, im Review, fertig. Gilt in allen Inhaltsmodulen
    /// auf einmal, weil die Spalte an dieser Basis hängt.
    /// </summary>
    public ContentStatus Status { get; set; } = ContentStatus.Draft;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Modul der Entität — siehe <see cref="ModuleKeys"/>. Nicht persistiert.</summary>
    public abstract string ModuleKey { get; }
}
