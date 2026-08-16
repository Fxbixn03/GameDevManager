using GameDevManager.Data.Assets;
using GameDevManager.Data.Services;
using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Verwaiste Dateien im Speicher: die Gegenrichtung zum Health Check „verwaiste Sprites“, der
/// Zeilen ohne Besitzer meldet. Hier geht es um Dateien ohne Zeile — angezeigt statt gelöscht.
/// </summary>
public class OrphanedFileTests
{
    private static async Task<Asset> UploadAsync(TestDatabase test, string fileName)
    {
        using var content = new MemoryStream([1, 2, 3]);

        return await test.GetService<AssetService>().UploadAsync(
            test.ProjectId, fileName, "image/png", content);
    }

    private static TestDatabase.InMemoryAssetStorage Storage(TestDatabase test) =>
        (TestDatabase.InMemoryAssetStorage)test.GetService<IAssetStorage>();

    [Fact]
    public async Task Eine_Datei_mit_Zeile_ist_kein_Waise()
    {
        using var test = new TestDatabase();
        await UploadAsync(test, "schwert.png");

        Assert.Empty(await test.GetService<AssetService>().FindOrphanedFilesAsync());
    }

    [Fact]
    public async Task Eine_Datei_ohne_Zeile_wird_gefunden()
    {
        using var test = new TestDatabase();
        await UploadAsync(test, "schwert.png");

        // Genau der Fall: ein abgebrochener Import hat die Datei geschrieben, die Zeile nicht.
        Storage(test).AddStrayFile("11111111111111111111111111111111/22222222222222222222222222222222.png");

        var orphans = await test.GetService<AssetService>().FindOrphanedFilesAsync();

        Assert.Equal(
            "11111111111111111111111111111111/22222222222222222222222222222222.png",
            Assert.Single(orphans));
    }

    [Fact]
    public async Task Geloescht_wird_nur_das_wirklich_Verwaiste()
    {
        using var test = new TestDatabase();
        var asset = await UploadAsync(test, "schwert.png");

        Storage(test).AddStrayFile("stray/file.png");

        var assets = test.GetService<AssetService>();

        // Die Datei eines vorhandenen Assets steht in der Liste, ist aber kein Waise —
        // vor dem Löschen wird deshalb noch einmal geprüft.
        var deleted = await assets.DeleteOrphanedFilesAsync(["stray/file.png", asset.StorageKey]);

        Assert.Equal(1, deleted);
        Assert.Empty(await assets.FindOrphanedFilesAsync());

        await using var db = test.CreateContext();
        Assert.Single(await db.Assets.ToListAsync());
        Assert.Contains(asset.StorageKey, Storage(test).ListKeys());
    }

    [Fact]
    public async Task Ohne_Schreibrecht_wird_nichts_geloescht()
    {
        using var test = new TestDatabase();
        Storage(test).AddStrayFile("stray/file.png");

        test.Permissions.Current = UserPermissions.Full with { CanWrite = false };

        await Assert.ThrowsAsync<ContentValidationException>(() =>
            test.GetService<AssetService>().DeleteOrphanedFilesAsync(["stray/file.png"]));
    }
}
