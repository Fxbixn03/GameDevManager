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

    /// <summary>
    /// Einzigartige Figur (läuft genau einmal durchs Dorf) oder wiederkehrender Spawn
    /// (der Waschbär, der immer wieder auftaucht). Unabhängig von <see cref="Kind"/> —
    /// auch ein Mob kann einzigartig sein, etwa ein Boss.
    /// </summary>
    public bool IsUnique { get; set; }

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

    /// <summary>
    /// Vorlieben als kommagetrennte Stichwörter („Honig, Angeln, Regen“). Freitext ohne
    /// eigene Tabelle — dieselbe Abwägung wie bei den Polygon-Punkten der Karten: So gehen
    /// die Werte ohne Zutun durch Export, Import und Duplizieren.
    /// </summary>
    public string? Preferences { get; set; }

    /// <summary>Persönlichkeitsmerkmale, kommagetrennt wie <see cref="Preferences"/>.</summary>
    public string? Personality { get; set; }

    /// <summary>
    /// Wesenszüge als kanonischer Text <c>"schlüssel:wert;…"</c> — die zehn festen Züge aus
    /// <see cref="NpcTraits.Keys"/> mit Werten von 0 bis 10. Lesen und Schreiben über
    /// <see cref="NpcTraits"/>, damit derselbe Stand denselben Export ergibt.
    /// </summary>
    public string? Traits { get; set; }

    public List<TraderOffer> Offers { get; set; } = [];

    public List<NpcRelation> Relations { get; set; } = [];

    /// <summary>Wo, wie viele und wie oft dieser NPC erscheint — siehe <see cref="SpawnRule"/>.</summary>
    public List<SpawnRule> SpawnRules { get; set; } = [];
}

/// <summary>
/// Die zehn festen Wesenszüge eines NPCs samt Lese- und Schreibregeln für die kanonische
/// Textspalte <see cref="Npc.Traits"/>. Die Schlüssel stehen in der Datenbank und im Export
/// und dürfen sich nicht mehr ändern; ihre Anzeigenamen kommen aus den Texten der Oberfläche.
/// </summary>
public static class NpcTraits
{
    public const int MaxValue = 10;

    /// <summary>Feste Reihenfolge — sie ist zugleich die Reihenfolge der Maske und des Exports.</summary>
    public static readonly IReadOnlyList<string> Keys =
    [
        "empathy",
        "impulsiveness",
        "loyalty",
        "courage",
        "honesty",
        "dominance",
        "patience",
        "distrust",
        "riskTaking",
        "compassion"
    ];

    /// <summary>
    /// Liest die Textspalte. Unbekannte Schlüssel werden übergangen (ein älterer Stand darf
    /// einen neueren nicht umwerfen), Werte auf 0 bis 10 begrenzt; fehlende Züge stehen auf 0.
    /// </summary>
    public static Dictionary<string, int> Parse(string? traits)
    {
        var result = Keys.ToDictionary(key => key, _ => 0);

        if (string.IsNullOrWhiteSpace(traits))
        {
            return result;
        }

        foreach (var pair in traits.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = pair.Split(':');

            if (parts.Length != 2 || !result.ContainsKey(parts[0]) || !int.TryParse(parts[1], out var value))
            {
                continue;
            }

            result[parts[0]] = Math.Clamp(value, 0, MaxValue);
        }

        return result;
    }

    /// <summary>
    /// Schreibt die Textspalte kanonisch: feste Schlüsselreihenfolge, nur gesetzte Züge
    /// (Wert über 0), keine Züge ergibt <c>null</c> — derselbe Stand ergibt denselben Export.
    /// </summary>
    public static string? Format(IReadOnlyDictionary<string, int> values)
    {
        var parts = Keys
            .Where(key => values.GetValueOrDefault(key) > 0)
            .Select(key => $"{key}:{Math.Clamp(values[key], 0, MaxValue)}")
            .ToList();

        return parts.Count == 0 ? null : string.Join(';', parts);
    }
}

/// <summary>
/// Eine Spawn-Regel: <b>wo</b>, <b>wie viele</b> und <b>wie oft</b> ein NPC erscheint.
/// <para>
/// Die Karten-Markierung deckt allein das <i>Wo</i> ab; alles Übrige stand bisher bestenfalls
/// in benutzerdefinierten Feldern und war damit nicht auswertbar. Das schließt den letzten
/// offenen Halbsatz des NPC-Kapitels im Konzept ab („manche NPCs gibt es nur einmal … andere
/// spawnen nur in bestimmten Bereichen“).
/// </para>
/// <para>
/// <b>Wann</b> er erscheint, steht nicht hier, sondern als <see cref="ConditionSet"/> im Slot
/// <see cref="ConditionSlots.Spawn"/> an der GUID der Regel — Tageszeit, Wetter und
/// Freischaltung gibt es im Bedingungssystem längst, und eine zweite Fassung davon liefe beim
/// ersten neuen Bedingungstyp auseinander.
/// </para>
/// </summary>
public class SpawnRule
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid NpcId { get; set; }

    public Npc? Npc { get; set; }

    /// <summary>
    /// Die Karte, auf der er erscheint. GUID-Referenz über die Modulgrenze, ohne
    /// Fremdschlüssel — der <c>MapService</c> nullt sie beim Löschen einer Karte.
    /// </summary>
    public Guid? TargetMapId { get; set; }

    /// <summary>
    /// Die Markierung auf <see cref="TargetMapId"/> — der genaue Ort oder das Gebiet. Ohne
    /// Markierung meint die Regel die ganze Karte.
    /// </summary>
    public Guid? TargetMarkerId { get; set; }

    /// <summary>Untere Grenze der Anzahl; gleich der oberen für eine feste Zahl.</summary>
    public int MinCount { get; set; } = 1;

    public int MaxCount { get; set; } = 1;

    /// <summary>
    /// Nach wie vielen Sekunden nachgewachsen wird. <c>null</c> heißt „gar nicht“ — der
    /// einmalige NPC des Konzepts.
    /// </summary>
    public int? RespawnSeconds { get; set; }

    public int SortOrder { get; set; }

    /// <summary>Kurzfassung der Anzahl für Listen: „3“ oder „1–5“.</summary>
    public string DescribeCount() => MinCount == MaxCount ? MinCount.ToString() : $"{MinCount}–{MaxCount}";
}
