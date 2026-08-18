using GameDevManager.Data.Services;
using GameDevManager.Domain.Entities;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Der Crafting-Graph: Grundstoff-Rechnung mit Ausbeuten, Baumaufbau und die Zyklensuche
/// (Health Check „zyklische Rezepte“ aus dem Konzept).
/// </summary>
public class CraftingTests
{
    // ------------------------------------------------------------ Grundstoff-Rechnung (pur)

    private static CraftingTreeNode Leaf(Guid itemId, string name, int quantity) =>
        new(itemId, name, null, quantity, null, null, 1, 0, IsCycle: false, []);

    [Fact]
    public void SummarizeBaseCost_verrechnet_Ausbeuten_und_rundet_je_Stufe_auf()
    {
        // Ein Tisch braucht 6 Stäbe; das Stab-Rezept liefert 4 je Lauf und braucht je 1 Holz.
        // Für 6 Stäbe muss es zweimal laufen → 2 Holz, der Rest bleibt übrig.
        var holzId = Guid.NewGuid();
        var holz = Leaf(holzId, "Holz", 1);
        var stab = new CraftingTreeNode(
            Guid.NewGuid(), "Stab", null, 6, Guid.NewGuid(), "4× Stab", 4, 0, IsCycle: false, [holz]);
        var tisch = new CraftingTreeNode(
            Guid.NewGuid(), "Tisch", null, 1, Guid.NewGuid(), "Tisch", 1, 0, IsCycle: false, [stab]);

        var totals = CraftingService.SummarizeBaseCost(tisch);

        var requirement = Assert.Single(totals);
        Assert.Equal(holzId, requirement.ItemId);
        Assert.Equal(2, requirement.Quantity);
    }

    [Fact]
    public void SummarizeBaseCost_zaehlt_denselben_Grundstoff_ueber_mehrere_Zweige_zusammen()
    {
        var holzId = Guid.NewGuid();
        var fackel = new CraftingTreeNode(
            Guid.NewGuid(), "Fackel", null, 1, Guid.NewGuid(), "Fackel", 1, 0, IsCycle: false,
            [Leaf(holzId, "Holz", 2), Leaf(Guid.NewGuid(), "Kohle", 1), Leaf(holzId, "Holz", 3)]);

        var totals = CraftingService.SummarizeBaseCost(fackel);

        Assert.Equal(5, totals.Single(entry => entry.ItemId == holzId).Quantity);
        Assert.Equal(2, totals.Count);
    }

    [Fact]
    public void SummarizeBaseCost_liefert_nichts_fuer_ein_Item_ohne_Rezept()
    {
        Assert.Empty(CraftingService.SummarizeBaseCost(Leaf(Guid.NewGuid(), "Erz", 1)));
    }

    // ------------------------------------------------------------------ Graph (mit Datenbank)

    private static Item AddItem(GameDevManager.Data.GameDevManagerDbContext db, Guid projectId, string name)
    {
        var item = new Item { GameProjectId = projectId, Name = name };
        db.Items.Add(item);
        return item;
    }

    private static void AddRecipe(
        GameDevManager.Data.GameDevManagerDbContext db, Guid projectId,
        (Guid ItemId, int Quantity)[] outputs, (Guid ItemId, int Quantity)[] ingredients)
    {
        var recipe = new Recipe { GameProjectId = projectId, Name = string.Empty };
        recipe.Outputs.AddRange(outputs.Select((output, index) => new RecipeOutput
        {
            RecipeId = recipe.Id,
            ItemId = output.ItemId,
            Quantity = output.Quantity,
            SortOrder = index
        }));
        recipe.Ingredients.AddRange(ingredients.Select((ingredient, index) => new RecipeIngredient
        {
            RecipeId = recipe.Id,
            ItemId = ingredient.ItemId,
            Quantity = ingredient.Quantity,
            SortOrder = index
        }));

        db.Recipes.Add(recipe);
    }

    [Fact]
    public async Task BuildTreeAsync_loest_Zutaten_ueber_mehrere_Stufen_auf()
    {
        using var database = new TestDatabase();

        Guid fackelId, holzId;
        await using (var db = database.CreateContext())
        {
            var fackel = AddItem(db, database.ProjectId, "Fackel");
            var holz = AddItem(db, database.ProjectId, "Holz");
            (fackelId, holzId) = (fackel.Id, holz.Id);
            AddRecipe(db, database.ProjectId, [(fackelId, 2)], [(holzId, 3)]);
            await db.SaveChangesAsync();
        }

        var tree = await database.GetService<CraftingService>().BuildTreeAsync(database.ProjectId, fackelId);

        Assert.NotNull(tree);
        Assert.Equal(2, tree.RecipeOutputQuantity);
        var child = Assert.Single(tree.Children);
        Assert.Equal(holzId, child.ItemId);
        Assert.Equal(3, child.Quantity);
        Assert.Empty(child.Children);
    }

