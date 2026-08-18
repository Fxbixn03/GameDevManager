using GameDevManager.Data.Services;
using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Die Health Checks des Konzepts: unerfüllbare Bedingungen, Dialog-Sackgassen und
/// Loot-Wahrscheinlichkeiten über 100 %.
/// </summary>
public class HealthCheckTests
{
    // -------------------------------------------------------------------- Stummschaltung

    private static async Task<LootTable> SeedOverfullTableAsync(TestDatabase database, string name)
    {
        await using var db = database.CreateContext();

        var itemId = Guid.NewGuid();
        var table = new LootTable
        {
            GameProjectId = database.ProjectId,
            Name = name,
            RollMode = LootRollMode.SinglePick,
            Entries =
            [
                new LootEntry { ItemId = itemId, Chance = 60 },
                new LootEntry { ItemId = itemId, Chance = 50 }
            ]
        };

        db.LootTables.Add(table);
        await db.SaveChangesAsync();

        return table;
    }

    [Fact]
    public async Task Ein_stummgeschalteter_Fund_zaehlt_nicht_im_Zustandsband()
    {
        using var database = new TestDatabase();
        var table = await SeedOverfullTableAsync(database, "Truhe");

        var mutes = database.GetService<HealthCheckMuteService>();
        var overview = database.GetService<DashboardOverviewService>();

        var loud = await overview.GetHealthAsync(database.ProjectId);
        Assert.Equal(1, loud.Checks.Single(c => c.CheckKey == HealthCheckKeys.OverfullLoot).Findings);

        await mutes.MuteAsync(database.ProjectId, HealthCheckKeys.OverfullLoot, table.Id, table.Name);

        var muted = await overview.GetHealthAsync(database.ProjectId);
        Assert.Equal(0, muted.Checks.Single(c => c.CheckKey == HealthCheckKeys.OverfullLoot).Findings);

        // Einsehbar bleibt sie — nur eben still.
        var entry = Assert.Single(await mutes.GetForProjectAsync(database.ProjectId));
        Assert.Equal("Truhe", entry.EntityName);

        // Aufheben macht den Fund wieder laut.
        await mutes.UnmuteAsync(database.ProjectId, HealthCheckKeys.OverfullLoot, table.Id);

        var again = await overview.GetHealthAsync(database.ProjectId);
        Assert.Equal(1, again.Checks.Single(c => c.CheckKey == HealthCheckKeys.OverfullLoot).Findings);
    }

    [Fact]
    public async Task Eine_Stummschaltung_faellt_weg_wenn_der_Fund_verschwindet()
    {
        using var database = new TestDatabase();
        var table = await SeedOverfullTableAsync(database, "Truhe");

        var mutes = database.GetService<HealthCheckMuteService>();
        await mutes.MuteAsync(database.ProjectId, HealthCheckKeys.OverfullLoot, table.Id, table.Name);

        // Der Fund verschwindet: Die Tabelle wird auf 100 % zurechtgestutzt.
        await using (var db = database.CreateContext())
        {
            var entry = db.LootEntries.First(e => e.LootTableId == table.Id && e.Chance == 50);
            entry.Chance = 40;
            await db.SaveChangesAsync();
        }

        // Der Prüf-Lauf räumt die Stummschaltung ab — kein Leichenbestand.
        await database.GetService<DashboardOverviewService>().GetHealthAsync(database.ProjectId);

        Assert.Empty(await mutes.GetForProjectAsync(database.ProjectId));
    }

    [Fact]
    public async Task Ohne_Schreibrecht_laesst_sich_nichts_stummschalten()
    {
        using var database = new TestDatabase();
        var table = await SeedOverfullTableAsync(database, "Truhe");

        database.Permissions.Current = UserPermissions.Full with { IsAdministrator = false, CanWrite = false };

        await Assert.ThrowsAsync<ContentValidationException>(() => database
            .GetService<HealthCheckMuteService>()
            .MuteAsync(database.ProjectId, HealthCheckKeys.OverfullLoot, table.Id, table.Name));
    }

    // ------------------------------------------------------------------------------- Loot

