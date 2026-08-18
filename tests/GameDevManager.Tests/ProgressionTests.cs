using GameDevManager.Data.Services;
using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Die Fortschritts-Sicht: Der Freischaltungs-Graph zeigt, woran etwas hängt — diese
/// Auswertung zeigt, wann es kommt. Der interessante Teil ist das Erben der Stufe über die
/// Voraussetzungen.
/// </summary>
public class ProgressionTests
{
    // ------------------------------------------------------------- Simulation (pur)

    [Fact]
    public void Die_Zeitachse_verrechnet_Quest_Topf_und_Rate_und_markiert_Engpaesse()
    {
        // XP je Schritt: Stufe 1–4 je 100, Stufe 5 kostet 1000 — der Engpass.
        var curve = new GameDevManager.Domain.Curves.CurveDefinition
        {
            Expression = null,
            From = 1,
            To = 5,
            Overrides =
            [
                new() { X = 1, Y = 100 },
                new() { X = 2, Y = 100 },
                new() { X = 3, Y = 100 },
                new() { X = 4, Y = 100 },
                new() { X = 5, Y = 1000 }
            ]
        };

        var steps = ProgressionSimulation.Run(curve, questXp: 250, xpPerHour: 100);

        Assert.Equal(5, steps.Count);

        // Der Quest-Topf trägt die ersten zweieinhalb Stufen.
        Assert.Equal(0, steps[0].GrindXp);
        Assert.Equal(0, steps[1].GrindXp);
        Assert.Equal(50, steps[2].GrindXp);
        Assert.Equal(100, steps[3].GrindXp);

        // Danach zählt die Rate: 50 XP Grind bei 100 XP/h sind eine halbe Stunde.
        Assert.Equal(0.5, steps[2].Hours);
        Assert.Equal(10, steps[4].Hours);

        // Nur der teure Schritt ist ein Engpass — gemessen am Median, nicht am Mittelwert.
        Assert.Equal([false, false, false, false, true], steps.Select(step => step.IsBottleneck));
    }

    [Fact]
    public void Ohne_Rate_bleibt_die_Zeitspalte_leer_statt_zu_raten()
    {
        var curve = new GameDevManager.Domain.Curves.CurveDefinition
        {
            Expression = "100 * x",
            From = 1,
            To = 3
        };

        var steps = ProgressionSimulation.Run(curve, questXp: 0, xpPerHour: 0);

        Assert.Equal(3, steps.Count);
        Assert.All(steps, step => Assert.Null(step.Hours));
        Assert.Equal(600, steps[^1].CumulativeXp);
    }

    // ------------------------------------------------------------------ XP-Quellen

    [Fact]
    public async Task Quest_XP_wird_summiert_und_Mob_XP_gemittelt()
    {
        using var test = new TestDatabase();

        Guid questFieldId, mobFieldId;
        await using (var db = test.CreateContext())
        {
            var questType = new ContentType { GameProjectId = test.ProjectId, ModuleKey = ModuleKeys.Quests, Name = "Haupt" };
            var questField = new FieldDefinition
            {
                ContentTypeId = questType.Id, ModuleKey = ModuleKeys.Quests, Name = "XP", Type = ContentFieldType.Integer
            };
            var mobType = new ContentType { GameProjectId = test.ProjectId, ModuleKey = ModuleKeys.Npcs, Name = "Mob" };
            var mobField = new FieldDefinition
            {
                ContentTypeId = mobType.Id, ModuleKey = ModuleKeys.Npcs, Name = "XP", Type = ContentFieldType.Integer
            };

            var questA = new Quest { GameProjectId = test.ProjectId, Name = "Kräuter" };
            var questB = new Quest { GameProjectId = test.ProjectId, Name = "Bote" };
            var wolf = new Npc { GameProjectId = test.ProjectId, Name = "Wolf" };
            var bear = new Npc { GameProjectId = test.ProjectId, Name = "Bär" };

            db.ContentTypes.AddRange(questType, mobType);
            db.FieldDefinitions.AddRange(questField, mobField);
            db.Quests.AddRange(questA, questB);
            db.Npcs.AddRange(wolf, bear);
            db.FieldValues.AddRange(
                new FieldValue { OwnerEntityId = questA.Id, OwnerModuleKey = ModuleKeys.Quests, FieldDefinitionId = questField.Id, NumberValue = 100 },
                new FieldValue { OwnerEntityId = questB.Id, OwnerModuleKey = ModuleKeys.Quests, FieldDefinitionId = questField.Id, NumberValue = 150 },
                new FieldValue { OwnerEntityId = wolf.Id, OwnerModuleKey = ModuleKeys.Npcs, FieldDefinitionId = mobField.Id, NumberValue = 10 },
                new FieldValue { OwnerEntityId = bear.Id, OwnerModuleKey = ModuleKeys.Npcs, FieldDefinitionId = mobField.Id, NumberValue = 30 });

            await db.SaveChangesAsync();
            (questFieldId, mobFieldId) = (questField.Id, mobField.Id);
        }

        var progression = test.GetService<ProgressionService>();

        var quests = await progression.SumXpFieldAsync(test.ProjectId, ModuleKeys.Quests, questFieldId);
        Assert.Equal(250, quests.Sum);
        Assert.Equal(2, quests.Count);

        var mobs = await progression.SumXpFieldAsync(test.ProjectId, ModuleKeys.Npcs, mobFieldId);
        Assert.Equal(20, mobs.Average);
    }

