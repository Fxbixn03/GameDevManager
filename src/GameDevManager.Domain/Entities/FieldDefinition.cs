namespace GameDevManager.Domain.Entities;

/// <summary>
/// Ein vom Nutzer definiertes Feld. Es gibt genau zwei Ausprägungen, die sich gegenseitig
/// ausschließen:
/// <list type="bullet">
/// <item><description>
/// <b>Art-Feld</b> — <see cref="ContentTypeId"/> ist gesetzt. Das Feld gilt für alle Entitäten
/// dieser Art (jede „Waffe" hat einen Schadenswert).
/// </description></item>
/// <item><description>
/// <b>Individuelles Feld</b> — <see cref="OwnerEntityId"/> ist gesetzt. Das Feld gilt nur für
/// diese eine Entität; damit werden exotische Items mit einzigartigen Werten abgebildet.
/// </description></item>
/// </list>
/// </summary>
public class FieldDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Modul, zu dem das Feld gehört — siehe <see cref="ModuleKeys"/>.</summary>
    public required string ModuleKey { get; set; }

    /// <summary>Gesetzt bei Art-Feldern, sonst <c>null</c>.</summary>
    public Guid? ContentTypeId { get; set; }

    public ContentType? ContentType { get; set; }

    /// <summary>
    /// Gesetzt bei individuellen Feldern: die GUID der Entität, der das Feld allein gehört.
    /// Bewusst ohne Fremdschlüssel, weil die Zielentität in jedem beliebigen Modul liegen kann.
    /// </summary>
    public Guid? OwnerEntityId { get; set; }

    public required string Name { get; set; }

    /// <summary>Hilfetext, der in der Maske unter dem Eingabefeld steht.</summary>
    public string? Description { get; set; }

    public ContentFieldType Type { get; set; } = ContentFieldType.Text;

    public bool IsRequired { get; set; }

    /// <summary>
    /// Nur bei <see cref="ContentFieldType.Text"/>: Das Feld nimmt statt eines einzelnen Textes
    /// mehrere Stichwörter auf (Elemente eines Zaubers, Schadensarten einer Waffe). Erfasst
    /// werden sie als Chips, gespeichert kanonisch kommagetrennt in
    /// <see cref="FieldValue.TextValue"/> — siehe <see cref="KeywordList"/>.
    /// <para>
    /// Bewusst ein Schalter am Textfeld und kein eigener <see cref="ContentFieldType"/>: Der
    /// Wert bleibt Text und damit durchsuchbar, und ein bestehendes Textfeld lässt sich ohne
    /// Verlust seiner Werte umstellen — ein Typwechsel löscht sie.
    /// </para>
    /// </summary>
    public bool IsTagList { get; set; }

    /// <summary>Trägt dieses Feld eine Stichwortliste? Nur Textfelder können das.</summary>
    public bool IsKeywordField => IsTagList && Type == ContentFieldType.Text;

    /// <summary>Einheit zur reinen Anzeige, z. B. „kg", „s" oder „%".</summary>
    public string? Unit { get; set; }

    /// <summary>
    /// Nur bei <see cref="ContentFieldType.EntityReference"/>: Modul, auf dessen Entitäten das Feld
    /// verweisen darf — siehe <see cref="ModuleKeys"/>.
    /// </summary>
    public string? ReferenceModuleKey { get; set; }

    public int SortOrder { get; set; }

    /// <summary>Auswahlmöglichkeiten bei <see cref="ContentFieldType.Select"/>.</summary>
    public List<FieldOption> Options { get; set; } = [];

    /// <summary>Ein individuelles Feld gehört genau einer Entität, kein Art-Feld.</summary>
    public bool IsIndividual => OwnerEntityId is not null;

    /// <summary>
    /// Tiefe Kopie inklusive der Auswahlmöglichkeiten. Die Bearbeitungsdialoge arbeiten darauf,
    /// damit ein Abbruch die geladene Definition unberührt lässt.
    /// </summary>
    public FieldDefinition Clone() => new()
    {
        Id = Id,
        ModuleKey = ModuleKey,
        ContentTypeId = ContentTypeId,
        OwnerEntityId = OwnerEntityId,
        Name = Name,
        Description = Description,
        Type = Type,
        IsRequired = IsRequired,
        IsTagList = IsTagList,
        Unit = Unit,
        ReferenceModuleKey = ReferenceModuleKey,
        SortOrder = SortOrder,
        Options = [.. Options.Select(option => option.Clone())]
    };
}