    [Fact]
    public async Task Loot_ueber_100_Prozent_wird_nur_bei_einem_gemeinsamen_Wurf_gemeldet()
    {
        using var database = new TestDatabase();

        Guid overfullId;
        await using (var db = database.CreateContext())
        {
            var itemId = Guid.NewGuid();

            var overfull = new LootTable
            {
                GameProjectId = database.ProjectId,
                Name = "Truhe",
                RollMode = LootRollMode.SinglePick,
                Entries =
                [
                    new LootEntry { ItemId = itemId, Chance = 60 },
                    new LootEntry { ItemId = itemId, Chance = 50 }
                ]
            };
            overfullId = overfull.Id;

            // Unabhängige Würfe: über 100 % ist normal und kein Fund.
            var independent = new LootTable
            {
                GameProjectId = database.ProjectId,
                Name = "Mob",
                RollMode = LootRollMode.Independent,
                Entries =
                [
                    new LootEntry { ItemId = itemId, Chance = 80 },
                    new LootEntry { ItemId = itemId, Chance = 80 }
                ]
            };

            // Gemeinsamer Wurf, aber genau 100 % — erlaubt.
            var full = new LootTable
            {
                GameProjectId = database.ProjectId,
                Name = "Boss",
                RollMode = LootRollMode.SinglePick,
                Entries =
                [
                    new LootEntry { ItemId = itemId, Chance = 50 },
                    new LootEntry { ItemId = itemId, Chance = 50 }
                ]
            };

            db.LootTables.AddRange(overfull, independent, full);
            await db.SaveChangesAsync();
        }

        var rows = await database.GetService<LootService>().FindOverfullTablesAsync(database.ProjectId);

        var row = Assert.Single(rows);
        Assert.Equal(overfullId, row.Id);
        Assert.Equal(110, row.TotalChance, precision: 5);
    }

    // ---------------------------------------------------------------------------- Dialoge

    [Fact]
    public async Task Dialog_Sackgassen_melden_unerreichbare_Zeilen_aber_kein_normales_Ende()
    {
        using var database = new TestDatabase();

        Guid unreachableId;
        await using (var db = database.CreateContext())
        {
            var start = new DialogueLine { Text = "Hallo!", SortOrder = 0 };
            var ende = new DialogueLine { Text = "Bis bald.", SortOrder = 1 };
            var verwaist = new DialogueLine { Text = "Das hört niemand.", SortOrder = 2 };
            unreachableId = verwaist.Id;

            start.Choices.Add(new DialogueChoice { DialogueLineId = start.Id, Text = "Tschüss", NextLineId = ende.Id });

            var conversation = new Dialogue
            {
                GameProjectId = database.ProjectId,
                Name = "Begrüßung",
                Kind = DialogueKind.Conversation,
                Lines = [start, ende, verwaist]
            };

            // Sprechblasen stehen absichtlich unverbunden nebeneinander — kein Fund.
            var bark = new Dialogue
            {
                GameProjectId = database.ProjectId,
                Name = "Rufe",
                Kind = DialogueKind.Bark,
                Lines =
                [
                    new DialogueLine { Text = "He du!", SortOrder = 0 },
                    new DialogueLine { Text = "Schönes Wetter.", SortOrder = 1 }
                ]
            };

            db.Dialogues.AddRange(conversation, bark);
            await db.SaveChangesAsync();
        }

        var problems = await database.GetService<DialogueService>().FindProblemsAsync(database.ProjectId);

        var problem = Assert.Single(problems);
        Assert.Equal(unreachableId, problem.LineId);
    }

    // ------------------------------------------------------------------------ Bedingungen

