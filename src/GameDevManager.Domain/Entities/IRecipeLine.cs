namespace GameDevManager.Domain.Entities;

/// <summary>
/// Eine Zeile eines Rezepts — ein Item in einer Menge, an einer Position der Liste.
/// <para>
/// Ziel-Items und Zutaten sind bewusst zwei Tabellen, verhalten sich beim Speichern aber
/// identisch. Die Schnittstelle gibt es nur, damit der <c>CraftingService</c> beide mit
/// demselben Abgleich sichern kann.
/// </para>
/// </summary>
public interface IRecipeLine
{
    Guid Id { get; set; }

    Guid RecipeId { get; set; }

    Guid ItemId { get; set; }

    int Quantity { get; set; }

    int SortOrder { get; set; }
}
