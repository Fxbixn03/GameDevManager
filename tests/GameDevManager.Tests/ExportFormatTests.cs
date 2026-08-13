using System.Text.Json;
using GameDevManager.Data.Services;
using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Die JSON-Regeln des Exportformats: Navigationsobjekte fallen raus, GUID-Referenzen und
/// Kind-Sammlungen bleiben — die Regel des Konzepts. Wer hier etwas rot macht, hat das
/// Exportformat geändert und muss die <c>FormatVersion</c> erhöhen.
/// </summary>
public class ExportFormatTests
{
    [Fact]
    public void Serialisierung_entfernt_Navigationen_und_berechnete_Eigenschaften()
    {
        var project = new GameProject { Name = "P" };
        var type = new ContentType { GameProjectId = project.Id, ModuleKey = ModuleKeys.Crafting, Name = "Schmieden" };
        var recipe = new Recipe
        {
            GameProjectId = project.Id,
            GameProject = project,
            ContentTypeId = type.Id,
            ContentType = type,
            Name = "2× Fackel"
        };
        // Rückverweis absichtlich gesetzt: Ohne das Entfernen der Navigationen liefe die
        // Serialisierung hier in einen Zyklus.
        recipe.Outputs.Add(new RecipeOutput { RecipeId = recipe.Id, Recipe = recipe, ItemId = Guid.NewGuid(), Quantity = 2 });

        var json = JsonSerializer.Serialize(recipe, ExportFormat.JsonOptions);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        // GUID-Spalten bleiben, die Objekte dahinter nicht.
        Assert.Equal(recipe.ContentTypeId.ToString(), root.GetProperty("contentTypeId").GetString());
        Assert.Equal(recipe.GameProjectId.ToString(), root.GetProperty("gameProjectId").GetString());
        Assert.False(root.TryGetProperty("contentType", out _));
        Assert.False(root.TryGetProperty("gameProject", out _));

        // Berechnete Nur-Lese-Eigenschaften stehen nicht im Export.
        Assert.False(root.TryGetProperty("moduleKey", out _));

        // Kind-Sammlungen bleiben eingebettet, ihre Rücknavigation nicht.
        var output = root.GetProperty("outputs")[0];
        Assert.Equal(recipe.Outputs[0].ItemId.ToString(), output.GetProperty("itemId").GetString());
        Assert.Equal(2, output.GetProperty("quantity").GetInt32());
        Assert.False(output.TryGetProperty("recipe", out _));
    }

    [Fact]
    public void Import_liest_mit_denselben_Regeln_wie_der_Export_schreibt()
    {
        var recipe = new Recipe { GameProjectId = Guid.NewGuid(), Name = "Barren" };
        recipe.Outputs.Add(new RecipeOutput { RecipeId = recipe.Id, ItemId = Guid.NewGuid(), Quantity = 4 });
        recipe.Ingredients.Add(new RecipeIngredient { RecipeId = recipe.Id, ItemId = Guid.NewGuid(), Quantity = 2 });

        var json = JsonSerializer.Serialize(recipe, ExportFormat.JsonOptions);
        var restored = JsonSerializer.Deserialize<Recipe>(json, ExportFormat.JsonOptions);

        Assert.NotNull(restored);
        Assert.Equal(recipe.Id, restored.Id);
        Assert.Equal(recipe.Name, restored.Name);
        Assert.Equal(recipe.Outputs[0].ItemId, Assert.Single(restored.Outputs).ItemId);
        Assert.Equal(recipe.Ingredients[0].ItemId, Assert.Single(restored.Ingredients).ItemId);
    }

    [Fact]
    public void AssetTag_Zuordnungen_werden_unterdrueckt()
    {
        // Die Zuordnungen stehen an den Assets; eine immer leere Liste im Export sähe nach
        // „keine Zuordnungen“ aus.
        var tag = new AssetTag { GameProjectId = Guid.NewGuid(), Name = "Prio", Color = "#FFC300" };

        var json = JsonSerializer.Serialize(tag, ExportFormat.JsonOptions);
        using var document = JsonDocument.Parse(json);

        Assert.Equal("Prio", document.RootElement.GetProperty("name").GetString());
        Assert.False(document.RootElement.TryGetProperty("assignments", out _));
    }

    [Fact]
    public void Enums_stehen_als_Text_im_Export()
    {
        var table = new LootTable
        {
            GameProjectId = Guid.NewGuid(),
            Name = "Truhe",
            RollMode = LootRollMode.SinglePick
        };

        var json = JsonSerializer.Serialize(table, ExportFormat.JsonOptions);
        using var document = JsonDocument.Parse(json);

        Assert.Equal("singlePick", document.RootElement.GetProperty("rollMode").GetString());
    }
}
