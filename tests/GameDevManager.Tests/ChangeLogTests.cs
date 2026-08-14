using GameDevManager.Data.Services;
using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Das Änderungsprotokoll. Geschrieben wird es beim Speichern von selbst
/// (<see cref="ChangeLogInterceptor"/>) und beim Löschen über <see cref="ChangeLog"/> — geprüft
/// wird deshalb über die echten Modul-Dienste und nicht am Protokoll vorbei.
/// </summary>
public class ChangeLogTests
{
    [Fact]
    public async Task Anlegen_Aendern_und_Loeschen_stehen_mit_dem_handelnden_Benutzer_im_Protokoll()
    {
        using var test = new TestDatabase();
        var items = test.GetService<ItemService>();
        var log = test.GetService<ChangeLogService>();

        var author = new ChangeAuthor(Guid.NewGuid(), "Fabian");
        test.Author.Current = author;

        // Anlegen
        var context = await items.LoadForEditAsync(test.ProjectId, null);
        context!.Entity.Name = "Eisenschwert";
        await items.SaveItemAsync(context);
        var itemId = context.Entity.Id;

        // Ändern
        var reloaded = await items.LoadForEditAsync(test.ProjectId, itemId);
        reloaded!.Entity.Description = "Scharf.";
        await items.SaveItemAsync(reloaded);

        // Löschen
        await items.DeleteItemAsync(itemId);

        var entries = (await log.GetEntriesAsync(test.ProjectId)).Rows;

        Assert.Equal(
            [ChangeAction.Deleted, ChangeAction.Updated, ChangeAction.Created],
            entries.Select(entry => entry.Action).ToArray());

        Assert.All(entries, entry =>
        {
            Assert.Equal(author.UserId, entry.UserId);
            Assert.Equal("Fabian", entry.UserName);
            Assert.Equal(ModuleKeys.Items, entry.ModuleKey);
            Assert.Equal(itemId, entry.EntityId);
            // Der Name steht als Momentaufnahme im Eintrag — nach dem Löschen gäbe es nichts
            // mehr aufzulösen, und genau dieser Eintrag ist der wichtigste.
            Assert.Equal("Eisenschwert", entry.EntityName);
        });
    }

    [Fact]
    public async Task Der_Aenderungseintrag_nennt_die_geaenderten_Eigenschaften()
    {
        using var test = new TestDatabase();
        var items = test.GetService<ItemService>();

        var context = await items.LoadForEditAsync(test.ProjectId, null);
        context!.Entity.Name = "Fackel";
        await items.SaveItemAsync(context);

        var reloaded = await items.LoadForEditAsync(test.ProjectId, context.Entity.Id);
        reloaded!.Entity.Name = "Grubenlampe";
        await items.SaveItemAsync(reloaded);

        var entries = (await test.GetService<ChangeLogService>().GetEntriesAsync(test.ProjectId)).Rows;
        var update = entries.First(entry => entry.Action == ChangeAction.Updated);

        Assert.Contains("Name", update.Details);
        // Der Zeitstempel ändert sich bei jedem Speichern und stünde sonst in jedem Eintrag.
        Assert.DoesNotContain(nameof(ContentEntity.UpdatedAtUtc), update.Details);
    }

    [Fact]
    public async Task Zwei_Benutzer_hinterlassen_getrennte_Spuren()
    {
        using var test = new TestDatabase();
        var items = test.GetService<ItemService>();
        var log = test.GetService<ChangeLogService>();

        var anna = new ChangeAuthor(Guid.NewGuid(), "Anna");
        var bruno = new ChangeAuthor(Guid.NewGuid(), "Bruno");

        test.Author.Current = anna;
        var context = await items.LoadForEditAsync(test.ProjectId, null);
        context!.Entity.Name = "Schild";
        await items.SaveItemAsync(context);

        test.Author.Current = bruno;
        var reloaded = await items.LoadForEditAsync(test.ProjectId, context.Entity.Id);
        reloaded!.Entity.Description = "Aus Eichenholz.";
        await items.SaveItemAsync(reloaded);

        var authors = await log.GetAuthorsAsync(test.ProjectId);
        Assert.Equal(2, authors.Count);

        var byAnna = await log.GetEntriesAsync(test.ProjectId, new ChangeLogFilter(UserId: anna.UserId));
        Assert.Equal(ChangeAction.Created, Assert.Single(byAnna.Rows).Action);

        var byBruno = await log.GetEntriesAsync(test.ProjectId, new ChangeLogFilter(UserId: bruno.UserId));
        Assert.Equal(ChangeAction.Updated, Assert.Single(byBruno.Rows).Action);
    }

