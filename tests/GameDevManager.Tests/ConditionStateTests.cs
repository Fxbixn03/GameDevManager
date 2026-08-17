using GameDevManager.Data.Services;
using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Die Zustands-Sicht: alle Bedingungssätze eines Projekts mit aufgelösten Besitzern — ganze
/// Entitäten über die Modul-Quellen, Teilobjekte (Händler-Posten, Dialogzeilen, …) über die
/// ausdrücklichen Nachschlagewege im Dienst.
/// </summary>
public class ConditionStateTests
{
    private static Task SaveConditionAsync(TestDatabase test, Guid ownerId, string moduleKey, string slot) =>
        test.GetService<ConditionService>().SaveAsync(new ConditionSet
        {
            GameProjectId = test.ProjectId,
            OwnerId = ownerId,
            OwnerModuleKey = moduleKey,
            Slot = slot,
            Logic = ConditionLogic.All,
            Conditions =
            [
                new Condition
                {
                    Kind = ConditionKind.PlayerLevel,
                    Operator = ComparisonOperator.AtLeast,
                    NumberValue = 5
                }
            ]
        });

    [Fact]
    public async Task Besitzer_werden_aufgeloest_auch_Teilobjekte()
    {
        using var test = new TestDatabase();

        // Ein Item und ein Händler mit einem Posten darauf.
        Guid itemId;
        await using (var db = test.CreateContext())
        {
            var item = new Item { GameProjectId = test.ProjectId, Name = "Eisenschwert" };
            db.Items.Add(item);
            await db.SaveChangesAsync();
            itemId = item.Id;
        }

        var npcs = test.GetService<NpcService>();
        var npcContext = await npcs.LoadForEditAsync(test.ProjectId, null);
        npcContext!.Entity.Name = "Alrik";
        npcContext.Entity.IsTrader = true;
        var offer = new TraderOffer { NpcId = npcContext.Entity.Id, ItemId = itemId, SortOrder = 0 };
        npcContext.Entity.Offers.Add(offer);
        await npcs.SaveNpcAsync(npcContext);

        // Ein Dialog mit Bedingung an sich selbst — der direkte Fall.
        var dialogues = test.GetService<DialogueService>();
        var dialogueContext = await dialogues.LoadForEditAsync(test.ProjectId, null);
        dialogueContext!.Entity.Name = "Torwache";
        dialogueContext.Entity.Lines.Add(new DialogueLine
        {
            DialogueId = dialogueContext.Entity.Id,
            Text = "Halt!",
            SortOrder = 0
        });
        await dialogues.SaveDialogueAsync(dialogueContext);

        await SaveConditionAsync(test, offer.Id, ModuleKeys.Npcs, ConditionSlots.Shop);
        await SaveConditionAsync(test, dialogueContext.Entity.Id, ModuleKeys.Dialogs, ConditionSlots.Availability);

        var rows = await test.GetService<ConditionStateService>().GetOwnersAsync(test.ProjectId);

        Assert.Equal(2, rows.Count);

        // Der Posten ist ein Teilobjekt: Beschriftet mit dem Händler, Detail ist das Item,
        // und das Sprungziel ist die NPC-Maske.
        var offerRow = Assert.Single(rows, row => row.OwnerId == offer.Id);
        Assert.Equal("Alrik", offerRow.Label);
        Assert.Equal("Eisenschwert", offerRow.Detail);
        Assert.Equal(ModuleKeys.Npcs, offerRow.NavigateModuleKey);
        Assert.Equal(npcContext.Entity.Id, offerRow.NavigateEntityId);

        var dialogueRow = Assert.Single(rows, row => row.OwnerId == dialogueContext.Entity.Id);
        Assert.Equal("Torwache", dialogueRow.Label);
        Assert.Null(dialogueRow.Detail);
        Assert.Equal(dialogueContext.Entity.Id, dialogueRow.NavigateEntityId);

        // Die Bedingungen kommen mit — die Seite rechnet damit.
        Assert.True(ConditionEvaluator.Evaluate(
            offerRow.Set, new GameStateAssumption { PlayerLevel = 5 }).Satisfied);
        Assert.False(ConditionEvaluator.Evaluate(
            offerRow.Set, new GameStateAssumption { PlayerLevel = 1 }).Satisfied);
    }

    [Fact]
    public async Task Ein_verwaister_Satz_wird_ausgewiesen_statt_verschwiegen()
    {
        using var test = new TestDatabase();

        await SaveConditionAsync(test, Guid.NewGuid(), ModuleKeys.Items, ConditionSlots.Availability);

        var row = Assert.Single(await test.GetService<ConditionStateService>().GetOwnersAsync(test.ProjectId));

        Assert.Equal("Unbekannter Besitzer", row.Label);
        Assert.Null(row.Detail);
    }
}
