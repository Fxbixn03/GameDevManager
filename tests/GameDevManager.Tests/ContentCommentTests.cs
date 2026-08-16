using GameDevManager.Data.Services;
using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Anmerkungen an Entitäten. Werkzeug-Daten wie das Änderungsprotokoll — der Urheber steht als
/// Momentaufnahme im Eintrag, und Erledigtes bleibt stehen statt gelöscht zu werden.
/// </summary>
public class ContentCommentTests
{
    private static async Task<Guid> SeedItemAsync(TestDatabase test, string name = "Schwert")
    {
        await using var db = test.CreateContext();

        var item = new Item { GameProjectId = test.ProjectId, Name = name };
        db.Items.Add(item);
        await db.SaveChangesAsync();

        return item.Id;
    }

    [Fact]
    public async Task Eine_Anmerkung_traegt_ihren_Urheber_als_Namen()
    {
        using var test = new TestDatabase();
        var itemId = await SeedItemAsync(test);

        test.Author.Current = new ChangeAuthor(Guid.NewGuid(), "Alrik");

        var comments = test.GetService<ContentCommentService>();
        await comments.AddAsync(test.ProjectId, itemId, ModuleKeys.Items, "  Schaden zu hoch.  ");

        var comment = Assert.Single(await comments.GetForEntityAsync(itemId));

        Assert.Equal("Schaden zu hoch.", comment.Text);
        Assert.Equal("Alrik", comment.AuthorName);
        Assert.False(comment.IsResolved);
    }

    [Fact]
    public async Task Ein_leerer_Text_wird_abgelehnt()
    {
        using var test = new TestDatabase();
        var itemId = await SeedItemAsync(test);

        await Assert.ThrowsAsync<ContentValidationException>(() =>
            test.GetService<ContentCommentService>()
                .AddAsync(test.ProjectId, itemId, ModuleKeys.Items, "   "));
    }

    [Fact]
    public async Task Erledigtes_bleibt_stehen_und_laesst_sich_zurueckholen()
    {
        using var test = new TestDatabase();
        var itemId = await SeedItemAsync(test);

        var comments = test.GetService<ContentCommentService>();
        await comments.AddAsync(test.ProjectId, itemId, ModuleKeys.Items, "Schaden zu hoch.");

        var comment = Assert.Single(await comments.GetForEntityAsync(itemId));
        await comments.SetResolvedAsync(comment.Id, resolved: true);

        // Der Eintrag bleibt — er ist der Beleg, dass etwas besprochen war.
        Assert.True(Assert.Single(await comments.GetForEntityAsync(itemId)).IsResolved);
        Assert.Equal(0, await comments.CountOpenAsync(itemId));
        Assert.Empty(await comments.GetOpenAsync(test.ProjectId));

        await comments.SetResolvedAsync(comment.Id, resolved: false);
        Assert.Equal(1, await comments.CountOpenAsync(itemId));
    }

    [Fact]
    public async Task Das_Dashboard_zeigt_offene_Anmerkungen_mit_dem_Namen_der_Entitaet()
    {
        using var test = new TestDatabase();
        var itemId = await SeedItemAsync(test, "Eisenschwert");

        var comments = test.GetService<ContentCommentService>();
        await comments.AddAsync(test.ProjectId, itemId, ModuleKeys.Items, "Schaden zu hoch.");

        var open = Assert.Single(await comments.GetOpenAsync(test.ProjectId));

        Assert.Equal("Eisenschwert", open.OwnerName);
        Assert.Equal(ModuleKeys.Items, open.OwnerModuleKey);
    }

    [Fact]
    public async Task Beim_Loeschen_der_Entitaet_gehen_ihre_Anmerkungen_mit()
    {
        using var test = new TestDatabase();
        var itemId = await SeedItemAsync(test);

        await test.GetService<ContentCommentService>()
            .AddAsync(test.ProjectId, itemId, ModuleKeys.Items, "Schaden zu hoch.");

        await test.GetService<ItemService>().DeleteItemAsync(itemId);

        await using var db = test.CreateContext();
        Assert.Empty(await db.ContentComments.ToListAsync());
    }

    [Fact]
    public async Task Anmerkungen_stehen_nicht_im_Export_und_ueberstehen_den_Import()
    {
        using var test = new TestDatabase();
        var itemId = await SeedItemAsync(test);

        await test.GetService<ContentCommentService>()
            .AddAsync(test.ProjectId, itemId, ModuleKeys.Items, "Schaden zu hoch.");

        using var zip = new MemoryStream();
        await test.GetService<ExportService>()
            .WriteExportAsync(test.ProjectId, ExportTarget.Json, includeAssets: false, zip);
        zip.Position = 0;

        await test.GetService<ImportService>().ImportAsync(test.ProjectId, zip, replaceExisting: true);

        // Werkzeug-Daten: Sie standen in keinem Archiv und wurden vom Wipe trotzdem nicht
        // mitgenommen — die Entität behält ihre GUID.
        await using var db = test.CreateContext();
        Assert.Single(await db.ContentComments.ToListAsync());
    }
}
