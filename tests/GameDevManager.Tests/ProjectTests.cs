using GameDevManager.Data.Services;
using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Projektverwaltung: das Duplizieren über die Export→Import-Strecke und das Sicherheitsnetz
/// vor den zerstörenden Aktionen (ersetzender Import, Löschen eines Projekts).
/// </summary>
public class ProjectTests
{
    /// <summary>
    /// Legt einen kleinen, aber quervernetzten Bestand an: eine Art mit Feld, ein Item mit
    /// Feldwert, ein Rezept, das auf das Item zeigt, und ein Bedingungssatz daran. Genau
    /// diese Verweise muss das Duplizieren mitnehmen.
    /// </summary>
    private static async Task<(Guid ItemId, Guid RecipeId)> SeedAsync(TestDatabase database, Guid projectId)
    {
        await using var db = database.CreateContext();

        var itemType = new ContentType { GameProjectId = projectId, ModuleKey = ModuleKeys.Items, Name = "Waffe" };
        var field = new FieldDefinition
        {
            ContentTypeId = itemType.Id,
            ModuleKey = ModuleKeys.Items,
            Name = "Schaden",
            Type = ContentFieldType.Integer
        };
        var recipeType = new ContentType { GameProjectId = projectId, ModuleKey = ModuleKeys.Crafting, Name = "Schmieden" };

        var item = new Item { GameProjectId = projectId, ContentTypeId = itemType.Id, Name = "Fackel" };
        var value = new FieldValue
        {
            OwnerEntityId = item.Id,
            OwnerModuleKey = ModuleKeys.Items,
            FieldDefinitionId = field.Id,
            NumberValue = 12
        };

        var recipe = new Recipe { GameProjectId = projectId, ContentTypeId = recipeType.Id, Name = "1× Fackel" };
        recipe.Outputs.Add(new RecipeOutput { RecipeId = recipe.Id, ItemId = item.Id, Quantity = 1 });

        var condition = new ConditionSet
        {
            GameProjectId = projectId,
            OwnerId = item.Id,
            OwnerModuleKey = ModuleKeys.Items,
            Slot = ConditionSlots.Shop,
            Conditions =
            [
                new Condition
                {
                    Kind = ConditionKind.HasItem,
                    TargetModuleKey = ModuleKeys.Items,
                    TargetEntityId = item.Id,
                    NumberValue = 1
                }
            ]
        };

        db.ContentTypes.AddRange(itemType, recipeType);
        db.FieldDefinitions.Add(field);
        db.Items.Add(item);
        db.FieldValues.Add(value);
        db.Recipes.Add(recipe);
        db.ConditionSets.Add(condition);
        await db.SaveChangesAsync();

        return (item.Id, recipe.Id);
    }

