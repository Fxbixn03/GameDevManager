using GameDevManager.Data.Services;
using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// „Als Vorlage kopieren“: Kind-Sammlungen und alles, was an der GUID hängt, kommen mit;
/// Verweise nach außen bleiben auf dem Original stehen.
/// </summary>
public class EntityDuplicationTests
{
    [Fact]
    public async Task Kopie_uebernimmt_Feldwerte_individuelle_Felder_und_Bedingungen()
    {
        using var test = new TestDatabase();

        Guid itemId;
        Guid typeFieldId;
        Guid individualFieldId;

        await using (var db = test.CreateContext())
        {
            var type = new ContentType { GameProjectId = test.ProjectId, ModuleKey = ModuleKeys.Items, Name = "Waffe" };
            var typeField = new FieldDefinition
            {
                ContentTypeId = type.Id,
                ModuleKey = ModuleKeys.Items,
                Name = "Schaden",
                Type = ContentFieldType.Integer
            };

            var item = new Item { GameProjectId = test.ProjectId, ContentTypeId = type.Id, Name = "Schwert" };

            // Ein Feld nur für dieses eine Item — das exotische Item aus dem Konzept.
            var individualField = new FieldDefinition
            {
                OwnerEntityId = item.Id,
                ModuleKey = ModuleKeys.Items,
                Name = "Fluch",
                Type = ContentFieldType.Text
            };

            db.ContentTypes.Add(type);
            db.FieldDefinitions.AddRange(typeField, individualField);
            db.Items.Add(item);
            db.FieldValues.AddRange(
                new FieldValue
                {
                    OwnerEntityId = item.Id,
                    OwnerModuleKey = ModuleKeys.Items,
                    FieldDefinitionId = typeField.Id,
                    NumberValue = 7
                },
                new FieldValue
                {
                    OwnerEntityId = item.Id,
                    OwnerModuleKey = ModuleKeys.Items,
                    FieldDefinitionId = individualField.Id,
                    TextValue = "brennt"
                });
            db.ConditionSets.Add(new ConditionSet
            {
                GameProjectId = test.ProjectId,
                OwnerId = item.Id,
                OwnerModuleKey = ModuleKeys.Items,
                Slot = ConditionSlots.Availability,
                Conditions = [new Condition { Kind = ConditionKind.PlayerLevel, NumberValue = 5 }]
            });

            await db.SaveChangesAsync();

            itemId = item.Id;
            typeFieldId = typeField.Id;
            individualFieldId = individualField.Id;
        }

        var copyId = await test.GetService<EntityDuplicationService>()
            .DuplicateAsync(test.ProjectId, ModuleKeys.Items, itemId);

        await using (var db = test.CreateContext())
        {
            var copy = await db.Items.SingleAsync(i => i.Id == copyId);
            Assert.Equal("Schwert (Kopie)", copy.Name);

            // Die Art bleibt dieselbe — sie wird nicht mitkopiert, sondern geteilt.
            Assert.Equal((await db.Items.SingleAsync(i => i.Id == itemId)).ContentTypeId, copy.ContentTypeId);

            // Das individuelle Feld ist ein eigenes, neues.
            var copiedField = await db.FieldDefinitions.SingleAsync(f => f.OwnerEntityId == copyId);
            Assert.Equal("Fluch", copiedField.Name);
            Assert.NotEqual(individualFieldId, copiedField.Id);

            var values = await db.FieldValues.Where(v => v.OwnerEntityId == copyId).ToListAsync();
            Assert.Equal(2, values.Count);

            // Der Wert am Art-Feld zeigt weiter dorthin, der am individuellen Feld auf dessen Kopie.
            Assert.Equal(7, values.Single(v => v.FieldDefinitionId == typeFieldId).NumberValue);
            Assert.Equal("brennt", values.Single(v => v.FieldDefinitionId == copiedField.Id).TextValue);

            var condition = await db.ConditionSets
                .Include(s => s.Conditions)
                .SingleAsync(s => s.OwnerId == copyId);
            Assert.Equal(5, Assert.Single(condition.Conditions).NumberValue);

            // Das Original ist unangetastet geblieben.
            Assert.Single(await db.FieldValues.Where(v => v.OwnerEntityId == itemId).ToListAsync(), v => v.NumberValue == 7);
        }
    }

