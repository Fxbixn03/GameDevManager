namespace GameDevManager.Domain.Entities;

/// <summary>
/// Eine benutzerdefinierte Art innerhalb eines Moduls — im Items-Modul z. B. „Waffe" oder
/// „Rüstung", im NPC-Modul „Händler" oder „Boss".
/// <para>
/// Die Art trägt die Felder, die alle Entitäten dieser Art gemeinsam haben. Zusätzliche
/// Felder für eine einzelne Entität hängen direkt an der Entität
/// (siehe <see cref="FieldDefinition.OwnerEntityId"/>).
/// </para>
/// <para>
/// Arten können ineinander stecken: „Waffe“ mit den Unterarten „Nahkampf“, „Fernkampf“ und
/// „Magie“. Eine Unterart <b>erbt</b> die Felder ihrer Eltern-Art — das ist der Grund, warum
/// die Hierarchie hier und nicht über das Tag-Modul abgebildet ist: Tags tragen keine Felder.
/// </para>
/// </summary>
public class ContentType
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid GameProjectId { get; set; }

    public GameProject? GameProject { get; set; }

    /// <summary>Modul, zu dem die Art gehört — siehe <see cref="ModuleKeys"/>.</summary>
    public required string ModuleKey { get; set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    /// <summary>Optionaler Material-Icon-Name, mit dem die Art in Listen dargestellt wird.</summary>
    public string? Icon { get; set; }

    /// <summary>Reihenfolge in Auswahllisten; bei Gleichstand wird nach Name sortiert.</summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Die Eltern-Art, deren Felder diese hier erbt. <c>null</c> heißt: eine Art auf oberster
    /// Ebene. Die Eltern-Art liegt immer im selben Projekt und Modul.
    /// </summary>
    public Guid? ParentId { get; set; }

    public ContentType? Parent { get; set; }

    public List<ContentType> Children { get; set; } = [];

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Die eigenen Felder dieser Art — ohne die geerbten.</summary>
    public List<FieldDefinition> Fields { get; set; } = [];

    /// <summary>
    /// Die Felder der Eltern-Arten, oberste zuerst. Nicht persistiert und nicht im Export:
    /// Die Felder stehen an ihrer Eltern-Art, hier sind sie nur zusammengetragen. Gefüllt
    /// wird die Liste vom <c>ContentTypeService</c>; wer eine Art anders lädt, bekommt sie leer.
    /// </summary>
    public List<FieldDefinition> InheritedFields { get; set; } = [];
}
