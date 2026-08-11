namespace GameDevManager.Domain.Entities;

/// <summary>
/// Ein Crafting-Rezept: eine Menge an Zutaten ergibt eine Menge an Ziel-Items —
/// „3× Holz + 5× Kohle = 1× Fackel“.
/// <para>
/// Ein Rezept kennt genau drei Angaben: seine Ziel-Items, seine Zutaten und seine Art.
/// Alles Weitere — Herstellungsdauer, benötigte Werkbank, Mindestlevel — definiert der Nutzer
/// als Felder an der Rezept-Art. Damit gilt hier dieselbe Regel wie bei den Items: das Schema
/// ist nutzerdefiniert, nicht fest kodiert.
/// </para>
/// <para>
/// Einen eigenen Namen trägt das Rezept nicht mehr: Er wird beim Speichern aus den Ziel-Items
/// gebildet, weil er in Listen, Suche und Referenzansicht gebraucht wird.
/// </para>
/// </summary>
public class Recipe : ContentEntity
{
    public override string ModuleKey => ModuleKeys.Crafting;

    /// <summary>Was ein Durchlauf liefert — mehrere Ziel-Items sind erlaubt.</summary>
    public List<RecipeOutput> Outputs { get; set; } = [];

    public List<RecipeIngredient> Ingredients { get; set; } = [];
}
