using GameDevManager.Data.Services;
using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Teil-Import aus einem fremden Export (F42): einzelne Entitäten übernehmen, statt den ganzen
/// Bestand zu ersetzen. Beide Bausteine lagen vor — der Diff über die GUID und der GUID-Tausch
/// für die Kopie; zusammen ergeben sie den Auswahl-Import.
/// </summary>
public class PartialImportTests
{
    /// <summary>
    /// Ein Archiv aus dem Bestand des Projekts bauen und den Bestand danach so verändern, dass
    /// sich Neues, Geändertes und Identisches gegenüberstehen.
    /// </summary>
    private static async Task<MemoryStream> ExportAsync(TestDatabase test)
    {
        var zip = new MemoryStream();

        await test.GetService<ExportService>().WriteExportAsync(
            test.ProjectId, ExportTarget.Json, includeAssets: false, zip);

        zip.Position = 0;
        return zip;
    }

    private static async Task<Item> SaveItemAsync(TestDatabase test, string name, string? description = null)
    {
        var items = test.GetService<ItemService>();
        var context = await items.LoadForEditAsync(test.ProjectId, null);

        context!.Entity.Name = name;
        context.Entity.Description = description;

        await items.SaveItemAsync(context);
        return context.Entity;
    }

    [Fact]
    public async Task Die_Vorschau_trennt_neu_geaendert_und_identisch()
    {
        using var test = new TestDatabase();

        var unchanged = await SaveItemAsync(test, "Fackel");
        var changed = await SaveItemAsync(test, "Schwert", "Alt");
        var removedHere = await SaveItemAsync(test, "Trank");

        using var archive = await ExportAsync(test);

        // Nach dem Export: eines ändern, eines löschen.
        var items = test.GetService<ItemService>();
        var context = await items.LoadForEditAsync(test.ProjectId, changed.Id);
        context!.Entity.Description = "Neu";
        await items.SaveItemAsync(context);

        await items.DeleteItemAsync(removedHere.Id);

        archive.Position = 0;
        var preview = await test.GetService<PartialImportService>().PreviewAsync(test.ProjectId, archive);

        var byId = preview.Candidates.ToDictionary(candidate => candidate.Id);

        Assert.True(byId[unchanged.Id].IsIdentical);
        Assert.True(byId[changed.Id].ExistsHere);
        Assert.False(byId[changed.Id].IsIdentical);
        Assert.Contains("description", byId[changed.Id].ChangedProperties);
        Assert.False(byId[removedHere.Id].ExistsHere);

        Assert.Equal(1, preview.NewCount);
        Assert.Equal(1, preview.ChangedCount);
    }

    [Fact]
    public async Task Uebernehmen_ersetzt_den_eigenen_Stand()
    {
        using var test = new TestDatabase();
        var item = await SaveItemAsync(test, "Schwert", "Aus dem Archiv");

        using var archive = await ExportAsync(test);

        var items = test.GetService<ItemService>();
        var context = await items.LoadForEditAsync(test.ProjectId, item.Id);
        context!.Entity.Description = "Hier geändert";
        await items.SaveItemAsync(context);

        archive.Position = 0;
        var result = await test.GetService<PartialImportService>().ImportAsync(
            test.ProjectId, archive, new Dictionary<Guid, PartialImportChoice>
            {
                [item.Id] = PartialImportChoice.Take
            });

        Assert.Equal(1, result.Taken);

        await using var db = test.CreateContext();
        Assert.Equal("Aus dem Archiv", (await db.Items.SingleAsync()).Description);
    }

