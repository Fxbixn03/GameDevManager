using GameDevManager.Data.Services;
using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Massen-Upload mit Namenszuordnung: „eisenschwert.png“ findet das Item „Eisenschwert“.
/// Zugeordnet wird nie stillschweigend — der Dienst schlägt vor, die Oberfläche bestätigt.
/// </summary>
public class AssetOwnerMatchTests
{
    private static async Task<Asset> UploadAsync(TestDatabase test, string fileName)
    {
        using var content = new MemoryStream([1, 2, 3]);

        return await test.GetService<AssetService>().UploadAsync(
            test.ProjectId, fileName, "image/png", content);
    }

    private static async Task<Guid> SeedItemAsync(TestDatabase test, string name)
    {
        await using var db = test.CreateContext();

        var item = new Item { GameProjectId = test.ProjectId, Name = name };
        db.Items.Add(item);
        await db.SaveChangesAsync();

        return item.Id;
    }

    [Fact]
    public async Task Der_Dateiname_findet_die_gleichnamige_Entitaet()
    {
        using var test = new TestDatabase();
        var itemId = await SeedItemAsync(test, "Eisenschwert");
        await UploadAsync(test, "eisenschwert.png");

        var suggestion = Assert.Single(await test.GetService<AssetService>().SuggestOwnersAsync(test.ProjectId));

        Assert.Equal(itemId, Assert.Single(suggestion.Candidates).Id);
    }

    [Fact]
    public async Task Trennzeichen_im_Dateinamen_stoeren_nicht()
    {
        using var test = new TestDatabase();
        await SeedItemAsync(test, "Eisen Schwert");
        await UploadAsync(test, "eisen-schwert.png");

        // Dateinamen tragen Trennzeichen, wo ein Anzeigename ein Leerzeichen hat.
        Assert.Single(Assert.Single(
            await test.GetService<AssetService>().SuggestOwnersAsync(test.ProjectId)).Candidates);
    }

    [Fact]
    public async Task Zwei_gleichnamige_Entitaeten_werden_beide_angeboten()
    {
        using var test = new TestDatabase();
        await SeedItemAsync(test, "Feuer");

        await using (var db = test.CreateContext())
        {
            db.GameEffects.Add(new GameEffect { GameProjectId = test.ProjectId, Name = "Feuer" });
            await db.SaveChangesAsync();
        }

        await UploadAsync(test, "feuer.png");

        var suggestion = Assert.Single(await test.GetService<AssetService>().SuggestOwnersAsync(test.ProjectId));

        // Die Wahl wäre geraten — deshalb stehen beide da.
        Assert.Equal(2, suggestion.Candidates.Count);
    }

    [Fact]
    public async Task Ohne_Treffer_bleibt_die_Datei_ohne_Vorschlag()
    {
        using var test = new TestDatabase();
        await SeedItemAsync(test, "Eisenschwert");
        await UploadAsync(test, "hintergrund.png");

        Assert.Empty(Assert.Single(
            await test.GetService<AssetService>().SuggestOwnersAsync(test.ProjectId)).Candidates);
    }

    [Fact]
    public async Task Zugeordnet_wird_in_einem_Rutsch_und_das_erste_wird_Icon()
    {
        using var test = new TestDatabase();
        var itemId = await SeedItemAsync(test, "Eisenschwert");

        var first = await UploadAsync(test, "eisenschwert.png");
        var second = await UploadAsync(test, "eisenschwert.jpg");

        var assets = test.GetService<AssetService>();

        var assigned = await assets.AssignOwnersAsync(new Dictionary<Guid, (string, Guid)>
        {
            [first.Id] = (ModuleKeys.Items, itemId),
            [second.Id] = (ModuleKeys.Items, itemId)
        });

        Assert.Equal(2, assigned);

        await using var db = test.CreateContext();
        var stored = await db.Assets.OrderBy(a => a.SortOrder).ToListAsync();

        Assert.All(stored, asset => Assert.Equal(itemId, asset.OwnerEntityId));

        // Je Entität genau ein Icon — das erste zugeordnete.
        Assert.Single(stored, asset => asset.IsPrimary);
        Assert.True(stored.Single(asset => asset.Id == first.Id).IsPrimary);

        // Und danach gibt es nichts mehr vorzuschlagen.
        Assert.Empty(await assets.SuggestOwnersAsync(test.ProjectId));
    }
}
