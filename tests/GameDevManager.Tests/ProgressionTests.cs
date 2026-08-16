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
