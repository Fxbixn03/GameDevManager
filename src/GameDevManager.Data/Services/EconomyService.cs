using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GameDevManager.Data.Services;

/// <summary>
/// Ein Rezept, mit dem sich Geld aus dem Nichts erzeugen lässt: Die Zutaten kosten beim
/// Händler weniger, als das Ergebnis dort einbringt.
/// </summary>
/// <param name="IngredientCost">Was die Zutaten im Einkauf kosten, in der Grundeinheit.</param>
/// <param name="OutputValue">Was die Ziele im Verkauf einbringen, in der Grundeinheit.</param>
/// <param name="MissingPrices">
/// Zutaten oder Ziele ohne Händlerpreis. Sie fehlen in der Rechnung — der Fund bleibt damit
/// eine Vermutung und wird als solche ausgewiesen.
/// </param>
public sealed record MoneyPrinter(
    Guid RecipeId,
    string RecipeName,
    double IngredientCost,
    double OutputValue,
    int MissingPrices)
{
    /// <summary>Der Gewinn je Durchlauf — die Zahl, um die es geht.</summary>
    public double Profit => OutputValue - IngredientCost;

    /// <summary>Ohne vollständige Preise ist der Fund ein Hinweis und kein Beleg.</summary>
    public bool IsCertain => MissingPrices == 0;
}

/// <summary>Ein Item, das bei keinem Händler einen Preis hat — die Pflegelücke.</summary>
public sealed record UnpricedItem(Guid ItemId, string Name, string? TypeName);

/// <summary>Ein Händler als Sprungziel — mit der Zahl seiner Posten in dieser Währung.</summary>
public sealed record TraderLink(Guid NpcId, string Name, int OfferCount);

/// <summary>
/// Die Preislage einer Währung für das Ökonomie-Dashboard. Die Preisspanne ist die der
/// Verkaufspreise (was Spieler zahlen); der Median sagt bei schiefen Verteilungen mehr als
/// der Mittelwert — dieselbe Überlegung wie bei der Wartezeit des Loot-Simulators.
/// </summary>
public sealed record CurrencyEconomy(
    Guid CurrencyId,
    string Name,
    string? Symbol,
    double ExchangeRate,
    int OfferCount,
    int PricedItemCount,
    double? MinSellPrice,
    double? MedianSellPrice,
    double? MaxSellPrice,
    IReadOnlyList<TraderLink> Traders);

/// <summary>
/// Quellen und Senken der Wirtschaft, als Item-Zahlen: Woher bekommt ein Spieler Dinge
/// (Loot, Händler, Rezepte), wohin gehen sie (Ankauf, Rezept-Zutaten)?
/// </summary>
public sealed record EconomyFlows(
    int TotalItems,
    int LootSourceItems,
    int TraderSourceItems,
    int RecipeSourceItems,
    int TraderSinkItems,
    int RecipeSinkItems);

/// <summary>
/// Die Wirtschafts-Prüfung: wo erzeugt ein Spieler Geld aus dem Nichts?
/// <para>
/// Reine Auswertung über zwei vorhandene Bestände — die Rezepte und die Händler-Posten. Kein
/// eigener Datenbestand, dasselbe Muster wie beim Loot-Simulator und beim Freischaltungs-Graphen.
/// </para>
/// <para>
/// <b>Der Preis eines Items ist der beste, den ein Händler dafür bietet</b>, und die Kosten
/// sind der günstigste Ankauf: Ein Spieler sucht sich den besten Handel, und genau den muss
/// die Prüfung annehmen. Umgerechnet wird über
/// <see cref="Currency.ExchangeRate"/> — ohne einen Kurs ließen sich zwei Währungen nicht
/// vergleichen.
/// </para>
/// <para>
/// Gemeldet, nicht verboten — dieselbe Linie wie beim Loot-Check: Es steht unter den Health
/// Checks, also unter „nachschauen“.
/// </para>
/// </summary>
public class EconomyService(IDbContextFactory<GameDevManagerDbContext> factory)
{
    /// <summary>
    /// Die Übersicht je Währung: Posten, bepreiste Items, Preisspanne der Verkaufspreise und
    /// die Händler als Sprungziele. Währungen ohne einen einzigen Posten stehen mit leerer
    /// Spanne da — auch das ist eine Auskunft.
    /// </summary>
    public async Task<List<CurrencyEconomy>> GetCurrencyOverviewAsync(
        Guid projectId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var currencies = await db.Currencies
            .AsNoTracking()
            .Where(currency => currency.GameProjectId == projectId)
            .OrderBy(currency => currency.Name)
            .ToListAsync(ct);

        var offers = await db.TraderOffers
            .AsNoTracking()
            .Where(offer => offer.Npc!.GameProjectId == projectId && offer.CurrencyId != null)
            .Select(offer => new
            {
                CurrencyId = offer.CurrencyId!.Value,
                offer.ItemId,
                offer.SellPrice,
                offer.BuyPrice,
                offer.NpcId,
                TraderName = offer.Npc!.Name
            })
            .ToListAsync(ct);

        return
        [
            .. currencies.Select(currency =>
            {
                var own = offers.Where(offer => offer.CurrencyId == currency.Id).ToList();
                var sellPrices = own
                    .Where(offer => offer.SellPrice is not null)
                    .Select(offer => offer.SellPrice!.Value)
                    .OrderBy(price => price)
                    .ToList();

                List<TraderLink> traders =
                [
                    .. own
                        .GroupBy(offer => (offer.NpcId, offer.TraderName))
                        .Select(group => new TraderLink(group.Key.NpcId, group.Key.TraderName, group.Count()))
                        .OrderBy(trader => trader.Name, StringComparer.CurrentCultureIgnoreCase)
                ];

                return new CurrencyEconomy(
                    currency.Id,
                    currency.Name,
                    currency.Symbol,
                    currency.ExchangeRate,
                    own.Count,
                    own.Where(offer => offer.SellPrice is not null || offer.BuyPrice is not null)
                        .Select(offer => offer.ItemId)
                        .Distinct()
                        .Count(),
                    sellPrices.Count > 0 ? sellPrices[0] : null,
                    Median(sellPrices),
                    sellPrices.Count > 0 ? sellPrices[^1] : null,
                    traders);
            })
        ];
    }

