using GameDevManager.Data;
using GameDevManager.Data.Services;
using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Die Stichprobe für den Inhaltsregen des Startscreens: Sie zieht quer durch alle Module,
/// bleibt im aktiven Projekt und liefert Sprite und Seltenheitsfarbe gleich mit.
/// </summary>
public class StartScreenTests
{
    private static Item AddItem(GameDevManagerDbContext db, Guid projectId, string name)
    {
        var item = new Item { GameProjectId = projectId, Name = name };
        db.Items.Add(item);
        return item;
    }

    /// <summary>
    /// Hängt einer Entität eine Seltenheit an — wie in der Maske: ein Feld vom Typ
    /// <see cref="ContentFieldType.Rarity"/> und ein Wert, der auf die Stufe zeigt.
    /// </summary>
    private static void AssignRarity(
        GameDevManagerDbContext db, ContentEntity entity, Rarity rarity, int fieldSortOrder = 0)
    {
        var definition = new FieldDefinition
        {
            ModuleKey = entity.ModuleKey,
            OwnerEntityId = entity.Id,
            Name = $"Seltenheit {fieldSortOrder}",
            Type = ContentFieldType.Rarity,
            ReferenceModuleKey = ModuleKeys.Rarities,
            SortOrder = fieldSortOrder
        };

        db.FieldDefinitions.Add(definition);

        db.FieldValues.Add(new FieldValue
        {
            FieldDefinitionId = definition.Id,
            OwnerEntityId = entity.Id,
            OwnerModuleKey = entity.ModuleKey,
            ReferenceValue = rarity.Id
        });
    }

    [Fact]
    public async Task Stichprobe_sammelt_ueber_mehrere_Module_und_haelt_die_Obergrenze_ein()
    {
        using var test = new TestDatabase();

        await using (var db = test.CreateContext())
        {
            for (var i = 0; i < 5; i++)
            {
                AddItem(db, test.ProjectId, $"Item {i}");
            }

            db.Npcs.Add(new Npc { GameProjectId = test.ProjectId, Name = "Händler" });
            db.Maps.Add(new GameMap { GameProjectId = test.ProjectId, Name = "Hafen" });
            await db.SaveChangesAsync();
        }

        // Gezogen wird reihum über die Module: bei drei Modulen mit Inhalt und vier Tropfen
        // ist jedes Modul in jedem einzelnen Lauf dabei — nicht erst über viele Läufe hinweg.
        for (var run = 0; run < 25; run++)
        {
            var drawn = await test.GetService<StartScreenService>()
                .SampleEntitiesAsync(test.ProjectId, 4);

            Assert.Equal(4, drawn.Count);

            Assert.Equal(
                [ModuleKeys.Items, ModuleKeys.Maps, ModuleKeys.Npcs],
                drawn.Select(entity => entity.Hit.ModuleKey).Distinct().Order().ToArray());
        }
    }

    [Fact]
    public async Task Stichprobe_erreicht_auch_Entitaeten_jenseits_des_Topfes()
    {
        using var test = new TestDatabase();

        // Mehr Items, als ein Modul je Zug in den Topf legt: ohne zufälliges Fenster kämen
        // immer dieselben, und der Rest regnete nie.
        await using (var db = test.CreateContext())
        {
            for (var i = 0; i < 20; i++)
            {
                AddItem(db, test.ProjectId, $"Item {i}");
            }

            await db.SaveChangesAsync();
        }

        var seen = new HashSet<string>();

        for (var run = 0; run < 60; run++)
        {
            foreach (var entity in await test.GetService<StartScreenService>()
                .SampleEntitiesAsync(test.ProjectId, 16))
            {
                seen.Add(entity.Hit.Name);
            }
        }

        Assert.Equal(20, seen.Count);
    }

    [Fact]
    public async Task Stichprobe_liefert_das_primaere_Sprite_mit()
    {
        using var test = new TestDatabase();
        Guid assetId;

        await using (var db = test.CreateContext())
        {
            var item = AddItem(db, test.ProjectId, "Fackel");

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

        var drawn = Assert.Single(
            await test.GetService<StartScreenService>().SampleEntitiesAsync(test.ProjectId, 8));

        Assert.Equal(assetId, drawn.Hit.PrimaryAssetId);
    }

    [Fact]
    public async Task Stichprobe_liefert_die_Farbe_der_Seltenheit_und_sonst_nichts()
    {
        using var test = new TestDatabase();

        await using (var db = test.CreateContext())
        {
            var episch = new Rarity
            {
                GameProjectId = test.ProjectId,
                Name = "Episch",
                Color = "#A335EE",
                SortOrder = 3
            };

            db.Rarities.Add(episch);

            AssignRarity(db, AddItem(db, test.ProjectId, "Schwert"), episch);
            AddItem(db, test.ProjectId, "Stock");
            await db.SaveChangesAsync();
        }

        var byName = (await test.GetService<StartScreenService>()
                .SampleEntitiesAsync(test.ProjectId, 8))
            .ToDictionary(entity => entity.Hit.Name, entity => entity.RarityColor);

        Assert.Equal("#A335EE", byName["Schwert"]);
        Assert.Null(byName["Stock"]);

        // Die Stufe selbst regnet ebenfalls mit und trägt ihre eigene Farbe.
        Assert.Equal("#A335EE", byName["Episch"]);
    }

    [Fact]
    public async Task Bei_mehreren_Seltenheitsfeldern_gewinnt_das_oberste()
    {
        using var test = new TestDatabase();

        await using (var db = test.CreateContext())
        {
            var gewoehnlich = new Rarity
            {
                GameProjectId = test.ProjectId,
                Name = "Gewöhnlich",
                Color = "#9D9D9D",
                SortOrder = 0
            };

            var legendaer = new Rarity
            {
                GameProjectId = test.ProjectId,
                Name = "Legendär",
                Color = "#FF8000",
                SortOrder = 4
            };

            db.Rarities.AddRange(gewoehnlich, legendaer);

            var item = AddItem(db, test.ProjectId, "Amulett");
            AssignRarity(db, item, legendaer, fieldSortOrder: 1);
            AssignRarity(db, item, gewoehnlich, fieldSortOrder: 0);
            await db.SaveChangesAsync();
        }

        var amulett = (await test.GetService<StartScreenService>()
                .SampleEntitiesAsync(test.ProjectId, 8))
            .Single(entity => entity.Hit.Name == "Amulett");

        Assert.Equal("#9D9D9D", amulett.RarityColor);
    }

    [Fact]
    public async Task Stichprobe_bleibt_im_Projekt_und_ist_ohne_Inhalt_leer()
    {
        using var test = new TestDatabase();
        Guid otherProjectId;

        await using (var db = test.CreateContext())
        {
            var other = new GameProject { Name = "Zweites Projekt" };
            db.GameProjects.Add(other);
            AddItem(db, other.Id, "Fremdes Item");
            await db.SaveChangesAsync();

            otherProjectId = other.Id;
        }

        Assert.Empty(await test.GetService<StartScreenService>().SampleEntitiesAsync(test.ProjectId, 8));
        Assert.Single(await test.GetService<StartScreenService>().SampleEntitiesAsync(otherProjectId, 8));
    }
}
