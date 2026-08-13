using GameDevManager.Data;
using GameDevManager.Data.Services;
using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Die Stichprobe für den Inhaltsregen des Startscreens: Sie zieht quer durch alle Module,
/// bleibt im aktiven Projekt und liefert das Sprite gleich mit.
/// </summary>
public class StartScreenTests
{
    private static Item AddItem(GameDevManagerDbContext db, Guid projectId, string name)
    {
        var item = new Item { GameProjectId = projectId, Name = name };
        db.Items.Add(item);
        return item;
    }

    [Fact]
    public async Task Stichprobe_sammelt_ueber_mehrere_Module_und_haelt_die_Obergrenze_ein()
    {
        using var test = new TestDatabase();

        await using (var db = test.CreateContext())
        {
            for (var i = 0; i < 5; i++)
            {
                AddItem(db, test.ProjectId, $"Item {i}");
            }

            db.Npcs.Add(new Npc { GameProjectId = test.ProjectId, Name = "Händler" });
            db.Maps.Add(new GameMap { GameProjectId = test.ProjectId, Name = "Hafen" });
            await db.SaveChangesAsync();
        }

        var hits = await test.GetService<StartScreenService>().SampleEntitiesAsync(test.ProjectId, 4);

        Assert.Equal(4, hits.Count);

        // Über mehrere Läufe kommt jedes der drei Module vor — sonst zöge die Stichprobe
        // nicht gemischt, sondern nur den Anfang der ersten Quelle.
        var modules = new HashSet<string>();

        for (var run = 0; run < 25; run++)
        {
            foreach (var hit in await test.GetService<StartScreenService>()
                .SampleEntitiesAsync(test.ProjectId, 4))
            {
                modules.Add(hit.ModuleKey);
            }
        }

        Assert.Equal(
            [ModuleKeys.Items, ModuleKeys.Maps, ModuleKeys.Npcs],
            modules.OrderBy(key => key).ToArray());
    }

    [Fact]
    public async Task Stichprobe_liefert_das_primaere_Sprite_mit()
    {
        using var test = new TestDatabase();
        Guid assetId;

        await using (var db = test.CreateContext())
        {
            var item = AddItem(db, test.ProjectId, "Fackel");

            var asset = new Asset
            {
                GameProjectId = test.ProjectId,
                OwnerEntityId = item.Id,
                OwnerModuleKey = ModuleKeys.Items,
                IsPrimary = true,
                FileName = "fackel.png",
                MimeType = "image/png",
                StorageKey = "fackel.png"
            };

            db.Assets.Add(asset);
            await db.SaveChangesAsync();

            assetId = asset.Id;
        }

        var hit = Assert.Single(
            await test.GetService<StartScreenService>().SampleEntitiesAsync(test.ProjectId, 8));

        Assert.Equal(assetId, hit.PrimaryAssetId);
    }

    [Fact]
    public async Task Stichprobe_bleibt_im_Projekt_und_ist_ohne_Inhalt_leer()
    {
        using var test = new TestDatabase();
        Guid otherProjectId;

        await using (var db = test.CreateContext())
        {
            var other = new GameProject { Name = "Zweites Projekt" };
            db.GameProjects.Add(other);
            AddItem(db, other.Id, "Fremdes Item");
            await db.SaveChangesAsync();

            otherProjectId = other.Id;
        }

        Assert.Empty(await test.GetService<StartScreenService>().SampleEntitiesAsync(test.ProjectId, 8));
        Assert.Single(await test.GetService<StartScreenService>().SampleEntitiesAsync(otherProjectId, 8));
    }
}