    [Fact]
    public async Task Umbenanntes_Item_zieht_den_gespeicherten_Rezeptnamen_nach()
    {
        using var database = new TestDatabase();

        Guid fackelId;
        await using (var db = database.CreateContext())
        {
            fackelId = AddItem(db, database.ProjectId, "Fackel").Id;
            AddRecipe(db, database.ProjectId, [(fackelId, 2)], []);
            await db.SaveChangesAsync();
        }

        var items = database.GetService<ItemService>();
        var context = await items.LoadForEditAsync(database.ProjectId, fackelId);
        context!.Entity.Name = "Kienspan";
        await items.SaveItemAsync(context);

        await using (var db = database.CreateContext())
        {
            var recipe = Assert.Single(db.Recipes);
            Assert.Equal("2× Kienspan", recipe.Name);
        }
    }

    [Fact]
    public async Task FindCyclesAsync_findet_Items_die_sich_selbst_herstellen()
    {
        using var database = new TestDatabase();

        await using (var db = database.CreateContext())
        {
            var barren = AddItem(db, database.ProjectId, "Barren");
            var schrott = AddItem(db, database.ProjectId, "Schrott");
            var erz = AddItem(db, database.ProjectId, "Erz");

            // Barren → Schrott → Barren ist ein Zyklus; Erz → Barren gehört nicht dazu.
            AddRecipe(db, database.ProjectId, [(barren.Id, 1)], [(schrott.Id, 1)]);
            AddRecipe(db, database.ProjectId, [(schrott.Id, 1)], [(barren.Id, 1)]);
            AddRecipe(db, database.ProjectId, [(barren.Id, 1)], [(erz.Id, 2)]);
            await db.SaveChangesAsync();
        }

        var cycles = await database.GetService<CraftingService>().FindCyclesAsync(database.ProjectId);

        var cycle = Assert.Single(cycles);
        Assert.Equal(2, cycle.ItemNames.Count);
        Assert.Contains("Barren", cycle.ItemNames);
        Assert.Contains("Schrott", cycle.ItemNames);
    }

    [Fact]
    public async Task FindCyclesAsync_meldet_nichts_bei_einer_normalen_Rezeptkette()
    {
        using var database = new TestDatabase();

        await using (var db = database.CreateContext())
        {
            var erz = AddItem(db, database.ProjectId, "Erz");
            var barren = AddItem(db, database.ProjectId, "Barren");
            var schwert = AddItem(db, database.ProjectId, "Schwert");
            AddRecipe(db, database.ProjectId, [(barren.Id, 1)], [(erz.Id, 2)]);
            AddRecipe(db, database.ProjectId, [(schwert.Id, 1)], [(barren.Id, 3)]);
            await db.SaveChangesAsync();
        }

        Assert.Empty(await database.GetService<CraftingService>().FindCyclesAsync(database.ProjectId));
    }

    [Fact]
    public async Task BuildTreeAsync_markiert_ein_wiederkehrendes_Item_statt_endlos_zu_laufen()
    {
        using var database = new TestDatabase();

        Guid barrenId;
        await using (var db = database.CreateContext())
        {
            var barren = AddItem(db, database.ProjectId, "Barren");
            var schrott = AddItem(db, database.ProjectId, "Schrott");
            barrenId = barren.Id;
            AddRecipe(db, database.ProjectId, [(barren.Id, 1)], [(schrott.Id, 1)]);
            AddRecipe(db, database.ProjectId, [(schrott.Id, 1)], [(barren.Id, 1)]);
            await db.SaveChangesAsync();
        }

        var tree = await database.GetService<CraftingService>().BuildTreeAsync(database.ProjectId, barrenId);

        Assert.NotNull(tree);
        var schrottNode = Assert.Single(tree.Children);
        var cycleNode = Assert.Single(schrottNode.Children);
        Assert.Equal(barrenId, cycleNode.ItemId);
        Assert.True(cycleNode.IsCycle);
        Assert.Empty(cycleNode.Children);
    }
}
