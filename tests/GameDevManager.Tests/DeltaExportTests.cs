using System.IO.Compression;
using System.Text.Json.Nodes;
using GameDevManager.Data.Services;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Der Delta-Export: nur Geändertes plus Löschliste, unmissverständlich als Delta markiert —
/// und vom Import abgewiesen, damit niemand ein Delta als Projektstand einspielt.
/// </summary>
public class DeltaExportTests
{
    private static async Task<Guid> AddItemAsync(TestDatabase test, string name)
    {
        var items = test.GetService<ItemService>();
        var context = await items.LoadForEditAsync(test.ProjectId, null);

        context!.Entity.Name = name;
        await items.SaveItemAsync(context);

        return context.Entity.Id;
    }

    [Fact]
    public async Task Das_Delta_traegt_nur_Geaendertes_die_Loeschliste_und_den_Marker()
    {
        using var test = new TestDatabase();

        var bleibt = await AddItemAsync(test, "Fackel");
        var wird_geaendert = await AddItemAsync(test, "Schwert");
        var wird_geloescht = await AddItemAsync(test, "Schild");

        var snapshots = test.GetService<ExportSnapshotService>();
        var basis = await snapshots.CreateAsync(test.ProjectId, includeAssets: false);

        // Danach: eines geändert, eines gelöscht, eines neu — die Fackel bleibt unberührt.
        var items = test.GetService<ItemService>();
        var edit = await items.LoadForEditAsync(test.ProjectId, wird_geaendert);
        edit!.Entity.Name = "Eisenschwert";
        await items.SaveItemAsync(edit);

        await items.DeleteItemAsync(wird_geloescht);
        var neu = await AddItemAsync(test, "Axt");

        using var delta = new MemoryStream();
        await snapshots.WriteDeltaAsync(test.ProjectId, basis.FileName, delta);
        delta.Position = 0;

        using var archive = new ZipArchive(delta, ZipArchiveMode.Read);

        // Die Inhaltsdatei trägt nur Geändertes und Neues — das Unberührte fehlt.
        using (var stream = archive.GetEntry("content/items.json")!.Open())
        {
            var itemsJson = (JsonObject)JsonNode.Parse(stream)!;
            var ids = ((JsonArray)itemsJson["items"]!)
                .Select(item => item!["id"]!.GetValue<string>())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            Assert.Equal(2, ids.Count);
            Assert.Contains(wird_geaendert.ToString(), ids);
            Assert.Contains(neu.ToString(), ids);
            Assert.DoesNotContain(bleibt.ToString(), ids);
        }

        // Die Löschliste kennt das Verschwundene, mit Modul und GUID.
        using (var stream = archive.GetEntry("content/deleted.json")!.Open())
        {
            var deleted = (JsonObject)JsonNode.Parse(stream)!;
            var entry = ((JsonArray)deleted["deleted"]!)
                .Single(node => node!["file"]!.GetValue<string>() == "items.json");

            Assert.Equal("items", entry!["module"]!.GetValue<string>());
            Assert.Equal(
                wird_geloescht.ToString(),
                ((JsonArray)entry["ids"]!).Single()!.GetValue<string>(),
                ignoreCase: true);
        }

        // Das Manifest markiert das Archiv eindeutig als Delta, samt Basis-Stand.
        using (var stream = archive.GetEntry("project.json")!.Open())
        {
            var manifest = (JsonObject)JsonNode.Parse(stream)!;

            Assert.Equal(basis.FileName, manifest["delta"]!["baseFileName"]!.GetValue<string>());
        }
    }

    [Fact]
    public async Task Der_Import_weist_ein_Delta_Archiv_ab()
    {
        using var test = new TestDatabase();
        await AddItemAsync(test, "Fackel");

        var snapshots = test.GetService<ExportSnapshotService>();
        var basis = await snapshots.CreateAsync(test.ProjectId, includeAssets: false);

        await AddItemAsync(test, "Axt");

        using var delta = new MemoryStream();
        await snapshots.WriteDeltaAsync(test.ProjectId, basis.FileName, delta);
        delta.Position = 0;

        // Kein versehentlicher Voll-Import: Das Delta löschte still den halben Bestand.
        await Assert.ThrowsAsync<ContentValidationException>(() => test
            .GetService<ImportService>()
            .ImportAsync(test.ProjectId, delta, replaceExisting: true));

        await using var db = test.CreateContext();
        Assert.Equal(2, await db.Items.CountAsync());
    }

    [Fact]
    public async Task Ohne_Aenderung_ist_das_Delta_leer_aber_vollstaendig_geformt()
    {
        using var test = new TestDatabase();
        await AddItemAsync(test, "Fackel");

        var snapshots = test.GetService<ExportSnapshotService>();
        var basis = await snapshots.CreateAsync(test.ProjectId, includeAssets: false);

        using var delta = new MemoryStream();
        await snapshots.WriteDeltaAsync(test.ProjectId, basis.FileName, delta);
        delta.Position = 0;

        using var archive = new ZipArchive(delta, ZipArchiveMode.Read);

        // Leere Listen statt fehlender Dateien — und eine leere Löschliste, damit „nichts
        // gelöscht“ nicht nach einer vergessenen Datei aussieht.
        using var stream = archive.GetEntry("content/items.json")!.Open();
        Assert.Empty((JsonArray)((JsonObject)JsonNode.Parse(stream)!)["items"]!);

        using var deletedStream = archive.GetEntry("content/deleted.json")!.Open();
        Assert.Empty((JsonArray)((JsonObject)JsonNode.Parse(deletedStream)!)["deleted"]!);
    }
}
