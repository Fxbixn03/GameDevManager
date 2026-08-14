namespace GameDevManager.Domain.Entities;

/// <summary>
/// Wie eine Beziehung zwischen zwei NPCs gestimmt ist. Die drei Stufen kommen direkt aus der
/// Anforderung („Freundlich, Neutral oder Feindlich“) und geben der Anzeige ihre Farbe.
/// </summary>
public enum NpcRelationStance
{
    Friendly = 0,

    Neutral = 1,

    Hostile = 2
}

/// <summary>
/// Eine vom Nutzer definierte Beziehungsart zwischen NPCs — „Ist Vater von“ mit der
/// Gegenrichtung „Ist Sohn von“. Beide Bezeichnungen definiert der Nutzer frei; eine
/// symmetrische Beziehung („Ist Verbündeter von“) trägt auf beiden Seiten denselben Text.
/// <para>
/// Bewusst keine <see cref="ContentType"/>-Art: Arten tragen Felder und vererben sie —
/// eine Beziehungsart ist nur ein Bezeichnungspaar und braucht nichts davon.
/// </para>
/// </summary>
public class NpcRelationType
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid GameProjectId { get; set; }

    public GameProject? GameProject { get; set; }

    /// <summary>Bezeichnung in Pfeilrichtung: „&lt;NPC A&gt; ist Vater von &lt;NPC B&gt;“.</summary>
    public required string Name { get; set; }

    /// <summary>Bezeichnung der Gegenrichtung: „&lt;NPC B&gt; ist Sohn von &lt;NPC A&gt;“.</summary>
    public required string InverseName { get; set; }
}

/// <summary>
/// Eine Beziehung zwischen zwei NPCs. Gerichtet gespeichert — der besitzende NPC ist die
/// Quelle, gelesen wird „&lt;Besitzer&gt; &lt;Beziehungsart&gt; &lt;anderer NPC&gt;“; die Maske
/// des anderen NPCs zeigt dieselbe Zeile mit der Gegenrichtungs-Bezeichnung.
/// <para>
/// Der andere NPC hängt wie alle modulübergreifenden Verweise über die GUID daran, ohne
/// Fremdschlüssel — beim Löschen eines NPCs räumt der <c>NpcService</c> eingehende
/// Beziehungen selbst ab.
/// </para>
/// </summary>
public class NpcRelation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Der NPC, an dem die Beziehung gespeichert ist — die Quelle der Leserichtung.</summary>
    public Guid NpcId { get; set; }

    public Npc? Npc { get; set; }

    /// <summary>Das Ziel der Beziehung. GUID-Referenz ohne Fremdschlüssel.</summary>
    public Guid OtherNpcId { get; set; }

    public Guid RelationTypeId { get; set; }

    public NpcRelationType? RelationType { get; set; }

    public NpcRelationStance Stance { get; set; } = NpcRelationStance.Neutral;

    public int SortOrder { get; set; }
}
