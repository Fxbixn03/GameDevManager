using GameDevManager.Data.Services;
using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Der Papierkorb (F24): Löschen ist nicht mehr endgültig. Kein Soft-Delete-Schalter — der
/// zöge eine Filterbedingung durch jede Abfrage des Bestands; stattdessen dieselbe Strecke wie
/// beim Duplizieren, nur rückwärts und mit den <b>originalen</b> GUIDs.
/// </summary>
public class RecycleBinTests
{
    private static async Task<(Recipe Recipe, Item Output, Item Ingredient)> SeedRecipeAsync(TestDatabase test)
    {
        var items = test.GetService<ItemService>();

        async Task<Item> ItemAsync(string name)
        {
            var context = await items.LoadForEditAsync(test.ProjectId, null);
            context!.Entity.Name = name;
            await items.SaveItemAsync(context);

            return context.Entity;
        }

        var output = await ItemAsync("Fackel");
        var ingredient = await ItemAsync("Holz");

        var crafting = test.GetService<CraftingService>();
        var recipeContext = await crafting.LoadForEditAsync(test.ProjectId, null);

        recipeContext!.Entity.Outputs.Add(new RecipeOutput
        {
            RecipeId = recipeContext.Entity.Id, ItemId = output.Id, Quantity = 2
        });
        recipeContext.Entity.Ingredients.Add(new RecipeIngredient
        {
            RecipeId = recipeContext.Entity.Id, ItemId = ingredient.Id, Quantity = 1
        });

        await crafting.SaveRecipeAsync(recipeContext);

        return (recipeContext.Entity, output, ingredient);
    }

    [Fact]
    public async Task Loeschen_legt_einen_Eintrag_an()
    {
        using var test = new TestDatabase();
        var (recipe, _, _) = await SeedRecipeAsync(test);

        await test.GetService<CraftingService>().DeleteRecipeAsync(recipe.Id);

        var row = Assert.Single(await test.GetService<RecycleBinService>().GetEntriesAsync(test.ProjectId));

        Assert.Equal(ModuleKeys.Crafting, row.ModuleKey);
        Assert.Equal(recipe.Id, row.EntityId);
        Assert.False(row.IsBlocked);
    }

    [Fact]
    public async Task Zurueckholen_stellt_die_Entitaet_mit_ihrer_GUID_und_ihren_Kindern_her()
    {
        using var test = new TestDatabase();
        var (recipe, output, ingredient) = await SeedRecipeAsync(test);

        await test.GetService<CraftingService>().DeleteRecipeAsync(recipe.Id);

        var bin = test.GetService<RecycleBinService>();
        var row = Assert.Single(await bin.GetEntriesAsync(test.ProjectId));

        await bin.RestoreAsync(row.Id);

        await using var db = test.CreateContext();

        var restored = await db.Recipes
            .Include(r => r.Outputs)
            .Include(r => r.Ingredients)
            .SingleAsync(r => r.Id == recipe.Id);

        // Dieselbe GUID — jeder Verweis, der auf das Rezept zeigte, trägt wieder.
        Assert.Equal(recipe.Id, restored.Id);

        // Die Kind-Sammlungen kommen aus dem EF-Modell mit, ohne eine Zeile Modulwissen.
        Assert.Equal(output.Id, Assert.Single(restored.Outputs).ItemId);
        Assert.Equal(ingredient.Id, Assert.Single(restored.Ingredients).ItemId);

        // Und der Eintrag ist weg: Er beschreibt einen Zustand, den es nicht mehr gibt.
        Assert.Empty(await bin.GetEntriesAsync(test.ProjectId));
    }

    [Fact]
    public async Task Feldwerte_und_Bedingungen_kommen_mit_zurueck()
    {
        using var test = new TestDatabase();
        var types = test.GetService<ContentTypeService>();

        var type = new ContentType
        {
            GameProjectId = test.ProjectId, ModuleKey = ModuleKeys.Items, Name = "Waffe"
        };
        await types.SaveTypeAsync(type);

        var field = new FieldDefinition
        {
            ModuleKey = ModuleKeys.Items,
            ContentTypeId = type.Id,
            Name = "Schaden",
            Type = ContentFieldType.Integer
        };
        await types.SaveFieldAsync(field);

        var items = test.GetService<ItemService>();
        var context = await items.LoadForEditAsync(test.ProjectId, null);
        context!.Entity.Name = "Eisenschwert";
        context.Entity.ContentTypeId = type.Id;
        context.ValueFor(field).NumberValue = 12;
        await items.SaveItemAsync(context);

        var itemId = context.Entity.Id;

        await test.GetService<ConditionService>().SaveAsync(new ConditionSet
        {
            GameProjectId = test.ProjectId,
            OwnerId = itemId,
            OwnerModuleKey = ModuleKeys.Items,
            Slot = ConditionSlots.Unlock,
            Conditions = [new Condition { Kind = ConditionKind.PlayerLevel, NumberValue = 5 }]
        });

        await items.DeleteItemAsync(itemId);

        var bin = test.GetService<RecycleBinService>();
        await bin.RestoreAsync((await bin.GetEntriesAsync(test.ProjectId)).Single().Id);

        var restored = await items.LoadForEditAsync(test.ProjectId, itemId);
        Assert.Equal(12, restored!.Values[field.Id].NumberValue);

        await using var db = test.CreateContext();
        var set = await db.ConditionSets.Include(s => s.Conditions).SingleAsync(s => s.OwnerId == itemId);
        Assert.Equal(5, Assert.Single(set.Conditions).NumberValue);
    }