    [Fact]
    public async Task Als_Kopie_laesst_den_eigenen_Stand_stehen()
    {
        using var test = new TestDatabase();
        var item = await SaveItemAsync(test, "Schwert", "Aus dem Archiv");

        using var archive = await ExportAsync(test);

        var items = test.GetService<ItemService>();
        var context = await items.LoadForEditAsync(test.ProjectId, item.Id);
        context!.Entity.Description = "Hier geändert";
        await items.SaveItemAsync(context);

        archive.Position = 0;
        var result = await test.GetService<PartialImportService>().ImportAsync(
            test.ProjectId, archive, new Dictionary<Guid, PartialImportChoice>
            {
                [item.Id] = PartialImportChoice.Copy
            });

        Assert.Equal(1, result.Copied);

        await using var db = test.CreateContext();
        var stored = await db.Items.OrderBy(entity => entity.Description).ToListAsync();

        // Zwei Datensätze: der eigene bleibt, der fremde kommt mit neuer GUID daneben.
        Assert.Equal(2, stored.Count);
        Assert.Contains(stored, entity => entity.Id == item.Id && entity.Description == "Hier geändert");
        Assert.Contains(stored, entity => entity.Id != item.Id && entity.Description == "Aus dem Archiv");
    }

    [Fact]
    public async Task Was_nicht_gewaehlt_ist_bleibt_unangetastet()
    {
        using var test = new TestDatabase();
        var first = await SaveItemAsync(test, "Schwert", "Original");
        var second = await SaveItemAsync(test, "Trank", "Original");

        using var archive = await ExportAsync(test);

        var items = test.GetService<ItemService>();

        foreach (var id in new[] { first.Id, second.Id })
        {
            var context = await items.LoadForEditAsync(test.ProjectId, id);
            context!.Entity.Description = "Hier geändert";
            await items.SaveItemAsync(context);
        }

        archive.Position = 0;
        await test.GetService<PartialImportService>().ImportAsync(
            test.ProjectId, archive, new Dictionary<Guid, PartialImportChoice>
            {
                [first.Id] = PartialImportChoice.Take
            });

        await using var db = test.CreateContext();

        // Ein Ausschnitt darf nichts anfassen, was nicht gewählt ist — dieselbe Regel wie beim
        // Modul-CSV.
        Assert.Equal("Original", (await db.Items.SingleAsync(e => e.Id == first.Id)).Description);
        Assert.Equal("Hier geändert", (await db.Items.SingleAsync(e => e.Id == second.Id)).Description);
    }

    [Fact]
    public async Task Feldwerte_kommen_mit()
    {
        using var test = new TestDatabase();
        var types = test.GetService<ContentTypeService>();

        var type = new ContentType
        {
            GameProjectId = test.ProjectId, ModuleKey = ModuleKeys.Items, Name = "Waffe"
        };
        await types.SaveTypeAsync(type);

        var field = new FieldDefinition
        {
            ModuleKey = ModuleKeys.Items,
            ContentTypeId = type.Id,
            Name = "Schaden",
            Type = ContentFieldType.Integer
        };
        await types.SaveFieldAsync(field);

        var items = test.GetService<ItemService>();
        var context = await items.LoadForEditAsync(test.ProjectId, null);
        context!.Entity.Name = "Schwert";
        context.Entity.ContentTypeId = type.Id;
        context.ValueFor(field).NumberValue = 42;
        await items.SaveItemAsync(context);

        var itemId = context.Entity.Id;

        using var archive = await ExportAsync(test);

        // Wert hier verändern, dann den fremden Stand übernehmen.
        var again = await items.LoadForEditAsync(test.ProjectId, itemId);
        again!.ValueFor(field).NumberValue = 1;
        await items.SaveItemAsync(again);

        archive.Position = 0;
        await test.GetService<PartialImportService>().ImportAsync(
            test.ProjectId, archive, new Dictionary<Guid, PartialImportChoice>
            {
                [itemId] = PartialImportChoice.Take
            });

        var restored = await items.LoadForEditAsync(test.ProjectId, itemId);
        Assert.Equal(42, restored!.Values[field.Id].NumberValue);
    }

    [Fact]
    public async Task Ein_Archiv_ohne_Manifest_wird_abgelehnt()
    {
        using var test = new TestDatabase();

        using var zip = new MemoryStream();

        using (var archive = new System.IO.Compression.ZipArchive(
            zip, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            archive.CreateEntry("irgendwas.txt");
        }

        zip.Position = 0;

        await Assert.ThrowsAsync<ContentValidationException>(
            () => test.GetService<PartialImportService>().PreviewAsync(test.ProjectId, zip));
    }
}
