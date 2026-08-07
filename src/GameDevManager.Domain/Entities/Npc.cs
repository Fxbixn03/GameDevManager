namespace GameDevManager.Domain.Entities;

/// <summary>
/// Ob es sich um eine friedliche Figur oder eine gegnerische Entität handelt. Beide liegen
/// laut Konzept im selben Modul, lassen sich aber gegeneinander filtern.
/// </summary>
public enum NpcKind
{
    /// <summary>Eine Figur, mit der der Spieler umgeht — Händler, Questgeber, Bewohner.</summary>
    Npc = 0,

    /// <summary>Ein Gegner.</summary>
    Mob = 1
}

/// <summary>
/// Eine Figur des Spiels: NPC oder Mob.
/// <para>
/// Strukturell trägt sie nur, was das Tool selbst auswerten muss: die Unterscheidung
/// NPC/Mob für den Filter und die beiden Rollen. Eigenschaften wie Lebenspunkte, Stufe oder
/// Fraktionszugehörigkeit definiert der Nutzer als Felder an der NPC-Art.
/// </para>
/// <para>
/// Noch nicht abgebildet, weil die Grundlage dafür fehlt: Spawn-Orte (brauchen das
/// Karten-Modul), Loot-Tables (brauchen das Loot-Modul) und die Verfügbarkeitsbedingungen
/// von Shops (brauchen das Bedingungssystem).
/// </para>
/// </summary>
public class Npc : ContentEntity
{
    public override string ModuleKey => ModuleKeys.Npcs;

    public NpcKind Kind { get; set; } = NpcKind.Npc;

    /// <summary>Bietet Waren an. Erst dann sind <see cref="Offers"/> überhaupt sichtbar.</summary>
    public bool IsTrader { get; set; }

    /// <summary>Vergibt Quests. Das Quest-Modul knüpft später hier an.</summary>
    public bool IsQuestGiver { get; set; }

    /// <summary>
    /// Was beim Besiegen fällt. GUID-Referenz auf eine Loot-Table, ohne Fremdschlüssel —
    /// im Konzept: „Diese Loot-Tables sollen dann im NPC-Modul auswählbar sein.“
    /// </summary>
    public Guid? LootTableId { get; set; }

    /// <summary>
    /// GUID-Referenz auf die Klasse des NPCs — im Konzept: „Klassen …, welche dann auf die
    /// Spielerfigur und die NPCs gemappt werden können.“
    /// </summary>
    public Guid? CharacterClassId { get; set; }

    public List<TraderOffer> Offers { get; set; } = [];
}