    [Fact]
    public async Task Eine_belegte_GUID_blockt_das_Zurueckholen()
    {
        using var test = new TestDatabase();
        var items = test.GetService<ItemService>();

        var context = await items.LoadForEditAsync(test.ProjectId, null);
        context!.Entity.Name = "Trank";
        await items.SaveItemAsync(context);

        var itemId = context.Entity.Id;
        await items.DeleteItemAsync(itemId);

        // Wieder da — etwa über einen eingespielten Exportstand.
        await using (var db = test.CreateContext())
        {
            db.Items.Add(new Item { Id = itemId, GameProjectId = test.ProjectId, Name = "Trank" });
            await db.SaveChangesAsync();
        }

        var bin = test.GetService<RecycleBinService>();
        var row = Assert.Single(await bin.GetEntriesAsync(test.ProjectId));

        Assert.True(row.IsBlocked);
        await Assert.ThrowsAsync<ContentValidationException>(() => bin.RestoreAsync(row.Id));
    }

    [Fact]
    public async Task Endgueltig_entfernen_und_leeren()
    {
        using var test = new TestDatabase();
        var items = test.GetService<ItemService>();

        for (var index = 0; index < 3; index++)
        {
            var context = await items.LoadForEditAsync(test.ProjectId, null);
            context!.Entity.Name = $"Trank {index}";
            await items.SaveItemAsync(context);
            await items.DeleteItemAsync(context.Entity.Id);
        }

        var bin = test.GetService<RecycleBinService>();
        var rows = await bin.GetEntriesAsync(test.ProjectId);
        Assert.Equal(3, rows.Count);

        await bin.PurgeAsync(rows[0].Id);
        Assert.Equal(2, (await bin.GetEntriesAsync(test.ProjectId)).Count);

        Assert.Equal(2, await bin.EmptyAsync(test.ProjectId));
        Assert.Empty(await bin.GetEntriesAsync(test.ProjectId));
    }

    [Fact]
    public async Task Die_Aufbewahrung_kuerzt_auf_die_Obergrenze()
    {
        using var test = new TestDatabase();
        var options = test.GetService<RecycleBinOptions>();
        options.MaxPerProject = 2;
        options.MaxAgeDays = 0;

        var items = test.GetService<ItemService>();

        for (var index = 0; index < 5; index++)
        {
            var context = await items.LoadForEditAsync(test.ProjectId, null);
            context!.Entity.Name = $"Trank {index}";
            await items.SaveItemAsync(context);
            await items.DeleteItemAsync(context.Entity.Id);
        }

        var bin = test.GetService<RecycleBinService>();
        Assert.Equal(3, await bin.PruneAsync(test.ProjectId));

        var rows = await bin.GetEntriesAsync(test.ProjectId);
        Assert.Equal(2, rows.Count);

        // Die jüngsten bleiben — sie sind die, deren Löschung man gerade bereut.
        Assert.Equal("Trank 4", rows[0].EntityName);
    }

    [Fact]
    public async Task Die_Aufbewahrung_kuerzt_nach_Alter()
    {
        using var test = new TestDatabase();
        var options = test.GetService<RecycleBinOptions>();
        options.MaxAgeDays = 30;
        options.MaxPerProject = 0;

        var items = test.GetService<ItemService>();
        var context = await items.LoadForEditAsync(test.ProjectId, null);
        context!.Entity.Name = "Trank";
        await items.SaveItemAsync(context);
        await items.DeleteItemAsync(context.Entity.Id);

        await using (var db = test.CreateContext())
        {
            var entry = await db.RecycleBinEntries.SingleAsync();
            entry.DeletedAtUtc = DateTime.UtcNow.AddDays(-31);
            await db.SaveChangesAsync();
        }

        var bin = test.GetService<RecycleBinService>();
        Assert.Equal(1, await bin.PruneAsync(test.ProjectId));
        Assert.Empty(await bin.GetEntriesAsync(test.ProjectId));
    }

    [Fact]
    public async Task Abgeschaltet_landet_nichts_im_Papierkorb()
    {
        using var test = new TestDatabase();
        test.GetService<RecycleBinOptions>().Enabled = false;

        var items = test.GetService<ItemService>();
        var context = await items.LoadForEditAsync(test.ProjectId, null);
        context!.Entity.Name = "Trank";
        await items.SaveItemAsync(context);
        await items.DeleteItemAsync(context.Entity.Id);

        // Wer den Papierkorb abschaltet, bekommt das Verhalten von vorher.
        Assert.Empty(await test.GetService<RecycleBinService>().GetEntriesAsync(test.ProjectId));
    }
}
