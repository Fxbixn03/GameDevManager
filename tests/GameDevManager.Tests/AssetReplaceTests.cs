using GameDevManager.Data.Assets;
using GameDevManager.Data.Services;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Asset ersetzen: Die Datei wird getauscht, die Zeile bleibt — und damit die GUID, an der
/// alle Verweise hängen. Die vorherige Fassung wandert in die Historie.
/// </summary>
public class AssetReplaceTests
{
    private static Task<Asset> UploadAsync(TestDatabase test, string fileName = "schwert.png")
    {
        var content = new MemoryStream([1, 2, 3]);

        return test.GetService<AssetService>().UploadAsync(
            test.ProjectId, fileName, "image/png", content);
    }

    private static Task<Asset> ReplaceAsync(TestDatabase test, Guid assetId, string fileName)
    {
        var content = new MemoryStream([4, 5, 6, 7]);

        return test.GetService<AssetService>().ReplaceAsync(assetId, fileName, "image/png", content);
    }

    [Fact]
    public async Task Die_GUID_bleibt_und_die_Datei_wechselt()
    {
        using var test = new TestDatabase();
        var asset = await UploadAsync(test);
        var oldKey = asset.StorageKey;

        var replaced = await ReplaceAsync(test, asset.Id, "schwert-hd.png");

        Assert.Equal(asset.Id, replaced.Id);
        Assert.Equal("schwert-hd.png", replaced.FileName);

        // Ein neuer Schlüssel: Der Auslieferungs-Endpunkt cached unbefristet, unter demselben
        // bekäme ein Browser tagelang die alte Datei.
        Assert.NotEqual(oldKey, replaced.StorageKey);
    }

    [Fact]
    public async Task Die_vorherige_Fassung_bleibt_erhalten()
    {
        using var test = new TestDatabase();
        var asset = await UploadAsync(test, "schwert.png");

        await ReplaceAsync(test, asset.Id, "schwert-hd.png");

        await using var db = test.CreateContext();
        var version = await db.AssetVersions.SingleAsync();

        Assert.Equal(asset.Id, version.AssetId);
        Assert.Equal("schwert.png", version.FileName);
    }

    [Fact]
    public async Task Die_Historie_wird_auf_die_eingestellte_Zahl_gekuerzt()
    {
        using var test = new TestDatabase();
        var options = test.GetService<AssetStorageOptions>();
        options.MaxVersionsPerAsset = 2;

        var asset = await UploadAsync(test, "v0.png");

        for (var round = 1; round <= 4; round++)
        {
            await ReplaceAsync(test, asset.Id, $"v{round}.png");
        }

        await using var db = test.CreateContext();
        var versions = await db.AssetVersions.OrderByDescending(v => v.ReplacedAtUtc).ToListAsync();

        // Was von allein wächst, muss von allein wieder abnehmen.
        Assert.Equal(2, versions.Count);
        Assert.Equal("v3.png", versions[0].FileName);
    }

    [Fact]
    public async Task Beim_Loeschen_gehen_alle_Fassungen_mit()
    {
        using var test = new TestDatabase();
        var asset = await UploadAsync(test);

        await ReplaceAsync(test, asset.Id, "schwert-hd.png");

        var assets = test.GetService<AssetService>();
        await assets.DeleteAsync(asset.Id);

        await using var db = test.CreateContext();
        Assert.Empty(await db.AssetVersions.ToListAsync());

        // Und keine Datei bleibt als Waise liegen.
        Assert.Empty(await assets.FindOrphanedFilesAsync());
    }

    [Fact]
    public async Task Ein_unbekanntes_Asset_wird_abgelehnt()
    {
        using var test = new TestDatabase();

        await Assert.ThrowsAsync<ContentValidationException>(
            () => ReplaceAsync(test, Guid.NewGuid(), "schwert.png"));
    }
}