    /// <summary>
    /// Quellen und Senken als Item-Zahlen. Gezählt werden Items, nicht Vorkommen: Ein Item in
    /// drei Loot-Tabellen ist eine Quelle, keine drei.
    /// </summary>
    public async Task<EconomyFlows> GetFlowsAsync(Guid projectId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var total = await db.Items.CountAsync(item => item.GameProjectId == projectId, ct);

        var lootSources = await db.LootEntries
            .Where(entry => entry.LootTable!.GameProjectId == projectId)
            .Select(entry => entry.ItemId)
            .Distinct()
            .CountAsync(ct);

        var traderSources = await db.TraderOffers
            .Where(offer => offer.Npc!.GameProjectId == projectId && offer.SellPrice != null)
            .Select(offer => offer.ItemId)
            .Distinct()
            .CountAsync(ct);

        var recipeSources = await db.RecipeOutputs
            .Where(output => output.Recipe!.GameProjectId == projectId)
            .Select(output => output.ItemId)
            .Distinct()
            .CountAsync(ct);

        var traderSinks = await db.TraderOffers
            .Where(offer => offer.Npc!.GameProjectId == projectId && offer.BuyPrice != null)
            .Select(offer => offer.ItemId)
            .Distinct()
            .CountAsync(ct);

        var recipeSinks = await db.RecipeIngredients
            .Where(ingredient => ingredient.Recipe!.GameProjectId == projectId)
            .Select(ingredient => ingredient.ItemId)
            .Distinct()
            .CountAsync(ct);

        return new EconomyFlows(total, lootSources, traderSources, recipeSources, traderSinks, recipeSinks);
    }

    /// <summary>Der Median einer aufsteigend sortierten Liste — <c>null</c>, wenn sie leer ist.</summary>
    private static double? Median(List<double> sorted) => sorted.Count switch
    {
        0 => null,
        var count when count % 2 == 1 => sorted[count / 2],
        var count => (sorted[count / 2 - 1] + sorted[count / 2]) / 2
    };

    /// <summary>
    /// Alle Items, die bei keinem Händler einen Preis haben — weder Verkauf noch Ankauf.
    /// Genau diese Lücke macht die Gelddruckmaschinen-Prüfung zur Vermutung; hier wird sie
    /// als Liste sichtbar. Reine Auswertung über die vorhandenen Händler-Posten, kein eigener
    /// Datenbestand — und gemeldet, nicht verboten: Ein Quest-Item ohne Preis kann Absicht sein.
    /// </summary>
    public async Task<List<UnpricedItem>> FindUnpricedItemsAsync(
        Guid projectId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        // Ein Posten ohne jeden Preis bepreist nichts — ein Händler, der etwas führt, aber
        // nicht handelt, ist ein gültiger Fall und lässt die Lücke offen.
        var pricedItemIds = await db.TraderOffers
            .AsNoTracking()
            .Where(offer => offer.Npc!.GameProjectId == projectId
                && (offer.SellPrice != null || offer.BuyPrice != null))
            .Select(offer => offer.ItemId)
            .Distinct()
            .ToListAsync(ct);

        return await db.Items
            .AsNoTracking()
            .Where(item => item.GameProjectId == projectId && !pricedItemIds.Contains(item.Id))
            .OrderBy(item => item.Name)
            .ThenBy(item => item.Id)
            .Select(item => new UnpricedItem(item.Id, item.Name, item.ContentType!.Name))
            .ToListAsync(ct);
    }

