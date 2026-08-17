using GameDevManager.Domain.Entities;

namespace GameDevManager.Data.Services;

/// <summary>Womit ein Feldwert verglichen wird.</summary>
public enum FieldComparison
{
    /// <summary>Der Text enthält den gesuchten — der Normalfall bei Text.</summary>
    Contains = 0,

    Equals = 1,

    /// <summary>Zahl größer als. Bei Text und Ja/Nein ohne Bedeutung.</summary>
    GreaterThan = 2,

    LessThan = 3,

    /// <summary>Das Feld ist nicht ausgefüllt — die Frage, die eine Bestandspflege am häufigsten stellt.</summary>
    IsEmpty = 4,

    IsNotEmpty = 5
}

/// <summary>Eine Bedingung an einem benutzerdefinierten Feld.</summary>
public sealed class FieldCriterion
{
    public Guid FieldDefinitionId { get; set; }

    public FieldComparison Comparison { get; set; }

    /// <summary>
    /// Der Vergleichswert als Text — auch für Zahlen. Ein Text und keine Wertspalten je Typ:
    /// Der Filter geht als JSON in die gespeicherte Ansicht, und ein neuer Feldtyp soll dort
    /// keine Migration verlangen. Umgewandelt wird beim Auswerten, in fester Kultur.
    /// </summary>
    public string? Value { get; set; }
}

/// <summary>
/// Ein Filter über den Bestand eines Moduls — „alle Waffen ohne Sprite, Schaden über 50, Status
/// Entwurf“.
/// <para>
/// Eine Klasse und kein Satz von Parametern: Sie geht als JSON in eine gespeicherte Ansicht
/// (<see cref="SavedView"/>), und neue Filterarten brauchen dafür keine Migration — dieselbe
/// Überlegung wie bei den Kurven.
/// </para>
/// <para>
/// Alle gesetzten Kriterien müssen zutreffen. Ein „oder“ gibt es bewusst nicht: Es verdoppelte
/// die Bedienoberfläche für einen Fall, den zwei gespeicherte Ansichten genauso lösen.
/// </para>
/// </summary>
public sealed class ContentFilter
{
    /// <summary>Freitext über Name und Beschreibung.</summary>
    public string? Text { get; set; }

    /// <summary>Nur diese Art. <c>null</c> heißt „alle“.</summary>
    public Guid? ContentTypeId { get; set; }

    /// <summary>
    /// Ob Unterarten der gewählten Art mitzählen. Vorgabe ja — wer „Waffe“ filtert, meint fast
    /// immer auch „Nahkampf“ und „Fernkampf“.
    /// </summary>
    public bool IncludeSubtypes { get; set; } = true;

    /// <summary>Nur diese Bearbeitungsstände. Leer heißt „alle“.</summary>
    public List<ContentStatus> Statuses { get; set; } = [];

    /// <summary>Nur Entitäten ohne primäres Sprite.</summary>
    public bool WithoutSprite { get; set; }

    /// <summary>Nur Varianten, die ein Vorbild haben.</summary>
    public bool OnlyVariants { get; set; }

    /// <summary>Nur Entitäten mit allen diesen Tags.</summary>
    public List<Guid> TagIds { get; set; } = [];

    /// <summary>Bedingungen an benutzerdefinierten Feldern.</summary>
    public List<FieldCriterion> Fields { get; set; } = [];

    /// <summary>
    /// Die gewählte Art samt ihrer Unterarten, aufgelöst vom <c>SavedViewService</c> kurz vor
    /// der Abfrage. Steht am Filter, damit die Modul-Quelle sie ohne zweiten Parameter kennt,
    /// und wird bewusst <b>nicht</b> mitgespeichert: Wer später eine Unterart anlegt, soll sie
    /// in der gespeicherten Ansicht wiederfinden, ohne sie neu zu wählen.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public List<Guid> ExpandedTypeIds { get; set; } = [];

    /// <summary>Ob überhaupt eingeschränkt wird — eine leere Filterdefinition zeigt alles.</summary>
    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(Text)
        && ContentTypeId is null
        && Statuses.Count == 0
        && !WithoutSprite
        && !OnlyVariants
        && TagIds.Count == 0
        && Fields.Count == 0;
}
