using GameDevManager.Data.Services;
using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Die globale Suche über mehr als den Namen: Beschreibungen, Textwerte benutzerdefinierter
/// Felder und die gesprochenen Zeilen der Dialoge.
/// </summary>
public class SearchTests
{
    [Fact]
    public async Task Textfeldwerte_fuehren_zur_besitzenden_Entitaet()
    {
        using var test = new TestDatabase();

        Guid itemId;
        await using (var db = test.CreateContext())
        {
            var type = new ContentType { GameProjectId = test.ProjectId, ModuleKey = ModuleKeys.Items, Name = "Waffe" };
            var field = new FieldDefinition
            {
                ContentTypeId = type.Id,
                ModuleKey = ModuleKeys.Items,
                Name = "Flavour",
                Type = ContentFieldType.MultilineText
            };
            var item = new Item { GameProjectId = test.ProjectId, ContentTypeId = type.Id, Name = "Schwert" };

            db.ContentTypes.Add(type);
            db.FieldDefinitions.Add(field);
            db.Items.Add(item);
            db.FieldValues.Add(new FieldValue
            {
                OwnerEntityId = item.Id,
                OwnerModuleKey = ModuleKeys.Items,
                FieldDefinitionId = field.Id,
                TextValue = "Geschmiedet in den Drachenhallen"
            });

            await db.SaveChangesAsync();
            itemId = item.Id;
        }

        var hits = await test.GetService<SearchService>().SearchAsync(test.ProjectId, "drachenhallen");

        var hit = Assert.Single(hits);
        Assert.Equal(itemId, hit.Id);
        Assert.Equal(ModuleKeys.Items, hit.ModuleKey);
    }

    [Fact]
    public async Task Dialogzeilen_fuehren_zum_Gespraech()
    {
        using var test = new TestDatabase();

        Guid dialogueId;
        await using (var db = test.CreateContext())
        {
            var dialogue = new Dialogue
            {
                GameProjectId = test.ProjectId,
                Name = "Begrüßung am Tor",
                Kind = DialogueKind.Conversation
            };
            dialogue.Lines.Add(new DialogueLine
            {
                DialogueId = dialogue.Id,
                Text = "Halt! Niemand betritt die Stadt ohne Siegel.",
                SortOrder = 0
            });

            db.Dialogues.Add(dialogue);
            await db.SaveChangesAsync();
            dialogueId = dialogue.Id;
        }

        var hits = await test.GetService<SearchService>().SearchAsync(test.ProjectId, "siegel");

        Assert.Equal(dialogueId, Assert.Single(hits).Id);
    }

    [Fact]
    public async Task Namenstreffer_verdraengt_den_Feldwerttreffer_derselben_Entitaet()
    {
        using var test = new TestDatabase();

        await using (var db = test.CreateContext())
        {
            var type = new ContentType { GameProjectId = test.ProjectId, ModuleKey = ModuleKeys.Items, Name = "Waffe" };
            var field = new FieldDefinition
            {
                ContentTypeId = type.Id,
                ModuleKey = ModuleKeys.Items,
                Name = "Notiz",
                Type = ContentFieldType.Text
            };
            var item = new Item { GameProjectId = test.ProjectId, ContentTypeId = type.Id, Name = "Fackel" };

            db.ContentTypes.Add(type);
            db.FieldDefinitions.Add(field);
            db.Items.Add(item);
            db.FieldValues.Add(new FieldValue
            {
                OwnerEntityId = item.Id,
                OwnerModuleKey = ModuleKeys.Items,
                FieldDefinitionId = field.Id,
                TextValue = "Die Fackel brennt lange"
            });

            await db.SaveChangesAsync();
        }

        var hits = await test.GetService<SearchService>().SearchAsync(test.ProjectId, "fackel");

        // Ein Eintrag, und zwar der Namenstreffer — sonst stünde dieselbe Entität zweimal da.
        var hit = Assert.Single(hits);
        Assert.Equal("Waffe", hit.Subtitle);
    }
}
