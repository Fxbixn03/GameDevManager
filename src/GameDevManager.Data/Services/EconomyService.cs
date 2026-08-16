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