    [Fact]
    public async Task Kopie_nimmt_Kind_Sammlungen_mit_und_zeigt_weiter_auf_dieselben_Items()
    {
        using var test = new TestDatabase();

        var zutatId = Guid.NewGuid();
        var zielId = Guid.NewGuid();
        Guid recipeId;

        await using (var db = test.CreateContext())
        {
            var recipe = new Recipe { GameProjectId = test.ProjectId, Name = "1× Fackel" };
            recipe.Outputs.Add(new RecipeOutput { RecipeId = recipe.Id, ItemId = zielId, Quantity = 1 });
            recipe.Ingredients.Add(new RecipeIngredient { RecipeId = recipe.Id, ItemId = zutatId, Quantity = 3 });

            db.Recipes.Add(recipe);
            await db.SaveChangesAsync();

            recipeId = recipe.Id;
        }

        var copyId = await test.GetService<EntityDuplicationService>()
            .DuplicateAsync(test.ProjectId, ModuleKeys.Crafting, recipeId);

        await using (var db = test.CreateContext())
        {
            var copy = await db.Recipes
                .Include(r => r.Outputs)
                .Include(r => r.Ingredients)
                .SingleAsync(r => r.Id == copyId);

            // Die Kinder gehören der Kopie und tragen eigene GUIDs …
            var output = Assert.Single(copy.Outputs);
            var ingredient = Assert.Single(copy.Ingredients);
            Assert.Equal(copyId, output.RecipeId);
            Assert.Equal(copyId, ingredient.RecipeId);

            // … zeigen aber weiter auf dieselben Items: Die Kopie stellt dasselbe her.
            Assert.Equal(zielId, output.ItemId);
            Assert.Equal(zutatId, ingredient.ItemId);
            Assert.Equal(3, ingredient.Quantity);

            // Das Original behält seine Kinder.
            var original = await db.Recipes.Include(r => r.Outputs).SingleAsync(r => r.Id == recipeId);
            Assert.Single(original.Outputs);
        }
    }

    [Fact]
    public async Task Zweite_Kopie_bekommt_einen_freien_Namen()
    {
        using var test = new TestDatabase();

        Guid currencyId;
        await using (var db = test.CreateContext())
        {
            var currency = new Currency { GameProjectId = test.ProjectId, Name = "Gold", Symbol = "G" };
            db.Currencies.Add(currency);
            await db.SaveChangesAsync();
            currencyId = currency.Id;
        }

        var duplication = test.GetService<EntityDuplicationService>();
        await duplication.DuplicateAsync(test.ProjectId, ModuleKeys.Currencies, currencyId);
        var secondId = await duplication.DuplicateAsync(test.ProjectId, ModuleKeys.Currencies, currencyId);

        await using (var db = test.CreateContext())
        {
            // Währungsnamen müssen je Projekt eindeutig sein — die zweite Kopie darf nicht
            // noch einmal „Gold (Kopie)“ heißen.
            Assert.Equal("Gold (Kopie 2)", (await db.Currencies.SingleAsync(c => c.Id == secondId)).Name);
        }
    }

    [Fact]
    public async Task Diplomatische_Beziehungen_lassen_sich_nicht_kopieren()
    {
        using var test = new TestDatabase();

        var duplication = test.GetService<EntityDuplicationService>();

        Assert.False(duplication.CanDuplicate(ModuleKeys.Diplomacy));
        Assert.True(duplication.CanDuplicate(ModuleKeys.Items));

        await Assert.ThrowsAsync<ContentValidationException>(
            () => duplication.DuplicateAsync(test.ProjectId, ModuleKeys.Diplomacy, Guid.NewGuid()));
    }
}