    [Fact]
    public async Task Bedingungen_melden_fehlende_Ziele_und_Widersprueche()
    {
        using var database = new TestDatabase();

        Guid missingTargetOwner, flagOwner, rangeOwner;
        await using (var db = database.CreateContext())
        {
            var item = new Item { GameProjectId = database.ProjectId, Name = "Schlüssel" };
            db.Items.Add(item);

            // Ziel-GUID, die es im Items-Modul nicht gibt → Fund.
            var missingTarget = new ConditionSet
            {
                GameProjectId = database.ProjectId,
                OwnerId = Guid.NewGuid(),
                OwnerModuleKey = ModuleKeys.Npcs,
                Slot = ConditionSlots.Availability,
                Conditions =
                [
                    new Condition
                    {
                        Kind = ConditionKind.HasItem,
                        TargetModuleKey = ModuleKeys.Items,
                        TargetEntityId = Guid.NewGuid(),
                        Operator = ComparisonOperator.AtLeast,
                        NumberValue = 1
                    }
                ]
            };
            missingTargetOwner = missingTarget.OwnerId;

            // Ein Schalter, der gesetzt und nicht gesetzt sein soll — bei „alle müssen
            // zutreffen“ ein Widerspruch.
            var contradictingFlag = new ConditionSet
            {
                GameProjectId = database.ProjectId,
                OwnerId = Guid.NewGuid(),
                OwnerModuleKey = ModuleKeys.Quests,
                Slot = ConditionSlots.Availability,
                Logic = ConditionLogic.All,
                Conditions =
                [
                    new Condition { Kind = ConditionKind.Flag, TextValue = "tor_offen", BooleanValue = true },
                    new Condition { Kind = ConditionKind.Flag, TextValue = "tor_offen", BooleanValue = false }
                ]
            };
            flagOwner = contradictingFlag.OwnerId;

            // Eine Menge gleichzeitig über 10 und unter 5.
            var contradictingRange = new ConditionSet
            {
                GameProjectId = database.ProjectId,
                OwnerId = Guid.NewGuid(),
                OwnerModuleKey = ModuleKeys.Npcs,
                Slot = ConditionSlots.Shop,
                Logic = ConditionLogic.All,
                Conditions =
                [
                    new Condition
                    {
                        Kind = ConditionKind.HasItem,
                        TargetModuleKey = ModuleKeys.Items,
                        TargetEntityId = item.Id,
                        Operator = ComparisonOperator.AtLeast,
                        NumberValue = 10
                    },
                    new Condition
                    {
                        Kind = ConditionKind.HasItem,
                        TargetModuleKey = ModuleKeys.Items,
                        TargetEntityId = item.Id,
                        Operator = ComparisonOperator.AtMost,
                        NumberValue = 5
                    }
                ]
            };
            rangeOwner = contradictingRange.OwnerId;

            // Erfüllbarer Satz mit existierendem Ziel — kein Fund.
            var fine = new ConditionSet
            {
                GameProjectId = database.ProjectId,
                OwnerId = Guid.NewGuid(),
                OwnerModuleKey = ModuleKeys.Npcs,
                Slot = ConditionSlots.Availability,
                Logic = ConditionLogic.All,
                Conditions =
                [
                    new Condition
                    {
                        Kind = ConditionKind.HasItem,
                        TargetModuleKey = ModuleKeys.Items,
                        TargetEntityId = item.Id,
                        Operator = ComparisonOperator.AtLeast,
                        NumberValue = 1
                    }
                ]
            };

            db.ConditionSets.AddRange(missingTarget, contradictingFlag, contradictingRange, fine);
            await db.SaveChangesAsync();
        }

        var problems = await database.GetService<ConditionService>().FindProblemsAsync(database.ProjectId);

        Assert.Equal(3, problems.Count);
        Assert.Contains(problems, p => p.OwnerId == missingTargetOwner);
        Assert.Contains(problems, p => p.OwnerId == flagOwner);
        Assert.Contains(problems, p => p.OwnerId == rangeOwner);
    }

    [Fact]
    public async Task Gegenlaeufige_Bedingungen_sind_bei_mindestens_eine_muss_zutreffen_kein_Widerspruch()
    {
        using var database = new TestDatabase();

        await using (var db = database.CreateContext())
        {
            db.ConditionSets.Add(new ConditionSet
            {
                GameProjectId = database.ProjectId,
                OwnerId = Guid.NewGuid(),
                OwnerModuleKey = ModuleKeys.Quests,
                Slot = ConditionSlots.Availability,
                Logic = ConditionLogic.Any,
                Conditions =
                [
                    new Condition { Kind = ConditionKind.Flag, TextValue = "tor_offen", BooleanValue = true },
                    new Condition { Kind = ConditionKind.Flag, TextValue = "tor_offen", BooleanValue = false }
                ]
            });
            await db.SaveChangesAsync();
        }

        Assert.Empty(await database.GetService<ConditionService>().FindProblemsAsync(database.ProjectId));
    }
}
