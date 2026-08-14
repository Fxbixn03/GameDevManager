using System.IO.Compression;
using System.Text.Json;
using GameDevManager.Data.Services;
using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Der Bearbeitungsstand je Entität. Eine Spalte an <see cref="ContentEntity"/> und damit in
/// allen Inhaltsmodulen auf einmal — geprüft wird, dass sie gespeichert wird, dass die
/// Massenbearbeitung sie setzt und dass der Export sich darauf einschränken lässt.
/// </summary>
public class ContentStatusTests
{
    private static async Task<Guid> SaveItemAsync(TestDatabase test, string name, ContentStatus status)
    {
        var items = test.GetService<ItemService>();
        var context = await items.LoadForEditAsync(test.ProjectId, null);

        context!.Entity.Name = name;
        context.Entity.Status = status;

        await items.SaveItemAsync(context);
        return context.Entity.Id;
    }

    [Fact]
    public async Task Neue_Inhalte_sind_Entwurf()
    {
        using var test = new TestDatabase();

        var items = test.GetService<ItemService>();
        var context = await items.LoadForEditAsync(test.ProjectId, null);
        context!.Entity.Name = "Schwert";
        await items.SaveItemAsync(context);

        await using var db = test.CreateContext();

        // Der Entwurf ist die Null und damit der Stand alles Bestehenden.
        Assert.Equal(ContentStatus.Draft, (await db.Items.SingleAsync()).Status);
    }

    [Fact]
    public async Task Der_Stand_wird_gespeichert_und_wieder_geladen()
    {
        using var test = new TestDatabase();
        var itemId = await SaveItemAsync(test, "Schwert", ContentStatus.InReview);

        var reloaded = await test.GetService<ItemService>().LoadForEditAsync(test.ProjectId, itemId);
        Assert.Equal(ContentStatus.InReview, reloaded!.Entity.Status);

        // Und er lässt sich ändern, ohne dass etwas anderes daran hängt.
        reloaded.Entity.Status = ContentStatus.Done;
        await test.GetService<ItemService>().SaveItemAsync(reloaded);

        await using var db = test.CreateContext();
        Assert.Equal(ContentStatus.Done, (await db.Items.SingleAsync()).Status);
    }

    [Fact]
    public async Task Die_Massenbearbeitung_setzt_den_Stand_vieler_Eintraege()
    {
        using var test = new TestDatabase();

        var first = await SaveItemAsync(test, "Schwert", ContentStatus.Draft);
        var second = await SaveItemAsync(test, "Axt", ContentStatus.Done);

        var result = await test.GetService<BulkEditService>()
            .SetStatusAsync(test.ProjectId, ModuleKeys.Items, [first, second], ContentStatus.Done);

        // Was schon so dasteht, zählt als übersprungen statt als Änderung.
        Assert.Equal(1, result.Changed);
        Assert.Equal(1, result.Skipped);

        await using var db = test.CreateContext();
        Assert.All(await db.Items.ToListAsync(), item => Assert.Equal(ContentStatus.Done, item.Status));
    }

    [Fact]
    public async Task Der_Stand_zaehlt_im_Dashboard_ueber_alle_Module()
    {
        using var test = new TestDatabase();

        await SaveItemAsync(test, "Schwert", ContentStatus.Done);
        await SaveItemAsync(test, "Axt", ContentStatus.Done);
        await SaveItemAsync(test, "Stock", ContentStatus.InProgress);

        var counts = await test.GetService<DashboardOverviewService>().GetStatusCountsAsync(test.ProjectId);

        Assert.Equal(2, counts[ContentStatus.Done]);
        Assert.Equal(1, counts[ContentStatus.InProgress]);
        Assert.False(counts.ContainsKey(ContentStatus.InReview));
    }

    [Fact]
    public async Task Der_Export_laesst_sich_auf_einen_Mindeststand_einschraenken()
    {
        using var test = new TestDatabase();

        await SaveItemAsync(test, "Fertig", ContentStatus.Done);
        await SaveItemAsync(test, "Im Review", ContentStatus.InReview);
        await SaveItemAsync(test, "Entwurf", ContentStatus.Draft);

        var names = await ExportedItemNamesAsync(test, ContentStatus.InReview);

        // Ein Mindeststand und kein einzelner: „im Review“ nimmt das Abgenommene mit.
        Assert.Equal(new[] { "Fertig", "Im Review" }, names.Order());

        // Ohne Angabe geht alles hinaus.
        Assert.Equal(3, (await ExportedItemNamesAsync(test, null)).Count);
    }

    private static async Task<List<string>> ExportedItemNamesAsync(TestDatabase test, ContentStatus? minimum)
    {
        using var zip = new MemoryStream();
        await test.GetService<ExportService>()
            .WriteExportAsync(test.ProjectId, ExportTarget.Json, includeAssets: false, zip, minimum);

        zip.Position = 0;
        using var archive = new ZipArchive(zip, ZipArchiveMode.Read);

        await using var content = archive.GetEntry("content/items.json")!.Open();
        using var document = await JsonDocument.ParseAsync(content);

        return
        [
            .. document.RootElement.GetProperty("items")
                .EnumerateArray()
                .Select(item => item.GetProperty("name").GetString()!)
        ];
    }

    [Fact]
    public async Task Der_Stand_uebersteht_Export_und_Import()
    {
        using var test = new TestDatabase();
        await SaveItemAsync(test, "Schwert", ContentStatus.InProgress);

        using var zip = new MemoryStream();
        await test.GetService<ExportService>()
            .WriteExportAsync(test.ProjectId, ExportTarget.Json, includeAssets: false, zip);
        zip.Position = 0;

        await test.GetService<ImportService>().ImportAsync(test.ProjectId, zip, replaceExisting: true);

        await using var db = test.CreateContext();
        Assert.Equal(ContentStatus.InProgress, (await db.Items.SingleAsync()).Status);
    }
}