    /// <summary>Legt einen Skilltree an, damit Skills einen Besitzer haben.</summary>
    private static async Task<Guid> SeedTreeAsync(TestDatabase test)
    {
        await using var db = test.CreateContext();

        var tree = new SkillTree { GameProjectId = test.ProjectId, Name = "Kampf" };
        db.SkillTrees.Add(tree);
        await db.SaveChangesAsync();

        return tree.Id;
    }

    private static async Task<Guid> AddSkillAsync(TestDatabase test, Guid treeId, string name)
    {
        await using var db = test.CreateContext();

        var skill = new Skill { GameProjectId = test.ProjectId, Name = name, SkillTreeId = treeId };
        db.Skills.Add(skill);
        await db.SaveChangesAsync();

        return skill.Id;
    }

    private static Task RequireLevelAsync(TestDatabase test, Guid ownerId, int level) =>
        test.GetService<ConditionService>().SaveAsync(new ConditionSet
        {
            GameProjectId = test.ProjectId,
            OwnerId = ownerId,
            OwnerModuleKey = ModuleKeys.Player,
            Slot = ConditionSlots.Unlock,
            Logic = ConditionLogic.All,
            Conditions =
            [
                new Condition
                {
                    Kind = ConditionKind.PlayerLevel,
                    Operator = ComparisonOperator.AtLeast,
                    NumberValue = level
                }
            ]
        });

    private static Task RequireUnlockAsync(TestDatabase test, Guid ownerId, Guid requiredId) =>
        test.GetService<ConditionService>().SaveAsync(new ConditionSet
        {
            GameProjectId = test.ProjectId,
            OwnerId = ownerId,
            OwnerModuleKey = ModuleKeys.Player,
            Slot = ConditionSlots.Unlock,
            Logic = ConditionLogic.All,
            Conditions =
            [
                new Condition
                {
                    Kind = ConditionKind.Unlocked,
                    TargetModuleKey = ModuleKeys.Player,
                    TargetEntityId = requiredId,
                    BooleanValue = true
                }
            ]
        });

    [Fact]
    public async Task Die_Stufe_am_Inhalt_selbst_zaehlt()
    {
        using var test = new TestDatabase();
        var treeId = await SeedTreeAsync(test);

        var basic = await AddSkillAsync(test, treeId, "Hieb");
        var advanced = await AddSkillAsync(test, treeId, "Wirbel");

        await RequireUnlockAsync(test, advanced, basic);
        await RequireLevelAsync(test, advanced, 10);

        var entries = await test.GetService<ProgressionService>().GetProgressionAsync(test.ProjectId);

        var wirbel = entries.Single(entry => entry.Name == "Wirbel");
        Assert.Equal(10, wirbel.Level);
        Assert.False(wirbel.IsInherited);
    }

    [Fact]
    public async Task Die_Stufe_erbt_sich_ueber_die_Voraussetzung()
    {
        using var test = new TestDatabase();
        var treeId = await SeedTreeAsync(test);

        var basic = await AddSkillAsync(test, treeId, "Hieb");
        var advanced = await AddSkillAsync(test, treeId, "Wirbel");

        // Am Grundskill hängt die Stufe, am Folgeskill nur die Voraussetzung.
        await RequireLevelAsync(test, basic, 10);
        await RequireUnlockAsync(test, advanced, basic);

        var entries = await test.GetService<ProgressionService>().GetProgressionAsync(test.ProjectId);

        var wirbel = entries.Single(entry => entry.Name == "Wirbel");

        // Wer den Grundskill erst auf Stufe 10 bekommt, hat den Folgeskill nicht früher.
        Assert.Equal(10, wirbel.Level);
        Assert.True(wirbel.IsInherited);
    }

    [Fact]
    public async Task Bei_zwei_Voraussetzungen_zaehlt_die_spaetere()
    {
        using var test = new TestDatabase();
        var treeId = await SeedTreeAsync(test);

        var early = await AddSkillAsync(test, treeId, "Früh");
        var late = await AddSkillAsync(test, treeId, "Spät");
        var target = await AddSkillAsync(test, treeId, "Ziel");

        await RequireLevelAsync(test, early, 5);
        await RequireLevelAsync(test, late, 20);

        await test.GetService<ConditionService>().SaveAsync(new ConditionSet
        {
            GameProjectId = test.ProjectId,
            OwnerId = target,
            OwnerModuleKey = ModuleKeys.Player,
            Slot = ConditionSlots.Unlock,
            Logic = ConditionLogic.All,
            Conditions =
            [
                new Condition
                {
                    Kind = ConditionKind.Unlocked,
                    TargetModuleKey = ModuleKeys.Player,
                    TargetEntityId = early,
                    BooleanValue = true
                },
                new Condition
                {
                    Kind = ConditionKind.Unlocked,
                    TargetModuleKey = ModuleKeys.Player,
                    TargetEntityId = late,
                    BooleanValue = true
                }
            ]
        });

        var entries = await test.GetService<ProgressionService>().GetProgressionAsync(test.ProjectId);

        Assert.Equal(20, entries.Single(entry => entry.Name == "Ziel").Level);
    }

    [Fact]
    public async Task Ohne_Stufenbezug_steht_ein_Inhalt_bei_jederzeit()
    {
        using var test = new TestDatabase();
        var treeId = await SeedTreeAsync(test);

        var basic = await AddSkillAsync(test, treeId, "Hieb");
        var advanced = await AddSkillAsync(test, treeId, "Wirbel");

        await RequireUnlockAsync(test, advanced, basic);

        var entries = await test.GetService<ProgressionService>().GetProgressionAsync(test.ProjectId);

        Assert.All(entries, entry => Assert.Null(entry.Level));
    }
}
