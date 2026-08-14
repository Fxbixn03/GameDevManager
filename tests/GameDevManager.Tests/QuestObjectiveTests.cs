using GameDevManager.Data.Services;
using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Quest-Ziele: die einzelnen Schritte einer Quest. Der interessante Teil ist nicht das
/// Speichern, sondern dass ihre Abschlussbedingung an <b>ihrer</b> GUID hängt — und was
/// daraus für den Health Check und fürs Aufräumen folgt.
/// </summary>
public sealed class QuestObjectiveTests : IDisposable
{
    private readonly TestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    private QuestService Quests => _database.GetService<QuestService>();

    private ConditionService Conditions => _database.GetService<ConditionService>();

    private async Task<Quest> CreateQuestAsync(string name, params string[] objectives)
    {
        var context = await Quests.LoadForEditAsync(_database.ProjectId, null);
        context!.Entity.Name = name;

        foreach (var text in objectives)
        {
            context.Entity.Objectives.Add(new QuestObjective
            {
                QuestId = context.Entity.Id,
                Text = text,
                SortOrder = context.Entity.Objectives.Count
            });
        }

        await Quests.SaveQuestAsync(context);
        return context.Entity;
    }

    private Task SetCompletionAsync(Guid ownerId) =>
        Conditions.SaveAsync(new ConditionSet
        {
            GameProjectId = _database.ProjectId,
            OwnerId = ownerId,
            OwnerModuleKey = ModuleKeys.Quests,
            Slot = ConditionSlots.Completion,
            Logic = ConditionLogic.All,
            Conditions = [new Condition { Kind = ConditionKind.Flag, TextValue = "erledigt", BooleanValue = true }]
        });

    [Fact]
    public async Task Ziele_werden_in_ihrer_Reihenfolge_gespeichert()
    {
        var quest = await CreateQuestAsync(
            "Der Botengang", "Sprich mit Alrik", "Sammle 5 Kräuter", "Kehre zurück");

        var reloaded = await Quests.LoadForEditAsync(_database.ProjectId, quest.Id);

        Assert.Equal(
            new[] { "Sprich mit Alrik", "Sammle 5 Kräuter", "Kehre zurück" },
            reloaded!.Entity.Objectives.Select(objective => objective.Text));
        Assert.Equal(new[] { 0, 1, 2 }, reloaded.Entity.Objectives.Select(objective => objective.SortOrder));
    }

    [Fact]
    public async Task Ein_Ziel_ohne_Text_wird_abgelehnt()
    {
        var context = await Quests.LoadForEditAsync(_database.ProjectId, null);
        context!.Entity.Name = "Der Botengang";
        context.Entity.Objectives.Add(new QuestObjective { QuestId = context.Entity.Id, Text = "  " });

        await Assert.ThrowsAsync<ContentValidationException>(() => Quests.SaveQuestAsync(context));
    }

    [Fact]
    public async Task Der_Health_Check_schaut_bei_Zielen_auf_die_Ziele_statt_auf_die_Quest()
    {
        var quest = await CreateQuestAsync("Der Botengang", "Sprich mit Alrik", "Kehre zurück");

        // Ohne jede Bedingung: ein Fund.
        Assert.Single(await Quests.FindQuestsWithoutCompletionAsync(_database.ProjectId));

        // Die Bedingung an der Quest hilft nicht — der Verlauf ist es, der abgehakt wird.
        await SetCompletionAsync(quest.Id);
        Assert.Single(await Quests.FindQuestsWithoutCompletionAsync(_database.ProjectId));

        var objectives = (await Quests.LoadForEditAsync(_database.ProjectId, quest.Id))!.Entity.Objectives;

        await SetCompletionAsync(objectives[0].Id);
        Assert.Single(await Quests.FindQuestsWithoutCompletionAsync(_database.ProjectId));

        // Erst wenn jedes Ziel eine hat, gilt die Quest als versorgt.
        await SetCompletionAsync(objectives[1].Id);
        Assert.Empty(await Quests.FindQuestsWithoutCompletionAsync(_database.ProjectId));
    }

    [Fact]
    public async Task Ohne_Ziele_zaehlt_weiter_die_Bedingung_an_der_Quest()
    {
        var quest = await CreateQuestAsync("Der Botengang");

        Assert.Single(await Quests.FindQuestsWithoutCompletionAsync(_database.ProjectId));

        await SetCompletionAsync(quest.Id);
        Assert.Empty(await Quests.FindQuestsWithoutCompletionAsync(_database.ProjectId));
    }

    [Fact]
    public async Task Ein_entferntes_Ziel_nimmt_seine_Bedingung_mit()
    {
        var quest = await CreateQuestAsync("Der Botengang", "Sprich mit Alrik", "Kehre zurück");

        var context = await Quests.LoadForEditAsync(_database.ProjectId, quest.Id);
        var removed = context!.Entity.Objectives[0];
        await SetCompletionAsync(removed.Id);

        context = await Quests.LoadForEditAsync(_database.ProjectId, quest.Id);
        context!.Entity.Objectives.RemoveAt(0);
        await Quests.SaveQuestAsync(context);

        await using var db = _database.CreateContext();

        Assert.Empty(await db.ConditionSets.Where(set => set.OwnerId == removed.Id).ToListAsync());
        Assert.Single(await db.QuestObjectives.ToListAsync());
    }

    [Fact]
    public async Task Beim_Loeschen_der_Quest_gehen_Ziele_und_ihre_Bedingungen_mit()
    {
        var quest = await CreateQuestAsync("Der Botengang", "Sprich mit Alrik");

        var objective = (await Quests.LoadForEditAsync(_database.ProjectId, quest.Id))!.Entity.Objectives[0];
        await SetCompletionAsync(objective.Id);

        await Quests.DeleteQuestAsync(quest.Id);

        await using var db = _database.CreateContext();

        Assert.Empty(await db.QuestObjectives.ToListAsync());
        Assert.Empty(await db.ConditionSets.ToListAsync());
    }

    [Fact]
    public async Task Ziele_ueberstehen_Export_und_Import()
    {
        await CreateQuestAsync("Der Botengang", "Sprich mit Alrik", "Kehre zurück");

        using var zip = new MemoryStream();
        await _database.GetService<ExportService>()
            .WriteExportAsync(_database.ProjectId, ExportTarget.Json, includeAssets: false, zip);
        zip.Position = 0;

        await _database.GetService<ImportService>()
            .ImportAsync(_database.ProjectId, zip, replaceExisting: true);

        await using var db = _database.CreateContext();
        var quest = await db.Quests.Include(q => q.Objectives).SingleAsync();

        Assert.Equal(
            new[] { "Sprich mit Alrik", "Kehre zurück" },
            quest.Objectives.OrderBy(objective => objective.SortOrder).Select(objective => objective.Text));
    }
}
