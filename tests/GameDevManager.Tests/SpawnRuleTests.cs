using GameDevManager.Data.Services;
using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Spawn-Regeln: wo, wie viele und wie oft ein NPC erscheint. Das <b>Wann</b> steht als
/// Bedingungssatz an der GUID der Regel — geprüft wird vor allem, dass er dort auch wieder
/// abgeräumt wird.
/// </summary>
public class SpawnRuleTests
{
    private static async Task<Npc> CreateNpcAsync(TestDatabase test, params SpawnRule[] rules)
    {
        var npcs = test.GetService<NpcService>();
        var context = await npcs.LoadForEditAsync(test.ProjectId, null);

        context!.Entity.Name = "Wolf";

        foreach (var rule in rules)
        {
            rule.NpcId = context.Entity.Id;
            context.Entity.SpawnRules.Add(rule);
        }

        await npcs.SaveNpcAsync(context);
        return context.Entity;
    }

    private static async Task<Guid> SeedMapAsync(TestDatabase test)
    {
        var maps = test.GetService<MapService>();
        var context = await maps.LoadForEditAsync(test.ProjectId, null);

        context!.Entity.Name = "Wald";
        await maps.SaveMapAsync(context);

        return context.Entity.Id;
    }

    private static Task SetSpawnConditionAsync(TestDatabase test, Guid ruleId) =>
        test.GetService<ConditionService>().SaveAsync(new ConditionSet
        {
            GameProjectId = test.ProjectId,
            OwnerId = ruleId,
            OwnerModuleKey = ModuleKeys.Npcs,
            Slot = ConditionSlots.Spawn,
            Logic = ConditionLogic.All,
            Conditions = [new Condition { Kind = ConditionKind.Flag, TextValue = "nacht", BooleanValue = true }]
        });

    [Fact]
    public async Task Eine_Regel_wird_gespeichert_und_wieder_geladen()
    {
        using var test = new TestDatabase();
        var mapId = await SeedMapAsync(test);

        var npc = await CreateNpcAsync(test, new SpawnRule
        {
            TargetMapId = mapId,
            MinCount = 2,
            MaxCount = 5,
            RespawnSeconds = 300
        });

        var reloaded = await test.GetService<NpcService>().LoadForEditAsync(test.ProjectId, npc.Id);
        var rule = Assert.Single(reloaded!.Entity.SpawnRules);

        Assert.Equal(mapId, rule.TargetMapId);
        Assert.Equal("2–5", rule.DescribeCount());
        Assert.Equal(300, rule.RespawnSeconds);
    }

    [Fact]
    public async Task Eine_verdrehte_Spanne_wird_gerade_gezogen()
    {
        using var test = new TestDatabase();

        var npc = await CreateNpcAsync(test, new SpawnRule { MinCount = 0, MaxCount = -3 });

        var rule = Assert.Single(
            (await test.GetService<NpcService>().LoadForEditAsync(test.ProjectId, npc.Id))!.Entity.SpawnRules);

        // Mindestens einer, und die obere Grenze nie unter der unteren.
        Assert.Equal(1, rule.MinCount);
        Assert.Equal(1, rule.MaxCount);
    }

    [Fact]
    public async Task Eine_Markierung_ohne_Karte_wird_nicht_gespeichert()
    {
        using var test = new TestDatabase();

        var npc = await CreateNpcAsync(test, new SpawnRule { TargetMarkerId = Guid.NewGuid() });

        var rule = Assert.Single(
            (await test.GetService<NpcService>().LoadForEditAsync(test.ProjectId, npc.Id))!.Entity.SpawnRules);

        Assert.Null(rule.TargetMarkerId);
    }

    [Fact]
    public async Task Eine_entfernte_Regel_nimmt_ihre_Bedingung_mit()
    {
        using var test = new TestDatabase();
        var npc = await CreateNpcAsync(test, new SpawnRule(), new SpawnRule());

        var npcs = test.GetService<NpcService>();
        var removed = (await npcs.LoadForEditAsync(test.ProjectId, npc.Id))!.Entity.SpawnRules[0];
        await SetSpawnConditionAsync(test, removed.Id);

        var context = await npcs.LoadForEditAsync(test.ProjectId, npc.Id);
        context!.Entity.SpawnRules.RemoveAll(rule => rule.Id == removed.Id);
        await npcs.SaveNpcAsync(context);

        await using var db = test.CreateContext();

        Assert.Empty(await db.ConditionSets.Where(set => set.OwnerId == removed.Id).ToListAsync());
        Assert.Single(await db.SpawnRules.ToListAsync());
    }

