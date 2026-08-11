namespace GameDevManager.Domain.Entities;

/// <summary>
/// Ein Ziel-Item eines Rezepts: ein Item in einer bestimmten Menge.
/// <para>
/// Ein Rezept darf mehrere davon haben — ein Durchlauf liefert oft nicht nur das eigentliche
/// Erzeugnis, sondern auch Nebenprodukte („1× Barren + 2× Schlacke“).
/// </para>
/// </summary>
public class RecipeOutput : IRecipeLine
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RecipeId { get; set; }

    public Recipe? Recipe { get; set; }

    /// <summary>
    /// Das hergestellte Item. GUID-Referenz ohne Fremdschlüssel, weil sie über die Modulgrenze
    /// zeigt — die Referenzansicht macht sichtbar, welche Rezepte ein Item herstellen.
    /// </summary>
    public Guid ItemId { get; set; }

    /// <summary>Wie viele Stück dieses Items ein Durchlauf des Rezepts liefert.</summary>
    public int Quantity { get; set; } = 1;

    public int SortOrder { get; set; }
}
