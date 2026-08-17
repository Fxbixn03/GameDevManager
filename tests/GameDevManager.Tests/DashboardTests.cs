using GameDevManager.Data.Services;
using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Die Bänder des Dashboards: das „Weiterarbeiten“ quer über alle Module, die Zusammenfassung
/// der Health Checks und die je Projekt gespeicherte Anordnung.
/// </summary>
public class DashboardTests
{
    // --------------------------------------------------------------------- Weiterarbeiten

    [Fact]
    public async Task Weiterarbeiten_reiht_die_Module_nach_Zeitpunkt_ineinander()
    {
        using var test = new TestDatabase();
        var now = DateTime.UtcNow;

        await using (var db = test.CreateContext())
        {
            db.Items.Add(new Item
            {
                GameProjectId = test.ProjectId, Name = "Rostklinge", UpdatedAtUtc = now.AddDays(-3)
            });
            db.Npcs.Add(new Npc
            {
                GameProjectId = test.ProjectId, Name = "Hafenmeister", UpdatedAtUtc = now.AddHours(-2)
            });
            db.Maps.Add(new GameMap
            {
                GameProjectId = test.ProjectId, Name = "Kaltbucht", UpdatedAtUtc = now.AddMinutes(-1)
            });
            await db.SaveChangesAsync();
        }

        var recent = await test.GetService<DashboardOverviewService>()
            .GetRecentlyEditedAsync(test.ProjectId, 10);

        // Die Zusammenführung der Module ist der eigentliche Punkt: jede Quelle liefert für sich
        // sortiert, erst gemeinsam ergibt sich die tatsächliche Reihenfolge.
        Assert.Equal(
            ["Kaltbucht", "Hafenmeister", "Rostklinge"],
            recent.Select(entry => entry.Hit.Name).ToArray());

        Assert.Equal(
            [ModuleKeys.Maps, ModuleKeys.Npcs, ModuleKeys.Items],
            recent.Select(entry => entry.Hit.ModuleKey).ToArray());
    }

    [Fact]
    public async Task Weiterarbeiten_haelt_die_Obergrenze_ein_und_bleibt_im_Projekt()
    {
        using var test = new TestDatabase();
        var now = DateTime.UtcNow;

        await using (var db = test.CreateContext())
        {
            for (var i = 0; i < 5; i++)
            {
                db.Items.Add(new Item
                {
                    GameProjectId = test.ProjectId,
                    Name = $"Item {i}",
                    UpdatedAtUtc = now.AddMinutes(-i)
                });
            }

            var other = new GameProject { Name = "Zweites Projekt" };
            db.GameProjects.Add(other);
            db.Items.Add(new Item { GameProjectId = other.Id, Name = "Fremdes Item", UpdatedAtUtc = now });

            await db.SaveChangesAsync();
        }

        var recent = await test.GetService<DashboardOverviewService>()
            .GetRecentlyEditedAsync(test.ProjectId, 3);

        Assert.Equal(["Item 0", "Item 1", "Item 2"], recent.Select(entry => entry.Hit.Name).ToArray());
    }

    [Fact]
    public async Task Weiterarbeiten_liefert_das_primaere_Sprite_mit()
    {
        using var test = new TestDatabase();
        Guid assetId;

        await using (var db = test.CreateContext())
        {
            var item = new Item { GameProjectId = test.ProjectId, Name = "Fackel" };
            db.Items.Add(item);

            var asset = new Asset
            {
                GameProjectId = test.ProjectId,
                OwnerEntityId = item.Id,
                OwnerModuleKey = ModuleKeys.Items,
                IsPrimary = true,
                FileName = "fackel.png",
                MimeType = "image/png",
                StorageKey = "fackel.png"
            };

            db.Assets.Add(asset);
            await db.SaveChangesAsync();

            assetId = asset.Id;
        }

        var entry = Assert.Single(
            await test.GetService<DashboardOverviewService>().GetRecentlyEditedAsync(test.ProjectId, 8));

        Assert.Equal(assetId, entry.Hit.PrimaryAssetId);
    }

    // ---------------------------------------------------------------------------- Zustand

    [Fact]
    public async Task Zustand_stellt_Funde_vor_die_sauberen_Pruefungen()
    {
        using var test = new TestDatabase();

        await using (var db = test.CreateContext())
        {
            // Ein gemeinsamer Wurf über 100 % — der einzige Fund. Ohne angelegte Items meldet
            // die Prüfung „Items ohne Bezugsquelle" nichts, obwohl sie zuerst deklariert ist.
            db.LootTables.Add(new LootTable
            {
                GameProjectId = test.ProjectId,
                Name = "Truhe",
                RollMode = LootRollMode.SinglePick,
                Entries =
                [
                    new LootEntry { ItemId = Guid.NewGuid(), Chance = 60 },
                    new LootEntry { ItemId = Guid.NewGuid(), Chance = 50 }
                ]
            });

            await db.SaveChangesAsync();
        }

        var health = await test.GetService<DashboardOverviewService>().GetHealthAsync(test.ProjectId);

        Assert.False(health.IsClean);
        Assert.Equal(1, health.TotalFindings);
        Assert.Equal(HealthCheckKeys.OverfullLoot, health.Checks[0].CheckKey);
        Assert.All(health.Checks.Skip(1), check => Assert.Equal(0, check.Findings));
    }

