namespace GameDevManager.Domain.Entities;

/// <summary>
/// Ein Crafting-Rezept: eine Menge an Zutaten ergibt eine Menge eines Items —
/// „3× Holz + 5× Kohle = Fackel“.
/// <para>
/// Struktur hat das Rezept nur, wo sie fachlich unumgänglich ist: Ergebnis, Menge und Zutaten.
/// Alles Weitere — Herstellungsdauer, benötigte Werkbank, Mindestlevel — definiert der Nutzer
/// als Felder an der Rezept-Art. Damit gilt hier dieselbe Regel wie bei den Items: das Schema
/// ist nutzerdefiniert, nicht fest kodiert.
/// </para>
/// </summary>
public class Recipe : ContentEntity
{
    public override string ModuleKey => ModuleKeys.Crafting;

    /// <summary>
    /// Das hergestellte Item. GUID-Referenz ohne Fremdschlüssel, weil sie über die Modulgrenze
    /// zeigt — die Referenzansicht macht sichtbar, welche Rezepte ein Item verwenden.
    /// </summary>
    public Guid? OutputItemId { get; set; }

    /// <summary>Wie viele Stück ein Durchlauf des Rezepts liefert.</summary>
    public int OutputQuantity { get; set; } = 1;

    public List<RecipeIngredient> Ingredients { get; set; } = [];
}
