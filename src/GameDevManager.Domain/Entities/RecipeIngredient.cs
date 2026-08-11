namespace GameDevManager.Domain.Entities;

/// <summary>
/// Eine Zutat eines Rezepts: ein Item in einer bestimmten Menge.
/// </summary>
public class RecipeIngredient : IRecipeLine
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RecipeId { get; set; }

    public Recipe? Recipe { get; set; }

    /// <summary>
    /// Das benötigte Item. Wie beim Ergebnis eine GUID-Referenz ohne Fremdschlüssel — die
    /// Zutat liegt in einem anderen Modul.
    /// </summary>
    public Guid ItemId { get; set; }

    public int Quantity { get; set; } = 1;

    public int SortOrder { get; set; }
}