    [Fact]
    public async Task Kopie_bekommt_neue_GUIDs_und_behaelt_alle_Verweise()
    {
        using var database = new TestDatabase();
        var (itemId, recipeId) = await SeedAsync(database, database.ProjectId);

        var copy = await database.GetService<ProjectService>()
            .DuplicateProjectAsync(database.ProjectId, "Testprojekt (Kopie)", "abgeleitet");

        Assert.Equal("Testprojekt (Kopie)", copy.Name);
        Assert.Equal("abgeleitet", copy.Description);

        await using var db = database.CreateContext();

        // Das Original bleibt unangetastet.
        Assert.Single(await db.Items.Where(i => i.GameProjectId == database.ProjectId).ToListAsync());

        var copiedItem = Assert.Single(await db.Items.Where(i => i.GameProjectId == copy.Id).ToListAsync());
        Assert.Equal("Fackel", copiedItem.Name);
        Assert.NotEqual(itemId, copiedItem.Id);

        // Die Art wanderte mit, und das kopierte Item hängt an der Kopie der Art.
        var copiedType = Assert.Single(await db.ContentTypes
            .Where(t => t.GameProjectId == copy.Id && t.ModuleKey == ModuleKeys.Items)
            .ToListAsync());
        Assert.Equal(copiedType.Id, copiedItem.ContentTypeId);

        // Der Feldwert zeigt auf das kopierte Item und die Kopie der Felddefinition.
        var copiedField = Assert.Single(await db.FieldDefinitions
            .Where(f => f.ContentTypeId == copiedType.Id)
            .ToListAsync());
        var copiedValue = Assert.Single(await db.FieldValues
            .Where(v => v.OwnerEntityId == copiedItem.Id)
            .ToListAsync());
        Assert.Equal(copiedField.Id, copiedValue.FieldDefinitionId);
        Assert.Equal(12, copiedValue.NumberValue);

        // Das Rezept zeigt auf das kopierte Item, nicht mehr auf das Original.
        var copiedRecipe = Assert.Single(await db.Recipes
            .Include(r => r.Outputs)
            .Where(r => r.GameProjectId == copy.Id)
            .ToListAsync());
        Assert.NotEqual(recipeId, copiedRecipe.Id);
        Assert.Equal(copiedItem.Id, Assert.Single(copiedRecipe.Outputs).ItemId);

        // Auch der Bedingungssatz hängt am kopierten Item — Besitzer wie Ziel.
        var copiedCondition = Assert.Single(await db.ConditionSets
            .Include(s => s.Conditions)
            .Where(s => s.GameProjectId == copy.Id)
            .ToListAsync());
        Assert.Equal(copiedItem.Id, copiedCondition.OwnerId);
        Assert.Equal(copiedItem.Id, Assert.Single(copiedCondition.Conditions).TargetEntityId);
    }

    [Fact]
    public async Task Kopie_mit_belegtem_Namen_wird_abgelehnt_und_hinterlaesst_nichts()
    {
        using var database = new TestDatabase();

        var projects = database.GetService<ProjectService>();

        await Assert.ThrowsAsync<ContentValidationException>(
            () => projects.DuplicateProjectAsync(database.ProjectId, "Testprojekt", null));

        Assert.Single(await projects.GetProjectsAsync());
    }

    [Fact]
    public async Task Loeschen_bewahrt_den_Bestand_vorher_als_Exportstand_auf()
    {
        using var database = new TestDatabase();
        await SeedAsync(database, database.ProjectId);

        // Ein zweites Projekt, damit das zu löschende nicht das letzte ist.
        var projects = database.GetService<ProjectService>();
        var keeper = new GameProject { Name = "Zweitprojekt" };
        await projects.SaveProjectAsync(keeper);

        await projects.DeleteProjectAsync(database.ProjectId);

        var snapshot = Assert.Single(database.GetService<ExportSnapshotService>().List(database.ProjectId));
        Assert.True(snapshot.EntryCount > 0);

        await using var db = database.CreateContext();
        Assert.Empty(await db.Items.Where(i => i.GameProjectId == database.ProjectId).ToListAsync());
    }

    [Fact]
    public async Task Leeres_Projekt_wird_ohne_Exportstand_geloescht()
    {
        using var database = new TestDatabase();

        var projects = database.GetService<ProjectService>();
        var empty = new GameProject { Name = "Leerprojekt" };
        await projects.SaveProjectAsync(empty);

        await projects.DeleteProjectAsync(empty.Id);

        // Nichts zu sichern heißt kein Stand — sonst füllte jedes Aufräumen die Historie.
        Assert.Empty(database.GetService<ExportSnapshotService>().List(empty.Id));
    }

    [Fact]
    public async Task Ersetzender_Import_bewahrt_den_bisherigen_Stand_vorher_auf()
    {
        using var database = new TestDatabase();
        await SeedAsync(database, database.ProjectId);

        // Ein Export des eigenen Standes ist das einfachste gültige Import-ZIP.
        using var zip = new MemoryStream();
        await database.GetService<ExportService>()
            .WriteExportAsync(database.ProjectId, ExportTarget.Json, includeAssets: false, zip);
        zip.Position = 0;

        await database.GetService<ImportService>().ImportAsync(database.ProjectId, zip, replaceExisting: true);

        Assert.Single(database.GetService<ExportSnapshotService>().List(database.ProjectId));
    }
}