    [Fact]
    public async Task Beim_Loeschen_des_NPCs_gehen_Regeln_und_Bedingungen_mit()
    {
        using var test = new TestDatabase();
        var npc = await CreateNpcAsync(test, new SpawnRule());

        var rule = (await test.GetService<NpcService>()
            .LoadForEditAsync(test.ProjectId, npc.Id))!.Entity.SpawnRules[0];

        await SetSpawnConditionAsync(test, rule.Id);
        await test.GetService<NpcService>().DeleteNpcAsync(npc.Id);

        await using var db = test.CreateContext();

        Assert.Empty(await db.SpawnRules.ToListAsync());
        Assert.Empty(await db.ConditionSets.ToListAsync());
    }

    [Fact]
    public async Task Eine_geloeschte_Karte_laesst_die_Regel_stehen()
    {
        using var test = new TestDatabase();
        var mapId = await SeedMapAsync(test);
        var npc = await CreateNpcAsync(test, new SpawnRule { TargetMapId = mapId, MinCount = 3, MaxCount = 3 });

        await test.GetService<MapService>().DeleteMapAsync(mapId);

        var rule = Assert.Single(
            (await test.GetService<NpcService>().LoadForEditAsync(test.ProjectId, npc.Id))!.Entity.SpawnRules);

        // Die Regel sagt weiterhin, wie viele und wie oft — nur das Wo ist offen.
        Assert.Null(rule.TargetMapId);
        Assert.Equal(3, rule.MinCount);
    }

    [Fact]
    public async Task Die_Karte_kennt_ihre_Spawn_Regeln()
    {
        using var test = new TestDatabase();

        var maps = test.GetService<MapService>();
        var mapContext = await maps.LoadForEditAsync(test.ProjectId, null);
        mapContext!.Entity.Name = "Wald";
        var marker = new MapMarker { MapId = mapContext.Entity.Id, X = 0.5, Y = 0.5 };
        mapContext.Entity.Markers.Add(marker);
        await maps.SaveMapAsync(mapContext);

        var npc = await CreateNpcAsync(test,
            new SpawnRule
            {
                TargetMapId = mapContext.Entity.Id,
                TargetMarkerId = marker.Id,
                MinCount = 2,
                MaxCount = 5,
                RespawnSeconds = 60
            },
            new SpawnRule { TargetMapId = mapContext.Entity.Id });

        var conditioned = (await test.GetService<NpcService>().LoadForEditAsync(test.ProjectId, npc.Id))!
            .Entity.SpawnRules.First(rule => rule.TargetMarkerId == marker.Id);
        await SetSpawnConditionAsync(test, conditioned.Id);

        var rows = await test.GetService<NpcService>().GetSpawnRulesForMapAsync(mapContext.Entity.Id);

        Assert.Equal(2, rows.Count);

        // An der Markierung: Anzahl, Nachwachsen und der Schalter für den Bedingungssatz.
        var atMarker = Assert.Single(rows, row => row.TargetMarkerId == marker.Id);
        Assert.Equal("Wolf", atMarker.NpcName);
        Assert.Equal("2–5", atMarker.DescribeCount());
        Assert.Equal(60, atMarker.RespawnSeconds);
        Assert.True(atMarker.HasConditions);

        // Ohne Markierung meint die Regel die ganze Karte — und ist hier unbedingt.
        var wholeMap = Assert.Single(rows, row => row.TargetMarkerId is null);
        Assert.False(wholeMap.HasConditions);
    }

    [Fact]
    public async Task Regeln_ueberstehen_Export_und_Import()
    {
        using var test = new TestDatabase();
        await CreateNpcAsync(test, new SpawnRule { MinCount = 2, MaxCount = 4, RespawnSeconds = 60 });

        using var zip = new MemoryStream();
        await test.GetService<ExportService>()
            .WriteExportAsync(test.ProjectId, ExportTarget.Json, includeAssets: false, zip);
        zip.Position = 0;

        await test.GetService<ImportService>().ImportAsync(test.ProjectId, zip, replaceExisting: true);

        await using var db = test.CreateContext();
        var rule = await db.SpawnRules.SingleAsync();

        Assert.Equal(2, rule.MinCount);
        Assert.Equal(4, rule.MaxCount);
        Assert.Equal(60, rule.RespawnSeconds);
    }
}
