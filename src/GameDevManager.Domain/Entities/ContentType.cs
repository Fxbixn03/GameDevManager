namespace GameDevManager.Domain.Entities;

/// <summary>
/// Eine benutzerdefinierte Art innerhalb eines Moduls — im Items-Modul z. B. „Waffe" oder
/// „Rüstung", im NPC-Modul „Händler" oder „Boss".
/// <para>
/// Die Art trägt die Felder, die alle Entitäten dieser Art gemeinsam haben. Zusätzliche
/// Felder für eine einzelne Entität hängen direkt an der Entität
/// (siehe <see cref="FieldDefinition.OwnerEntityId"/>).
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

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<FieldDefinition> Fields { get; set; } = [];
}
