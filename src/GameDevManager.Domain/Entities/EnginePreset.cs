namespace GameDevManager.Domain.Entities;

/// <summary>Die Engines, für die sich Presets bauen lassen.</summary>
public enum TargetEngine
{
    Unity = 0,

    Unreal = 1,

    Godot = 2
}

/// <summary>Woher der Wert einer Preset-Eigenschaft kommt.</summary>
public enum PresetSource
{
    /// <summary>Der Name der Entität.</summary>
    Name = 0,

    /// <summary>Ihre Beschreibung.</summary>
    Description = 1,

    /// <summary>Der Wert eines benutzerdefinierten Feldes — <see cref="EnginePresetMapping.FieldDefinitionId"/>.</summary>
    Field = 2,

    /// <summary>Ein fester Text, der in jedem erzeugten Objekt gleich steht.</summary>
    Constant = 3,

    /// <summary>Die GUID der Entität — der Schlüssel zurück ins Tool.</summary>
    EntityId = 4,

    /// <summary>Der Name ihrer Art.</summary>
    TypeName = 5,

    /// <summary>Der Dateiname ihres Icons, damit die Engine das Sprite findet.</summary>
    PrimaryAssetFile = 6
}

/// <summary>
/// Ein Bauplan für ein Objekt in einer Game Engine: „so sieht ein NPC in Unity aus“.
/// <para>
/// Das Konzept dahinter: Beim Export in eine Engine wählt man ein Preset, und aus jedem Eintrag
/// des zugeordneten Moduls entsteht ein fertig gefülltes Objekt — statt die Werte in der Engine
/// von Hand zusammenzusuchen. Was genau erzeugt wird, entscheidet die Engine
/// (<see cref="Engine"/>); die Zuordnung Inhalt → Eigenschaft steht in den
/// <see cref="Mappings"/>.
/// </para>
/// <para>
/// Bewusst <b>keine</b> <c>ContentEntity</c>: Ein Preset ist kein Spielinhalt, sondern eine
/// Vorschrift, wie Spielinhalt in eine Engine wandert. Es trägt deshalb weder Arten noch
/// Felder, taucht nicht in Suche und Referenzansicht auf und braucht kein Sprite.
/// </para>
/// </summary>
public class EnginePreset
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid GameProjectId { get; set; }

    public GameProject? GameProject { get; set; }

    public TargetEngine Engine { get; set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    /// <summary>Das Modul, dessen Einträge dieses Preset abbildet — siehe <see cref="ModuleKeys"/>.</summary>
    public required string ModuleKey { get; set; }

    /// <summary>
    /// Nur Einträge dieser Art werden erzeugt. <c>null</c> heißt „alle Einträge des Moduls“ —
    /// ein Projekt, das seine NPCs nicht in Arten trennt, soll trotzdem exportieren können.
    /// </summary>
    public Guid? ContentTypeId { get; set; }

    public ContentType? ContentType { get; set; }

    /// <summary>
    /// Wie der Typ in der Engine heißt: die ScriptableObject-Klasse (Unity), das Row-Struct
    /// der DataTable (Unreal) oder die Resource-Klasse (Godot). Er landet im erzeugten Objekt
    /// und ist der Name, unter dem die Engine es kennt.
    /// </summary>
    public required string TypeName { get; set; }

    public int SortOrder { get; set; }

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<EnginePresetMapping> Mappings { get; set; } = [];
}

/// <summary>Eine Eigenschaft des Engine-Objekts und woher ihr Wert kommt.</summary>
public class EnginePresetMapping
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid EnginePresetId { get; set; }

    public EnginePreset? EnginePreset { get; set; }

    /// <summary>Der Name der Eigenschaft im Engine-Objekt, z. B. <c>displayName</c> oder <c>maxHealth</c>.</summary>
    public required string Target { get; set; }

    public PresetSource Source { get; set; }

    /// <summary>Bei <see cref="PresetSource.Field"/>: welches Feld. Ohne Fremdschlüssel wie überall sonst.</summary>
    public Guid? FieldDefinitionId { get; set; }

    /// <summary>Bei <see cref="PresetSource.Constant"/>: der feste Text.</summary>
    public string? ConstantValue { get; set; }

    public int SortOrder { get; set; }
}
