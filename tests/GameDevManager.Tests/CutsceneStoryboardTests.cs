using GameDevManager.Data.Services;
using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Das Cutscene-Storyboard: Dauer und Kameranotiz je Einstellung, das Skizzenbild als Asset an
/// ihrer eigenen GUID. Der interessante Teil ist das Aufräumen — ein entferntes Bild darf nicht
/// im Speicher zurückbleiben.
/// </summary>
public class CutsceneStoryboardTests
{
    private static async Task<Cutscene> CreateAsync(TestDatabase test, params string[] shots)
    {
        var service = test.GetService<CutsceneService>();
        var context = await service.LoadForEditAsync(test.ProjectId, null);

        context!.Entity.Name = "Der Aufbruch";

        foreach (var text in shots)
        {
            context.Entity.Shots.Add(new CutsceneShot
            {
                CutsceneId = context.Entity.Id,
                Text = text,
                SortOrder = context.Entity.Shots.Count
            });
        }

        await service.SaveCutsceneAsync(context);
        return context.Entity;
    }

    private static Task<Asset> UploadForAsync(TestDatabase test, Guid ownerId)
    {
        using var content = new MemoryStream([1, 2, 3]);

        return test.GetService<AssetService>().UploadAsync(
            test.ProjectId, "skizze.png", "image/png", content, ModuleKeys.Cutscenes, ownerId);
    }

    [Fact]
    public async Task Dauer_und_Kameranotiz_werden_gespeichert()
    {
        using var test = new TestDatabase();
        var cutscene = await CreateAsync(test, "Weite Landschaft");

        var service = test.GetService<CutsceneService>();
        var context = await service.LoadForEditAsync(test.ProjectId, cutscene.Id);

        context!.Entity.Shots[0].DurationSeconds = 4.5;
        context.Entity.Shots[0].CameraNote = "  Totale, langsamer Zoom  ";

        await service.SaveCutsceneAsync(context);

        var reloaded = await service.LoadForEditAsync(test.ProjectId, cutscene.Id);
        var shot = Assert.Single(reloaded!.Entity.Shots);

        Assert.Equal(4.5, shot.DurationSeconds);
        Assert.Equal("Totale, langsamer Zoom", shot.CameraNote);
    }

    [Fact]
    public async Task Das_Skizzenbild_haengt_an_der_GUID_der_Einstellung()
    {
        using var test = new TestDatabase();
        var cutscene = await CreateAsync(test, "Weite Landschaft");

        var service = test.GetService<CutsceneService>();
        var shotId = (await service.LoadForEditAsync(test.ProjectId, cutscene.Id))!.Entity.Shots[0].Id;

        await UploadForAsync(test, shotId);

        // Es braucht keine Spalte — die Einstellung hat eine eigene GUID.
        Assert.NotNull(await test.GetService<AssetService>().GetPrimaryAssetIdAsync(shotId));
    }

    [Fact]
    public async Task Eine_entfernte_Einstellung_nimmt_ihr_Bild_mit()
    {
        using var test = new TestDatabase();
        var cutscene = await CreateAsync(test, "Weite Landschaft", "Nahaufnahme");

        var service = test.GetService<CutsceneService>();
        var context = await service.LoadForEditAsync(test.ProjectId, cutscene.Id);
        var removedId = context!.Entity.Shots[0].Id;

        await UploadForAsync(test, removedId);

        context = await service.LoadForEditAsync(test.ProjectId, cutscene.Id);
        context!.Entity.Shots.RemoveAll(shot => shot.Id == removedId);
        await service.SaveCutsceneAsync(context);

        await using var db = test.CreateContext();
        Assert.Empty(await db.Assets.Where(asset => asset.OwnerEntityId == removedId).ToListAsync());
    }

    [Fact]
    public async Task Beim_Loeschen_der_Cutscene_gehen_alle_Skizzen_mit()
    {
        using var test = new TestDatabase();
        var cutscene = await CreateAsync(test, "Weite Landschaft", "Nahaufnahme");

        var service = test.GetService<CutsceneService>();
        var shots = (await service.LoadForEditAsync(test.ProjectId, cutscene.Id))!.Entity.Shots;

        foreach (var shot in shots)
        {
            await UploadForAsync(test, shot.Id);
        }

        await service.DeleteCutsceneAsync(cutscene.Id);

        await using var db = test.CreateContext();
        Assert.Empty(await db.Assets.ToListAsync());
        Assert.Empty(await db.CutsceneShots.ToListAsync());
    }
}
