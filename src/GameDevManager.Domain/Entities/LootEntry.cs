namespace GameDevManager.Domain.Entities;

/// <summary>
/// Ein Eintrag einer Loot-Table: ein Item mit Wahrscheinlichkeit und Mengenspanne.
/// <para>
/// Dasselbe Item darf mehrfach vorkommen — „zu 50 % eine Münze, zu 5 % gleich zwanzig“ ist
/// ein üblicher Fall und anders als bei Rezept-Zutaten oder Händler-Posten kein Versehen.
/// </para>
/// </summary>
public class LootEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid LootTableId { get; set; }

    public LootTable? LootTable { get; set; }

    /// <summary>Das fallende Item. GUID-Referenz über die Modulgrenze, ohne Fremdschlüssel.</summary>
    public Guid ItemId { get; set; }

    /// <summary>Wahrscheinlichkeit in Prozent (0–100).</summary>
    public double Chance { get; set; } = 100;

    /// <summary>Untere Grenze der Menge; gleich der oberen für eine feste Anzahl.</summary>
    public int MinQuantity { get; set; } = 1;

    public int MaxQuantity { get; set; } = 1;

    public int SortOrder { get; set; }

    /// <summary>Kurzfassung der Menge für Listen: „3“ oder „1–5“.</summary>
    public string DescribeQuantity() =>
        MinQuantity == MaxQuantity ? MinQuantity.ToString() : $"{MinQuantity}–{MaxQuantity}";
}
