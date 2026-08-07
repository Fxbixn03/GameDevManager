namespace GameDevManager.Domain.Entities;

/// <summary>Wie die Bedingungen eines Satzes zusammenwirken.</summary>
public enum ConditionLogic
{
    /// <summary>Alle müssen zutreffen.</summary>
    All = 0,

    /// <summary>Mindestens eine muss zutreffen.</summary>
    Any = 1
}

/// <summary>
/// Ein Satz Bedingungen, der an irgendetwas hängt.
/// <para>
/// Das Konzept verlangt „ein einheitliches System, welches über alle Module hinweg verknüpfbar
/// ist“. Deshalb hängt der Satz — wie die Feldwerte — über eine GUID an seinem Besitzer und
/// nicht über einen Fremdschlüssel. Besitzer kann eine ganze Entität sein (ein NPC, ein Dialog)
/// oder ein Teilobjekt mit eigener GUID (ein einzelner Händler-Posten).
/// </para>
/// </summary>
public class ConditionSet
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid GameProjectId { get; set; }

    public GameProject? GameProject { get; set; }

    /// <summary>GUID dessen, wozu die Bedingungen gehören.</summary>
    public Guid OwnerId { get; set; }

    /// <summary>Modul des Besitzers — siehe <see cref="ModuleKeys"/>.</summary>
    public required string OwnerModuleKey { get; set; }

    /// <summary>
    /// Welcher Aspekt des Besitzers gemeint ist. Ein NPC kann mehrere Bedingungssätze haben —
    /// einen für seinen Shop, später einen für sein Erscheinen. Siehe <see cref="ConditionSlots"/>.
    /// </summary>
    public required string Slot { get; set; }

    public ConditionLogic Logic { get; set; } = ConditionLogic.All;

    public List<Condition> Conditions { get; set; } = [];
}

/// <summary>
/// Die benannten Aspekte, an denen Bedingungen hängen können. Die Werte stehen in der Datenbank
/// und dürfen sich nicht mehr ändern.
/// </summary>
public static class ConditionSlots
{
    /// <summary>Der Standardfall: „ist verfügbar, wenn …“.</summary>
    public const string Availability = "availability";

    /// <summary>Das Warenangebot eines NPCs als Ganzes.</summary>
    public const string Shop = "shop";

    /// <summary>
    /// „Ist abgeschlossen, wenn …“ — für Quests. Getrennt von der Verfügbarkeit, weil beides
    /// gleichzeitig an derselben Quest hängt; der Health Check „Quests ohne
    /// Abschlussbedingung“ schaut genau auf diesen Slot.
    /// </summary>
    public const string Completion = "completion";

    /// <summary>„Wird freigeschaltet, wenn …“ — für Achievements.</summary>
    public const string Unlock = "unlock";
}
