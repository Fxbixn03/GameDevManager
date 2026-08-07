namespace GameDevManager.Domain.Entities;

/// <summary>
/// Ein Zufalls-Event. Das Konzept löst Events aus dem Quest-Modul heraus und macht sie
/// anpassbarer: welche Mobs spawnen, welcher Loot-Table die Belohnung ist und wie hoch die
/// Wahrscheinlichkeit liegt.
/// <para>
/// Wo das Event passieren kann, wird im Karten-Modul markiert — eine Markierung, die auf
/// das Event zeigt, genau wie bei NPC-Spawns. So bleiben Punkt und Gebiet („nur in Höhlen
/// oder überall?“) ohne eigene Ortsspalten abgedeckt. Der Name <c>GameEvent</c> statt
/// <c>Event</c>, weil Letzteres in C# ein Schlüsselwortkontext und in Blazor allgegenwärtig ist.
/// </para>
/// </summary>
public class GameEvent : ContentEntity
{
    public override string ModuleKey => ModuleKeys.Events;

    /// <summary>Wahrscheinlichkeit des Auftretens in Prozent (0–100).</summary>
    public double Chance { get; set; } = 10;

    /// <summary>GUID-Referenz auf den Loot-Table, der die Belohnung stellt.</summary>
    public Guid? RewardLootTableId { get; set; }

    public List<EventSpawn> Spawns { get; set; } = [];
}

/// <summary>
/// Ein Mob-Spawn eines Events: welcher NPC/Mob in welcher Anzahl erscheint. Der NPC hängt
/// über seine GUID daran, ohne Fremdschlüssel über die Modulgrenze.
/// </summary>
public class EventSpawn
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid GameEventId { get; set; }

    public GameEvent? GameEvent { get; set; }

    public Guid NpcId { get; set; }

    /// <summary>Wie viele davon spawnen.</summary>
    public int Count { get; set; } = 1;

    public int SortOrder { get; set; }
}
