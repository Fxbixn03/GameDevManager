using GameDevManager.Data.Services;
using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Das Beispielprojekt: über die echten Modul-Dienste angelegt, damit es zu jedem Stand des
/// Codes gültig ist. Geprüft wird, dass die versprochenen Bausteine wirklich dastehen — und
/// dass die Health Checks am Beispiel nichts zu beanstanden haben.
/// </summary>
public class SampleProjectTests
{
    [Fact]
    public async Task Das_Beispielprojekt_traegt_alle_versprochenen_Bausteine()
    {
        using var test = new TestDatabase();

        var project = await test.GetService<SampleProjectService>().CreateAsync();

        await using var db = test.CreateContext();

        Assert.Equal(5, await db.Items.CountAsync(item => item.GameProjectId == project.Id));
        Assert.Equal(3, await db.Recipes.CountAsync(recipe => recipe.GameProjectId == project.Id));
        Assert.Equal(2, await db.Npcs.CountAsync(npc => npc.GameProjectId == project.Id));
        Assert.Equal(1, await db.Currencies.CountAsync(currency => currency.GameProjectId == project.Id));
        Assert.Equal(1, await db.Dialogues.CountAsync(dialogue => dialogue.GameProjectId == project.Id));
        Assert.Equal(1, await db.Maps.CountAsync(map => map.GameProjectId == project.Id));

        // Die Unterart erbt die Felder der Eltern-Art — der Kern des Feldsystems.
        var types = await test.GetService<ContentTypeService>().GetTypesAsync(project.Id, ModuleKeys.Items);
        var melee = types.Single(type => type.ParentId is not null);
        Assert.Contains(melee.InheritedFields, field => field.Name == "Schaden");

        // Das Schwert trägt einen Wert im geerbten Feld.
        var sword = await db.Items.SingleAsync(item => item.Name == "Eisenschwert");
        var damage = await db.FieldValues.SingleAsync(value =>
            value.OwnerEntityId == sword.Id && value.FieldDefinition!.Name == "Schaden");
        Assert.Equal(12, damage.NumberValue);

        // Das Händler-Angebot des Schwerts ist bedingt — am Posten, nicht am NPC.
        var offer = await db.TraderOffers.SingleAsync(o => o.ItemId == sword.Id);
        var condition = await db.ConditionSets.SingleAsync(set => set.OwnerId == offer.Id);
        Assert.Equal(ConditionSlots.Shop, condition.Slot);

        // Die Karte hat ein Polygon-Gebiet und einen Marker auf den Schmied.
        var markers = await db.MapMarkers.ToListAsync();
        Assert.Contains(markers, marker => marker.IsPolygon);
        Assert.Contains(markers, marker => marker.TargetModuleKey == ModuleKeys.Npcs);

        // Der Wolf erscheint über eine Spawn-Regel auf der Karte.
        var map = await db.Maps.SingleAsync(m => m.GameProjectId == project.Id);
        Assert.Equal(map.Id, (await db.SpawnRules.SingleAsync()).TargetMapId);

        // Der Dialog verzweigt: Die Einstiegszeile bietet zwei Antworten an.
        var dialogue = await db.Dialogues
            .Include(d => d.Lines).ThenInclude(line => line.Choices)
            .SingleAsync(d => d.GameProjectId == project.Id);
        Assert.Equal(3, dialogue.Lines.Count);
        Assert.Equal(2, dialogue.Lines.OrderBy(line => line.SortOrder).First().Choices.Count);
    }

    [Fact]
    public async Task Die_Health_Checks_haben_am_Beispiel_nichts_zu_beanstanden()
    {
        using var test = new TestDatabase();

        var project = await test.GetService<SampleProjectService>().CreateAsync();
        var health = await test.GetService<DashboardOverviewService>().GetHealthAsync(project.Id);

        // Ein Beispiel, das mit Funden startet, lehrte das Falsche.
        Assert.Equal(0, health.TotalFindings);
    }

    [Fact]
    public async Task Der_Name_weicht_aus_wenn_es_das_Beispiel_schon_gibt()
    {
        using var test = new TestDatabase();
        var samples = test.GetService<SampleProjectService>();

        var first = await samples.CreateAsync();
        var second = await samples.CreateAsync();

        Assert.Equal("Beispielprojekt", first.Name);
        Assert.Equal("Beispielprojekt 2", second.Name);
    }
}