    public async Task<List<MoneyPrinter>> FindMoneyPrintersAsync(
        Guid projectId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var rates = await db.Currencies
            .AsNoTracking()
            .Where(currency => currency.GameProjectId == projectId)
            .ToDictionaryAsync(currency => currency.Id, currency => currency.ExchangeRate, ct);

        if (rates.Count == 0)
        {
            // Ohne Währung gibt es keine Preise und damit nichts zu prüfen.
            return [];
        }

        var offers = await db.TraderOffers
            .AsNoTracking()
            .Where(offer => offer.Npc!.GameProjectId == projectId && offer.CurrencyId != null)
            .Select(offer => new
            {
                offer.ItemId,
                CurrencyId = offer.CurrencyId!.Value,
                offer.SellPrice,
                offer.BuyPrice
            })
            .ToListAsync(ct);

        // Was ein Item kostet und was es einbringt, je in der Grundeinheit.
        var buyFrom = new Dictionary<Guid, double>();
        var sellTo = new Dictionary<Guid, double>();

        foreach (var offer in offers)
        {
            if (!rates.TryGetValue(offer.CurrencyId, out var rate) || rate <= 0)
            {
                continue;
            }

            // SellPrice ist der Preis, zu dem der Händler verkauft — für den Spieler die
            // Kosten. BuyPrice ist, was der Händler zahlt — für den Spieler der Erlös.
            if (offer.SellPrice is { } sell)
            {
                var value = sell * rate;
                buyFrom[offer.ItemId] = buyFrom.TryGetValue(offer.ItemId, out var cheapest)
                    ? Math.Min(cheapest, value)
                    : value;
            }

            if (offer.BuyPrice is { } buy)
            {
                var value = buy * rate;
                sellTo[offer.ItemId] = sellTo.TryGetValue(offer.ItemId, out var best)
                    ? Math.Max(best, value)
                    : value;
            }
        }

        var recipes = await db.Recipes
            .AsNoTracking()
            .Include(recipe => recipe.Outputs)
            .Include(recipe => recipe.Ingredients)
            .Where(recipe => recipe.GameProjectId == projectId)
            .ToListAsync(ct);

        // Der Name des Rezepts wird aus den aktuellen Item-Namen gebildet — der gespeicherte
        // kann nach dem Umbenennen eines Items bis zum nächsten Speichern veralten.
        var itemNames = await db.Items
            .AsNoTracking()
            .Where(item => item.GameProjectId == projectId)
            .ToDictionaryAsync(item => item.Id, item => item.Name, ct);

        var printers = new List<MoneyPrinter>();

        foreach (var recipe in recipes)
        {
            if (recipe.Outputs.Count == 0 || recipe.Ingredients.Count == 0)
            {
                continue;
            }

            var missing = 0;
            var cost = 0d;
            var value = 0d;

            foreach (var ingredient in recipe.Ingredients)
            {
                if (buyFrom.TryGetValue(ingredient.ItemId, out var price))
                {
                    cost += price * ingredient.Quantity;
                }
                else
                {
                    missing++;
                }
            }

            foreach (var output in recipe.Outputs)
            {
                if (sellTo.TryGetValue(output.ItemId, out var price))
                {
                    value += price * output.Quantity;
                }
                else
                {
                    missing++;
                }
            }

            // Ein Rezept, für das gar kein Preis bekannt ist, ist keine Gelddruckmaschine,
            // sondern schlicht unbepreist — sonst stünde der halbe Bestand im Fund.
            if (value > cost && (cost > 0 || value > 0) && missing < recipe.Ingredients.Count + recipe.Outputs.Count)
            {
                printers.Add(new MoneyPrinter(
                    recipe.Id,
                    CraftingService.FormatOutputs(recipe.Outputs
                        .OrderBy(output => output.SortOrder)
                        .Select(output => (itemNames.GetValueOrDefault(output.ItemId, recipe.Name), output.Quantity))),
                    cost,
                    value,
                    missing));
            }
        }

        return
        [
            .. printers
                .OrderByDescending(printer => printer.IsCertain)
                .ThenByDescending(printer => printer.Profit)
                .ThenBy(printer => printer.RecipeName)
        ];
    }
}
