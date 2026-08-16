using GameDevManager.Data.Services;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Die Wirtschafts-Prüfung: Wo erzeugt ein Spieler Geld aus dem Nichts? Reine Auswertung über
/// Rezepte und Händler-Posten — gemeldet, nicht verboten.
/// </summary>
public class EconomyTests
{
    /// <summary>
    /// Legt zwei Items, eine Währung, ein Rezept und einen Händler an. Die Preise kommen von
    /// außen, damit jeder Test seinen eigenen Fall aufbauen kann.
    /// </summary>
    private static async Task SeedAsync(
        TestDatabase test,
        double? ingredientSellPrice,
        double? outputBuyPrice,
        double exchangeRate = 1)
    {
        await using var db = test.CreateContext();

        var currency = new Currency
        {
            GameProjectId = test.ProjectId,
            Name = "Gold",
            ExchangeRate = exchangeRate
        };

        var ore = new Item { GameProjectId = test.ProjectId, Name = "Erz" };
        var bar = new Item { GameProjectId = test.ProjectId, Name = "Barren" };

        var recipe = new Recipe { GameProjectId = test.ProjectId, Name = "1× Barren" };
        recipe.Ingredients.Add(new RecipeIngredient { RecipeId = recipe.Id, ItemId = ore.Id, Quantity = 2 });
        recipe.Outputs.Add(new RecipeOutput { RecipeId = recipe.Id, ItemId = bar.Id, Quantity = 1 });

        var trader = new Npc { GameProjectId = test.ProjectId, Name = "Händler", IsTrader = true };
        trader.Offers.Add(new TraderOffer
        {
            NpcId = trader.Id,
            ItemId = ore.Id,
            CurrencyId = currency.Id,
            SellPrice = ingredientSellPrice
        });
        trader.Offers.Add(new TraderOffer
        {
            NpcId = trader.Id,
            ItemId = bar.Id,
            CurrencyId = currency.Id,
            BuyPrice = outputBuyPrice
        });

        db.Currencies.Add(currency);
        db.Items.AddRange(ore, bar);
        db.Recipes.Add(recipe);
        db.Npcs.Add(trader);

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Zutaten_billiger_als_das_Ergebnis_ist_ein_Fund()
    {
        using var test = new TestDatabase();

        // Zwei Erz zu je 3 kosten 6, der Barren bringt 20 — das ist eine Gelddruckmaschine.
        await SeedAsync(test, ingredientSellPrice: 3, outputBuyPrice: 20);

        var printer = Assert.Single(await test.GetService<EconomyService>().FindMoneyPrintersAsync(test.ProjectId));

        Assert.Equal(6, printer.IngredientCost);
        Assert.Equal(20, printer.OutputValue);
        Assert.Equal(14, printer.Profit);
        Assert.True(printer.IsCertain);
    }

    [Fact]
    public async Task Ein_tragfaehiges_Rezept_ist_kein_Fund()
    {
        using var test = new TestDatabase();
        await SeedAsync(test, ingredientSellPrice: 12, outputBuyPrice: 20);

        Assert.Empty(await test.GetService<EconomyService>().FindMoneyPrintersAsync(test.ProjectId));
    }

    [Fact]
    public async Task Der_Wechselkurs_geht_in_die_Rechnung_ein()
    {
        using var test = new TestDatabase();

        // Dieselben Zahlen, aber jede Einheit ist zehnmal so viel wert — am Verhältnis
        // ändert das nichts, und genau das muss die Rechnung zeigen.
        await SeedAsync(test, ingredientSellPrice: 3, outputBuyPrice: 20, exchangeRate: 10);

        var printer = Assert.Single(await test.GetService<EconomyService>().FindMoneyPrintersAsync(test.ProjectId));

        Assert.Equal(60, printer.IngredientCost);
        Assert.Equal(200, printer.OutputValue);
    }

    [Fact]
    public async Task Fehlende_Preise_machen_den_Fund_zur_Vermutung()
    {
        using var test = new TestDatabase();

        // Die Zutat ist unbepreist — was das Rezept wirklich kostet, weiß niemand.
        await SeedAsync(test, ingredientSellPrice: null, outputBuyPrice: 20);

        var printer = Assert.Single(await test.GetService<EconomyService>().FindMoneyPrintersAsync(test.ProjectId));

        Assert.False(printer.IsCertain);
        Assert.Equal(1, printer.MissingPrices);
    }

    [Fact]
    public async Task Ein_ganz_unbepreistes_Rezept_ist_kein_Fund()
    {
        using var test = new TestDatabase();
        await SeedAsync(test, ingredientSellPrice: null, outputBuyPrice: null);

        // Sonst stünde der halbe Bestand im Fund, nur weil noch niemand Preise gepflegt hat.
        Assert.Empty(await test.GetService<EconomyService>().FindMoneyPrintersAsync(test.ProjectId));
    }

    [Fact]
    public async Task Der_Kurs_uebersteht_Export_und_Import()
    {
        using var test = new TestDatabase();
        await SeedAsync(test, ingredientSellPrice: 3, outputBuyPrice: 20, exchangeRate: 2.5);

        using var zip = new MemoryStream();
        await test.GetService<ExportService>()
            .WriteExportAsync(test.ProjectId, ExportTarget.Json, includeAssets: false, zip);
        zip.Position = 0;

        await test.GetService<ImportService>().ImportAsync(test.ProjectId, zip, replaceExisting: true);

        await using var db = test.CreateContext();
        Assert.Equal(2.5, (await db.Currencies.SingleAsync()).ExchangeRate);
    }
}
