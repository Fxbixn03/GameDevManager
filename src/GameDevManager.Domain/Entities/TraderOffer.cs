namespace GameDevManager.Domain.Entities;

/// <summary>
/// Ein Posten im Warenangebot eines Händlers: welches Item er zu welchem Preis in welcher
/// Währung führt, wie viel davon vorrätig ist und wie lange es dauert, bis nachgefüllt wird.
/// <para>
/// Preise sind je Posten getrennt nach Verkauf und Ankauf, weil Händler in aller Regel
/// unterschiedlich kaufen und verkaufen. Beide dürfen leer bleiben — ein Händler, der etwas
/// nur ankauft, es aber nicht führt, ist ein gültiger Fall.
/// </para>
/// </summary>
public class TraderOffer
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid NpcId { get; set; }

    public Npc? Npc { get; set; }

    /// <summary>Das gehandelte Item. GUID-Referenz über die Modulgrenze, ohne Fremdschlüssel.</summary>
    public Guid ItemId { get; set; }

    /// <summary>Die Währung des Postens. Ebenfalls eine GUID-Referenz.</summary>
    public Guid? CurrencyId { get; set; }

    /// <summary>Preis, zu dem der Händler das Item an den Spieler abgibt.</summary>
    public double? SellPrice { get; set; }

    /// <summary>Preis, zu dem der Händler das Item vom Spieler ankauft.</summary>
    public double? BuyPrice { get; set; }

    /// <summary>Vorrat. <c>null</c> heißt unbegrenzt verfügbar.</summary>
    public int? Stock { get; set; }

    /// <summary>Wie lange es dauert, bis der Vorrat wieder aufgefüllt ist.</summary>
    public double? RestockSeconds { get; set; }

    public int SortOrder { get; set; }
}