    [Fact]
    public async Task Ein_Import_hinterlaesst_einen_Eintrag_und_nicht_tausend()
    {
        using var test = new TestDatabase();
        var items = test.GetService<ItemService>();

        foreach (var name in new[] { "Stein", "Holz", "Erz" })
        {
            var context = await items.LoadForEditAsync(test.ProjectId, null);
            context!.Entity.Name = name;
            await items.SaveItemAsync(context);
        }

        using var archive = new MemoryStream();
        await test.GetService<ExportService>()
            .WriteExportAsync(test.ProjectId, ExportTarget.Json, includeAssets: false, archive);

        archive.Position = 0;
        await test.GetService<ImportService>().ImportAsync(test.ProjectId, archive, replaceExisting: true);

        var log = test.GetService<ChangeLogService>();
        var imports = await log.GetEntriesAsync(test.ProjectId, new ChangeLogFilter(Action: ChangeAction.Imported));

        Assert.Single(imports.Rows);
        Assert.Equal(ModuleKeys.Changelog, imports.Rows[0].ModuleKey);

        // Die drei Items wurden vom Import wieder angelegt — als Einzeleinträge stünden sie
        // erneut im Protokoll und machten es unlesbar.
        var created = await log.GetEntriesAsync(test.ProjectId, new ChangeLogFilter(Action: ChangeAction.Created));
        Assert.Equal(3, created.Total);
    }

    [Fact]
    public async Task Das_Protokoll_uebersteht_den_ersetzenden_Import()
    {
        using var test = new TestDatabase();
        var items = test.GetService<ItemService>();

        var context = await items.LoadForEditAsync(test.ProjectId, null);
        context!.Entity.Name = "Amulett";
        await items.SaveItemAsync(context);

        using var archive = new MemoryStream();
        await test.GetService<ExportService>()
            .WriteExportAsync(test.ProjectId, ExportTarget.Json, includeAssets: false, archive);

        archive.Position = 0;
        await test.GetService<ImportService>().ImportAsync(test.ProjectId, archive, replaceExisting: true);

        // Wie die Moduleinstellungen und die Dashboard-Bänder ist das Protokoll
        // Werkzeug-Sache: Der Wipe nimmt es nicht mit.
        var created = await test.GetService<ChangeLogService>()
            .GetEntriesAsync(test.ProjectId, new ChangeLogFilter(Action: ChangeAction.Created));

        Assert.NotEmpty(created.Rows);
    }

    [Fact]
    public async Task Die_Geschichte_einer_Entitaet_laesst_sich_einzeln_abrufen()
    {
        using var test = new TestDatabase();
        var items = test.GetService<ItemService>();

        var first = await items.LoadForEditAsync(test.ProjectId, null);
        first!.Entity.Name = "Erstes";
        await items.SaveItemAsync(first);

        var second = await items.LoadForEditAsync(test.ProjectId, null);
        second!.Entity.Name = "Zweites";
        await items.SaveItemAsync(second);

        var history = await test.GetService<ChangeLogService>()
            .GetForEntityAsync(test.ProjectId, first.Entity.Id);

        Assert.Equal("Erstes", Assert.Single(history.Rows).EntityName);
    }

    [Fact]
    public async Task Auch_eine_Art_landet_im_Protokoll()
    {
        using var test = new TestDatabase();
        var types = test.GetService<ContentTypeService>();

        var type = new ContentType
        {
            GameProjectId = test.ProjectId,
            ModuleKey = ModuleKeys.Items,
            Name = "Waffe"
        };

        await types.SaveTypeAsync(type);
        await types.DeleteTypeAsync(type.Id);

        var entries = await test.GetService<ChangeLogService>()
            .GetEntriesAsync(test.ProjectId, new ChangeLogFilter(EntityId: type.Id));

        Assert.Equal(
            [ChangeAction.Deleted, ChangeAction.Created],
            entries.Rows.Select(entry => entry.Action).ToArray());
    }
}