    [Fact]
    public async Task Zustand_meldet_ein_leeres_Projekt_als_sauber_und_prueft_trotzdem_alles()
    {
        using var test = new TestDatabase();

        var health = await test.GetService<DashboardOverviewService>().GetHealthAsync(test.ProjectId);

        Assert.True(health.IsClean);

        // Jede Prüfung des Konzepts hat eine Zeile — auch die ohne Fund, sonst wäre nicht
        // erkennbar, dass geprüft wurde.
        Assert.Equal(
            [
                HealthCheckKeys.CraftingCycles,
                HealthCheckKeys.CustomRules,
                HealthCheckKeys.DeadItems,
                HealthCheckKeys.DialogueDeadEnds,
                HealthCheckKeys.ImpossibleConditions,
                HealthCheckKeys.MissingRecordings,
                HealthCheckKeys.MoneyPrinters,
                HealthCheckKeys.OrphanedAssets,
                HealthCheckKeys.OverfullLoot,
                HealthCheckKeys.QuestsWithoutCompletion,
                HealthCheckKeys.UnlockCycles
            ],
            health.Checks.Select(check => check.CheckKey).OrderBy(key => key, StringComparer.Ordinal).ToArray());
    }

    // ----------------------------------------------------------------------------- Bänder

    [Fact]
    public async Task Ohne_Anpassung_gilt_die_Vorgabe_und_die_Datenbank_bleibt_aus()
    {
        using var test = new TestDatabase();

        var bands = await test.GetService<DashboardService>().GetBandsAsync(test.ProjectId);

        Assert.Equal(DashboardBands.All, bands.Select(band => band.BandKey));

        Assert.All(
            bands.Where(band => band.BandKey != DashboardBands.Database),
            band => Assert.True(band.IsVisible));

        Assert.False(bands.Single(band => band.BandKey == DashboardBands.Database).IsVisible);
    }

    [Fact]
    public async Task Gespeicherte_Anordnung_gilt_und_raeumt_das_alte_Kartenraster_ab()
    {
        using var test = new TestDatabase();

        await using (var db = test.CreateContext())
        {
            // Eine Zeile, wie sie das frühere Dashboard mit einer Karte je Modul hinterlassen hat.
            db.DashboardCards.Add(new DashboardCard
            {
                GameProjectId = test.ProjectId, CardKey = ModuleKeys.Items, SortOrder = 0
            });
            await db.SaveChangesAsync();
        }

        var service = test.GetService<DashboardService>();

        // Unbekannte Schlüssel erscheinen gar nicht erst als Band.
        Assert.DoesNotContain(
            ModuleKeys.Items,
            (await service.GetBandsAsync(test.ProjectId)).Select(band => band.BandKey));

        await service.SaveBandsAsync(test.ProjectId,
        [
            new(DashboardBands.Health, IsHidden: false),
            new(DashboardBands.Project, IsHidden: false),
            new(DashboardBands.Recent, IsHidden: true),
            new(DashboardBands.Inventory, IsHidden: false),
            new(DashboardBands.Database, IsHidden: false)
        ]);

        var bands = await service.GetBandsAsync(test.ProjectId);

        // Ein später hinzugekommenes Band hat keine Zeile und steht deshalb hinten — die
        // gespeicherte Anordnung der übrigen bleibt davon unberührt.
        Assert.Equal(
            [
                DashboardBands.Health,
                DashboardBands.Project,
                DashboardBands.Recent,
                DashboardBands.Inventory,
                DashboardBands.Database,
                DashboardBands.Pinned,
                DashboardBands.Tasks,
                DashboardBands.Comments,
                DashboardBands.Status
            ],
            bands.Select(band => band.BandKey).ToArray());

        Assert.False(bands.Single(band => band.BandKey == DashboardBands.Recent).IsVisible);

        // Ausdrücklich eingeschaltet schlägt die Vorgabe „standardmäßig aus".
        Assert.True(bands.Single(band => band.BandKey == DashboardBands.Database).IsVisible);

        await using (var db = test.CreateContext())
        {
            Assert.DoesNotContain(ModuleKeys.Items, db.DashboardCards.Select(card => card.CardKey).ToList());
        }
    }
}
