using GameDevManager.Data.Services;
using GameDevManager.Domain;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Der Live-Sync: Der Interceptor veröffentlicht Änderungsereignisse an verbundene
/// Editoren — gebündelt gelesen über den SSE-Endpunkt, hier geprüft am Broadcaster.
/// </summary>
public class SyncEventTests
{
    private static async Task<Guid> SaveItemAsync(TestDatabase test, string name, Guid? id = null)
    {
        var items = test.GetService<ItemService>();
        var context = await items.LoadForEditAsync(test.ProjectId, id);

        context!.Entity.Name = name;
        await items.SaveItemAsync(context);

        return context.Entity.Id;
    }

    [Fact]
    public async Task Speichern_veroeffentlicht_ein_Ereignis_an_verbundene_Abnehmer()
    {
        using var test = new TestDatabase();
        var broadcaster = test.GetService<SyncEventBroadcaster>();

        var (id, reader) = broadcaster.Subscribe();
        try
        {
            var itemId = await SaveItemAsync(test, "Eisenschwert");

            var created = await reader.ReadAsync();
            Assert.Equal(ModuleKeys.Items, created.ModuleKey);
            Assert.Equal(itemId, created.EntityId);
            Assert.Equal("Created", created.Action);

            // Ein zweites Speichern ist ein Update — der Editor lädt das Modul neu.
            await SaveItemAsync(test, "Eisenschwert +1", itemId);

            var updated = await reader.ReadAsync();
            Assert.Equal("Updated", updated.Action);
            Assert.Equal("Eisenschwert +1", updated.EntityName);
        }
        finally
        {
            broadcaster.Unsubscribe(id);
        }
    }

    [Fact]
    public async Task Auch_Loeschungen_erreichen_den_Abnehmer()
    {
        using var test = new TestDatabase();
        var itemId = await SaveItemAsync(test, "Fackel");

        var broadcaster = test.GetService<SyncEventBroadcaster>();
        var (id, reader) = broadcaster.Subscribe();

        try
        {
            // Löschungen laufen über ExecuteDelete am Änderungsverfolger vorbei — der
            // Sync bekommt sie über den Protokolleintrag des Dienstes trotzdem.
            await test.GetService<ItemService>().DeleteItemAsync(itemId);

            var deleted = await reader.ReadAsync();
            Assert.Equal("Deleted", deleted.Action);
            Assert.Equal(itemId, deleted.EntityId);
        }
        finally
        {
            broadcaster.Unsubscribe(id);
        }
    }

    [Fact]
    public async Task Ohne_Abnehmer_wird_nichts_veroeffentlicht_und_nichts_gehalten()
    {
        using var test = new TestDatabase();
        var broadcaster = test.GetService<SyncEventBroadcaster>();

        Assert.False(broadcaster.HasSubscribers);

        // Speichern ohne Abnehmer: kein Ereignis, kein wachsender Speicher — und wer sich
        // danach verbindet, sieht Vergangenes zu Recht nicht (dafür ist der Voll-Abgleich da).
        await SaveItemAsync(test, "Fackel");

        var (id, reader) = broadcaster.Subscribe();
        try
        {
            Assert.True(broadcaster.HasSubscribers);
            Assert.False(reader.TryRead(out _));
        }
        finally
        {
            broadcaster.Unsubscribe(id);
        }

        Assert.False(broadcaster.HasSubscribers);
    }

    [Fact]
    public async Task Der_Sammeleintrag_eines_Imports_kommt_als_changelog_Ereignis()
    {
        using var test = new TestDatabase();
        await SaveItemAsync(test, "Fackel");

        using var zip = new MemoryStream();
        await test.GetService<ExportService>()
            .WriteExportAsync(test.ProjectId, ExportTarget.Json, includeAssets: false, zip);
        zip.Position = 0;

        var broadcaster = test.GetService<SyncEventBroadcaster>();
        var (id, reader) = broadcaster.Subscribe();

        try
        {
            await test.GetService<ImportService>().ImportAsync(test.ProjectId, zip, replaceExisting: true);

            // Mindestens ein Ereignis trägt das Modul „changelog“ — für den Editor die
            // Ansage zum Voll-Abgleich, siehe knowledge/live-sync.md.
            var sawChangelog = false;

            while (reader.TryRead(out var syncEvent))
            {
                sawChangelog |= syncEvent.ModuleKey == ModuleKeys.Changelog;
            }

            Assert.True(sawChangelog);
        }
        finally
        {
            broadcaster.Unsubscribe(id);
        }
    }
}
