using GameDevManager.Data.Services;
using GameDevManager.Domain.Entities;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Der Loot-Simulator. Geprüft wird nicht die einzelne Zufallszahl, sondern das, worauf sich
/// ein Designer verlassen können muss: Derselbe Startwert ergibt denselben Lauf, die beiden
/// Verfahren bedeuten wirklich Verschiedenes, und die Zahlen treffen die angesetzten Werte.
/// </summary>
public class LootSimulationTests
{
    private static readonly Guid Coin = Guid.NewGuid();
    private static readonly Guid Sword = Guid.NewGuid();

    private static readonly Dictionary<Guid, string> Names = new()
    {
        [Coin] = "Münze",
        [Sword] = "Schwert"
    };

    private static LootTable Table(LootRollMode mode, params (Guid Item, double Chance, int Min, int Max)[] entries)
    {
        var table = new LootTable
        {
            GameProjectId = Guid.NewGuid(),
            Name = "Beutel",
            RollMode = mode
        };

        var order = 0;

        foreach (var (item, chance, min, max) in entries)
        {
            table.Entries.Add(new LootEntry
            {
                LootTableId = table.Id,
                ItemId = item,
                Chance = chance,
                MinQuantity = min,
                MaxQuantity = max,
                SortOrder = order++
            });
        }

        return table;
    }

    [Fact]
    public void Derselbe_Startwert_ergibt_denselben_Lauf()
    {
        var table = Table(LootRollMode.Independent, (Coin, 50, 1, 3), (Sword, 5, 1, 1));

        var first = LootSimulation.Run(table, Names, 5_000, seed: 42);
        var second = LootSimulation.Run(table, Names, 5_000, seed: 42);
        var other = LootSimulation.Run(table, Names, 5_000, seed: 43);

        Assert.Equal(
            first.Rows.Select(row => row.Drops),
            second.Rows.Select(row => row.Drops));

        // Ein anderer Startwert ist ein anderer Lauf — sonst wäre der Startwert sinnlos.
        Assert.NotEqual(
            first.Rows.Select(row => row.Drops),
            other.Rows.Select(row => row.Drops));
    }

    [Fact]
    public void Unabhaengige_Wuerfe_treffen_die_angesetzte_Wahrscheinlichkeit()
    {
        var table = Table(LootRollMode.Independent, (Coin, 50, 1, 1), (Sword, 5, 1, 1));

        var result = LootSimulation.Run(table, Names, 50_000, seed: 7);

        Assert.Equal(50, result.Rows[0].DropRate, 0.6);
        Assert.Equal(5, result.Rows[1].DropRate, 0.3);

        // Bei unabhängigen Würfen fallen mehrere Dinge gleichzeitig — die Summe der Anteile
        // darf und soll über der Trefferquote liegen.
        Assert.True(result.AverageItemsPerRoll > 0.5);
    }

    [Fact]
    public void Beim_Einzelwurf_faellt_hoechstens_ein_Eintrag()
    {
        var table = Table(LootRollMode.SinglePick, (Coin, 30, 1, 1), (Sword, 20, 1, 1));

        var result = LootSimulation.Run(table, Names, 20_000, seed: 11);

        // Zusammen 50 % — die restlichen Würfe gehen leer aus.
        Assert.Equal(result.Rolls, result.Rows.Sum(row => row.Drops) + result.EmptyRolls);
        Assert.Equal(50, 100 - result.EmptyRate, 1.0);
        Assert.True(result.AverageItemsPerRoll <= 1);
    }

    [Fact]
    public void Ueber_100_Prozent_macht_den_hinteren_Eintrag_unerreichbar()
    {
        // Genau der Fall des Health Checks: Die Skala ist nach dem ersten Eintrag zu Ende.
        var table = Table(LootRollMode.SinglePick, (Coin, 100, 1, 1), (Sword, 40, 1, 1));

        var result = LootSimulation.Run(table, Names, 5_000, seed: 3);

        Assert.Equal(result.Rolls, result.Rows[0].Drops);
        Assert.Equal(0, result.Rows[1].Drops);
        Assert.Null(result.Rows[1].MedianWait);
        Assert.Equal(0, result.EmptyRolls);
    }

    [Fact]
    public void Die_Mengenspanne_wird_ausgewuerfelt_und_gemittelt()
    {
        var table = Table(LootRollMode.Independent, (Coin, 100, 1, 5));

        var result = LootSimulation.Run(table, Names, 20_000, seed: 5);
        var row = Assert.Single(result.Rows);

        Assert.Equal(result.Rolls, row.Drops);
        Assert.Equal(3, row.AverageQuantity, 0.1);
        Assert.Equal(row.AverageQuantity, row.ExpectedPerRoll, 0.001);
    }

    [Fact]
    public void Der_Median_der_Wartezeit_passt_zur_Seltenheit()
    {
        var table = Table(LootRollMode.Independent, (Coin, 100, 1, 1), (Sword, 1, 1, 1));

        var result = LootSimulation.Run(table, Names, 50_000, seed: 13);

        // Was immer fällt, fällt in jedem Wurf.
        Assert.Equal(1, result.Rows[0].MedianWait);

        // Bei 1 % liegt der Median der Wartezeit bei rund 69 Würfen (ln 2 / ln(1/0,99)).
        Assert.InRange(result.Rows[1].MedianWait!.Value, 50, 95);
    }

    [Fact]
    public void Eine_Tabelle_ohne_Eintraege_ergibt_lauter_leere_Wuerfe()
    {
        var result = LootSimulation.Run(Table(LootRollMode.Independent), Names, 100, seed: 1);

        Assert.Empty(result.Rows);
        Assert.Equal(100, result.EmptyRolls);
        Assert.Equal(100, result.EmptyRate);
    }

    [Fact]
    public void Ein_geloeschtes_Item_bleibt_in_der_Auswertung_stehen()
    {
        // Der Eintrag zeigt ins Leere; der Lauf soll trotzdem sagen, wie oft er fällt.
        var table = Table(LootRollMode.Independent, (Guid.NewGuid(), 100, 1, 1));

        var row = Assert.Single(LootSimulation.Run(table, Names, 50, seed: 1).Rows);

        Assert.Equal(string.Empty, row.ItemName);
        Assert.Equal(50, row.Drops);
    }
}
