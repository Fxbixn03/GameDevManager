using System.IO.Compression;
using System.Text.Json;
using GameDevManager.Data.Services;
using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Git-freundlicher Export (F41): eine Datei je Entität statt einer je Modul. Beide Ablagen
/// tragen denselben Inhalt, und der Import liest beide — der Unterschied ist ausschließlich
/// einer für den Diff in Git.
/// </summary>
public class ExportLayoutTests
{
    private static async Task<(Guid First, Guid Second)> SeedItemsAsync(TestDatabase test)
    {
        await using var db = test.CreateContext();

        var first = new Item { GameProjectId = test.ProjectId, Name = "Eisenschwert" };
        var second = new Item { GameProjectId = test.ProjectId, Name = "Trank" };

        db.Items.AddRange(first, second);
        await db.SaveChangesAsync();

        return (first.Id, second.Id);
    }

    private static async Task<MemoryStream> ExportAsync(TestDatabase test, ExportLayout layout)
    {
        var zip = new MemoryStream();

        await test.GetService<ExportService>().WriteExportAsync(
            test.ProjectId, ExportTarget.Json, includeAssets: false, zip, layout: layout);

        zip.Position = 0;
        return zip;
    }

    [Fact]
    public async Task Das_Ordner_Layout_legt_je_Entitaet_eine_Datei_an()
    {
        using var test = new TestDatabase();
        var (first, second) = await SeedItemsAsync(test);

        using var zip = await ExportAsync(test, ExportLayout.PerEntity);
        using var archive = new ZipArchive(zip, ZipArchiveMode.Read);

        // Der Dateiname trägt die GUID und nicht den Namen: Ein Umbenennen soll keine Datei
        // verschieben, sonst zeigte der Diff eine gelöschte und eine neue.
        Assert.NotNull(archive.GetEntry($"content/items/{first:D}.json"));
        Assert.NotNull(archive.GetEntry($"content/items/{second:D}.json"));

        // Die Sammeldatei bleibt daneben stehen — mit leerer Liste, damit der Import jede
        // Datei findet, wo er sie erwartet.
        await using var content = archive.GetEntry("content/items.json")!.Open();
        using var document = await JsonDocument.ParseAsync(content);

        Assert.Equal(0, document.RootElement.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task Das_Standard_Layout_bleibt_eine_Datei_je_Modul()
    {
        using var test = new TestDatabase();
        await SeedItemsAsync(test);

        using var zip = await ExportAsync(test, ExportLayout.SingleFile);
        using var archive = new ZipArchive(zip, ZipArchiveMode.Read);

        Assert.DoesNotContain(archive.Entries, entry => entry.FullName.StartsWith("content/items/"));

        await using var content = archive.GetEntry("content/items.json")!.Open();
        using var document = await JsonDocument.ParseAsync(content);

        Assert.Equal(2, document.RootElement.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task Der_Import_liest_das_Ordner_Layout()
    {
        using var test = new TestDatabase();
        var (first, _) = await SeedItemsAsync(test);

        using var zip = await ExportAsync(test, ExportLayout.PerEntity);

        // Ersetzend ins selbe Projekt: Der Import behält die GUIDs, ein zweites Projekt in
        // derselben Datenbank liefe deshalb in den Schlüsselkonflikt.
        zip.Position = 0;
        var result = await test.GetService<ImportService>()
            .ImportAsync(test.ProjectId, zip, replaceExisting: true);

        Assert.Equal(2, result.Counts[ModuleKeys.Items]);

        await using var check = test.CreateContext();
        var imported = await check.Items.Where(item => item.GameProjectId == test.ProjectId).ToListAsync();

        Assert.Equal(2, imported.Count);
        Assert.Contains(imported, item => item.Id == first);
    }

    [Fact]
    public async Task Beide_Ablagen_tragen_denselben_Inhalt()
    {
        using var test = new TestDatabase();

        await using (var db = test.CreateContext())
        {
            var npc = new Npc { GameProjectId = test.ProjectId, Name = "Alrik", IsTrader = true };
            db.Npcs.Add(npc);

            // Die Beziehungsarten stehen neben der NPC-Liste in derselben Datei — im
            // Ordner-Layout bleiben sie in der Sammeldatei und dürfen nicht verloren gehen.
            db.NpcRelationTypes.Add(new NpcRelationType
            {
                GameProjectId = test.ProjectId, Name = "Vater von", InverseName = "Kind von"
            });

            await db.SaveChangesAsync();
        }

        using var folder = await ExportAsync(test, ExportLayout.PerEntity);
        using var archive = new ZipArchive(folder, ZipArchiveMode.Read);

        await using var content = archive.GetEntry("content/npcs.json")!.Open();
        using var document = await JsonDocument.ParseAsync(content);

        Assert.Equal(0, document.RootElement.GetProperty("npcs").GetArrayLength());
        Assert.Equal(1, document.RootElement.GetProperty("relationTypes").GetArrayLength());
    }
}
